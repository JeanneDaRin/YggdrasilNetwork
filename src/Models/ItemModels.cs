using UnityEngine;
using static YggdrasilNetwork.Models.ModelKit;

namespace YggdrasilNetwork.Models
{
    /// <summary>
    /// Modelos de los objetos (cuando están tirados en el suelo):
    /// Runa de acceso (gema azul con ᚱ), Runa del Vacío (esquirla de piedra negra con ᚱ morada)
    /// y Cristal del Vacío (cristal morado). Se ajustan al tamaño del objeto original.
    /// </summary>
    internal static class ItemModels
    {
        // Runa ᚱ (en un objeto pequeño y plano no se ve chueca).
        private static readonly Vector2[] Raido =
        {
            V(-0.07f, -0.15f), V(-0.07f, 0.15f), V(-0.07f, 0.15f), V(0.07f, 0.07f),
            V(0.07f, 0.07f), V(-0.07f, 0.0f), V(-0.07f, 0.0f), V(0.08f, -0.15f),
        };

        private static readonly Color AccessGem = new Color(0.45f, 0.75f, 1.00f);
        private static readonly Color AccessRuneLine = new Color(0.85f, 0.95f, 1.00f);
        private static readonly Color VoidGlow = new Color(0.62f, 0.25f, 0.95f);
        private static readonly Color CrystalColor = new Color(0.62f, 0.30f, 0.92f);

        public static void AccessRune(GameObject prefab) => Safe("AccessRune", () => BuildAccessRune(prefab));

        private static void BuildAccessRune(GameObject prefab)
        {
            var mb = new MeshBuilder(2) { Shade = true };
            Gem(mb, 0, Outline(4, 0.30f, 0.50f, 0f, 0), 0.11f, 0.60f);
            mb.Shade = false;
            RuneBothSides(mb, 1, 0.115f, 0.95f, 0.03f);
            Apply(prefab, mb.Build("YggdrasilAccessRune"),
                new[] { Glow("ygg_item_gem_blue", AccessGem * 1.1f), Glow("ygg_item_rune_white", AccessRuneLine * 1.4f) },
                GlowBlue);
        }

        public static void VoidRune(GameObject prefab) => Safe("VoidRune", () => BuildVoidRune(prefab));

        private static void BuildVoidRune(GameObject prefab)
        {
            var stone = Lit("ygg_item_void_stone",
                StoneTexture("ygg_stone_dark", new Color(0.075f, 0.070f, 0.090f), new Color(0.205f, 0.190f, 0.235f), 11), Color.white);
            if (!stone) return;
            var mb = new MeshBuilder(2);
            Gem(mb, 0, Outline(7, 0.30f, 0.50f, 0.22f, 7), 0.13f, 0.62f);
            RuneBothSides(mb, 1, 0.135f, 0.9f, 0.03f);
            Apply(prefab, mb.Build("YggdrasilVoidRune"),
                new[] { stone, Glow("ygg_item_rune_void", VoidGlow * 1.4f) }, GlowPurple);
        }

        public static void VoidCrystal(GameObject prefab) => Safe("VoidCrystal", () => BuildVoidCrystal(prefab));

        private static void BuildVoidCrystal(GameObject prefab)
        {
            var mb = new MeshBuilder(1) { Shade = true };
            Spire(mb, 0, Vector3.zero, Quaternion.identity, 0.20f, -0.30f, 0.20f, 0.48f, -0.46f, 6);
            Spire(mb, 0, new Vector3(0.18f, -0.18f, 0.04f), Quaternion.Euler(0f, 0f, -32f), 0.09f, -0.10f, 0.08f, 0.22f, -0.16f, 6);
            Spire(mb, 0, new Vector3(-0.15f, -0.22f, -0.05f), Quaternion.Euler(10f, 0f, 28f), 0.07f, -0.08f, 0.06f, 0.16f, -0.12f, 5);
            Apply(prefab, mb.Build("YggdrasilVoidCrystal"),
                new[] { Glow("ygg_item_crystal", CrystalColor * 1.1f) }, GlowPurple);
        }

        /// <summary>Si algo falla, el objeto queda con su modelo original en vez de no registrarse.</summary>
        private static void Safe(string what, System.Action build)
        {
            try { build(); }
            catch (System.Exception e)
            {
                YggdrasilPlugin.Log.LogWarning($"Modelo de {what}: no se pudo armar, queda el original. {e.Message}");
            }
        }

        // ---------- Formas ----------

        /// <summary>
        /// Contorno en el plano X/Y (alto = <paramref name="halfH"/>*2). Con <paramref name="jitter"/> &gt; 0
        /// los radios varían para que parezca una esquirla rota.
        /// </summary>
        private static Vector3[] Outline(int points, float halfW, float halfH, float jitter, int seed)
        {
            var rnd = new System.Random(seed);
            var o = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float a = Mathf.PI * 0.5f + 2f * Mathf.PI * i / points;   // empieza arriba
                float r = 1f - (float)rnd.NextDouble() * jitter;
                o[i] = new Vector3(Mathf.Cos(a) * halfW * r, Mathf.Sin(a) * halfH * r, 0f);
            }
            return o;
        }

        /// <summary>Gema plana: contorno al medio y una "mesa" más chica adelante y atrás.</summary>
        private static void Gem(MeshBuilder mb, int sub, Vector3[] outline, float thickness, float tableScale)
        {
            int n = outline.Length;
            var front = new Vector3[n];
            var back = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                front[i] = outline[i] * tableScale + Vector3.forward * thickness;
                back[i] = outline[i] * tableScale - Vector3.forward * thickness;
            }
            Vector3 c = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                mb.Quad(sub, outline[i], outline[j], front[j], front[i], c);
                mb.Quad(sub, outline[i], outline[j], back[j], back[i], c);
            }
            Vector3 fc = Vector3.forward * thickness, bc = -fc;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                mb.Tri(sub, front[i], front[j], fc, c);
                mb.Tri(sub, back[i], back[j], bc, c);
            }
        }

        /// <summary>Cristal tipo prisma con punta arriba y abajo.</summary>
        private static void Spire(MeshBuilder mb, int sub, Vector3 pos, Quaternion rot,
            float rBottom, float yBottom, float rTop, float yTip, float yBottomTip, int sides)
        {
            var lo = new Vector3[sides];
            var hi = new Vector3[sides];
            float yTop = yTip - (yTip - yBottom) * 0.28f;
            for (int i = 0; i < sides; i++)
            {
                float a = 2f * Mathf.PI * i / sides;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                lo[i] = pos + rot * (d * rBottom + Vector3.up * yBottom);
                hi[i] = pos + rot * (d * rTop + Vector3.up * yTop);
            }
            Vector3 tip = pos + rot * (Vector3.up * yTip);
            Vector3 bottom = pos + rot * (Vector3.up * yBottomTip);
            Vector3 c = pos + rot * (Vector3.up * ((yTip + yBottomTip) * 0.5f));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                mb.Quad(sub, lo[i], lo[j], hi[j], hi[i], c);
                mb.Tri(sub, hi[i], hi[j], tip, c);
                mb.Tri(sub, lo[i], lo[j], bottom, c);
            }
        }

        private static void RuneBothSides(MeshBuilder mb, int sub, float faceDist, float scale, float width)
        {
            foreach (var n in new[] { Vector3.forward, Vector3.back })
            {
                // ModelKit.Strokes mide la altura desde y=0; aquí el centro del objeto está en y=0.
                Strokes(mb, sub, Raido, n, _ => faceDist, 0f, 0f, scale, width, 0.02f);
            }
        }

        // ---------- Montaje ----------

        /// <summary>
        /// Oculta el modelo del objeto original (sin tocar su física) y pone el nuevo,
        /// escalado al tamaño del original y centrado donde estaba. Tiñe sus luces.
        /// </summary>
        private static void Apply(GameObject prefab, Mesh mesh, Material[] materials, Color lightColor)
        {
            var root = prefab.transform;
            bool any = false;
            var min = Vector3.positiveInfinity;
            var max = Vector3.negativeInfinity;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh) continue;
                var b = mf.sharedMesh.bounds;
                var toRoot = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1));
                    var p = toRoot.MultiplyPoint3x4(corner);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
                any = true;
            }
            Vector3 center = any ? (min + max) * 0.5f : Vector3.zero;
            float size = any ? Mathf.Max(max.x - min.x, Mathf.Max(max.y - min.y, max.z - min.z)) : 0.3f;

            foreach (var r in prefab.GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
            foreach (var l in prefab.GetComponentsInChildren<Light>(true))
            {
                l.color = lightColor;
                l.intensity = Mathf.Min(l.intensity, 1f);
            }

            mesh.RecalculateBounds();
            var mb = mesh.bounds.size;
            float meshSize = Mathf.Max(mb.x, Mathf.Max(mb.y, mb.z));
            var model = Child(root, "YggdrasilModel", prefab.layer, mesh, materials);
            model.transform.localPosition = center;
            model.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, meshSize));
        }
    }
}
