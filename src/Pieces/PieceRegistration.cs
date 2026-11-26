using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace YggdrasilNetwork.Pieces
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
        public const string HammerCategory = "Yggdrasil";

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
                Icon = Visuals.Icon("core"),
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
            // Modelo propio (pedestal con cristal flotante). Si no se pudo armar, queda el pilar de piedra teñido.
            if (Models.CoreModel.Apply(core.PiecePrefab))
                Models.ModelKit.AddLight(core.PiecePrefab, Models.ModelKit.GlowBlue, 5f, 1.0f, Models.CoreModel.CrystalPos);
            else
            {
                Visuals.Tint(core.PiecePrefab, new Color(0.80f, 0.90f, 1.15f), Visuals.RuneBlue * 0.35f);
                Visuals.AddLight(core.PiecePrefab, Visuals.RuneBlue, 5f, 1.6f, 2.2f);
            }

            // El pilar original es de piedra y la piedra no se sostiene sobre madera.
            // Como madera se puede poner sobre pisos de madera y sobre el suelo.
            var wear = core.PiecePrefab.GetComponent<WearNTear>();
            if (wear) wear.m_materialType = WearNTear.MaterialType.Wood;
            PieceManager.Instance.AddPiece(core);

            // Terminal de acceso: parte de la mesa de madera (interacción y soporte) con modelo propio.
            string terminalBase = PrefabManager.Instance.GetPrefab("piece_table") ? "piece_table" : "stone_pillar";
            var terminal = new CustomPiece(TerminalPrefab, terminalBase, new PieceConfig
            {
                Name = "$rv_terminal_name",
                Icon = Visuals.Icon("terminal"),
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
            // Modelo propio (losa rúnica). Si no se pudo armar, queda la mesa teñida.
            if (Models.TerminalModel.Apply(terminal.PiecePrefab))
                Models.ModelKit.AddLight(terminal.PiecePrefab, Models.ModelKit.GlowBlue, 3f, 0.8f, new Vector3(0f, 1.75f, 0f));
            else
            {
                Visuals.Tint(terminal.PiecePrefab, new Color(0.85f, 0.92f, 1.10f), Visuals.RuneBlue * 0.20f);
                Visuals.AddLight(terminal.PiecePrefab, Visuals.RuneBlue, 3f, 1.0f, 1.4f);
            }
            var terminalWear = terminal.PiecePrefab.GetComponent<WearNTear>();
            if (terminalWear) terminalWear.m_materialType = WearNTear.MaterialType.Wood;
            PieceManager.Instance.AddPiece(terminal);

            // Pilar rúnico (Llanuras). Parte del poste de madera (colocación y soporte de madera) con modelo propio.
            string pillarBase = PrefabManager.Instance.GetPrefab("wood_pole2") ? "wood_pole2" : "stone_pillar";
            var pillar = new CustomPiece(PillarPrefab, pillarBase, new PieceConfig
            {
                Name = "$rv_pillar_name",
                Icon = Visuals.Icon("pillar"),
                Description = "$rv_pillar_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceRegistration.HammerCategory,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("BlackMetal", 20, 0, true),
                    new RequirementConfig("TrophyGoblinShaman", 1, 0, true),
                },
            });
            // Modelo propio (obelisco con runas). Si no se pudo armar, queda el poste teñido.
            if (!Models.PillarModel.Apply(pillar.PiecePrefab))
                Visuals.Tint(pillar.PiecePrefab, new Color(0.55f, 0.48f, 0.70f), Visuals.RunePurple * 0.40f);
            var pillarWear = pillar.PiecePrefab.GetComponent<WearNTear>();
            if (pillarWear) pillarWear.m_health = Mathf.Max(pillarWear.m_health, 1000f);
            pillar.PiecePrefab.AddComponent<RunePillar>();
            pillar.PiecePrefab.AddComponent<RangeMarker>();
            Visuals.AddLight(pillar.PiecePrefab, Models.PillarModel.RuneColor, 4f, 0.8f, 1.8f);
            PieceManager.Instance.AddPiece(pillar);

            // Cofre vinculado (Pantano): mismo espacio que el cofre de metal negro.
            var linked = new CustomPiece(LinkedChestPrefab, "piece_chest_blackmetal", new PieceConfig
            {
                Name = "$rv_linked_name",
                Icon = Visuals.Icon("linked"),
                Description = "$rv_linked_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceRegistration.HammerCategory,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Iron", 30, 0, true),
                    new RequirementConfig("FineWood", 20, 0, true),
                    new RequirementConfig("TrophyDraugr", 1, 0, true),
                },
            });
            linked.PiecePrefab.AddComponent<LinkedChest>();
            // Mantiene el cofre de metal negro (tapa animada): sus acentos verdes pasan a azul
            // y se le agregan placas, trazas de red y un cristal flotante.
            // El _MetalColor solo tiñe los reflejos: para que el cofre se vea azul también hay que teñir
            // su color base (igual que el abisal con el morado).
            Visuals.Tint(linked.PiecePrefab, new Color(0.55f, 0.72f, 1.15f), Visuals.RuneBlue * 0.15f);
            Models.ModelKit.RecolorAccents(linked.PiecePrefab, new Color(0.60f, 2.20f, 4.50f));
            Models.ChestModels.Linked(linked.PiecePrefab);
            Visuals.AddLight(linked.PiecePrefab, Visuals.RuneBlue, 2.5f, 0.8f, 1.0f);
            var linkedContainer = linked.PiecePrefab.GetComponent<Container>();
            if (linkedContainer) linkedContainer.m_name = "$rv_linked_name";
            PieceManager.Instance.AddPiece(linked);

            // Cofre abisal (Deep North): 8x8 casillas, uno por mundo, siempre en la red.
            var abyssal = new CustomPiece(AbyssalChestPrefab, "piece_chest_blackmetal", new PieceConfig
            {
                Name = "$rv_abyssal_name",
                Icon = Visuals.Icon("abyssal"),
                Description = "$rv_abyssal_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceRegistration.HammerCategory,
                CraftingStation = CraftingStations.Workbench,
                Requirements = new[]
                {
                    new RequirementConfig("Frostwood", 30, 0, true),
                    new RequirementConfig("TrophyJotunWitch", 1, 0, true),
                },
            });
            abyssal.PiecePrefab.AddComponent<AbyssalChest>();
            Visuals.Tint(abyssal.PiecePrefab, new Color(0.42f, 0.34f, 0.55f), Visuals.RunePurple * 0.20f);
            Models.ModelKit.RecolorAccents(abyssal.PiecePrefab, new Color(0.90f, 0.20f, 1.80f));
            Models.ChestModels.Abyssal(abyssal.PiecePrefab);
            Visuals.AddLight(abyssal.PiecePrefab, Visuals.RunePurple, 3.5f, 1.2f, 1.2f);
            var abyssalContainer = abyssal.PiecePrefab.GetComponent<Container>();
            if (abyssalContainer)
            {
                abyssalContainer.m_name = "$rv_abyssal_name";
                abyssalContainer.m_width = AbyssalWidth;
                abyssalContainer.m_height = AbyssalHeight;
            }
            PieceManager.Instance.AddPiece(abyssal);

            YggdrasilPlugin.Log.LogInfo($"Piezas registradas: {CorePrefab}, {TerminalPrefab} (base {terminalBase})");
        }
    }
}
