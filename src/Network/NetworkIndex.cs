using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace YggdrasilNetwork.Network
{
    /// <summary>
    /// Lista (en el servidor) de los objetos del mundo que importan para la red: núcleo, pilares, cofres y guardas.
    /// Antes cada pedido recorría todos los objetos del mundo, lo que en mundos grandes provoca un tirón en el
    /// juego de quien hace de servidor. Ahora se recorre el mundo una sola vez y después la lista se mantiene sola:
    /// cada objeto que el juego ubica en el mapa (creado, cargado o recibido de un jugador) pasa por aquí.
    /// Por seguridad, la lista se rehace por completo cada 10 minutos de uso.
    /// </summary>
    internal static class NetworkIndex
    {
        private const float RebuildSeconds = 600f;

        private static readonly HashSet<ZDOID> Ids = new HashSet<ZDOID>();
        private static HashSet<int> _relevant;
        private static object _builtFor;        // ZDOMan del mundo para el que se armó la lista
        private static float _builtAt = -9999f;

        /// <summary>Objetos de la red que existen ahora (descarta los destruidos o reutilizados).</summary>
        internal static List<ZDO> Get(HashSet<int> relevant)
        {
            _relevant = relevant;
            if (_builtFor != ZDOMan.instance || Time.time - _builtAt > RebuildSeconds) Rebuild();

            var result = new List<ZDO>(Ids.Count);
            var gone = new List<ZDOID>();
            foreach (var id in Ids)
            {
                var zdo = ZDOMan.instance.GetZDO(id);
                if (zdo == null || !relevant.Contains(zdo.GetPrefab())) gone.Add(id);
                else result.Add(zdo);
            }
            foreach (var id in gone) Ids.Remove(id);
            return result;
        }

        private static void Rebuild()
        {
            var watch = Stopwatch.StartNew();
            Ids.Clear();
            int total = 0;
            var dict = Traverse.Create(ZDOMan.instance).Field("m_objectsByID").GetValue() as IDictionary;
            if (dict != null)
                foreach (var value in dict.Values)
                {
                    total++;
                    if (value is ZDO zdo && _relevant.Contains(zdo.GetPrefab())) Ids.Add(zdo.m_uid);
                }
            _builtFor = ZDOMan.instance;
            _builtAt = Time.time;
            YggdrasilPlugin.Log.LogInfo($"Red: lista del servidor armada ({Ids.Count} de {total} objetos del mundo) en {watch.ElapsedMilliseconds} ms.");
        }

        /// <summary>Llamado cada vez que el juego ubica un objeto en el mapa.</summary>
        internal static void Notice(ZDO zdo)
        {
            if (zdo == null || _relevant == null || _builtFor != ZDOMan.instance) return;
            if (_relevant.Contains(zdo.GetPrefab())) Ids.Add(zdo.m_uid);
        }
    }

    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.AddToSector))]
    internal static class NetworkIndexAddPatch
    {
        private static void Postfix(ZDO __0) => NetworkIndex.Notice(__0);
    }

    [HarmonyPatch(typeof(ZDOMan), "InitialAddToSector")]
    internal static class NetworkIndexInitialAddPatch
    {
        private static void Postfix(ZDO __0) => NetworkIndex.Notice(__0);
    }
}
