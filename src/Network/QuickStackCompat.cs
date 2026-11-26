using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using YggdrasilNetwork.Pieces;
using YggdrasilNetwork.UI;
using UnityEngine;

namespace YggdrasilNetwork.Network
{
    /// <summary>
    /// Integración con "Quick Stack - Store - Sort - Trash - Restock": después de su quick stack, si estás junto a
    /// una terminal (o con ella abierta), lo que sigue en tu inventario y ya existe en la red se reparte en ella
    /// (cofres cercanos, alcanzados por pilares y vinculados lejanos).
    ///
    /// No se usa la lista de ese mod: solo incluye lo que puede ir a cofres cercanos, así que un ítem que solo
    /// está en un cofre vinculado lejano nunca llegaba. Se revisa el inventario completo, igual que ese mod:
    /// sin la barra rápida, sin lo equipado y sin sus favoritos (ítem o casilla).
    /// Se espera un momento para que ese mod termine de guardar en los cofres cercanos.
    /// Si ese mod no está o cambió su código interno, esto simplemente no se activa.
    /// </summary>
    internal static class QuickStackCompat
    {
        private const float TerminalRange = 10f;
        private const float WaitSeconds = 1.0f;

        private static MethodInfo _getPlayerConfig;   // UserConfig.GetPlayerConfig(long)
        private static MethodInfo _isFavorited;       // UserConfig.IsItemNameOrSlotFavorited(ItemData)
        private static MonoBehaviour _runner;
        private static bool _running;

        internal static void TryPatch(Harmony harmony, MonoBehaviour runner)
        {
            _runner = runner;
            var module = AccessTools.TypeByName("QuickStackStore.QuickStackModule");
            var doQuick = module != null ? AccessTools.Method(module, "DoQuickStack") : null;
            var config = AccessTools.TypeByName("QuickStackStore.UserConfig");
            _getPlayerConfig = config != null ? AccessTools.Method(config, "GetPlayerConfig") : null;
            _isFavorited = config != null ? AccessTools.Method(config, "IsItemNameOrSlotFavorited") : null;

            if (doQuick == null || _getPlayerConfig == null || _isFavorited == null)
            {
                YggdrasilPlugin.Log.LogInfo("Quick Stack no encontrado (o cambió): sin integración de quick stack.");
                return;
            }
            harmony.Patch(doQuick, postfix: new HarmonyMethod(typeof(QuickStackCompat), nameof(DoQuickStackPostfix)));
            // Su aviso de "no se guardó nada" sale antes de que la red reparta: junto a la red se calla
            // y el resultado lo informa la red un segundo después.
            var report = AccessTools.Method(module, "ReportQuickStackResult");
            try
            {
                if (report != null)
                    harmony.Patch(report, prefix: new HarmonyMethod(typeof(QuickStackCompat), nameof(ReportPrefix)));
            }
            catch (System.Exception e)
            {
                YggdrasilPlugin.Log.LogWarning("Quick stack: no se pudo ajustar su mensaje de resultado: " + e.Message);
            }
            YggdrasilPlugin.Log.LogInfo("Integración con Quick Stack activa.");
        }

        private static void Diag(string msg)
        {
            if (YggdrasilPlugin.DebugLog.Value) YggdrasilPlugin.Log.LogInfo("[Debug] " + msg);
        }

        private static void DoQuickStackPostfix()
        {
            try
            {
                var player = Player.m_localPlayer;
                Diag("Quick stack: el mod de quick stack terminó su parte.");
                if (!player || _running || !_runner) return;
                if (!CoreFor(player))
                {
                    Diag($"Quick stack: sin terminal a menos de {TerminalRange} m (o sin núcleo cargado): no se usa la red.");
                    return;
                }
                LinkedChests.Query(true);   // datos frescos de los cofres lejanos
                _runner.StartCoroutine(AfterDelay());
            }
            catch (System.Exception e)
            {
                YggdrasilPlugin.Log.LogWarning("Integración de quick stack falló: " + e.Message);
            }
        }

        private static bool _quietReport;

        // ReportQuickStackResult(Player player, int movedCount)
        private static bool ReportPrefix(int __1)
        {
            var player = Player.m_localPlayer;
            if (__1 > 0 || !player || !CoreFor(player)) return true;
            _quietReport = true;   // la red dirá si guardó algo
            return false;
        }

        private static IEnumerator AfterDelay()
        {
            _running = true;
            yield return new WaitForSeconds(WaitSeconds);
            _running = false;
            try
            {
                var player = Player.m_localPlayer;
                if (player) Send(player);
            }
            catch (System.Exception e)
            {
                YggdrasilPlugin.Log.LogWarning("Integración de quick stack falló: " + e.Message);
            }
        }

        private static StorageCore CoreFor(Player player)
        {
            if (TerminalWindow.IsOpen) return StorageCore.All.FirstOrDefault(c => c);
            var terminal = AccessTerminal.All
                .FirstOrDefault(t => t && Vector3.Distance(t.transform.position, player.transform.position) <= TerminalRange);
            return terminal ? StorageCore.FindFor(terminal.transform.position) : null;
        }

        private static void Send(Player player)
        {
            var core = CoreFor(player);
            if (!core) return;
            var network = StorageNetwork.For(core, player.GetPlayerID());
            var names = new HashSet<string>(network.AggregateItems().Select(i => i.Name));
            var config = _getPlayerConfig.Invoke(null, new object[] { player.GetPlayerID() });
            var items = player.GetInventory().GetAllItems().ToList();
            Diag($"Quick stack: red con {network.ChestCount} cofres y {names.Count} tipos de ítem; {items.Count} ítems en el inventario.");

            int sent = 0;
            foreach (var item in items)
            {
                string why = null;
                if (item.m_gridPos.y == 0) why = "barra rápida";
                else if (player.IsItemEquiped(item)) why = "equipado";
                else if (TerminalWindow.IsProtected(item)) why = "runa en uso";
                else if (config != null && (bool)_isFavorited.Invoke(config, new object[] { item })) why = "favorito";
                else if (!names.Contains(item.m_shared.m_name)) why = "no hay de ese ítem en la red";
                if (why != null)
                {
                    Diag($"Quick stack: {item.m_shared.m_name} x{item.m_stack} no se envía: {why}.");
                    continue;
                }
                int n = network.RequestStore(player, item, item.m_stack);
                Diag($"Quick stack: {item.m_shared.m_name} x{item.m_stack} → {n} enviados.");
                sent += n;
            }
            if (sent > 0) player.Message(MessageHud.MessageType.Center, I18n.Texts.T("rv_stored_many", sent));
            else if (_quietReport) player.Message(MessageHud.MessageType.Center, I18n.Texts.T("rv_stored_none"));
            _quietReport = false;
        }
    }
}
