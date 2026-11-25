using BepInEx;
using BepInEx.Configuration;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Utils;

namespace YggdrasilNetwork
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(Compat.MultiUserChestGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Compat.StashLinkGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Compat.QuickStackStoreGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(Compat.PathOfValheimanGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class YggdrasilPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "jeanne.valheim.yggdrasilnetwork";
        public const string PluginName = "Yggdrasil Network";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<float> CoreRadius;
        internal static ConfigEntry<float> RadiusLevel1;
        internal static ConfigEntry<float> RadiusLevel2;
        internal static ConfigEntry<string> CostLevel1;
        internal static ConfigEntry<string> CostLevel2;
        internal static ConfigEntry<float> PillarRadius;
        internal static ConfigEntry<bool> DebugLog;
        internal static ConfigEntry<Network.ChestFilter> ChestFilterMode;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            // Todo lo que afecta al juego lo manda el servidor (Jötunn sincroniza las opciones "solo admin").
            var admin = new ConfigurationManagerAttributes { IsAdminOnly = true };

            CoreRadius = Config.Bind("Red", "Radio del núcleo", 8f,
                new ConfigDescription("Metros alrededor del núcleo en que se enlazan cofres.",
                    new AcceptableValueRange<float>(2f, 16f), admin));

            RadiusLevel1 = Config.Bind("Progresión", "Radio nivel 1", 14f,
                new ConfigDescription("Radio del núcleo tras la mejora del Pantano. Nunca menor que el radio base.", new AcceptableValueRange<float>(4f, 32f), admin));
            RadiusLevel2 = Config.Bind("Progresión", "Radio nivel 2", 20f,
                new ConfigDescription("Radio del núcleo tras la mejora de la Montaña. Nunca menor que el nivel 1.", new AcceptableValueRange<float>(8f, 64f), admin));
            CostLevel1 = Config.Bind("Progresión", "Costo nivel 1", "Iron:20,TrophyBonemass:1",
                new ConfigDescription("Materiales para mejorar el núcleo a nivel 1 (prefab:cantidad, separados por coma).", null, admin));
            CostLevel2 = Config.Bind("Progresión", "Costo nivel 2", "Silver:20,TrophyDragonQueen:1",
                new ConfigDescription("Materiales para mejorar el núcleo a nivel 2 (prefab:cantidad, separados por coma).", null, admin));

            PillarRadius = Config.Bind("Progresión", "Radio del pilar rúnico", 10f,
                new ConfigDescription("Metros que cubre cada pilar rúnico conectado a la red.", new AcceptableValueRange<float>(4f, 32f), admin));

            DebugLog = Config.Bind("Depuración", "Registro detallado", false,
                "Escribe en el log el detalle de los cofres vinculados (para encontrar errores).");

            ChestFilterMode = Config.Bind("Red", "Cofres que se enlazan", Network.ChestFilter.ConPermiso,
                "ConPermiso: todos los cofres que puedes abrir (útil en bases compartidas). " +
                "SoloPropios: solo los cofres que construiste tú.");

            I18n.Texts.Register();
            Pieces.PieceRegistration.Init();
            Items.AccessRune.Init();
            UI.TerminalWindow.Create(gameObject);
            gameObject.AddComponent<Network.LinkedChestTracker>();

            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll(typeof(YggdrasilPlugin).Assembly);
            Log.LogInfo($"{PluginName} {PluginVersion} cargado.");
        }

        private void Start()
        {
            // En Start todos los plugins ya están cargados por BepInEx.
            Compat.Detect();
            if (Compat.QuickStackStore) Network.QuickStackCompat.TryPatch(_harmony);
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
        }
    }

    /// <summary>Detección de mods con los que Yggdrasil Network debe convivir.</summary>
    internal static class Compat
    {
        // GUIDs de BepInEx (verificar en LogOutput.log si alguno no se detecta).
        public const string MultiUserChestGuid = "com.maxsch.valheim.MultiUserChest";
        public const string StashLinkGuid = "rhyzt.valheim.stashlink";
        public const string QuickStackStoreGuid = "goldenrevolver.quick_stack_store";
        // Overhaul con crafteo/construcción desde cofres (maestría de conveniencia),
        // "Chest Feed" para fundiciones y filas extra en la mochila.
        public const string PathOfValheimanGuid = "treextr.pathofvalheiman";

        public static bool MultiUserChest { get; private set; }
        public static bool StashLink { get; private set; }
        public static bool QuickStackStore { get; private set; }
        public static bool PathOfValheiman { get; private set; }

        /// <summary>Algún otro mod ya descuenta materiales desde cofres.</summary>
        public static bool OtherCraftFromChests => StashLink || PathOfValheiman;

        public static void Detect()
        {
            MultiUserChest = Chainloader.PluginInfos.ContainsKey(MultiUserChestGuid);
            StashLink = Chainloader.PluginInfos.ContainsKey(StashLinkGuid);
            QuickStackStore = Chainloader.PluginInfos.ContainsKey(QuickStackStoreGuid);
            PathOfValheiman = Chainloader.PluginInfos.ContainsKey(PathOfValheimanGuid);

            var log = YggdrasilPlugin.Log;
            log.LogInfo($"Compat: MultiUserChest={MultiUserChest}, StashLink={StashLink}, QuickStackStore={QuickStackStore}, PathOfValheiman={PathOfValheiman}");
            if (OtherCraftFromChests)
                log.LogInfo("Hay otro mod de crafteo desde cofres: el crafteo desde la red queda desactivado para evitar doble descuento.");
        }
    }
}
