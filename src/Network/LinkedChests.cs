using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using YggdrasilNetwork.Pieces;
using UnityEngine;
using static YggdrasilNetwork.I18n.Texts;

namespace YggdrasilNetwork.Network
{
    /// <summary>
    /// La red vista desde el servidor: núcleo, pilares conectados y todos los cofres de la red con su contenido,
    /// estén cargados o no. Sirve para:
    ///  • los cofres vinculados lejanos (siempre en la red),
    ///  • los cofres de la base cuando el jugador está lejos (Runa de acceso, hasta 2.500 m).
    ///
    /// Un cofre que no está cargado no tiene objeto en el juego, pero sus datos existen en el servidor. Para esos,
    /// la terminal le pide al servidor que lea la lista guardada, la cambie y la vuelva a guardar. Si el cofre está
    /// cargado cerca del jugador, se usa el camino normal (ChestRpc). En un mundo de un jugador, el juego es el servidor.
    /// </summary>
    internal static class LinkedChests
    {
        public const string Prefab = "piece_runevault_linkedchest";
        public const string AbyssalPrefab = "piece_runevault_abyssalchest";
        private const string CorePrefab = "piece_runevault_core";
        private const string PillarPrefab = "piece_runevault_pillar";
        private const string QueenKey = "defeated_queen";        // clave del juego al derrotar a la Reina
        public const int BaseLimit = 3;
        public const int QueenLimit = 10;

        private const string QueryRpc = "RuneVault_NetQuery";
        private const string QueryResultRpc = "RuneVault_NetQueryResult";
        private const string TakeRpc = "RuneVault_RemoteTake";
        internal const string TakeResultRpc = "RuneVault_RemoteTakeResult";
        private const string StoreRpc = "RuneVault_RemoteStore";
        internal const string StoreResultRpc = "RuneVault_RemoteStoreResult";

        /// <summary>Copia de un cofre según el último dato del servidor.</summary>
        internal sealed class RemoteChest
        {
            public ZDOID Id;
            public long Creator;
            public bool Linked;
            public bool Abyssal;
            public Vector3 Position;
            public Inventory Inventory;
        }

        // ---------- Lo que mandó el servidor ----------

        internal static bool HasCore { get; private set; }
        internal static Vector3 CorePosition { get; private set; }
        internal static readonly List<Vector3> Pillars = new List<Vector3>();
        internal static readonly List<RemoteChest> Known = new List<RemoteChest>();
        internal static float ReceivedAt { get; private set; } = -999f;

        private static int _placedSinceQuery;
        private static float _lastQuery = -999f;

        internal static int Limit =>
            ZoneSystem.instance && ZoneSystem.instance.GetGlobalKey(QueenKey) ? QueenLimit : BaseLimit;

        /// <summary>Cofres vinculados en el mundo según el último dato del servidor (más los recién puestos).</summary>
        internal static int Count => Known.Count(c => c.Linked && !c.Abyssal) + _placedSinceQuery;

        internal static void NotePlaced() => _placedSinceQuery++;

        private static int _abyssalPlacedSinceQuery;

        /// <summary>Cofres abisales en el mundo (máximo 1).</summary>
        internal static int AbyssalCount => Known.Count(c => c.Abyssal) + _abyssalPlacedSinceQuery;

        internal static void NoteAbyssalPlaced() => _abyssalPlacedSinceQuery++;

        /// <summary>Zonas de la red según el servidor (para usarla de lejos, sin el núcleo cargado).</summary>
        internal static List<NetworkSphere> Spheres()
        {
            var list = new List<NetworkSphere>();
            if (!HasCore) return list;
            list.Add(new NetworkSphere(CorePosition, StorageCore.RadiusFor(StorageCore.WorldLevel)));
            foreach (var p in Pillars) list.Add(new NetworkSphere(p, YggdrasilPlugin.PillarRadius.Value));
            return list;
        }

        /// <summary>Distancia del punto al núcleo o al pilar conectado más cercano (infinito si no hay núcleo).</summary>
        internal static float DistanceToNetwork(Vector3 pos)
        {
            if (!HasCore) return float.PositiveInfinity;
            float d = Vector3.Distance(pos, CorePosition);
            foreach (var p in Pillars) d = Mathf.Min(d, Vector3.Distance(pos, p));
            return d;
        }

        // ---------- Registro ----------

        internal static void Register()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null) return;
            rpc.Register<ZPackage>(QueryRpc, OnQuery);
            rpc.Register<ZPackage>(QueryResultRpc, (sender, pkg) => OnQueryResult(pkg));
            rpc.Register<ZPackage>(TakeRpc, OnTake);
            rpc.Register<ZPackage>(TakeResultRpc, (sender, pkg) => ChestRpc.OnTakeResult(pkg));
            rpc.Register<ZPackage>(StoreRpc, OnStore);
            rpc.Register<ZPackage>(StoreResultRpc, (sender, pkg) => ChestRpc.OnStoreResult(pkg));
        }

        /// <summary>Pide al servidor el estado de la red (como máximo cada 1,5 s, salvo que se fuerce).</summary>
        internal static void Query(bool force = false)
        {
            var player = Player.m_localPlayer;
            if (ZRoutedRpc.instance == null || !player) return;
            if (!force && Time.time - _lastQuery < 1.5f) return;
            _lastQuery = Time.time;
            var pkg = new ZPackage();
            pkg.Write(player.GetPlayerID());
            ZRoutedRpc.instance.InvokeRoutedRPC(QueryRpc, pkg);
        }

        /// <summary>¿Este cofre está cargado aquí? Entonces se usa el camino normal y no el del servidor.</summary>
        internal static bool IsLoadedHere(ZDOID id)
        {
            foreach (var c in ContainerRegistry.Loaded())
            {
                var zdo = ContainerRegistry.NView(c).GetZDO();
                if (zdo != null && zdo.m_uid.Equals(id)) return true;
            }
            return false;
        }

        // ---------- Pedidos del cliente ----------

        internal static void RequestTake(RemoteChest chest, NetworkItem entry, int amount)
        {
            var pkg = new ZPackage();
            pkg.Write(chest.Id);
            pkg.Write(entry.Name);
            pkg.Write(entry.Quality);
            pkg.Write(entry.Variant);
            pkg.Write(amount);
            ZRoutedRpc.instance.InvokeRoutedRPC(TakeRpc, pkg);
        }

        internal static void RequestStore(RemoteChest chest, Player player, ItemDrop.ItemData item, int amount)
        {
            var copy = item.Clone();
            copy.m_stack = amount;
            if (!player.GetInventory().RemoveItem(item, amount)) return;

            var pkg = new ZPackage();
            pkg.Write(chest.Id);
            pkg.Write(ChestRpc.Pack(new[] { copy }));
            ZRoutedRpc.instance.InvokeRoutedRPC(StoreRpc, pkg);
        }

        // ---------- Servidor ----------

        private static HashSet<int> _storagePrefabs;

        /// <summary>Prefabs de cofres construibles (con Container y Piece; sin barcos, carros ni el cofre de runas de Path of Valheiman).</summary>
        private static HashSet<int> StoragePrefabs()
        {
            if (_storagePrefabs != null) return _storagePrefabs;
            _storagePrefabs = new HashSet<int>();
            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                if (!prefab || !prefab.GetComponent<Container>() || !prefab.GetComponent<Piece>()) continue;
                if (prefab.GetComponentInChildren<Ship>(true) || prefab.GetComponentInChildren<Vagon>(true)) continue;
                if (prefab.name.StartsWith("PT_RuneVault")) continue;
                _storagePrefabs.Add(prefab.name.GetStableHashCode());
            }
            return _storagePrefabs;
        }

        private static IEnumerable<ZDO> AllZdos()
        {
            var dict = Traverse.Create(ZDOMan.instance).Field("m_objectsByID").GetValue() as IDictionary;
            if (dict == null) yield break;
            foreach (var value in dict.Values)
                if (value is ZDO zdo) yield return zdo;
        }

        private static void OnQuery(long sender, ZPackage pkg)
        {
            if (!ZNet.instance || !ZNet.instance.IsServer()) return;
            long playerId = pkg.ReadLong();

            int coreHash = CorePrefab.GetStableHashCode();
            int pillarHash = PillarPrefab.GetStableHashCode();
            int linkedHash = Prefab.GetStableHashCode();
            int abyssalHash = AbyssalPrefab.GetStableHashCode();
            var storage = StoragePrefabs();

            var all = AllZdos().ToList();
            var core = all.FirstOrDefault(z => z.GetPrefab() == coreHash);

            // Zonas: el núcleo y los pilares conectados en cadena.
            var spheres = new List<NetworkSphere>();
            var connectedPillars = new List<Vector3>();
            if (core != null)
            {
                spheres.Add(new NetworkSphere(core.GetPosition(), StorageCore.RadiusFor(StorageCore.WorldLevel)));
                var pending = all.Where(z => z.GetPrefab() == pillarHash).Select(z => z.GetPosition()).ToList();
                bool added = true;
                while (added)
                {
                    added = false;
                    foreach (var p in pending.ToList())
                    {
                        if (!spheres.Any(s => s.Contains(p))) continue;
                        spheres.Add(new NetworkSphere(p, YggdrasilPlugin.PillarRadius.Value));
                        connectedPillars.Add(p);
                        pending.Remove(p);
                        added = true;
                    }
                }
            }

            var wardPrefabs = WardPrefabs();
            var wards = all.Where(z => wardPrefabs.ContainsKey(z.GetPrefab()) && z.GetBool(ZDOVars.s_enabled, false))
                           .Select(z => new WardInfo { Zdo = z, Radius = wardPrefabs[z.GetPrefab()] })
                           .ToList();

            var chests = new List<ZDO>();
            foreach (var z in all)
            {
                int hash = z.GetPrefab();
                bool linked = hash == linkedHash || hash == abyssalHash;   // los dos están siempre en la red
                if (!linked && !storage.Contains(hash)) continue;
                if (!linked && !spheres.Any(s => s.Contains(z.GetPosition()))) continue;
                if (z.GetLong(ZDOVars.s_creator, 0L) == 0L) continue;   // cofres del mundo (mazmorras, ruinas): nadie los construyó
                if (IsPrivateOfOther(z, playerId)) continue;
                if (BlockedByWard(z.GetPosition(), playerId, wards)) continue;
                chests.Add(z);
            }

            if (YggdrasilPlugin.DebugLog.Value)
                YggdrasilPlugin.Log.LogInfo($"[Debug] Servidor: núcleo {(core != null)}, {connectedPillars.Count} pilares, {chests.Count} cofres en la red");

            var reply = new ZPackage();
            reply.Write(core != null);
            reply.Write(core != null ? core.GetPosition() : Vector3.zero);
            reply.Write(connectedPillars.Count);
            foreach (var p in connectedPillars) reply.Write(p);
            reply.Write(chests.Count);
            foreach (var z in chests)
            {
                reply.Write(z.m_uid);
                reply.Write(z.GetLong(ZDOVars.s_creator, 0L));
                reply.Write(z.GetPrefab() == linkedHash);
                reply.Write(z.GetPrefab() == abyssalHash);
                reply.Write(z.GetPosition());
                var size = ChestSize(z);
                reply.Write(size.x);
                reply.Write(size.y);
                reply.Write(z.GetByteArray(ZDOVars.s_items, null) ?? new byte[0]);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, QueryResultRpc, reply);
        }

        private sealed class WardInfo
        {
            public ZDO Zdo;
            public float Radius;
        }

        private static Dictionary<int, float> _wardPrefabs;

        /// <summary>Prefabs de guardas (PrivateArea) y su radio.</summary>
        private static Dictionary<int, float> WardPrefabs()
        {
            if (_wardPrefabs != null) return _wardPrefabs;
            _wardPrefabs = new Dictionary<int, float>();
            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                var area = prefab ? prefab.GetComponent<PrivateArea>() : null;
                if (area) _wardPrefabs[prefab.name.GetStableHashCode()] = area.m_radius;
            }
            return _wardPrefabs;
        }

        /// <summary>
        /// Igual que en el juego: un cofre dentro de una guarda activa solo se usa si el jugador es su dueño
        /// o está en su lista de permitidos.
        /// </summary>
        private static bool BlockedByWard(Vector3 pos, long playerId, List<WardInfo> wards)
        {
            foreach (var w in wards)
            {
                if (Vector3.Distance(w.Zdo.GetPosition(), pos) > w.Radius) continue;
                if (w.Zdo.GetLong(ZDOVars.s_creator, 0L) == playerId) continue;
                int permitted = w.Zdo.GetInt(ZDOVars.s_permitted, 0);
                bool allowed = false;
                for (int i = 0; i < permitted && !allowed; i++)
                    allowed = w.Zdo.GetLong("pu_id" + i, 0L) == playerId;
                if (!allowed) return true;
            }
            return false;
        }

        /// <summary>Cofres privados (el personal del juego) de otro jugador no entran.</summary>
        private static bool IsPrivateOfOther(ZDO zdo, long playerId)
        {
            var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            var container = prefab ? prefab.GetComponent<Container>() : null;
            if (!container || container.m_privacy != Container.PrivacySetting.Private) return false;
            return zdo.GetLong(ZDOVars.s_creator, 0L) != playerId;
        }

        /// <summary>
        /// El servidor puede tocar los datos del cofre si nadie lo tiene cargado, o si lo tiene cargado el
        /// propio servidor (anfitrión o mundo de un jugador). Si lo tiene cargado otro jugador, su juego manda.
        /// </summary>
        private static bool ServerMayEdit(ZDO zdo) =>
            zdo != null && (!zdo.HasOwner() || zdo.GetOwner() == ZDOMan.GetSessionID());

        /// <summary>Tamaño real del cofre (columnas, filas) según su prefab.</summary>
        private static Vector2i ChestSize(ZDO zdo)
        {
            var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            var container = prefab ? prefab.GetComponent<Container>() : null;
            return container ? new Vector2i(container.m_width, container.m_height) : new Vector2i(8, 4);
        }

        private static Inventory LoadInventory(ZDO zdo)
        {
            var size = ChestSize(zdo);
            var inv = new Inventory("YggdrasilRemote", null, size.x, size.y);
            var data = zdo.GetByteArray(ZDOVars.s_items, null);   // el juego guarda el inventario como bytes
            if (data != null && data.Length > 0) inv.Load(new ZPackage(data));
            return inv;
        }

        private static void SaveInventory(ZDO zdo, Inventory inv)
        {
            var pkg = new ZPackage();
            inv.Save(pkg);
            zdo.Set(ZDOVars.s_items, pkg.GetArray());   // igual que Container.Save; los cofres cargados se recargan solos
        }

        private static void OnTake(long sender, ZPackage pkg)
        {
            if (!ZNet.instance || !ZNet.instance.IsServer()) return;
            var zdo = ZDOMan.instance.GetZDO(pkg.ReadZDOID());
            string name = pkg.ReadString();
            int quality = pkg.ReadInt();
            int variant = pkg.ReadInt();
            int wanted = pkg.ReadInt();

            // Lo tiene cargado otro jugador: su juego manda sobre el cofre, así que se le reenvía el pedido
            // y él responde directo a quien lo pidió.
            if (zdo != null && !ServerMayEdit(zdo))
            {
                var fwd = new ZPackage();
                fwd.Write(sender);
                fwd.Write(name);
                fwd.Write(quality);
                fwd.Write(variant);
                fwd.Write(wanted);
                ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), zdo.m_uid, ChestRpc.TakeForRpc, fwd);
                return;
            }

            var removed = new List<ItemDrop.ItemData>();
            if (ServerMayEdit(zdo))
            {
                var inv = LoadInventory(zdo);
                int left = wanted;
                foreach (var item in inv.GetAllItems().ToList())
                {
                    if (left <= 0) break;
                    if (item.m_shared.m_name != name || item.m_quality != quality || item.m_variant != variant) continue;
                    int n = Mathf.Min(item.m_stack, left);
                    var copy = item.Clone();
                    copy.m_stack = n;
                    if (!inv.RemoveItem(item, n)) continue;
                    removed.Add(copy);
                    left -= n;
                }
                if (removed.Count > 0) SaveInventory(zdo, inv);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, TakeResultRpc, ChestRpc.Pack(removed));
        }

        private static void OnStore(long sender, ZPackage pkg)
        {
            if (!ZNet.instance || !ZNet.instance.IsServer()) return;
            var zdo = ZDOMan.instance.GetZDO(pkg.ReadZDOID());
            var itemsPkg = pkg.ReadPackage();

            if (zdo != null && !ServerMayEdit(zdo))
            {
                var fwd = new ZPackage();
                fwd.Write(sender);
                fwd.Write(itemsPkg);
                ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), zdo.m_uid, ChestRpc.StoreForRpc, fwd);
                return;
            }

            var incoming = ChestRpc.Unpack(itemsPkg);
            var leftovers = new List<ItemDrop.ItemData>();

            if (!ServerMayEdit(zdo))
            {
                leftovers.AddRange(incoming);
            }
            else
            {
                var inv = LoadInventory(zdo);
                foreach (var item in incoming)
                {
                    int fit = ChestRpc.MaxFit(inv, item, item.m_stack);
                    if (fit > 0)
                    {
                        var copy = item.Clone();
                        copy.m_stack = fit;
                        if (!inv.AddItem(copy)) fit = 0;
                    }
                    if (fit < item.m_stack)
                    {
                        var rest = item.Clone();
                        rest.m_stack = item.m_stack - fit;
                        leftovers.Add(rest);
                    }
                }
                SaveInventory(zdo, inv);
            }

            var reply = ChestRpc.Pack(leftovers);
            reply.Write(incoming.Sum(i => i.m_stack) - leftovers.Sum(i => i.m_stack));
            reply.Write(incoming.Count > 0 ? incoming[0].m_shared.m_name : "");
            ZRoutedRpc.instance.InvokeRoutedRPC(sender, StoreResultRpc, reply);
        }

        // ---------- Respuesta (cliente) ----------

        private static void OnQueryResult(ZPackage pkg)
        {
            HasCore = pkg.ReadBool();
            CorePosition = pkg.ReadVector3();
            Pillars.Clear();
            int pillars = pkg.ReadInt();
            for (int i = 0; i < pillars; i++) Pillars.Add(pkg.ReadVector3());

            Known.Clear();
            _placedSinceQuery = 0;
            _abyssalPlacedSinceQuery = 0;
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var chest = new RemoteChest
                {
                    Id = pkg.ReadZDOID(),
                    Creator = pkg.ReadLong(),
                    Linked = pkg.ReadBool(),
                    Abyssal = pkg.ReadBool(),
                    Position = pkg.ReadVector3(),
                };
                // Con el tamaño real: así se sabe de antemano si un ítem nuevo cabe (antes se suponía
                // espacio de sobra, el primer cofre lleno lo rechazaba y no se probaba el siguiente).
                int w = pkg.ReadInt(), h = pkg.ReadInt();
                chest.Inventory = new Inventory("YggdrasilRemote", null, Mathf.Max(1, w), Mathf.Max(1, h));
                var data = pkg.ReadByteArray();
                try
                {
                    if (data != null && data.Length > 0) chest.Inventory.Load(new ZPackage(data));
                }
                catch (System.Exception e)
                {
                    YggdrasilPlugin.Log.LogWarning($"Cofre {chest.Id}: no se pudo leer su inventario: {e.Message}");
                }
                Known.Add(chest);
            }
            ReceivedAt = Time.time;

            if (YggdrasilPlugin.DebugLog.Value)
                YggdrasilPlugin.Log.LogInfo($"[Debug] Red según el servidor: núcleo {HasCore}, {Pillars.Count} pilares, " +
                                            $"{Known.Count} cofres ({Known.Count(c => c.Linked)} vinculados, {Known.Count(c => !IsLoadedHere(c.Id))} lejanos)");
            UI.TerminalWindow.NotifyChanged();
        }
    }

    [HarmonyPatch(typeof(Game), "Start")]
    internal static class LinkedChestsRegisterPatch
    {
        private static void Postfix() => LinkedChests.Register();
    }

    /// <summary>Al construir un cofre vinculado: núcleo nivel 1 o más, y dentro del límite (3, o 10 tras la Reina).</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    internal static class LinkedChestPlacePatch
    {
        private static bool Prefix(Player __instance, Piece __0, ref bool __result)
        {
            if (__0 && __0.GetComponent<AbyssalChest>())
            {
                if (LinkedChests.AbyssalCount < 1) return true;
                __instance.Message(MessageHud.MessageType.Center, T("rv_abyssal_limit"));
                __result = false;
                return false;
            }
            if (!__0 || !__0.GetComponent<LinkedChest>()) return true;

            if (StorageCore.WorldLevel < 1)
            {
                __instance.Message(MessageHud.MessageType.Center, T("rv_linked_needs_level"));
                __result = false;
                return false;
            }
            if (LinkedChests.Count >= LinkedChests.Limit)
            {
                __instance.Message(MessageHud.MessageType.Center, T("rv_linked_limit", LinkedChests.Limit));
                __result = false;
                return false;
            }
            return true;
        }

        private static void Postfix(Piece __0, bool __result)
        {
            if (__result && __0 && __0.GetComponent<LinkedChest>()) LinkedChests.NotePlaced();
            if (__result && __0 && __0.GetComponent<AbyssalChest>()) LinkedChests.NoteAbyssalPlaced();
        }
    }

    /// <summary>Mantiene fresco el conteo mientras se construye.</summary>
    internal class LinkedChestTracker : MonoBehaviour
    {
        private float _next;

        private void Update()
        {
            if (Time.time < _next) return;
            _next = Time.time + 5f;
            var player = Player.m_localPlayer;
            if (player && player.InPlaceMode()) LinkedChests.Query();
        }
    }
}
