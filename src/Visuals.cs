using System.IO;
using Jotunn.Utils;
using UnityEngine;

namespace YggdrasilNetwork
{
    /// <summary>
    /// Apariencia propia sin modelos nuevos: íconos PNG que viajan junto al DLL (carpeta Assets),
    /// y piezas del juego teñidas, con brillo y una luz de color.
    /// Las fuentes de los íconos (SVG) están en la carpeta Art del proyecto.
    /// </summary>
    internal static class Visuals
    {
        public static readonly Color RuneBlue = new Color(0.30f, 0.70f, 1.00f);
        public static readonly Color RunePurple = new Color(0.72f, 0.40f, 1.00f);

        private static string AssetsDir =>
            Path.Combine(Path.GetDirectoryName(typeof(YggdrasilPlugin).Assembly.Location) ?? "", "Assets");

        /// <summary>Carga Assets/icon_{name}.png como sprite, o null si falta (la pieza queda con el ícono original).</summary>
        public static Sprite Icon(string name)
        {
            string path = Path.Combine(AssetsDir, $"icon_{name}.png");
            if (!File.Exists(path))
            {
                YggdrasilPlugin.Log.LogWarning($"Falta el ícono {path}");
                return null;
            }
            var tex = AssetUtils.LoadTexture(path, false);
            if (!tex) return null;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Tiñe todos los materiales del objeto (copias propias: el original del juego no cambia)
        /// y les da brillo del color indicado.
        /// </summary>
        public static void Tint(GameObject go, Color tint, Color emission)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetType().Name == "ParticleSystemRenderer") continue;   // humo, chispas: se dejan igual
                var mats = r.sharedMaterials;
                var copy = new Material[mats.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    var m = new Material(mats[i]);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", m.GetColor("_Color") * tint);
                    if (emission != Color.black && m.HasProperty("_EmissionColor"))
                    {
                        m.EnableKeyword("_EMISSION");
                        m.SetColor("_EmissionColor", emission);
                    }
                    copy[i] = m;
                }
                r.sharedMaterials = copy;
            }
        }

        /// <summary>Luz de color sobre la pieza (sin sombras, para no costar rendimiento).</summary>
        public static void AddLight(GameObject go, Color color, float range, float intensity, float height)
        {
            var lightGo = new GameObject("RuneVaultLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, height, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }
    }
}
