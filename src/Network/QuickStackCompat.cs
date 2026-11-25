using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using YggdrasilNetwork.Pieces;
using YggdrasilNetwork.UI;
using UnityEngine;

namespace YggdrasilNetwork.Network
{
    /// <summary>
    /// Integración con "Quick Stack - Store - Sort - Trash - Restock": cuando su quick stack termina y estás
    /// junto a una terminal (o con ella abierta), lo que quedó en tu inventario se reparte en la red, en los
    /// cofres que ya tienen ese ítem (incluye los alcanzados por pilares y los vinculados).
    /// Usa la misma lista que ese mod armó, así que respeta sus favoritos y su barra rápida.
    /// Si ese mod no está o cambió su código interno, esto simplemente no se activa.
    /// </summary>
    internal static class QuickStackCompat
    {
        private const float TerminalRange = 10f;

        internal static void TryPatch(Harmony harmony)
        {
            var type = AccessTools.TypeByName("QuickStackStore.QuickStackModule");
            var method = type != null ? AccessTools.Method(type, "QuickStackIntoMultipleContainers") : null;
            if (method == null)
            {
                YggdrasilPlugin.Log.LogInfo("Quick Stack no encontrado: sin integración de quick stack.");
                return;
            }
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(QuickStackCompat), nameof(Postfix)));
            YggdrasilPlugin.Log.LogInfo("Integración con Quick Stack activa.");
        }

        // QuickStackIntoMultipleContainers(List<ItemData> items, List<ItemData> ..., Player player, List<Container> containers)
        private static void Postfix(List<ItemDrop.ItemData> __0, Player __2)
        {
            try
            {
                if (!__2 || __2 != Player.m_localPlayer || __0 == null) return;

                StorageCore core;
                if (TerminalWindow.IsOpen) core = StorageCore.All.FirstOrDefault(c => c);
                else
                {
                    var terminal = AccessTerminal.All
                        .FirstOrDefault(t => t && Vector3.Distance(t.transform.position, __2.transform.position) <= TerminalRange);
                    if (!terminal) return;
                    core = StorageCore.FindFor(terminal.transform.position);
                    if (!core) return;
                }

                var network = StorageNetwork.For(core, __2.GetPlayerID());
                var names = new HashSet<string>(network.AggregateItems().Select(i => i.Name));
                var inv = __2.GetInventory();
                int sent = 0;
                foreach (var item in __0.ToList())
                {
                    if (!inv.ContainsItem(item)) continue;              // ya lo guardó el quick stack
                    if (__2.IsItemEquiped(item)) continue;
                    if (!names.Contains(item.m_shared.m_name)) continue;
                    sent += network.RequestStore(__2, item, item.m_stack);
                }
                if (sent > 0) YggdrasilPlugin.Log.LogInfo($"Quick stack a la red: {sent} ítems enviados.");
            }
            catch (System.Exception e)
            {
                YggdrasilPlugin.Log.LogWarning("Integración de quick stack falló: " + e.Message);
            }
        }
    }
}
