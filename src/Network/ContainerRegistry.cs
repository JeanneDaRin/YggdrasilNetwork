using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace RuneVault.Network
{
    /// <summary>
    /// Lista de cofres construidos que están cargados en el mundo.
    /// Se llena con un parche en Container.Awake, así no hay que buscar en toda la escena.
    /// </summary>
    internal static class ContainerRegistry
    {
        private static readonly HashSet<Container> Containers = new HashSet<Container>();

        // Container.m_nview es privado en el juego.
        internal static readonly AccessTools.FieldRef<Container, ZNetView> NView =
            AccessTools.FieldRefAccess<Container, ZNetView>("m_nview");

        internal static void Register(Container container)
        {
            if (container) Containers.Add(container);
        }

        /// <summary>
        /// Cofres construibles dentro de alguna de las zonas, sin carros, barcos ni tumbas.
        /// Ordenados por distancia al centro de la primera zona (el núcleo).
        /// </summary>
        internal static List<Container> FindInSpheres(List<NetworkSphere> spheres)
        {
            Containers.RemoveWhere(c => !c);
            var origin = spheres.Count > 0 ? spheres[0].Center : Vector3.zero;

            // Los cofres vinculados entran siempre, estén donde estén (si están cargados).
            return Containers
                .Where(IsStorageChest)
                .Where(c => c.GetComponent<Pieces.LinkedChest>() || c.GetComponent<Pieces.AbyssalChest>() ||
                            spheres.Any(sp => sp.Contains(c.transform.position)))
                .OrderBy(c => (c.transform.position - origin).sqrMagnitude)
                .ToList();
        }

        /// <summary>Todos los cofres construibles cargados ahora mismo.</summary>
        internal static List<Container> Loaded()
        {
            Containers.RemoveWhere(c => !c);
            return Containers.Where(IsStorageChest).ToList();
        }

        private static bool IsStorageChest(Container c)
        {
            var nview = NView(c);
            if (!nview || !nview.IsValid()) return false;          // fantasmas del martillo
            var piece = c.GetComponent<Piece>();
            if (!piece) return false;                               // tumbas y similares no son piezas
            if (piece.GetCreator() == 0L) return false;             // cofres generados con el mundo: nadie los construyó
            if (c.name.StartsWith("PT_RuneVault")) return false;   // cofre de runas de Path of Valheiman (casillas propias)
            if (c.GetComponentInParent<Ship>()) return false;
            if (c.GetComponentInParent<Vagon>()) return false;
            return c.GetInventory() != null;
        }
    }

    [HarmonyPatch(typeof(Container), "Awake")]
    internal static class ContainerAwakePatch
    {
        private static void Postfix(Container __instance) => ContainerRegistry.Register(__instance);
    }
}
