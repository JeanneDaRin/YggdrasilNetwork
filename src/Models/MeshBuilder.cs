using System.Collections.Generic;
using UnityEngine;

namespace YggdrasilNetwork.Models
{
    /// <summary>
    /// Arma mallas low-poly por código (caras planas, sin Unity ni Blender).
    /// Cada cara recibe su propia normal y UV planar; el sentido de las caras se corrige solo
    /// comparando con el centro del sólido, así no hay caras invisibles por orden de vértices.
    /// </summary>
    internal class MeshBuilder
    {
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int>[] _subs;

        /// <summary>Repeticiones de textura por metro.</summary>
        public float UvScale = 0.5f;

        /// <summary>
        /// Si está activo, el color de vértice lleva un sombreado por cara (para materiales sin iluminación,
        /// como los cristales: así se distinguen las facetas).
        /// </summary>
        public bool Shade;

        private static readonly Vector3 ShadeLight = new Vector3(0.45f, 0.80f, 0.40f).normalized;

        public MeshBuilder(int submeshes)
        {
            _subs = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) _subs[i] = new List<int>();
        }

        public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 solidCenter)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            if (Vector3.Dot(n, (a + b + c + d) * 0.25f - solidCenter) < 0f)
            {
                var t = b; b = d; d = t;
                n = -n;
            }
            n.Normalize();
            int i0 = Add(a, n, a, b - a);
            Add(b, n, a, b - a);
            Add(c, n, a, b - a);
            Add(d, n, a, b - a);
            var s = _subs[sub];
            s.Add(i0); s.Add(i0 + 1); s.Add(i0 + 2);
            s.Add(i0); s.Add(i0 + 2); s.Add(i0 + 3);
            // Doble cara con la misma normal hacia afuera: se ve sólida venga de donde venga la cámara,
            // sin depender del sentido de los vértices que use el shader para descartar caras.
            s.Add(i0); s.Add(i0 + 2); s.Add(i0 + 1);
            s.Add(i0); s.Add(i0 + 3); s.Add(i0 + 2);
        }

        public void Tri(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 solidCenter)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, (a + b + c) / 3f - solidCenter) < 0f)
            {
                var t = b; b = c; c = t;
                n = -n;
            }
            n.Normalize();
            int i0 = Add(a, n, a, b - a);
            Add(b, n, a, b - a);
            Add(c, n, a, b - a);
            var s = _subs[sub];
            s.Add(i0); s.Add(i0 + 1); s.Add(i0 + 2);
            s.Add(i0); s.Add(i0 + 2); s.Add(i0 + 1);
        }

        /// <summary>
        /// Sólido de 6 caras a partir de 8 esquinas. Índice de esquina = bit0 (eje 1) | bit1 (eje 2) | bit2 (eje 3).
        /// </summary>
        public void Hexa(int sub, Vector3[] c)
        {
            Vector3 center = Vector3.zero;
            foreach (var p in c) center += p;
            center /= 8f;
            for (int k = 0; k < 3; k++)
            {
                int i = (k + 1) % 3, j = (k + 2) % 3;
                for (int val = 0; val < 2; val++)
                {
                    Vector3 P(int x, int y) => c[(val << k) | (x << i) | (y << j)];
                    Quad(sub, P(0, 0), P(1, 0), P(1, 1), P(0, 1), center);
                }
            }
        }

        /// <summary>Tronco de pirámide cuadrado, centrado en X/Z.</summary>
        public void Frustum(int sub, float y0, float y1, float half0, float half1, Vector3 offset = default)
        {
            var c = new Vector3[8];
            for (int idx = 0; idx < 8; idx++)
            {
                float sx = (idx & 1) != 0 ? 1f : -1f;
                float sz = (idx & 2) != 0 ? 1f : -1f;
                bool top = (idx & 4) != 0;
                float h = top ? half1 : half0;
                c[idx] = offset + new Vector3(sx * h, top ? y1 : y0, sz * h);
            }
            Hexa(sub, c);
        }

        /// <summary>Caja alineada a los ejes.</summary>
        public void Box(int sub, Vector3 center, Vector3 size)
        {
            Vector3 h = size * 0.5f;
            var c = new Vector3[8];
            for (int idx = 0; idx < 8; idx++)
                c[idx] = center + new Vector3((idx & 1) != 0 ? h.x : -h.x, (idx & 4) != 0 ? h.y : -h.y, (idx & 2) != 0 ? h.z : -h.z);
            Hexa(sub, c);
        }

        /// <summary>
        /// Cristal: anillo de <paramref name="sides"/> puntas a la altura del centro, con vértice arriba y abajo.
        /// </summary>
        public void Crystal(int sub, Vector3 center, float radius, float up, float down, int sides, float rotationDeg)
        {
            var ring = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (rotationDeg + 360f * i / sides) * Mathf.Deg2Rad;
                ring[i] = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
            }
            Vector3 top = center + Vector3.up * up, bottom = center - Vector3.up * down;
            for (int i = 0; i < sides; i++)
            {
                Tri(sub, ring[i], ring[(i + 1) % sides], top, center);
                Tri(sub, ring[i], ring[(i + 1) % sides], bottom, center);
            }
        }

        /// <summary>Pirámide: base de 4 esquinas en orden cíclico y una punta.</summary>
        public void Pyramid(int sub, Vector3[] baseQuad, Vector3 tip)
        {
            Vector3 b = (baseQuad[0] + baseQuad[1] + baseQuad[2] + baseQuad[3]) * 0.25f;
            Vector3 center = b + (tip - b) * 0.25f;
            for (int i = 0; i < 4; i++)
                Tri(sub, baseQuad[i], baseQuad[(i + 1) % 4], tip, center);
            Quad(sub, baseQuad[0], baseQuad[1], baseQuad[2], baseQuad[3], center);
        }

        /// <summary>Púa: pirámide alargada que sale de <paramref name="origin"/> hacia <paramref name="dir"/>.</summary>
        public void Spike(int sub, Vector3 origin, Vector3 dir, float length, float half)
        {
            dir.Normalize();
            Vector3 a = Vector3.Cross(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 b = Vector3.Cross(dir, a).normalized;
            var q = new[]
            {
                origin + (a + b) * half,
                origin + (-a + b) * half,
                origin + (-a - b) * half,
                origin + (a - b) * half,
            };
            Pyramid(sub, q, origin + dir * length);
        }

        /// <summary>
        /// Barra fina entre dos puntos apoyada sobre una cara (para trazar runas).
        /// <paramref name="normal"/> es la normal de la cara; la barra sobresale hacia afuera.
        /// </summary>
        public void Bar(int sub, Vector3 p1, Vector3 p2, Vector3 normal, float width, float depth)
        {
            Vector3 dir = (p2 - p1).normalized;
            p1 -= dir * (width * 0.5f);
            p2 += dir * (width * 0.5f);
            Vector3 side = Vector3.Cross(dir, normal).normalized * (width * 0.5f);
            Vector3 dn = normal.normalized * (depth * 0.5f);
            var c = new Vector3[8];
            for (int idx = 0; idx < 8; idx++)
            {
                Vector3 p = (idx & 4) != 0 ? p2 : p1;
                c[idx] = p + ((idx & 1) != 0 ? side : -side) + ((idx & 2) != 0 ? dn : -dn);
            }
            Hexa(sub, c);
        }

        private int Add(Vector3 p, Vector3 n, Vector3 origin, Vector3 edge)
        {
            int idx = _verts.Count;
            Vector3 e1 = edge.sqrMagnitude > 1e-12f ? edge.normalized : Vector3.right;
            Vector3 e2 = Vector3.Cross(n, e1);
            Vector3 d = p - origin;
            // Desplaza por la posición para que caras vecinas no repitan exactamente la misma zona de textura.
            Vector2 shift = new Vector2(origin.x + origin.z, origin.y) * UvScale;
            _verts.Add(p);
            _normals.Add(n);
            _uvs.Add(new Vector2(Vector3.Dot(d, e1), Vector3.Dot(d, e2)) * UvScale + shift);
            if (Shade)
            {
                float k = 0.55f + 0.45f * Mathf.Abs(Vector3.Dot(n, ShadeLight));
                _colors.Add(new Color(k, k, k, 1f));
            }
            else _colors.Add(Color.white);
            return idx;
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetColors(_colors);
            mesh.subMeshCount = _subs.Length;
            for (int i = 0; i < _subs.Length; i++) mesh.SetTriangles(_subs[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
