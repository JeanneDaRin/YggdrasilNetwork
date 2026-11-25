using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace RuneVault.Pieces
{
    internal static class PieceRegistration
    {
        public const string CorePrefab = "piece_runevault_core";
        public const string TerminalPrefab = "piece_runevault_terminal";
        public const string PillarPrefab = "piece_runevault_pillar";
        public const string LinkedChestPrefab = Network.LinkedChests.Prefab;
        public const string AbyssalChestPrefab = Network.LinkedChests.AbyssalPrefab;
        public const int AbyssalWidth = 8;
        public const int AbyssalHeight = 8;

        /// <summary>Pestaña propia en el martillo (Jötunn la crea si no existe).</summary>
        public const string HammerCategory = "Rune Vault";

        public static void Init()
        {
            PrefabManager.OnVanillaPrefabsAvailable += AddPieces;
        }

        private static void AddPieces()
        {
            // Solo una vez: el evento se repite al volver al menú y entrar a otro mundo.
            PrefabManager.OnVanillaPrefabsAvailable -= AddPieces;

            var core = new CustomPiece(CorePrefab, "stone_pillar", new PieceConfig
            {
                Name = "$rv_core_name",
                Description = "$rv_core_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceRegistration.HammerCategory,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Wood", 10, 0, true),
                    new RequirementConfig("SurtlingCore", 1, 0, true),
                },
            });
            core.PiecePrefab.AddComponent<StorageCore>();
            core.PiecePrefab.AddComponent<RangeMarker>();

            // El pilar original es de piedra y la piedra no se sostiene sobre madera.
            // Como madera se puede poner sobre pisos de madera y sobre el suelo.
            var wear = core.PiecePrefab.GetComponent<WearNTear>();
            if (wear) wear.m_materialType = WearNTear.MaterialType.Wood;
            PieceManager.Instance.AddPiece(core);

            // Terminal de acceso: por ahora usa el modelo de la mesa de madera.
            string terminalBase = PrefabManager.Instance.GetPrefab("piece_table") ? "piece_table" : "stone_pillar";
            var terminal = new CustomPiece(TerminalPrefab, terminalBase, new PieceConfig
            {
                Name = "$rv_terminal_name",
                Description = "$rv_terminal_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceRegistration.HammerCategory,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Wood", 10, 0, true),
                    new RequirementConfig("SurtlingCore", 1, 0, true),
                },
            });
            terminal.PiecePrefab.AddComponent<AccessTerminal>();
            var terminalWear = terminal.PiecePrefab.GetComponent<WearNTear>();
            if (terminalWear) terminalWear.m_materialType = WearNTear.MaterialType.Wood;
            PieceManager.Instance.AddPiece(terminal);

            // Pilar rúnico (Llanuras). Por ahora usa el modelo del poste de madera.
            string pillarBase = PrefabManager.Instance.GetPrefab("wood_pole2") ? "wood_pole2" : "stone_pillar";
            var pillar = new CustomPiece(PillarPrefab, pillarBase, new PieceConfig
            {
                Name = "$rv_pillar_name",
                Description = "$rv_pillar_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceRegistration.HammerCategory,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("BlackMetal", 20, 0, true),
                    new RequirementConfig("TrophyGoblinShaman", 1, 0, false),
                },
            });
            pillar.PiecePrefab.AddComponent<RunePillar>();
            pillar.PiecePrefab.AddComponent<RangeMarker>();
            PieceManager.Instance.AddPiece(pillar);

            // Cofre vinculado (Pantano): mismo espacio que el cofre de metal negro.
            var linked = new CustomPiece(LinkedChestPrefab, "piece_chest_blackmetal", new PieceConfig
            {
                Name = "$rv_linked_name",
                Description = "$rv_linked_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceRegistration.HammerCategory,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Iron", 30, 0, true),
                    new RequirementConfig("FineWood", 20, 0, true),
                    new RequirementConfig("TrophyDraugr", 1, 0, false),
                },
            });
            linked.PiecePrefab.AddComponent<LinkedChest>();
            var linkedContainer = linked.PiecePrefab.GetComponent<Container>();
            if (linkedContainer) linkedContainer.m_name = "$rv_linked_name";
            PieceManager.Instance.AddPiece(linked);

            // Cofre abisal (Deep North): 8x8 casillas, uno por mundo, siempre en la red.
            var abyssal = new CustomPiece(AbyssalChestPrefab, "piece_chest_blackmetal", new PieceConfig
            {
                Name = "$rv_abyssal_name",
                Description = "$rv_abyssal_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceRegistration.HammerCategory,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Frostwood", 30, 0, true),
                    new RequirementConfig("TrophyJotunWitch", 1, 0, false),
                },
            });
            abyssal.PiecePrefab.AddComponent<AbyssalChest>();
            var abyssalContainer = abyssal.PiecePrefab.GetComponent<Container>();
            if (abyssalContainer)
            {
                abyssalContainer.m_name = "$rv_abyssal_name";
                abyssalContainer.m_width = AbyssalWidth;
                abyssalContainer.m_height = AbyssalHeight;
            }
            PieceManager.Instance.AddPiece(abyssal);

            RuneVaultPlugin.Log.LogInfo($"Piezas registradas: {CorePrefab}, {TerminalPrefab} (base {terminalBase})");
        }
    }
}
