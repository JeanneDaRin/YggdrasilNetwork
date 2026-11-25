using UnityEngine;
using static YggdrasilNetwork.Models.ModelKit;

namespace YggdrasilNetwork.Models
{
    /// <summary>
    /// Terminal: losa de piedra rúnica de pie, con marco y runas azules por ambas caras,
    /// sobre un bloque de piedra con abrazaderas de metal y una base de tablones.
    /// </summary>
    internal static class TerminalModel
    {
        private const int Stone = 0, Metal = 1, Rune = 2, Wood = 3, BaseStone = 4;

        private const float SlabBottom = 0.30f, SlabTop = 1.45f, SlabHalfW = 0.44f, SlabHalfT = 0.11f;

        public static bool Apply(GameObject prefab)
        {
            var stone = Lit("ygg_terminal_stone",
                StoneTexture("ygg_stone_light", new Color(0.24f, 0.25f, 0.27f), new Color(0.48f, 0.49f, 0.52f), 41), Color.white);
            var baseStone = Lit("ygg_terminal_base",
                StoneTexture("ygg_stone_gray", new Color(0.16f, 0.17f, 0.19f), new Color(0.36f, 0.37f, 0.40f), 31), Color.white);
            var metal = Lit("ygg_terminal_metal",
                MetalTexture("ygg_metal_black", new Color(0.035f, 0.032f, 0.040f), new Color(0.120f, 0.110f, 0.135f), 23), Color.white);
            var wood = Lit("ygg_terminal_wood",
                WoodTexture("ygg_wood", new Color(0.20f, 0.12f, 0.07f), new Color(0.40f, 0.26f, 0.15f), 53), Color.white);
            if (!stone || !metal || !wood || !baseStone)
            {
                YggdrasilPlugin.Log.LogWarning("Terminal: sin materiales del juego, se deja el modelo original.");
                return false;
            }
            var rune = Glow("ygg_terminal_rune", GlowBlue * 1.1f);

            Replace(prefab, BuildMesh(), new[] { stone, metal, rune, wood, baseStone }, new[]
            {
                new BoxSpec(new Vector3(0f, 0.15f, 0f), new Vector3(1.25f, 0.30f, 0.62f)),
                new BoxSpec(new Vector3(0f, (SlabBottom + 1.62f) * 0.5f, 0f), new Vector3(SlabHalfW * 2f, 1.62f - SlabBottom, SlabHalfT * 2f + 0.06f)),
            });
            return true;
        }

        private static float Front(float y) => SlabHalfT;

        /// <summary>Tramo biselado de la losa: se angosta de ancho entre y0 e y1.</summary>
        private static void Bevel(MeshBuilder mb, float y0, float y1, float hx0, float hx1, float hz0, float hz1)
        {
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                bool top = (i & 4) != 0;
                float hx = top ? hx1 : hx0, hz = top ? hz1 : hz0;
                c[i] = new Vector3((i & 1) != 0 ? hx : -hx, top ? y1 : y0, (i & 2) != 0 ? hz : -hz);
            }
            mb.Hexa(Stone, c);
        }

        private static Mesh BuildMesh()
        {
            var mb = new MeshBuilder(5);

            // Tablones.
            mb.Box(Wood, new Vector3(0f, 0.035f, 0.17f), new Vector3(1.25f, 0.07f, 0.22f));
            mb.Box(Wood, new Vector3(0f, 0.035f, -0.17f), new Vector3(1.25f, 0.07f, 0.22f));
            mb.Box(Wood, new Vector3(0.45f, 0.10f, 0f), new Vector3(0.16f, 0.06f, 0.62f));
            mb.Box(Wood, new Vector3(-0.45f, 0.10f, 0f), new Vector3(0.16f, 0.06f, 0.62f));

            // Bloque de piedra con abrazaderas.
            mb.Box(BaseStone, new Vector3(0f, 0.20f, 0f), new Vector3(1.04f, 0.20f, 0.48f));
            mb.Box(Metal, new Vector3(0.40f, 0.22f, 0f), new Vector3(0.06f, 0.22f, 0.52f));
            mb.Box(Metal, new Vector3(-0.40f, 0.22f, 0f), new Vector3(0.06f, 0.22f, 0.52f));

            // Losa gruesa con remate en dos biseles (silueta de lápida rúnica).
            mb.Box(Stone, new Vector3(0f, (SlabBottom + SlabTop) * 0.5f, 0f),
                new Vector3(SlabHalfW * 2f, SlabTop - SlabBottom, SlabHalfT * 2f));
            Bevel(mb, SlabTop, 1.55f, SlabHalfW, 0.34f, SlabHalfT, SlabHalfT);
            Bevel(mb, 1.55f, 1.62f, 0.34f, 0.18f, SlabHalfT, SlabHalfT - 0.01f);

            // Ambas caras: borde tallado de metal con una cinta de runas pequeñas por dentro
            // y, al centro, un panel con dos runas grandes.
            foreach (var n in new[] { Vector3.forward, Vector3.back })
            {
                Frame(mb, Metal, n, Front, 0.37f, 0.40f, 1.37f, 0.035f, 0.022f);

                int k = 0;
                for (float y = 0.52f; y <= 1.25f; y += 0.105f, k++)
                {
                    Strokes(mb, Rune, Glyphs[k % Glyphs.Length], n, Front, -0.30f, y, 0.22f, 0.012f, 0.012f);
                    Strokes(mb, Rune, Glyphs[(k + 2) % Glyphs.Length], n, Front, 0.30f, y, 0.22f, 0.012f, 0.012f);
                }
                for (int i = -2; i <= 2; i++)
                {
                    Strokes(mb, Rune, Glyphs[(i + 4) % Glyphs.Length], n, Front, i * 0.105f, 1.29f, 0.22f, 0.012f, 0.012f);
                    Strokes(mb, Rune, Glyphs[(i + 5) % Glyphs.Length], n, Front, i * 0.105f, 0.48f, 0.22f, 0.012f, 0.012f);
                }

                Frame(mb, Rune, n, Front, 0.17f, 0.66f, 1.14f, 0.016f, 0.012f);
                Strokes(mb, Rune, Glyphs[2], n, Front, -0.075f, 0.90f, 0.62f, 0.022f);
                Strokes(mb, Rune, Glyphs[3], n, Front, 0.075f, 0.90f, 0.62f, 0.022f);
            }
            return mb.Build("YggdrasilTerminal");
        }
    }
}
