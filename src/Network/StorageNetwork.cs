using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace YggdrasilNetwork.Network
{
    /// <summary>Una zona esférica de la red (el núcleo o un pilar).</summary>
    internal sealed class NetworkSphere
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public NetworkSphere(Vector3 center, float radius) { Center = center; Radius = radius; }
        public bool Contains(Vector3 p) => (p - Center).sqrMagnitude <= Radius * Radius;
    }

    /// <summary>Qué cofres del radio entran a la red.</summary>
    public enum ChestFilter
    {
        ConPermiso,
        SoloPropios,
    }

    /// <summary>
    /// Un tipo de ítem en la red. Los ítems con distinta calidad o variante se listan aparte
    /// (una espada nivel 1 y una nivel 3 no son lo mismo).
    /// </summary>
    internal sealed class NetworkItem
    {
        public readonly string Name;       // token sin traducir, p. ej. "$item_wood"
        public readonly int Quality;
        public readonly int Variant;
        public readonly ItemDrop.ItemData Sample;
        public int Amount;

        public NetworkItem(ItemDrop.ItemData sample)
        {
            Name = sample.m_shared.m_name;
            Quality = sample.m_quality;
            Variant = sample.m_variant;
            Sample = sample;
        }

        public bool Matches(ItemDrop.ItemData item) =>
            item.m_shared.m_name == Name && item.m_quality == Quality && item.m_variant == Variant;
    }

    /// <summary>
    /// Un cofre de la red: cargado (se le pide al dueño del objeto) o vinculado lejano (se le pide al servidor).
    /// </summary>
    internal abstract class ChestSlot
    {
        public abstract Inventory Inventory { get; }
        public abstract bool Usable { get; }
        public abstract void RequestTake(NetworkItem entry, int amount);
        public abstract void RequestStore(Player player, ItemDrop.ItemData item, int amount);
    }

    internal sealed class LoadedChest : ChestSlot
    {
        public readonly Container Container;
        public LoadedChest(Container c) { Container = c; }
        public override Inventory Inventory => Container.GetInventory();

        // Con MultiUserChest varios jugadores comparten cofres abiertos; sin él, uno abierto queda fuera.
        public override bool Usable
        {
            get
            {
                var nview = ContainerRegistry.NView(Container);
                return nview && nview.IsValid() && (Compat.MultiUserChest || !Container.IsInUse());
            }
        }

        public override void RequestTake(NetworkItem entry, int amount) => ChestRpc.RequestTake(Container, entry, amount);
        public override void RequestStore(Player p, ItemDrop.ItemData item, int amount) => ChestRpc.RequestStore(Container, p, item, amount);
    }

    internal sealed class FarLinkedChest : ChestSlot
    {
        public readonly LinkedChests.RemoteChest Remote;
        public FarLinkedChest(LinkedChests.RemoteChest r) { Remote = r; }
        public override Inventory Inventory => Remote.Inventory;
        public override bool Usable => true;
        public override void RequestTake(NetworkItem entry, int amount) => LinkedChests.RequestTake(Remote, entry, amount);
        public override void RequestStore(Player p, ItemDrop.ItemData item, int amount) => LinkedChests.RequestStore(Remote, p, item, amount);
    }

    /// <summary>Los cofres de la red de un núcleo: los del radio (núcleo + pilares) y los vinculados.</summary>
    internal sealed class StorageNetwork
    {
        public readonly List<ChestSlot> Chests;

        /// <summary>Cantidad de cofres (para mostrar).</summary>
        public int ChestCount => Chests.Count;

        // Container.CheckAccess es privado en el juego: lo llamamos por un delegado.
        private static readonly Func<Container, long, bool> CheckAccess =
            AccessTools.MethodDelegate<Func<Container, long, bool>>(
                AccessTools.Method(typeof(Container), "CheckAccess", new[] { typeof(long) }));

        private StorageNetwork(List<ChestSlot> chests) { Chests = chests; }

        /// <summary>
        /// La red completa. Con el núcleo cargado se usan sus zonas; sin él (Runa de acceso, de lejos),
        /// las que mandó el servidor. Los cofres no cargados aquí llegan siempre por el servidor.
        /// </summary>
        public static StorageNetwork For(Pieces.StorageCore core, long playerId)
        {
            LinkedChests.Query();   // refresca la copia del servidor (con límite de frecuencia)
            return In(core ? core.NetworkSpheres() : LinkedChests.Spheres(), playerId);
        }

        public static StorageNetwork In(List<NetworkSphere> spheres, long playerId)
        {
            bool onlyOwn = YggdrasilPlugin.ChestFilterMode.Value == ChestFilter.SoloPropios;
            var loaded = ContainerRegistry.FindInSpheres(spheres)
                .Where(c => CheckAccess(c, playerId))
                // Igual que al abrirlo a mano: dentro de una guarda ajena, sin permiso, no se usa.
                .Where(c => PrivateArea.CheckAccess(c.transform.position, 0f, false, false))
                .Where(c => !onlyOwn || c.GetComponent<Pieces.LinkedChest>() || c.GetComponent<Piece>().GetCreator() == playerId)
                .ToList();

            var chests = loaded.Select(c => (ChestSlot)new LoadedChest(c)).ToList();

            // Cofres de la red que no están cargados aquí: se manejan a través del servidor.
            foreach (var remote in LinkedChests.Known)
            {
                if (LinkedChests.IsLoadedHere(remote.Id)) continue;
                if (onlyOwn && !remote.Linked && remote.Creator != playerId) continue;
                chests.Add(new FarLinkedChest(remote));
            }

            return new StorageNetwork(chests);
        }

        /// <summary>Todos los ítems de todos los cofres, sumados por tipo, calidad y variante.</summary>
        public List<NetworkItem> AggregateItems()
        {
            var result = new List<NetworkItem>();
            foreach (var chest in Chests)
            {
                foreach (var item in chest.Inventory.GetAllItems())
                {
                    var entry = result.FirstOrDefault(e => e.Matches(item));
                    if (entry == null)
                    {
                        entry = new NetworkItem(item);
                        result.Add(entry);
                    }
                    entry.Amount += item.m_stack;
                }
            }
            return result;
        }

        /// <summary>
        /// Pide sacar hasta <paramref name="wanted"/> unidades, repartidas entre los cofres según lo que cada uno
        /// tiene. Los ítems llegan con cada respuesta. Devuelve cuántas se pidieron (0 = no hay o no caben).
        /// </summary>
        public int RequestTake(Player player, NetworkItem entry, int wanted)
        {
            wanted = ChestRpc.MaxFit(player.GetInventory(), entry.Sample, wanted);   // no pedir más de lo que cabe
            int requested = 0;
            foreach (var chest in Chests)
            {
                if (requested >= wanted) break;
                if (!chest.Usable) continue;
                int has = chest.Inventory.GetAllItems().Where(entry.Matches).Sum(i => i.m_stack);
                int n = Math.Min(has, wanted - requested);
                if (n <= 0) continue;
                chest.RequestTake(entry, n);
                requested += n;
            }
            return requested;
        }

        /// <summary>
        /// Pide guardar hasta <paramref name="amount"/> unidades: primero en los cofres que ya tienen ese objeto,
        /// después en cualquiera con espacio. Lo que no quepa vuelve al jugador. Devuelve cuántas se enviaron.
        /// </summary>
        public int RequestStore(Player player, ItemDrop.ItemData item, int amount)
        {
            string name = item.m_shared.m_name;
            int sent = 0;

            var ordered = Chests
                .Where(c => c.Usable)
                .OrderByDescending(c => c.Inventory.GetAllItems().Any(i => i.m_shared.m_name == name))
                .ToList();

            foreach (var chest in ordered)
            {
                int remaining = Math.Min(amount - sent, item.m_stack);
                if (remaining <= 0) break;
                int fit = ChestRpc.MaxFit(chest.Inventory, item, remaining);
                if (fit <= 0) continue;
                chest.RequestStore(player, item, fit);
                sent += fit;
            }
            return sent;
        }

        /// <summary>Guarda todo lo del inventario que ya exista en la red (sin la barra rápida ni lo equipado).</summary>
        public int RequestStoreExisting(Player player)
        {
            var names = new HashSet<string>(AggregateItems().Select(i => i.Name));
            int total = 0;
            foreach (var item in player.GetInventory().GetAllItems().ToList())
            {
                if (item.m_gridPos.y == 0) continue;            // barra rápida (1-8)
                if (player.IsItemEquiped(item)) continue;
                if (!names.Contains(item.m_shared.m_name)) continue;
                total += RequestStore(player, item, item.m_stack);
            }
            return total;
        }
    }
}
