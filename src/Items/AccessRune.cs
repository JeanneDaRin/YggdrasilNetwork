using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using RuneVault.UI;

namespace RuneVault.Items
{
    /// <summary>
    /// Runa de acceso (Ashlands): usarla desde el inventario o la barra rápida abre la red desde cualquier lugar
    /// a menos de 2.500 m del núcleo o de un pilar conectado. Sin costo por uso.
    /// Se fabrica en el banco de magia (galdr) con flametal + trofeo de Fader.
    /// </summary>
    internal static class AccessRune
    {
        public const string Prefab = "RuneVault_AccessRune";
        public const string NameToken = "$rv_rune_name";
        public const string VoidPrefab = "RuneVault_VoidRune";
        public const string VoidNameToken = "$rv_void_name";
        public const string CrystalPrefab = "RuneVault_VoidCrystal";

        public static void Init() => PrefabManager.OnVanillaPrefabsAvailable += Add;

        private static void Add()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Add;

            var rune = new CustomItem(Prefab, "Thunderstone", new ItemConfig
            {
                Name = NameToken,
                Description = "$rv_rune_desc",
                CraftingStation = "piece_magetable",
                Requirements = new[]
                {
                    new RequirementConfig("FlametalNew", 10),
                    new RequirementConfig("TrophyFader", 1),
                },
            });
            var shared = rune.ItemDrop.m_itemData.m_shared;
            shared.m_maxStackSize = 1;
            shared.m_teleportable = true;
            ItemManager.Instance.AddItem(rune);

            // La ventana de crafteo del juego muestra como máximo 4 materiales, y la runa necesita 5
            // (4 joyas + oro de sangre). Por eso las joyas se juntan antes en un Cristal del Vacío.
            var crystal = new CustomItem(CrystalPrefab, "Thunderstone", new ItemConfig
            {
                Name = "$rv_crystal_name",
                Description = "$rv_crystal_desc",
                CraftingStation = "piece_magetable",
                Requirements = new[]
                {
                    new RequirementConfig("AncientGemstoneBlack", 1),
                    new RequirementConfig("AncientGemstoneGreen", 1),
                    new RequirementConfig("AncientGemstoneOrange", 1),
                    new RequirementConfig("AncientGemstonePurple", 1),
                },
            });
            crystal.ItemDrop.m_itemData.m_shared.m_maxStackSize = 10;
            ItemManager.Instance.AddItem(crystal);

            // Runa del Vacío (Deep North): igual que la de acceso, pero sin límite de distancia.
            var voidRune = new CustomItem(VoidPrefab, "Thunderstone", new ItemConfig
            {
                Name = VoidNameToken,
                Description = "$rv_void_desc",
                CraftingStation = "piece_magetable",
                Requirements = new[]
                {
                    new RequirementConfig(CrystalPrefab, 1),
                    new RequirementConfig("Gold", 20),
                },
            });
            var voidShared = voidRune.ItemDrop.m_itemData.m_shared;
            voidShared.m_maxStackSize = 1;
            voidShared.m_teleportable = true;
            ItemManager.Instance.AddItem(voidRune);
        }

        internal static bool Is(ItemDrop.ItemData item) => item != null && item.m_shared.m_name == NameToken;

        internal static bool IsVoid(ItemDrop.ItemData item) => item != null && item.m_shared.m_name == VoidNameToken;
    }

    /// <summary>Usar la runa (clic derecho en el inventario o su tecla de la barra rápida) abre la red.</summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    internal static class AccessRuneUsePatch
    {
        private static bool Prefix(Humanoid __instance, ItemDrop.ItemData __1)
        {
            if (__instance != Player.m_localPlayer) return true;
            if (AccessRune.IsVoid(__1)) { TerminalWindow.OpenRemote(unlimited: true); return false; }
            if (AccessRune.Is(__1)) { TerminalWindow.OpenRemote(unlimited: false); return false; }
            return true;
        }
    }
}
