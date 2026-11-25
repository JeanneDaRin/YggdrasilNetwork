using UnityEngine;
using static YggdrasilNetwork.Models.ModelKit;

namespace YggdrasilNetwork.Models
{
    /// <summary>
    /// Núcleo: pedestal de piedra gris con runas azules en las cuatro caras
    /// y un cristal azul que flota y gira sobre él.
    /// </summary>
    internal static class CoreModel
    {
        private const int Stone = 0, Metal = 1, Rune = 2;

        private const float PedBottom = 0.30f, PedTop = 1.30f;
        private const float PedHalfBottom = 0.30f, PedHalfTop = 0.25f;
        public static readonly Vector3 CrystalPos = new Vector3(0f, 1.88f, 0f);

        public static bool Apply(GameObject prefab)
        {
            var stone = Lit("ygg_core_stone",
                StoneTexture("ygg_stone_gray", new Color(0.16f, 0.17f, 0.19f), new Color(0.36f, 0.37f, 0.40f), 31), Color.white);
            var metal = Lit("ygg_core_metal",
                MetalTexture("ygg_metal_black", new Color(0.035f, 0.032f, 0.040f), new Color(0.120f, 0.110f, 0.135f), 23), Color.white);
            if (!stone || !metal)
            {
                YggdrasilPlugin.Log.LogWarning("Núcleo: sin materiales del juego, se deja el modelo original.");
                return false;
            }
            var rune = Glow("ygg_core_rune", GlowBlue * 1.2f);
            var crystal = Glow("ygg_core_crystal", new Color(0.50f, 0.80f, 1.00f) * 1.25f);

            var model = Replace(prefab, BuildMesh(), new[] { stone, metal, rune }, new[]
            {
                new BoxSpec(new Vector3(0f, PedBottom * 0.5f, 0f), new Vector3(1.0f, PedBottom, 1.0f)),
                new BoxSpec(new Vector3(0f, (PedBottom + 1.46f) * 0.5f, 0f), new Vector3(0.62f, 1.46f - PedBottom, 0.62f)),
            });

            var gem = Child(prefab.transform, "YggdrasilCrystal", model.layer, BuildCrystal(), new[] { crystal });
            gem.transform.localPosition = CrystalPos;
            gem.AddComponent<FloatSpin>();
            return true;
        }

        private static float HalfAt(float y) =>
            Mathf.Lerp(PedHalfBottom, PedHalfTop, Mathf.InverseLerp(PedBottom, PedTop, y));

        private static Mesh BuildMesh()
        {
            var mb = new MeshBuilder(3);

            // Base escalonada.
            mb.Frustum(Stone, 0.00f, 0.18f, 0.50f, 0.47f);
            mb.Frustum(Stone, 0.18f, PedBottom, 0.40f, 0.37f);

            // Pedestal, banda de metal y remate en forma de copa.
            mb.Frustum(Stone, PedBottom, PedTop, PedHalfBottom, PedHalfTop);
            mb.Frustum(Metal, PedBottom - 0.01f, PedBottom + 0.08f, PedHalfBottom + 0.02f, PedHalfBottom + 0.015f);
            mb.Frustum(Stone, PedTop, 1.40f, 0.33f, 0.31f);
            mb.Frustum(Stone, 1.40f, 1.46f, 0.27f, 0.22f);
            mb.Frustum(Metal, 1.46f, 1.49f, 0.12f, 0.09f);   // engaste bajo el cristal

            // Runas: dos por cara dentro de un marco.
            for (int f = 0; f < Faces.Length; f++)
            {
                Vector3 n = Faces[f];
                Strokes(mb, Rune, Glyphs[f % Glyphs.Length], n, HalfAt, -0.095f, 0.86f, 0.8f, 0.024f);
                Strokes(mb, Rune, Glyphs[(f + 2) % Glyphs.Length], n, HalfAt, 0.095f, 0.86f, 0.8f, 0.024f);
                Frame(mb, Metal, n, HalfAt, 0.215f, 0.48f, 1.20f, 0.026f);
                Strokes(mb, Rune, Chevron, n, HalfAt, 0f, 1.12f, 0.9f, 0.02f);
            }
            return mb.Build("YggdrasilCore");
        }

        private static Mesh BuildCrystal()
        {
            var mb = new MeshBuilder(1) { Shade = true };
            mb.Crystal(0, Vector3.zero, 0.16f, 0.32f, 0.24f, 4, 45f);
            return mb.Build("YggdrasilCoreCrystal");
        }
    }
}
