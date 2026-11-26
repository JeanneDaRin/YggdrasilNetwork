using UnityEngine;
using static YggdrasilNetwork.Models.ModelKit;

namespace YggdrasilNetwork.Models
{
    /// <summary>
    /// Los cofres mantienen el modelo del cofre de metal negro (así conservan la tapa animada)
    /// y se les agrega encima: una placa de metal con la runa ᚠ brillando al frente y atrás,
    /// y en el cofre abisal, púas de metal en las esquinas y runas también a los costados.
    /// </summary>
    internal static class ChestModels
    {
        private const int Metal = 0, Rune = 1, Black = 2, Stone = 3;

        // ᚠ (fehu, riqueza): la runa del icono de los cofres.
        private static readonly Vector2[] Fehu =
        {
            V(-0.06f, -0.15f), V(-0.06f, 0.15f),
            V(-0.06f, 0.08f), V(0.08f, 0.15f),
            V(-0.06f, -0.02f), V(0.08f, 0.05f),
        };

        public static void Linked(GameObject prefab) => Safe("cofre vinculado", () => Build(prefab, GlowBlue, false));

        public static void Abyssal(GameObject prefab) => Safe("cofre abisal", () => Build(prefab, GlowPurple, true));

        private static void Safe(string what, System.Action build)
        {
            try { build(); }
            catch (System.Exception e)
            {
                YggdrasilPlugin.Log.LogWarning($"Modelo de {what}: no se pudo decorar. {e.Message}");
            }
        }

        private static void Build(GameObject prefab, Color glow, bool abyssal)
        {
            // Placa de hierro gris: sobre el cofre negro una placa negra no se distinguía.
            var metal = Lit("ygg_chest_iron",
                MetalTexture("ygg_metal_iron", new Color(0.22f, 0.22f, 0.24f), new Color(0.42f, 0.42f, 0.45f), 29), Color.white);
            if (!metal) return;
            var rune = Glow(abyssal ? "ygg_chest_rune_void" : "ygg_chest_rune_blue", glow * 1.3f);
            var stone = Lit("ygg_chest_stone",
                StoneTexture("ygg_stone_dark", new Color(0.075f, 0.070f, 0.090f), new Color(0.205f, 0.190f, 0.235f), 11), Color.white);
            var black = Lit("ygg_chest_black",
                MetalTexture("ygg_metal_black", new Color(0.035f, 0.032f, 0.040f), new Color(0.120f, 0.110f, 0.135f), 23), Color.white);

            if (!Bounds(prefab, out var min, out var max))
            {
                YggdrasilPlugin.Log.LogWarning("Cofre: no se pudo medir el modelo original.");
                return;
            }
            Vector3 size = max - min, center = (min + max) * 0.5f;
            float plateY = min.y + size.y * 0.40f;
            float plate = Mathf.Clamp(size.y * 0.42f, 0.18f, 0.40f);   // alto de la placa

            var mb = new MeshBuilder(4);

            // Placas con runa al frente y atrás (no sabemos de qué lado queda la cerradura: van en ambos).
            int plateSub = abyssal ? Black : Metal;
            Plate(mb, Vector3.forward, max.z, -center.x, plateY, plate, plateSub, abyssal);
            Plate(mb, Vector3.back, -min.z, center.x, plateY, plate, plateSub, abyssal);
            if (abyssal)
                AbyssalDecor(prefab, mb, min, max);
            else
                LinkedDecor(prefab, mb, min, max, plateY, plate);
            var model = Child(prefab.transform, "YggdrasilDecor", prefab.layer, mb.Build("YggdrasilChestDecor"),
                new[] { metal, rune, black, stone });
            model.transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Cofre abisal: zócalo de piedra negra con runas alrededor de la base, cuatro cuernos grandes
        /// en las esquinas, placas en los cuatro lados, un anillo de runas que gira sobre el cofre
        /// El cofre además es un 15 % más grande.
        /// </summary>
        private static void AbyssalDecor(GameObject prefab, MeshBuilder mb, Vector3 min, Vector3 max)
        {
            Vector3 size = max - min, center = (min + max) * 0.5f;
            float plateY = min.y + size.y * 0.40f;
            float plate = Mathf.Clamp(size.y * 0.42f, 0.18f, 0.40f);
            Plate(mb, Vector3.right, max.x, center.z, plateY, plate * 0.85f, Black, true);
            Plate(mb, Vector3.left, -min.x, -center.z, plateY, plate * 0.85f, Black, true);

            // Zócalo: marco de piedra negra alrededor de la base (el cofre queda dentro, no encima).
            const float skirtH = 0.20f, skirtW = 0.24f, gap = 0.02f;
            float x0 = min.x - gap - skirtW, x1 = max.x + gap + skirtW;
            float z0 = min.z - gap - skirtW, z1 = max.z + gap + skirtW;
            mb.Box(Stone, new Vector3(center.x, min.y + skirtH * 0.5f, z1 - skirtW * 0.5f), new Vector3(x1 - x0, skirtH, skirtW));
            mb.Box(Stone, new Vector3(center.x, min.y + skirtH * 0.5f, z0 + skirtW * 0.5f), new Vector3(x1 - x0, skirtH, skirtW));
            mb.Box(Stone, new Vector3(x1 - skirtW * 0.5f, min.y + skirtH * 0.5f, center.z), new Vector3(skirtW, skirtH, z1 - z0 - skirtW * 2f));
            mb.Box(Stone, new Vector3(x0 + skirtW * 0.5f, min.y + skirtH * 0.5f, center.z), new Vector3(skirtW, skirtH, z1 - z0 - skirtW * 2f));
            // Runas pequeñas en todo el contorno del zócalo.
            var sides = new[]
            {
                new SkirtSide(Vector3.forward, z1, x1 - x0, -center.x),
                new SkirtSide(Vector3.back, -z0, x1 - x0, center.x),
                new SkirtSide(Vector3.right, x1, z1 - z0, center.z),
                new SkirtSide(Vector3.left, -x0, z1 - z0, -center.z),
            };
            int k = 0;
            foreach (var side in sides)
            {
                float len = side.Length;
                int count = Mathf.Max(3, Mathf.FloorToInt(len / 0.26f));
                for (int i = 0; i < count; i++, k++)
                {
                    float u = -len * 0.5f + (i + 0.5f) * len / count + side.Axis;
                    float dist = side.Dist;
                    Strokes(mb, Rune, Glyphs[k % Glyphs.Length], side.N, _ => dist, u, min.y + skirtH * 0.5f, 0.42f, 0.014f, 0.01f);
                }
            }

            // Cuernos: cuatro púas grandes en las esquinas del zócalo, hacia afuera y arriba,
            // más altas que el cofre; y púas menores a media altura.
            float horn = Mathf.Max(0.9f, size.y * 1.15f);
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    var corner = new Vector3(sx > 0 ? x1 : x0, min.y + skirtH, sz > 0 ? z1 : z0) - new Vector3(sx, 0f, sz) * skirtW * 0.5f;
                    mb.Spike(Black, corner - Vector3.up * 0.05f, new Vector3(sx * 0.35f, 1f, sz * 0.35f), horn, 0.10f);
                    mb.Spike(Black, corner + Vector3.up * 0.02f, new Vector3(sx, 0.25f, sz), horn * 0.42f, 0.06f);
                    var mid = new Vector3(sx > 0 ? max.x : min.x, min.y + size.y * 0.30f, sz > 0 ? max.z : min.z);
                    mb.Spike(Black, mid - new Vector3(sx, 0f, sz) * 0.04f, new Vector3(sx, 0.35f, sz), horn * 0.30f, 0.05f);
                }

            // Anillo de runas que gira y flota sobre el cofre.
            float radius = Mathf.Max(size.x, size.z) * 0.62f;
            var ring = new MeshBuilder(1);
            const int glyphs = 10;
            for (int i = 0; i < glyphs; i++)
            {
                float a = 2f * Mathf.PI * i / glyphs;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Strokes(ring, 0, Glyphs[i % Glyphs.Length], n, _ => radius, 0f, 0f, 0.9f, 0.022f, 0.012f);
            }
            var ringGo = Child(prefab.transform, "YggdrasilRuneRing", prefab.layer, ring.Build("YggdrasilRuneRing"),
                new[] { Glow("ygg_chest_ring", GlowPurple * 1.1f) });
            ringGo.transform.localPosition = new Vector3(center.x, max.y + 0.45f, center.z);
            var spin = ringGo.AddComponent<FloatSpin>();
            spin.DegreesPerSecond = 14f;
            spin.BobHeight = 0.06f;
            spin.BobSpeed = 0.9f;

            prefab.transform.localScale = Vector3.one * 1.15f;
        }

        /// <summary>
        /// Cofre vinculado como "nodo de la red": trazas brillantes tipo diagrama de red que salen
        /// de la placa hacia las esquinas con nodos en las uniones, y un cristal pequeño que flota
        /// sobre el cofre (eco del cristal del núcleo).
        /// </summary>
        private static void LinkedDecor(GameObject prefab, MeshBuilder mb, Vector3 min, Vector3 max, float plateY, float plate)
        {
            Vector3 size = max - min, center = (min + max) * 0.5f;
            float w = plate * 0.78f;
            const float line = 0.014f, node = 0.05f, lift = 0.008f;

            // Caras largas (frente y atrás): de la placa salen trazas a ambos lados con nodos.
            foreach (var face in new[]
            {
                new SkirtSide(Vector3.forward, max.z, size.x, -center.x),
                new SkirtSide(Vector3.back, -min.z, size.x, center.x),
            })
            {
                float half = face.Length * 0.5f, d = face.Dist;
                foreach (float s in new[] { -1f, 1f })
                {
                    float uPlate = s * (w * 0.5f + 0.03f);
                    float uMid = s * half * 0.55f;
                    float uEnd = s * (half - 0.14f);
                    float yUp = plateY + size.y * 0.24f;
                    float yDown = min.y + size.y * 0.12f;
                    var segs = new[]
                    {
                        V(uPlate, plateY), V(uEnd, plateY),     // horizontal hasta la esquina
                        V(uMid, plateY), V(uMid, yUp),          // rama hacia arriba
                        V(uMid, yUp), V(uEnd, yUp),
                        V(uMid, plateY), V(uMid, yDown),        // rama hacia abajo
                    };
                    Trace(mb, face, segs, d + lift, line);
                    foreach (var p in new[] { V(uEnd, plateY), V(uEnd, yUp), V(uMid, yDown), V(uMid, plateY) })
                        Node(mb, face, p, d + lift, node);
                }
            }

            // Costados: una traza de lado a lado con tres nodos.
            foreach (var face in new[]
            {
                new SkirtSide(Vector3.right, max.x, size.z, center.z),
                new SkirtSide(Vector3.left, -min.x, size.z, -center.z),
            })
            {
                float half = face.Length * 0.5f - 0.14f;
                Trace(mb, face, new[] { V(-half, plateY), V(half, plateY) }, face.Dist + lift, line);
                foreach (float u in new[] { -half, 0f, half })
                    Node(mb, face, V(u, plateY), face.Dist + lift, node);
            }

            // Cristal pequeño flotando sobre el cofre.
            var gemMb = new MeshBuilder(1) { Shade = true };
            gemMb.Crystal(0, Vector3.zero, 0.09f, 0.18f, 0.13f, 4, 45f);
            var gem = Child(prefab.transform, "YggdrasilLinkCrystal", prefab.layer, gemMb.Build("YggdrasilLinkCrystal"),
                new[] { Glow("ygg_link_crystal", new Color(0.50f, 0.80f, 1.00f) * 1.2f) });
            gem.transform.localPosition = new Vector3(center.x, max.y + 0.65f, center.z);
            var spin = gem.AddComponent<FloatSpin>();
            spin.DegreesPerSecond = 35f;
            spin.BobHeight = 0.05f;
        }

        /// <summary>Segmentos (en coordenadas u/altura de la cara) como trazas brillantes.</summary>
        private static void Trace(MeshBuilder mb, SkirtSide face, Vector2[] segs, float dist, float width)
        {
            var shifted = new Vector2[segs.Length];
            for (int i = 0; i < segs.Length; i++) shifted[i] = new Vector2(segs[i].x + face.Axis, segs[i].y);
            Strokes(mb, Rune, shifted, face.N, _ => dist, 0f, 0f, 1f, width, 0.01f);
        }

        /// <summary>Nodo: cuadrito brillante en una unión de trazas.</summary>
        private static void Node(MeshBuilder mb, SkirtSide face, Vector2 p, float dist, float size)
        {
            var seg = new[] { V(p.x + face.Axis, p.y - size * 0.25f), V(p.x + face.Axis, p.y + size * 0.25f) };
            Strokes(mb, Rune, seg, face.N, _ => dist, 0f, 0f, 1f, size, 0.016f);
        }

        /// <summary>Un lado del zócalo: normal, distancia desde el centro, largo y desplazamiento lateral.</summary>
        private class SkirtSide
        {
            public Vector3 N;
            public float Dist, Length, Axis;
            public SkirtSide(Vector3 n, float dist, float length, float axis) { N = n; Dist = dist; Length = length; Axis = axis; }
        }

        /// <summary>
        /// Placa de metal con la runa ᚠ encima, sobre la cara con normal <paramref name="n"/>.
        /// <paramref name="u"/> se mide hacia la derecha de quien mira la cara.
        /// </summary>
        private static void Plate(MeshBuilder mb, Vector3 n, float faceDist, float u, float y, float h, int sub = Metal, bool border = false)
        {
            float w = h * 0.78f;
            Vector3 tan = Vector3.Cross(n, Vector3.up);
            Vector3 c = n * (faceDist + 0.03f) + tan * u + Vector3.up * y;
            // Plancha (caja delgada) pegada a la cara.
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = c + tan * ((i & 1) != 0 ? w * 0.5f : -w * 0.5f)
                               + Vector3.up * ((i & 4) != 0 ? h * 0.5f : -h * 0.5f)
                               + n * ((i & 2) != 0 ? 0.012f : -0.03f);
            mb.Hexa(sub, corners);
            if (border)
            {
                // Borde brillante alrededor de la placa (en el cofre abisal la placa es negra).
                var edge = new[]
                {
                    V(-w * 0.42f, -h * 0.42f), V(-w * 0.42f, h * 0.42f), V(w * 0.42f, -h * 0.42f), V(w * 0.42f, h * 0.42f),
                    V(-w * 0.42f, -h * 0.42f), V(w * 0.42f, -h * 0.42f), V(-w * 0.42f, h * 0.42f), V(w * 0.42f, h * 0.42f),
                };
                Strokes(mb, Rune, edge, n, _ => faceDist + 0.042f, u, y, 1f, 0.012f, 0.008f);
            }
            // Runa sobre la plancha.
            float scale = h / 0.30f * 0.75f;
            Strokes(mb, Rune, Fehu, n, _ => faceDist + 0.042f, u, y, scale, 0.018f * scale / 0.75f, 0.012f);
        }

        /// <summary>
        /// Caja del cuerpo del cofre, en coordenadas del propio prefab. Se mide con sus colisiones:
        /// las mallas incluyen adornos que sobresalen y dejaban las placas y púas flotando.
        /// </summary>
        private static bool Bounds(GameObject prefab, out Vector3 min, out Vector3 max)
        {
            min = Vector3.positiveInfinity;
            max = Vector3.negativeInfinity;
            bool any = false;
            var root = prefab.transform;
            foreach (var col in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger) continue;
                UnityEngine.Bounds b;
                if (col is BoxCollider box) b = new UnityEngine.Bounds(box.center, box.size);
                else if (col is MeshCollider mc && mc.sharedMesh) b = mc.sharedMesh.bounds;
                else continue;
                var toRoot = root.worldToLocalMatrix * col.transform.localToWorldMatrix;
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
            if (any)
                Diag($"Cofre: medidas por colisión {max - min:F2}, desde {min:F2}");
            return any;
        }
    }
}
