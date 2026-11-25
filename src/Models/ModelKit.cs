using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YggdrasilNetwork.Models
{
    /// <summary>Caja de colisión del modelo (centro y tamaño, en metros).</summary>
    internal class BoxSpec
    {
        public Vector3 Center, Size;
        public BoxSpec(Vector3 center, Vector3 size) { Center = center; Size = size; }
    }

    /// <summary>
    /// Piezas comunes de los modelos hechos por código: reemplazo del modelo del prefab,
    /// materiales (piedra, metal, madera, brillo) y runas trazadas sobre caras.
    /// </summary>
    internal static class ModelKit
    {
        // ---------- Colores ----------

        public static readonly Color GlowBlue = new Color(0.22f, 0.55f, 1.00f);
        public static readonly Color GlowPurple = new Color(0.34f, 0.08f, 0.60f);

        // ---------- Reemplazo del modelo ----------

        /// <summary>
        /// Oculta el modelo original del prefab, quita sus colisiones y puntos de encaje,
        /// y agrega el modelo nuevo con sus cajas de colisión. Devuelve el objeto del modelo.
        /// </summary>
        public static GameObject Replace(GameObject prefab, Mesh mesh, Material[] materials, IEnumerable<BoxSpec> boxes)
        {
            int layer = prefab.layer;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
            foreach (var col in prefab.GetComponentsInChildren<Collider>(true))
            {
                layer = col.gameObject.layer;
                Object.DestroyImmediate(col);
            }
            var snaps = new List<GameObject>();
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
                if (t != prefab.transform && t.CompareTag("snappoint")) snaps.Add(t.gameObject);
            foreach (var s in snaps) Object.DestroyImmediate(s);

            var model = Child(prefab.transform, "YggdrasilModel", layer, mesh, materials);

            var colliders = new GameObject("YggdrasilCollider") { layer = layer };
            colliders.transform.SetParent(prefab.transform, false);
            foreach (var b in boxes)
            {
                var box = colliders.AddComponent<BoxCollider>();
                box.center = b.Center;
                box.size = b.Size;
            }
            return model;
        }

        /// <summary>Objeto hijo con malla y materiales (sin colisión).</summary>
        public static GameObject Child(Transform parent, string name, int layer, Mesh mesh, Material[] materials)
        {
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            return go;
        }

        /// <summary>Luz de color en una posición local (sin sombras).</summary>
        public static void AddLight(GameObject go, Color color, float range, float intensity, Vector3 localPos)
        {
            var lightGo = new GameObject("YggdrasilLight");
            lightGo.transform.SetParent(go.transform, false);
            lightGo.transform.localPosition = localPos;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        // ---------- Materiales ----------

        private static Material _base;
        private static bool _searched, _loggedProps;

        /// <summary>Material de piezas del juego (Custom/Piece) que sirve de base, o null.</summary>
        public static Material BaseMaterial()
        {
            if (_searched) return _base;
            _searched = true;
            foreach (var name in new[] { "stone_pillar", "stone_wall_1x1", "wood_pole2", "piece_chest_blackmetal" })
            {
                var go = PrefabManager.Instance.GetPrefab(name);
                if (!go) continue;
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (!m || !m.shader) continue;
                        string shader = m.shader.name;
                        if (shader == "Custom/Piece" || (shader.Contains("Piece") && !m.name.ToLower().Contains("snow")))
                        {
                            _base = m;
                            YggdrasilPlugin.Log.LogInfo($"Modelos: material base de {name} ({m.name}, shader {shader})");
                            return _base;
                        }
                    }
            }
            YggdrasilPlugin.Log.LogWarning("Modelos: no se encontró un material de piezas del juego.");
            return null;
        }

        /// <summary>Material iluminado (shader de piezas) con textura propia, mate.</summary>
        public static Material Lit(string name, Texture tex, Color color)
        {
            var baseMat = BaseMaterial();
            if (!baseMat) return null;
            var m = new Material(baseMat) { name = name };
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_BumpMap")) m.SetTexture("_BumpMap", null);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0f);
            m.DisableKeyword("_NORMALMAP");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            Matte(m);
            return m;
        }

        /// <summary>
        /// Material que brilla por sí mismo (sin iluminación): el emisivo de Custom/Piece no se ve en el juego.
        /// Sprites/Default siempre viene incluido en los juegos de Unity. Multiplica por el color de vértice,
        /// así los cristales pueden llevar sombreado por cara.
        /// </summary>
        public static Material Glow(string name, Color color)
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader)
            {
                YggdrasilPlugin.Log.LogWarning("Modelos: no está el shader Sprites/Default.");
                var fallback = Lit(name, Texture2D.whiteTexture, color);
                return fallback;
            }
            var m = new Material(shader) { name = name };
            color.a = 1f;
            m.SetColor("_Color", color);
            m.renderQueue = 2450;
            return m;
        }

        /// <summary>Quita brillo, metalizado y efecto mojado del material.</summary>
        private static void Matte(Material m)
        {
            var shader = m.shader;
            int count = shader.GetPropertyCount();
            var changed = new List<string>();
            for (int i = 0; i < count; i++)
            {
                var type = shader.GetPropertyType(i);
                if (type != UnityEngine.Rendering.ShaderPropertyType.Float &&
                    type != UnityEngine.Rendering.ShaderPropertyType.Range) continue;
                string prop = shader.GetPropertyName(i);
                string low = prop.ToLowerInvariant();
                if (low.Contains("gloss") || low.Contains("smooth") || low.Contains("metal") ||
                    low.Contains("spec") || low.Contains("wet") || low.Contains("reflect"))
                {
                    changed.Add($"{prop}={m.GetFloat(prop):0.##}");
                    m.SetFloat(prop, 0f);
                }
            }
            if (_loggedProps) return;
            _loggedProps = true;
            YggdrasilPlugin.Log.LogInfo($"Modelos: propiedades puestas en 0 en {shader.name}: {string.Join(", ", changed)}");
        }

        // ---------- Texturas ----------

        private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();

        /// <summary>Piedra: ruido con grietas finas.</summary>
        public static Texture2D StoneTexture(string name, Color dark, Color light, int seed) =>
            Noise(name, 128, dark, light, 1f, 1f, 0.9f, seed);

        /// <summary>Metal: ruido suave sin grietas.</summary>
        public static Texture2D MetalTexture(string name, Color dark, Color light, int seed) =>
            Noise(name, 64, dark, light, 1f, 1f, 0f, seed);

        /// <summary>Madera: vetas estiradas en un eje.</summary>
        public static Texture2D WoodTexture(string name, Color dark, Color light, int seed) =>
            Noise(name, 128, dark, light, 1f, 6f, 0f, seed);

        private static Texture2D Noise(string name, int size, Color dark, Color light,
            float stretchU, float stretchV, float cracks, int seed)
        {
            if (Textures.TryGetValue(name, out var cached)) return cached;
            var rnd = new System.Random(seed);
            float ox = rnd.Next(0, 5000), oy = rnd.Next(0, 5000);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size, v = y / (float)size;
                    float n = Tile(u, v, 4f * stretchU, 4f * stretchV, ox, oy) * 0.55f
                            + Tile(u, v, 10f * stretchU, 10f * stretchV, ox + 300f, oy) * 0.30f
                            + Tile(u, v, 24f * stretchU, 24f * stretchV, ox, oy + 300f) * 0.15f;
                    n = Mathf.Clamp01((n - 0.25f) * 2f);
                    float crack = 0f;
                    if (cracks > 0f)
                    {
                        float ridge = 1f - Mathf.Abs(Tile(u, v, 12f, 12f, ox + 900f, oy + 900f) * 2f - 1f);
                        crack = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.955f, 0.995f, ridge)) * cracks;
                    }
                    var c = Color.Lerp(dark, light, n) * (1f - crack * 0.45f);
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            tex.SetPixels(px);
            tex.Apply(true);
            Textures[name] = tex;
            return tex;
        }

        // Ruido que se repite sin costuras (mezcla de 4 muestras desplazadas un período).
        private static float Tile(float u, float v, float fu, float fv, float ox, float oy)
        {
            float P(float x, float y) => Mathf.PerlinNoise(ox + x * fu, oy + y * fv);
            float a = P(u, v), b = P(u - 1f, v), c = P(u, v - 1f), d = P(u - 1f, v - 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // ---------- Runas ----------

        /// <summary>
        /// Runas simétricas (en un objeto que se angosta, las asimétricas se ven chuecas).
        /// Segmentos en pares de puntos, en coordenadas de la cara (u horizontal, v vertical), ±0.15 m.
        /// </summary>
        public static readonly Vector2[][] Glyphs =
        {
            // ᛏ
            new[] { V(0f, -0.15f), V(0f, 0.15f), V(0f, 0.15f), V(-0.085f, 0.05f), V(0f, 0.15f), V(0.085f, 0.05f) },
            // ᛟ
            new[] { V(0f, 0.15f), V(0.08f, 0.04f), V(0.08f, 0.04f), V(0f, -0.06f), V(0f, -0.06f), V(-0.08f, 0.04f),
                    V(-0.08f, 0.04f), V(0f, 0.15f), V(0f, -0.06f), V(-0.09f, -0.15f), V(0f, -0.06f), V(0.09f, -0.15f) },
            // ᛉ
            new[] { V(0f, -0.15f), V(0f, 0.15f), V(0f, 0.01f), V(-0.09f, 0.13f), V(0f, 0.01f), V(0.09f, 0.13f) },
            // ᛞ
            new[] { V(-0.09f, -0.13f), V(-0.09f, 0.13f), V(0.09f, -0.13f), V(0.09f, 0.13f),
                    V(-0.09f, 0.13f), V(0.09f, -0.13f), V(-0.09f, -0.13f), V(0.09f, 0.13f) },
        };

        public static readonly Vector2[] Chevron =
        {
            V(-0.13f, 0.05f), V(0f, -0.04f), V(0f, -0.04f), V(0.13f, 0.05f),
        };

        /// <summary>
        /// Traza segmentos sobre una cara vertical. <paramref name="faceDist"/> da la distancia desde el eje
        /// a la cara según la altura (para caras que se angostan); <paramref name="n"/> es la normal de la cara.
        /// </summary>
        public static void Strokes(MeshBuilder mb, int sub, Vector2[] segs, Vector3 n, Func<float, float> faceDist,
            float cu, float cy, float scale, float width, float depth = 0.018f)
        {
            // Apoyada sobre la cara, sin hundirse: si se entierra a medias, desde ángulos rasantes
            // el juego la tapa con la piedra y la runa aparece cortada.
            Vector3 tan = Vector3.Cross(n, Vector3.up);   // derecha al mirar la cara desde afuera
            Vector3 P(Vector2 s)
            {
                float y = cy + s.y * scale;
                return n * (faceDist(y) + depth * 0.5f + 0.003f) + tan * (cu + s.x * scale) + Vector3.up * y;
            }
            for (int i = 0; i + 1 < segs.Length; i += 2)
                mb.Bar(sub, P(segs[i]), P(segs[i + 1]), n, width, depth);
        }

        /// <summary>Rectángulo de líneas sobre una cara (marco).</summary>
        public static void Frame(MeshBuilder mb, int sub, Vector3 n, Func<float, float> faceDist,
            float halfU, float y0, float y1, float width, float depth = 0.024f)
        {
            var segs = new[]
            {
                V(-halfU, y0), V(-halfU, y1), V(halfU, y0), V(halfU, y1),
                V(-halfU, y0), V(halfU, y0), V(-halfU, y1), V(halfU, y1),
            };
            Strokes(mb, sub, segs, n, faceDist, 0f, 0f, 1f, width, depth);
        }

        public static readonly Vector3[] Faces = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        public static Vector2 V(float x, float y) => new Vector2(x, y);
    }

    /// <summary>Gira y flota suavemente (cristal del núcleo).</summary>
    public class FloatSpin : MonoBehaviour
    {
        public float DegreesPerSecond = 22f;
        public float BobHeight = 0.05f;
        public float BobSpeed = 1.3f;
        private Vector3 _base;

        private void Awake() => _base = transform.localPosition;

        private void Update()
        {
            float t = Time.time;
            transform.localRotation = Quaternion.Euler(0f, t * DegreesPerSecond, 0f);
            transform.localPosition = _base + Vector3.up * (Mathf.Sin(t * BobSpeed) * BobHeight);
        }
    }
}
