using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// A tiny procedural-mesh kit for the goods: revolve a profile (bottle, cup, egg, apple, cob),
    /// a superellipsoid (soft rounded box - loaf, bale - or a plain ellipsoid), and open double-sided
    /// sheets (leaves). Everything is smooth-shaded: normals are accumulated per triangle and then
    /// welded by POSITION inside a part, so seams and poles shade like the rest of the surface.
    /// Hard creases are made by building separate parts, never by duplicating points.
    ///
    /// Each material of a model is one submesh, so a model is one mesh asset + a material array
    /// (URP Lit has no vertex colours, and materials must be assets, never runtime-created).
    /// </summary>
    public class GoodsMeshBuilder
    {
        private readonly List<Vector3> _v = new List<Vector3>();
        private readonly List<Vector3> _n = new List<Vector3>();
        private readonly List<List<int>> _tris = new List<List<int>>();

        public int VertexCount => _v.Count;

        private List<int> Sub(int i)
        {
            while (_tris.Count <= i) _tris.Add(new List<int>());
            return _tris[i];
        }

        // ------------------------------------------------------------------ core

        /// <summary>
        /// A grid surface. <paramref name="pos"/> is called for i in 0..nu and j in 0..nv. With
        /// <paramref name="wrapU"/> column nu must coincide with column 0 (a full revolution).
        /// Closed surfaces are wound outward automatically (signed volume); open ones take
        /// <paramref name="flip"/> and may be given a back face.
        /// </summary>
        public void Surface(int sub, int nu, int nv, Func<int, int, Vector3> pos,
            bool closed = true, bool flip = false, float backFace = 0f)
        {
            int w = nu + 1;
            var p = new Vector3[w * (nv + 1)];
            for (int j = 0; j <= nv; j++)
                for (int i = 0; i <= nu; i++)
                    p[j * w + i] = pos(i, j);

            var idx = new List<int>();
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    int a = j * w + i, b = a + 1, c = a + w, d = c + 1;
                    AddTri(idx, p, a, c, b);
                    AddTri(idx, p, b, c, d);
                }

            bool reverse = flip;
            if (closed)
            {
                float vol = 0f;
                for (int t = 0; t < idx.Count; t += 3)
                    vol += Vector3.Dot(p[idx[t]], Vector3.Cross(p[idx[t + 1]], p[idx[t + 2]]));
                reverse = vol < 0f;
            }
            if (reverse)
                for (int t = 0; t < idx.Count; t += 3)
                {
                    int tmp = idx[t + 1]; idx[t + 1] = idx[t + 2]; idx[t + 2] = tmp;
                }

            // Area-weighted face normals, welded by position.
            var acc = new Vector3[p.Length];
            for (int t = 0; t < idx.Count; t += 3)
            {
                var fn = Vector3.Cross(p[idx[t + 1]] - p[idx[t]], p[idx[t + 2]] - p[idx[t]]);
                acc[idx[t]] += fn; acc[idx[t + 1]] += fn; acc[idx[t + 2]] += fn;
            }
            var weld = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 q) => new Vector3Int(
                Mathf.RoundToInt(q.x * 20000f), Mathf.RoundToInt(q.y * 20000f), Mathf.RoundToInt(q.z * 20000f));
            for (int k = 0; k < p.Length; k++)
            {
                var key = Key(p[k]);
                weld[key] = weld.TryGetValue(key, out var s) ? s + acc[k] : acc[k];
            }

            int baseIndex = _v.Count;
            for (int k = 0; k < p.Length; k++)
            {
                var n = weld[Key(p[k])];
                _v.Add(p[k]);
                _n.Add(n.sqrMagnitude > 1e-14f ? n.normalized : Vector3.up);
            }
            var list = Sub(sub);
            foreach (int i in idx) list.Add(baseIndex + i);

            if (backFace > 0f)
            {
                int back = _v.Count;
                for (int k = 0; k < p.Length; k++)
                {
                    var n = _n[baseIndex + k];
                    _v.Add(p[k] - n * backFace);
                    _n.Add(-n);
                }
                for (int t = 0; t < idx.Count; t += 3)
                {
                    list.Add(back + idx[t]); list.Add(back + idx[t + 2]); list.Add(back + idx[t + 1]);
                }
            }
        }

        private static void AddTri(List<int> idx, Vector3[] p, int a, int b, int c)
        {
            if (Vector3.Cross(p[b] - p[a], p[c] - p[a]).sqrMagnitude < 1e-14f) return;
            idx.Add(a); idx.Add(b); idx.Add(c);
        }

        // ------------------------------------------------------------------ shapes

        /// <summary>Spins a (radius, height) profile round Y. The first/last point may sit on the axis.</summary>
        public void Revolve(int sub, int segs, IList<Vector2> profile,
            Func<Vector3, float, float, Vector3> modify = null)
        {
            int rings = profile.Count - 1;
            Surface(sub, segs, rings, (i, j) =>
            {
                float u = i / (float)segs;
                float a = u * Mathf.PI * 2f;
                var q = new Vector3(profile[j].x * Mathf.Cos(a), profile[j].y, profile[j].x * Mathf.Sin(a));
                return modify != null ? modify(q, u, j / (float)rings) : q;
            });
        }

        public static float SE(float c, float e) => Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), e);

        /// <summary>
        /// A superellipsoid: e = 1 is an ellipsoid, e towards 0 is a box with soft edges. The
        /// polar axis is Y, or X when <paramref name="alongX"/>. Optionally moved/bent by
        /// <paramref name="modify"/>, then placed at <paramref name="centre"/> and yawed.
        /// </summary>
        public List<Vector3> Super(int sub, Vector3 centre, Vector3 half, float e1, float e2,
            int nu, int nv, bool alongX = false, float yaw = 0f, float pitch = 0f, float roll = 0f,
            Func<Vector3, Vector3> modify = null)
        {
            var pts = new List<Vector3>();
            var rot = Quaternion.Euler(pitch, yaw, roll);
            Surface(sub, nu, nv, (i, j) =>
            {
                float u = i / (float)nu * Mathf.PI * 2f;
                float v = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, j / (float)nv);
                Vector3 q;
                if (alongX)
                    q = new Vector3(half.x * SE(Mathf.Sin(v), e1),
                                    half.y * SE(Mathf.Cos(v), e1) * SE(Mathf.Sin(u), e2),
                                    half.z * SE(Mathf.Cos(v), e1) * SE(Mathf.Cos(u), e2));
                else
                    q = new Vector3(half.x * SE(Mathf.Cos(v), e1) * SE(Mathf.Cos(u), e2),
                                    half.y * SE(Mathf.Sin(v), e1),
                                    half.z * SE(Mathf.Cos(v), e1) * SE(Mathf.Sin(u), e2));
                if (modify != null) q = modify(q);
                q = centre + rot * q;
                pts.Add(q);
                return q;
            });
            return pts;
        }

        public void Ellipsoid(int sub, Vector3 centre, Vector3 half, int nu = 16, int nv = 10,
            float yaw = 0f, float pitch = 0f, float roll = 0f) =>
            Super(sub, centre, half, 1f, 1f, nu, nv, false, yaw, pitch, roll);

        // ------------------------------------------------------------------ profile helper

        /// <summary>Catmull-Rom through the points, so a profile can be sketched with a handful.</summary>
        public static List<Vector2> Spline(IList<Vector2> pts, int subdiv = 6)
        {
            var result = new List<Vector2>();
            int n = pts.Count;
            for (int s = 0; s < n - 1; s++)
            {
                var p0 = pts[Mathf.Max(s - 1, 0)];
                var p1 = pts[s];
                var p2 = pts[s + 1];
                var p3 = pts[Mathf.Min(s + 2, n - 1)];
                for (int k = 0; k < subdiv; k++)
                {
                    float t = k / (float)subdiv, t2 = t * t, t3 = t2 * t;
                    var q = 0.5f * (2f * p1 + (-p0 + p2) * t +
                        (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                        (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                    result.Add(new Vector2(Mathf.Max(0f, q.x), q.y));
                }
            }
            result.Add(pts[n - 1]);
            return result;
        }

        // ------------------------------------------------------------------ whole-model ops

        public void Transform(int fromVertex, Matrix4x4 m)
        {
            var nm = m.inverse.transpose;
            for (int i = fromVertex; i < _v.Count; i++)
            {
                _v[i] = m.MultiplyPoint3x4(_v[i]);
                _n[i] = nm.MultiplyVector(_n[i]).normalized;
            }
        }

        /// <summary>
        /// Uniformly scales and centres the whole model on its bounds, so its height is
        /// <paramref name="height"/> (or less if that would make it wider than
        /// <paramref name="maxWidth"/>). Returns the final bounds size.
        /// </summary>
        public Vector3 Fit(float height, float maxWidth)
        {
            var b = new Bounds(_v[0], Vector3.zero);
            foreach (var v in _v) b.Encapsulate(v);
            float s = Mathf.Min(height / b.size.y, maxWidth / Mathf.Max(b.size.x, b.size.z));
            Transform(0, Matrix4x4.Scale(Vector3.one * s) * Matrix4x4.Translate(-b.center));
            return b.size * s;
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (_v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(_v);
            m.SetNormals(_n);
            var uv = new Vector2[_v.Count];
            m.SetUVs(0, uv);
            m.subMeshCount = Mathf.Max(1, _tris.Count);
            for (int i = 0; i < _tris.Count; i++) m.SetTriangles(_tris[i], i);
            m.RecalculateBounds();
            return m;
        }
    }
}
