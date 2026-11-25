using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using YggdrasilNetwork.UI;
using UnityEngine;
using static YggdrasilNetwork.I18n.Texts;

namespace YggdrasilNetwork.Network
{
    /// <summary>
    /// Mensajes entre jugadores para mover ítems de un cofre de la red.
    ///
    /// Solo el dueño de la ZDO de un cofre lo modifica (como hace el juego y MultiUserChest):
    /// quien usa la terminal le pide al dueño "sácame N de esto" o "guárdame esto", el dueño lo hace
    /// y responde con lo que salió o con lo que no cupo. Si el dueño es uno mismo, todo pasa en el acto.
    /// Así dos jugadores pueden usar terminales sobre los mismos cofres sin duplicar ni perder ítems.
    /// </summary>
    internal static class ChestRpc
    {
        private const string TakeRpc = "RuneVault_Take";
        private const string TakeResultRpc = "RuneVault_TakeResult";
        private const string StoreRpc = "RuneVault_Store";
        private const string StoreResultRpc = "RuneVault_StoreResult";

        internal static void Register(Container container)
        {
            var nview = ContainerRegistry.NView(container);
            if (!nview) return;
            nview.Register<ZPackage>(TakeRpc, (sender, pkg) => OnTake(container, sender, pkg));
            nview.Register<ZPackage>(TakeResultRpc, (sender, pkg) => OnTakeResult(pkg));
            nview.Register<ZPackage>(StoreRpc, (sender, pkg) => OnStore(container, sender, pkg));
            nview.Register<ZPackage>(StoreResultRpc, (sender, pkg) => OnStoreResult(pkg));
            nview.Register<ZPackage>(TakeForRpc, (sender, pkg) => OnTakeFor(container, pkg));
            nview.Register<ZPackage>(StoreForRpc, (sender, pkg) => OnStoreFor(container, pkg));
        }

        // ---------- Pedidos (quien usa la terminal) ----------

        internal static void RequestTake(Container chest, NetworkItem entry, int amount)
        {
            var pkg = new ZPackage();
            pkg.Write(entry.Name);
            pkg.Write(entry.Quality);
            pkg.Write(entry.Variant);
            pkg.Write(amount);
            ContainerRegistry.NView(chest).InvokeRPC(TakeRpc, pkg);
        }

        /// <summary>Saca los ítems del jugador antes de enviarlos; lo que no quepa vuelve en la respuesta.</summary>
        internal static void RequestStore(Container chest, Player player, ItemDrop.ItemData item, int amount)
        {
            var copy = item.Clone();
            copy.m_stack = amount;
            if (!player.GetInventory().RemoveItem(item, amount)) return;
            ContainerRegistry.NView(chest).InvokeRPC(StoreRpc, Pack(new[] { copy }));
        }

        // ---------- Dueño del cofre ----------

        /// <summary>Pedido de otro jugador reenviado por el servidor (el cofre estaba lejos para él y cargado aquí).</summary>
        internal const string TakeForRpc = "RuneVault_TakeFor";
        internal const string StoreForRpc = "RuneVault_StoreFor";

        private static void OnTake(Container chest, long sender, ZPackage pkg)
        {
            var nview = ContainerRegistry.NView(chest);
            if (!nview || !nview.IsOwner()) return;   // ya no somos dueños: el pedido se pierde sin mover nada
            var removed = TakeFrom(chest, pkg);
            nview.InvokeRPC(sender, TakeResultRpc, Pack(removed));
        }

        private static void OnTakeFor(Container chest, ZPackage pkg)
        {
            long replyTo = pkg.ReadLong();
            var nview = ContainerRegistry.NView(chest);
            var removed = nview && nview.IsOwner() ? TakeFrom(chest, pkg) : new List<ItemDrop.ItemData>();
            ZRoutedRpc.instance.InvokeRoutedRPC(replyTo, LinkedChests.TakeResultRpc, Pack(removed));
        }

        /// <summary>Saca del cofre hasta lo pedido (nombre, calidad, variante, cantidad).</summary>
        private static List<ItemDrop.ItemData> TakeFrom(Container chest, ZPackage pkg)
        {
            string name = pkg.ReadString();
            int quality = pkg.ReadInt();
            int variant = pkg.ReadInt();
            int wanted = pkg.ReadInt();

            var inv = chest.GetInventory();
            var removed = new List<ItemDrop.ItemData>();
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
            return removed;
        }

        private static void OnStore(Container chest, long sender, ZPackage pkg)
        {
            var nview = ContainerRegistry.NView(chest);
            nview.InvokeRPC(sender, StoreResultRpc, StoreInto(chest, Unpack(pkg)));
        }

        private static void OnStoreFor(Container chest, ZPackage pkg)
        {
            long replyTo = pkg.ReadLong();
            var incoming = Unpack(pkg.ReadPackage());
            ZRoutedRpc.instance.InvokeRoutedRPC(replyTo, LinkedChests.StoreResultRpc, StoreInto(chest, incoming));
        }

        /// <summary>Guarda lo que quepa y arma la respuesta: sobrantes + cantidad guardada + nombre.</summary>
        private static ZPackage StoreInto(Container chest, List<ItemDrop.ItemData> incoming)
        {
            var nview = ContainerRegistry.NView(chest);
            var leftovers = new List<ItemDrop.ItemData>();

            if (!nview || !nview.IsOwner())
            {
                leftovers.AddRange(incoming);                     // devolver todo
            }
            else
            {
                var inv = chest.GetInventory();
                foreach (var item in incoming)
                {
                    int fit = MaxFit(inv, item, item.m_stack);
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
            }

            var reply = Pack(leftovers);
            reply.Write(incoming.Sum(i => i.m_stack) - leftovers.Sum(i => i.m_stack));
            reply.Write(incoming.Count > 0 ? incoming[0].m_shared.m_name : "");
            return reply;
        }

        // ---------- Respuestas (quien usa la terminal) ----------

        internal static void OnTakeResult(ZPackage pkg)
        {
            var player = Player.m_localPlayer;
            if (!player) return;

            var items = Unpack(pkg);
            if (items.Count == 0)
            {
                player.Message(MessageHud.MessageType.TopLeft, T("rv_take_failed"));
                return;
            }

            foreach (var item in items) GiveOrDrop(player, item);
            player.Message(MessageHud.MessageType.TopLeft,
                T("rv_took", items.Sum(i => i.m_stack), Localize(items[0].m_shared.m_name)));
            LinkedChests.Query(true);   // la copia de los vinculados lejanos cambió
            TerminalWindow.NotifyChanged();
        }

        internal static void OnStoreResult(ZPackage pkg)
        {
            var player = Player.m_localPlayer;
            if (!player) return;

            var leftovers = Unpack(pkg);
            int stored = pkg.ReadInt();
            string name = Localize(pkg.ReadString());

            foreach (var item in leftovers) GiveOrDrop(player, item);
            player.Message(MessageHud.MessageType.TopLeft,
                stored > 0 ? T("rv_stored", stored, name) : T("rv_store_failed", name));
            LinkedChests.Query(true);
            TerminalWindow.NotifyChanged();
        }

        // ---------- Utilidades ----------

        /// <summary>Al inventario si cabe; si no, al suelo a los pies del jugador (nunca se pierde).</summary>
        private static void GiveOrDrop(Player player, ItemDrop.ItemData item)
        {
            var inv = player.GetInventory();
            if (inv.CanAddItem(item, item.m_stack) && inv.AddItem(item)) return;
            var t = player.transform;
            ItemDrop.DropItem(item, item.m_stack, t.position + t.forward * 0.5f + Vector3.up, Quaternion.identity);
        }

        internal static int MaxFit(Inventory inv, ItemDrop.ItemData item, int max)
        {
            if (max <= 0) return 0;
            if (inv.CanAddItem(item, max)) return max;
            int lo = 0, hi = max - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (inv.CanAddItem(item, mid)) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>Serializa ítems con todos sus datos (calidad, variante, datos de otros mods).</summary>
        internal static ZPackage Pack(IEnumerable<ItemDrop.ItemData> items)
        {
            var list = items.ToList();
            var temp = new Inventory("Yggdrasil", null, 8, Mathf.Max(1, list.Count));
            int y = 0;
            foreach (var item in list)
            {
                item.m_gridPos = new Vector2i(0, y++);   // una fila por ítem: nunca se mezclan stacks
                temp.GetAllItems().Add(item);
            }
            var pkg = new ZPackage();
            temp.Save(pkg);
            return pkg;
        }

        internal static List<ItemDrop.ItemData> Unpack(ZPackage pkg)
        {
            var temp = new Inventory("Yggdrasil", null, 8, 64);
            temp.Load(pkg);
            return temp.GetAllItems().ToList();
        }

        private static string Localize(string token) => global::Localization.instance.Localize(token);
    }

    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class ContainerRpcRegisterPatch
    {
        private static void Postfix(Container __instance) => ChestRpc.Register(__instance);
    }
}
