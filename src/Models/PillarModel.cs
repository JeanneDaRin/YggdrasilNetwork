using System.Collections.Generic;
using UnityEngine;
using static YggdrasilNetwork.Models.ModelKit;

namespace YggdrasilNetwork.Models
{
    /// <summary>
    /// Pilar rúnico: obelisco de piedra oscura sobre zócalo escalonado, púas de metal
    /// y runas moradas en las cuatro caras, dentro de un marco de metal.
    /// </summary>
    internal static class PillarModel
    {
        private const int Stone = 0, Metal = 1, Rune = 2;

        private const float ShaftBottom = 0.42f, ShaftTop = 2.60f;
        private const float ShaftHalfBottom = 0.30f, ShaftHalfTop = 0.22f;
        private const float TipTop = 3.15f;

        public static Color RuneColor => GlowPurple;

        public static bool Apply(GameObject prefab)
        {
            var stone = Lit("ygg_pillar_stone",
                StoneTexture("ygg_stone_dark", new Color(0.075f, 0.070f, 0.090f), new Color(0.205f, 0.190f, 0.235f), 11), Color.white);
            var metal = Lit("ygg_pillar_metal",
                MetalTexture("ygg_metal_black", new Color(0.035f, 0.032f, 0.040f), new Color(0.120f, 0.110f, 0.135f), 23), Color.white);
            if (!stone || !metal)
            {
                YggdrasilPlugin.Log.LogWarning("Pilar: sin materiales del juego, se deja el modelo original.");
                return false;
            }
            var rune = Glow("ygg_pillar_rune", GlowPurple * 1.5f);

            Replace(prefab, BuildMesh(), new[] { stone, metal, rune }, new[]
            {
                new BoxSpec(new Vector3(0f, ShaftBottom * 0.5f, 0f), new Vector3(1.1f, ShaftBottom, 1.1f)),
                new BoxSpec(new Vector3(0f, (ShaftBottom + TipTop) * 0.5f, 0f),
                    new Vector3(ShaftHalfBottom * 2f, TipTop - ShaftBottom, ShaftHalfBottom * 2f)),
            });
            return true;
        }

        private static float HalfAt(float y) =>
            Mathf.Lerp(ShaftHalfBottom, ShaftHalfTop, Mathf.InverseLerp(ShaftBottom, ShaftTop, y));

        private static Mesh BuildMesh()
        {
            var mb = new MeshBuilder(3);

            // Zócalo escalonado.
            mb.Frustum(Stone, 0.00f, 0.22f, 0.55f, 0.52f);
            mb.Frustum(Stone, 0.22f, ShaftBottom, 0.42f, 0.38f);

            // Obelisco, collares de metal y punta.
            mb.Frustum(Stone, ShaftBottom, ShaftTop, ShaftHalfBottom, ShaftHalfTop);
            mb.Frustum(Metal, ShaftBottom - 0.01f, ShaftBottom + 0.10f, ShaftHalfBottom + 0.035f, ShaftHalfBottom + 0.03f);
            mb.Frustum(Metal, ShaftTop - 0.08f, ShaftTop + 0.02f, ShaftHalfTop + 0.035f, ShaftHalfTop + 0.03f);
            float t = ShaftHalfTop;
            mb.Pyramid(Stone, new[]
            {
                new Vector3(t, ShaftTop, t), new Vector3(-t, ShaftTop, t),
                new Vector3(-t, ShaftTop, -t), new Vector3(t, ShaftTop, -t),
            }, new Vector3(0f, TipTop, 0f));

            // Púas en las esquinas, con la base metida en la piedra.
            foreach (float y in new[] { 0.95f, 1.55f, 2.15f })
                foreach (var corner in Corners())
                {
                    float h = HalfAt(y) * 0.75f;
                    var dir = (corner + Vector3.up * 0.45f).normalized;
                    float len = Mathf.Lerp(0.46f, 0.32f, Mathf.InverseLerp(0.95f, 2.15f, y));
                    mb.Spike(Metal, new Vector3(corner.x * h, y, corner.z * h), dir, len + 0.06f, 0.055f);
                }
            foreach (var corner in Corners())
            {
                var dir = (corner + Vector3.up * 0.15f).normalized;
                mb.Spike(Metal, new Vector3(corner.x * 0.5f, 0.12f, corner.z * 0.5f), dir, 0.34f, 0.06f);
            }

            // Runas: tres por cara, chevron bajo la punta, marco de metal y marcas en el zócalo.
            for (int f = 0; f < Faces.Length; f++)
            {
                Vector3 n = Faces[f];
                float[] heights = { 1.05f, 1.55f, 2.05f };
                for (int g = 0; g < heights.Length; g++)
                    Strokes(mb, Rune, Glyphs[(f + g) % Glyphs.Length], n, HalfAt, 0f, heights[g], 1f, 0.024f);
                Strokes(mb, Rune, Chevron, n, HalfAt, 0f, 2.42f, 1f, 0.024f);
                Frame(mb, Metal, n, HalfAt, 0.155f, 0.80f, 2.30f, 0.028f);
                for (int i = -3; i <= 3; i++)
                    Strokes(mb, Rune, new[] { V(0f, 0.26f), V(0f, 0.38f) }, n, PlinthAt, i * 0.10f, 0f, 1f, 0.022f, 0.016f);
            }

            return mb.Build("YggdrasilPillar");
        }

        private static float PlinthAt(float y) => Mathf.Lerp(0.42f, 0.38f, (y - 0.22f) / 0.20f);

        private static IEnumerable<Vector3> Corners()
        {
            yield return new Vector3(1f, 0f, 1f);
            yield return new Vector3(-1f, 0f, 1f);
            yield return new Vector3(-1f, 0f, -1f);
            yield return new Vector3(1f, 0f, -1f);
        }
    }
}
