using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RuneVault.Network;
using UnityEngine;
using static RuneVault.I18n.Texts;

namespace RuneVault.Pieces
{
    /// <summary>
    /// Núcleo de almacenamiento: enlaza los cofres dentro de su radio.
    /// Tiene niveles (0 a 2) guardados en su ZDO; cada nivel agranda el radio.
    /// Solo puede existir uno en todo el mundo (se marca con una clave global del servidor).
    /// </summary>
    public class StorageCore : MonoBehaviour, Hoverable, Interactable
    {
        internal const string WorldKey = "runevault_core";
        private const string LevelKey = "rv_level";            // nivel antiguo guardado en la pieza (migración)
        private static readonly string[] LevelWorldKeys = { "", "runevault_core_lvl1", "runevault_core_lvl2" };
        internal const int MaxLevel = 2;

        /// <summary>Núcleos construidos y cargados en el mundo.</summary>
        internal static readonly List<StorageCore> All = new List<StorageCore>();

        private ZNetView _nview;

        private void Start()
        {
            _nview = GetComponent<ZNetView>();
            if (!_nview || !_nview.IsValid()) return;   // el fantasma del martillo no cuenta
            All.Add(this);

            // Marca para el límite de un núcleo por mundo (también en mundos donde ya existía).
            if (ZoneSystem.instance && !ZoneSystem.instance.GetGlobalKey(WorldKey))
                ZoneSystem.instance.SetGlobalKey(WorldKey);

            // Núcleos mejorados con la versión anterior (nivel guardado en la pieza): pasar al mundo.
            int old = Mathf.Clamp(_nview.GetZDO().GetInt(LevelKey, 0), 0, MaxLevel);
            for (int l = 1; l <= old; l++) SetWorldLevel(l);
        }

        private void OnDestroy() => All.Remove(this);

        // ---------- Nivel y radio ----------

        /// <summary>
        /// El nivel es del mundo, no de la pieza: si el núcleo se destruye y se vuelve a construir, lo conserva.
        /// Se guarda como claves globales del servidor (como las de los jefes derrotados).
        /// </summary>
        internal static int WorldLevel
        {
            get
            {
                var zs = ZoneSystem.instance;
                if (!zs) return 0;
                for (int l = MaxLevel; l >= 1; l--)
                    if (zs.GetGlobalKey(LevelWorldKeys[l])) return l;
                return 0;
            }
        }

        private static void SetWorldLevel(int level)
        {
            if (ZoneSystem.instance && level >= 1 && level <= MaxLevel && !ZoneSystem.instance.GetGlobalKey(LevelWorldKeys[level]))
                ZoneSystem.instance.SetGlobalKey(LevelWorldKeys[level]);
        }

        internal int Level => WorldLevel;

        internal float Radius => RadiusFor(Level);

        /// <summary>Radio por nivel. Una mejora nunca achica el radio, aunque la configuración lo diga.</summary>
        internal static float RadiusFor(int level)
        {
            float r = RuneVaultPlugin.CoreRadius.Value;
            if (level >= 1) r = Mathf.Max(r, RuneVaultPlugin.RadiusLevel1.Value);
            if (level >= 2) r = Mathf.Max(r, RuneVaultPlugin.RadiusLevel2.Value);
            return r;
        }

        /// <summary>Zonas que cubre la red: el núcleo y cada pilar conectado (en cadena).</summary>
        internal List<NetworkSphere> NetworkSpheres()
        {
            var spheres = new List<NetworkSphere> { new NetworkSphere(transform.position, Radius) };
            var pending = RunePillar.All.Where(p => p).ToList();
            bool added = true;
            while (added)
            {
                added = false;
                foreach (var pillar in pending.ToList())
                {
                    var pos = pillar.transform.position;
                    if (!spheres.Any(sp => sp.Contains(pos))) continue;
                    spheres.Add(new NetworkSphere(pos, RuneVaultPlugin.PillarRadius.Value));
                    pending.Remove(pillar);
                    added = true;
                }
            }
            return spheres;
        }

        internal bool Covers(Vector3 position) => NetworkSpheres().Any(sp => sp.Contains(position));

        private static string CostFor(int level) =>
            level == 1 ? RuneVaultPlugin.CostLevel1.Value : level == 2 ? RuneVaultPlugin.CostLevel2.Value : "";

        /// <summary>Un material del costo. (Sin tuplas: System.ValueTuple no existe en el runtime de Valheim.)</summary>
        private sealed class CostEntry
        {
            public ItemDrop.ItemData item;
            public int amount;
        }

        /// <summary>"Iron:20,TrophyBonemass:1" → materiales. Ignora entradas que el juego no conoce.</summary>
        private static List<CostEntry> ParseCost(string cost)
        {
            var result = new List<CostEntry>();
            if (!ObjectDB.instance || string.IsNullOrWhiteSpace(cost)) return result;
            foreach (var part in cost.Split(','))
            {
                var kv = part.Split(':');
                if (kv.Length != 2 || !int.TryParse(kv[1].Trim(), out int amount) || amount <= 0) continue;
                var prefab = ObjectDB.instance.GetItemPrefab(kv[0].Trim());
                var drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
                if (drop) result.Add(new CostEntry { item = drop.m_itemData, amount = amount });
            }
            return result;
        }

        private static string Describe(List<CostEntry> cost) =>
            string.Join(", ", cost.Select(c => $"{c.amount} {c.item.m_shared.m_name}"));

        /// <summary>El núcleo cuya red (núcleo + pilares) alcanza esta posición, o null.</summary>
        internal static StorageCore FindFor(Vector3 position)
        {
            return All
                .Where(c => c && c.Covers(position))
                .OrderBy(c => Vector3.Distance(c.transform.position, position))
                .FirstOrDefault();
        }

        // ---------- Interacción ----------

        public string GetHoverName() => T("rv_core_name");

        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            var player = Player.m_localPlayer;
            if (!player) return GetHoverName();

            var net = StorageNetwork.For(this, player.GetPlayerID());
            int pillars = NetworkSpheres().Count - 1;
            string text = $"{GetHoverName()} — {T("rv_core_level", Level)}\n" +
                          $"{T("rv_core_linked", net.ChestCount, Radius.ToString("0"))}" +
                          (pillars > 0 ? " · " + T("rv_core_pillars", pillars) : "") + "\n" +
                          $"[<color=yellow><b>$KEY_Use</b></color>] {T("rv_core_use")}";

            if (Level < MaxLevel)
            {
                var cost = ParseCost(CostFor(Level + 1));
                text += $"\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] " +
                        T("rv_upgrade", Level + 1, RadiusFor(Level + 1).ToString("0"), Describe(cost));
            }
            else
            {
                text += "\n" + T("rv_upgrade_max");
            }
            return Localization.instance.Localize(text);
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !(user is Player player)) return false;

            if (alt) return TryUpgrade(player);    // Shift + E

            var net = StorageNetwork.For(this, player.GetPlayerID());
            var items = net.AggregateItems();
            int total = items.Sum(i => i.Amount);

            // Solo un resumen: el detalle se ve en la Terminal de acceso.
            player.Message(MessageHud.MessageType.Center,
                T("rv_core_summary", net.ChestCount, items.Count, total) + "\n" + T("rv_core_hint"));
            return true;
        }

        private bool TryUpgrade(Player player)
        {
            if (Level >= MaxLevel)
            {
                player.Message(MessageHud.MessageType.Center, T("rv_upgrade_max"));
                return false;
            }

            int next = Level + 1;
            var cost = ParseCost(CostFor(next));
            var inv = player.GetInventory();
            // Lo que falta, no el costo completo: con 1 de plata de 20 dice "faltan 19".
            var missing = cost
                .Select(c => new CostEntry { item = c.item, amount = c.amount - inv.CountItems(c.item.m_shared.m_name) })
                .Where(c => c.amount > 0)
                .ToList();
            if (missing.Count > 0)
            {
                player.Message(MessageHud.MessageType.Center,
                    Localization.instance.Localize(T("rv_upgrade_missing", Describe(missing))));
                return false;
            }

            foreach (var c in cost) inv.RemoveItem(c.item.m_shared.m_name, c.amount);
            SetWorldLevel(next);

            player.Message(MessageHud.MessageType.Center, T("rv_upgraded", next, RadiusFor(next).ToString("0")));
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
    }

    /// <summary>No dejar construir un segundo núcleo en el mundo.</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    internal static class OneCorePerWorldPatch
    {
        private static bool Prefix(Player __instance, Piece __0, ref bool __result)
        {
            if (!__0 || !__0.GetComponent<StorageCore>()) return true;
            if (!ZoneSystem.instance || !ZoneSystem.instance.GetGlobalKey(StorageCore.WorldKey)) return true;

            __instance.Message(MessageHud.MessageType.Center, T("rv_core_exists"));
            __result = false;
            return false;
        }
    }

    /// <summary>Al destruir o desarmar el núcleo, se libera el cupo del mundo.</summary>
    [HarmonyPatch(typeof(WearNTear), "Destroy")]
    internal static class CoreDestroyedPatch
    {
        private static void Prefix(WearNTear __instance)
        {
            if (__instance.GetComponent<StorageCore>() && ZoneSystem.instance)
                ZoneSystem.instance.RemoveGlobalKey(StorageCore.WorldKey);
        }
    }
}
