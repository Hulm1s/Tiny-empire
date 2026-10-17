using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The florist's 3D art, in the same style as the approved goods / butcher / cash art: the four
    /// flowers as goods (pink rose, tulip, sunflower, blue hydrangea) plus a watering can and a
    /// wrapped bouquet, flower beds in three states (growing, blooming, dry), the well, the shop's
    /// counter, wall stands, corner stands and the square central stand.
    ///
    /// Everything is built from <see cref="GoodsMeshBuilder"/>: rounded superellipsoids, revolves and
    /// open double-sided sheets (leaves, paper), one submesh per material. Meshes are saved as assets
    /// (goods under Meshes/Goods like every other good, the rest under Meshes/Florist) and every
    /// material is an ASSET (Fl_*); a runtime Material renders magenta under URP.
    ///
    /// NOT wired in: nothing references these yet. Goods follow the GoodsModels contract (pivot at the
    /// centre of the stack cell, x1.6 convention, no shadow). Because LevelBuildKit.Item looks up
    /// <c>GoodsModels.PrefabPath(id)</c>, items with the ids below pick their prefab up automatically.
    ///
    /// The stands hold buckets of flowers as children named Fill_{id}: decorative, auto-filled, and the
    /// game can switch a flower's children on only once that flower is unlocked.
    ///
    /// Orientation: counter and stands face -z toward the customer (staff / wall side +z).
    /// </summary>
    public static partial class FloristModels
    {
        public const string MeshFolder = "Assets/_Project/Meshes/Florist";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Florist";

        public static readonly string[] GoodIds = { "rose", "tulip", "sunflower", "hydrangea", "watering_can", "bouquet" };
        public static readonly string[] FlowerIds = { "rose", "tulip", "sunflower", "hydrangea" };
        public static readonly string[] BedStates = { "growing", "blooming", "dry" };

        public enum Fl { Rose, Tulip, Sunflower, Hydrangea }

        /// <summary>Bed footprint in metres (x by z); soil top height.</summary>
        public static readonly Vector2 BedSize = new Vector2(1.80f, 2.44f);
        public const float SoilTop = 0.20f;

        // ------------------------------------------------------------------ materials

        private static Material M(string name, Color c, float smooth, float metallic = 0f)
        {
            var m = LevelBuildKit.Mat(name, c, smooth);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b);

        private static Material Lib(string k)
        {
            switch (k)
            {
                case "stem": return M("Fl_Stem", C(0.30f, 0.62f, 0.22f), 0.35f);
                case "leaf": return M("Fl_Leaf", C(0.42f, 0.76f, 0.28f), 0.40f);
                case "rose_a": return M("Fl_RoseA", C(1.00f, 0.52f, 0.68f), 0.50f);
                case "rose_b": return M("Fl_RoseB", C(0.88f, 0.26f, 0.48f), 0.50f);
                case "tulip_a": return M("Fl_TulipA", C(0.96f, 0.26f, 0.32f), 0.50f);
                case "tulip_b": return M("Fl_TulipB", C(1.00f, 0.52f, 0.44f), 0.50f);
                case "sun_petal": return M("Fl_SunPetal", C(1.00f, 0.82f, 0.12f), 0.45f);
                case "sun_petal_b": return M("Fl_SunPetalB", C(1.00f, 0.62f, 0.08f), 0.45f);
                case "sun_core": return M("Fl_SunCore", C(0.50f, 0.28f, 0.10f), 0.30f);
                case "sun_core_b": return M("Fl_SunCoreB", C(0.26f, 0.14f, 0.06f), 0.30f);
                case "hyd_a": return M("Fl_HydA", C(0.42f, 0.62f, 1.00f), 0.50f);
                case "hyd_b": return M("Fl_HydB", C(0.70f, 0.84f, 1.00f), 0.50f);
                case "hyd_c": return M("Fl_HydC", C(0.52f, 0.46f, 0.92f), 0.50f);
                case "dry_stem": return M("Fl_DryStem", C(0.58f, 0.52f, 0.28f), 0.20f);
                case "dry_leaf": return M("Fl_DryLeaf", C(0.70f, 0.60f, 0.32f), 0.20f);
                case "dry_petal": return M("Fl_DryPetal", C(0.78f, 0.64f, 0.46f), 0.20f);
                case "dry_petal_b": return M("Fl_DryPetalB", C(0.64f, 0.50f, 0.34f), 0.20f);
                case "dry_core": return M("Fl_DryCore", C(0.36f, 0.26f, 0.16f), 0.20f);
                case "soil": return M("Fl_Soil", C(0.40f, 0.27f, 0.17f), 0.15f);
                case "soil_dry": return M("Fl_SoilDry", C(0.80f, 0.66f, 0.48f), 0.10f);
                case "crack": return M("Fl_SoilCrack", C(0.50f, 0.38f, 0.26f), 0.10f);
                case "wood": return M("Fl_Wood", C(0.80f, 0.60f, 0.36f), 0.25f);
                case "wood_dark": return M("Fl_WoodDark", C(0.55f, 0.38f, 0.22f), 0.25f);
                case "wood_light": return M("Fl_WoodLight", C(0.93f, 0.77f, 0.55f), 0.30f);
                case "cream": return M("Fl_Cream", C(0.99f, 0.95f, 0.88f), 0.45f);
                case "pink": return M("Fl_Pink", C(1.00f, 0.72f, 0.80f), 0.50f);
                case "mint": return M("Fl_Mint", C(0.66f, 0.90f, 0.80f), 0.50f);
                case "paper": return M("Fl_Paper", C(1.00f, 0.91f, 0.76f), 0.25f);
                case "paper_pink": return M("Fl_PaperPink", C(1.00f, 0.68f, 0.78f), 0.30f);
                case "ribbon": return M("Fl_Ribbon", C(0.62f, 0.42f, 0.92f), 0.55f);
                case "tin": return M("Fl_Tin", C(0.80f, 0.86f, 0.90f), 0.70f, 0.20f);
                case "tin_band": return M("Fl_TinBand", C(1.00f, 0.74f, 0.82f), 0.50f);
                case "stone": return M("Fl_Stone", C(0.82f, 0.80f, 0.76f), 0.30f);
                case "stone_dark": return M("Fl_StoneDark", C(0.64f, 0.62f, 0.60f), 0.30f);
                case "water": return M("Fl_Water", C(0.36f, 0.72f, 0.96f), 0.90f);
                case "roof": return M("Fl_Roof", C(0.88f, 0.36f, 0.32f), 0.45f);
                case "rope": return M("Fl_Rope", C(0.84f, 0.70f, 0.46f), 0.20f);
                case "can": return M("Fl_Can", C(0.46f, 0.86f, 0.90f), 0.65f, 0.10f);
                case "can_dark": return M("Fl_CanDark", C(0.14f, 0.52f, 0.66f), 0.60f, 0.10f);
                case "brass": return M("Fl_Brass", C(1.00f, 0.82f, 0.30f), 0.70f, 0.30f);
                case "glass": return M("Fl_Glass", C(0.86f, 0.95f, 0.99f), 0.88f);
                case "dark": return M("Fl_Dark", C(0.24f, 0.22f, 0.26f), 0.30f);
            }
            return LibRound2(k);
        }

        /// <summary>
        /// A model's palette: material keys get submesh indices in order of first use, so a model
        /// never carries a material it does not draw with.
        /// </summary>
        private sealed class Pal
        {
            private readonly List<Material> _m = new List<Material>();
            private readonly Dictionary<string, int> _i = new Dictionary<string, int>();

            public int I(string key)
            {
                if (_i.TryGetValue(key, out int v)) return v;
                _i[key] = _m.Count;
                _m.Add(Lib(key));
                return _m.Count - 1;
            }

            public Material[] Mats => _m.ToArray();
        }

        private struct Model
        {
            public GoodsMeshBuilder Mesh;
            public Pal Pal;
        }

        // ------------------------------------------------------------------ primitives

        private static Vector2 V(float r, float y) => new Vector2(r, y);

        private static List<Vector2> Dense(int n, Func<float, Vector2> f)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= n; i++) l.Add(f(i / (float)n));
            return l;
        }

        /// <summary>Draws whatever <paramref name="draw"/> adds at the origin, then moves it into place.</summary>
        private static void Put(GoodsMeshBuilder b, Vector3 pos, Quaternion rot, float s, Action draw)
        {
            int from = b.VertexCount;
            draw();
            b.Transform(from, Matrix4x4.TRS(pos, rot, Vector3.one * s));
        }

        private static List<Vector3> Slab(GoodsMeshBuilder b, int sub, Vector3 c, Vector3 h, float e = 0.25f,
            float yaw = 0f, float pitch = 0f, float roll = 0f, int nu = 24, int nv = 14,
            Func<Vector3, Vector3> modify = null) =>
            b.Super(sub, c, h, e, e, nu, nv, false, yaw, pitch, roll, modify);

        private static void Ell(GoodsMeshBuilder b, int sub, Vector3 c, Vector3 half, int nu = 14, int nv = 10) =>
            b.Ellipsoid(sub, c, half, nu, nv);

        /// <summary>An ellipsoid petal whose tip narrows to a point.</summary>
        private static void Petal(GoodsMeshBuilder b, int sub, Vector3 half, float point = 0.7f, int nu = 12, int nv = 8) =>
            b.Super(sub, Vector3.zero, half, 1f, 1f, nu, nv, false, 0f, 0f, 0f, q =>
            {
                float t = Mathf.Clamp01(q.y / half.y * 0.5f + 0.5f);
                q.x *= 1f - point * t * t * t;
                return q;
            });

        /// <summary>
        /// A round tube along a path (t from 0 to 1) with rounded ends. <paramref name="side"/> is any
        /// vector not parallel to the path, only used to orient the rings; the winding is checked so
        /// the surface always faces outward.
        /// </summary>
        private static void Tube(GoodsMeshBuilder b, int sub, Func<float, Vector3> path, Vector3 side,
            float r0, float r1, int segs = 10, int around = 8)
        {
            Func<int, int, Vector3> pos = (i, j) =>
            {
                float t = j / (float)segs;
                var pt = path(t);
                var tan = (path(Mathf.Min(1f, t + 0.01f)) - path(Mathf.Max(0f, t - 0.01f))).normalized;
                var n = Vector3.Cross(tan, side).normalized;
                var bi = Vector3.Cross(tan, n);
                float r = Mathf.Lerp(r0, r1, t);
                float a = i / (float)around * Mathf.PI * 2f;
                return pt + (n * Mathf.Cos(a) + bi * Mathf.Sin(a)) * r;
            };
            var du = pos(1, 0) - pos(0, 0);
            var dj = pos(0, 1) - pos(0, 0);
            bool flip = Vector3.Dot(Vector3.Cross(dj, du), pos(0, 0) - path(0f)) < 0f;
            b.Surface(sub, around, segs, pos, closed: false, flip: flip);
            b.Ellipsoid(sub, path(0f), new Vector3(r0, r0, r0), 8, 6);
            b.Ellipsoid(sub, path(1f), new Vector3(r1, r1, r1), 8, 6);
        }

        /// <summary>A leaf lying along local +Z, lifted by <paramref name="pitchUp"/> degrees and drooping at the tip.</summary>
        private static void Leaf(GoodsMeshBuilder b, int sub, Vector3 pos, float yaw, float pitchUp, float len,
            float wid, float droop = 0.3f)
        {
            Put(b, pos, Quaternion.Euler(-pitchUp, yaw, 0f), 1f, () =>
                b.Surface(sub, 6, 8, (i, j) =>
                {
                    float w = i / 6f * 2f - 1f, s = j / 8f;
                    float half = wid * 0.5f * Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.75f));
                    float y = -Mathf.Abs(w) * half * 0.45f - droop * len * s * s;
                    return new Vector3(w * half, y, s * len);
                }, closed: false, flip: false, backFace: len * 0.015f));
        }

        // ------------------------------------------------------------------ flower heads
        // Each head is drawn at the origin, opening towards +Y, about 1 unit across, then placed.

        private static void Rose(GoodsMeshBuilder b, Pal p, Vector3 pos, Quaternion rot, float s, bool dry = false)
        {
            int A = p.I(dry ? "dry_petal" : "rose_a"), B = p.I(dry ? "dry_petal_b" : "rose_b"),
                G = p.I(dry ? "dry_stem" : "stem");
            float droop = dry ? 26f : 0f;
            Put(b, pos, rot, s, () =>
            {
                Ell(b, G, new Vector3(0f, 0.05f, 0f), new Vector3(0.26f, 0.13f, 0.26f), 14, 8);
                Ell(b, B, new Vector3(0f, 0.52f, 0f), new Vector3(0.20f, 0.36f, 0.20f), 14, 10);
                float[] rr = { 0.20f, 0.40f, 0.60f };
                float[] yy = { 0.58f, 0.42f, 0.26f };
                float[] tilt = { 14f, 36f, 62f };
                int[] cnt = { 5, 7, 6 };
                for (int k = 0; k < 3; k++)
                    for (int n = 0; n < cnt[k]; n++)
                    {
                        var q = Quaternion.Euler(0f, n * 360f / cnt[k] + k * 23f, 0f);
                        var half = new Vector3(0.24f + 0.03f * k, 0.30f, 0.055f);
                        int sub = k == 0 ? B : A;
                        Put(b, q * new Vector3(0f, yy[k], rr[k]), q * Quaternion.Euler(tilt[k] + droop * (k * 0.5f), 0f, 0f), 1f,
                            () => Ell(b, sub, Vector3.zero, half, 12, 8));
                    }
            });
        }

        private static void Tulip(GoodsMeshBuilder b, Pal p, Vector3 pos, Quaternion rot, float s, bool dry = false)
        {
            int A = p.I(dry ? "dry_petal" : "tulip_a"), B = p.I(dry ? "dry_petal_b" : "tulip_b");
            float open = dry ? 22f : 0f;
            Put(b, pos, rot, s, () =>
            {
                Ell(b, A, new Vector3(0f, 0.38f, 0f), new Vector3(0.28f, 0.38f, 0.28f), 16, 10);
                for (int n = 0; n < 3; n++)
                {
                    var qi = Quaternion.Euler(0f, 60f + n * 120f, 0f);
                    Put(b, qi * new Vector3(0f, 0.54f, 0.06f), qi * Quaternion.Euler(-4f + open, 0f, 0f), 1f,
                        () => Petal(b, B, new Vector3(0.25f, 0.48f, 0.13f)));
                    var qo = Quaternion.Euler(0f, n * 120f, 0f);
                    Put(b, qo * new Vector3(0f, 0.50f, 0.14f), qo * Quaternion.Euler(8f + open, 0f, 0f), 1f,
                        () => Petal(b, A, new Vector3(0.27f, 0.50f, 0.13f)));
                }
            });
        }

        private static void Sunflower(GoodsMeshBuilder b, Pal p, Vector3 pos, Quaternion rot, float s, bool dry = false)
        {
            int P = p.I(dry ? "dry_petal" : "sun_petal"), PB = p.I(dry ? "dry_petal_b" : "sun_petal_b"),
                C = p.I(dry ? "dry_core" : "sun_core"), CB = p.I(dry ? "dry_core" : "sun_core_b"),
                G = p.I(dry ? "dry_stem" : "stem");
            float droop = dry ? 30f : 0f;
            Put(b, pos, rot, s, () =>
            {
                b.Revolve(G, 24, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, -0.02f), V(0.30f, 0f), V(0.48f, 0.05f), V(0.50f, 0.10f), V(0f, 0.12f)
                }, 4));
                b.Revolve(C, 32, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, 0.205f), V(0.20f, 0.19f), V(0.36f, 0.15f), V(0.43f, 0.10f), V(0.40f, 0.07f), V(0f, 0.07f)
                }, 4));
                Ell(b, CB, new Vector3(0f, 0.17f, 0f), new Vector3(0.25f, 0.05f, 0.25f), 20, 8);
                for (int k = 1; k <= 22; k++)
                {
                    float a = k * 137.5f * Mathf.Deg2Rad, r = 0.24f * Mathf.Sqrt(k / 22f);
                    Ell(b, C, new Vector3(r * Mathf.Cos(a), 0.205f, r * Mathf.Sin(a)), new Vector3(0.030f, 0.024f, 0.030f), 8, 5);
                }
                for (int ring = 0; ring < 2; ring++)
                {
                    int n = 16;
                    float tilt = (ring == 0 ? 66f : 78f) + droop;
                    int sub = ring == 0 ? PB : P;
                    var half = new Vector3(ring == 0 ? 0.12f : 0.11f, 0.30f, 0.035f);
                    for (int i = 0; i < n; i++)
                    {
                        var q = Quaternion.Euler(0f, i * 360f / n + ring * 360f / n * 0.5f, 0f);
                        var dir = Quaternion.Euler(tilt, 0f, 0f) * Vector3.up;
                        var c = new Vector3(0f, 0.09f - (ring == 1 ? 0.01f : 0f), 0.30f) + dir * 0.27f;
                        Put(b, q * c, q * Quaternion.Euler(tilt, 0f, 0f), 1f, () => Petal(b, sub, half, 0.75f));
                    }
                }
            });
        }

        private static void Hydrangea(GoodsMeshBuilder b, Pal p, Vector3 pos, Quaternion rot, float s,
            bool dry = false, bool fine = true)
        {
            int A = p.I(dry ? "dry_petal" : "hyd_a"), B = p.I(dry ? "dry_petal_b" : "hyd_b"),
                Cc = p.I(dry ? "dry_core" : "hyd_c");
            Put(b, pos, rot, s, () =>
            {
                Ell(b, Cc, new Vector3(0f, 0.50f, 0f), new Vector3(0.46f, 0.40f, 0.46f), 20, 12);
                const int n = 48;
                for (int k = 0; k < n; k++)
                {
                    float t = (k + 0.5f) / n;
                    float y = 1f - t * 1.5f;
                    float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                    float phi = k * 2.39996f;
                    var nrm = new Vector3(r * Mathf.Cos(phi), y, r * Mathf.Sin(phi)).normalized;
                    var at = new Vector3(0f, 0.5f, 0f) + Vector3.Scale(nrm, new Vector3(0.5f, 0.45f, 0.5f));
                    int m = k % 5;
                    int sub = m < 2 ? A : (m < 4 ? B : Cc);
                    var q = Quaternion.FromToRotation(Vector3.up, nrm) * Quaternion.Euler(0f, k * 37f, 0f);
                    if (!fine)
                    {
                        Put(b, at, q, 1f, () => Ell(b, sub, Vector3.zero, new Vector3(0.115f, 0.040f, 0.115f), 8, 5));
                        continue;
                    }
                    Put(b, at, q, 1f, () =>
                    {
                        for (int a = 0; a < 4; a++)
                        {
                            float ang = a * Mathf.PI * 0.5f;
                            Ell(b, sub, new Vector3(0.062f * Mathf.Cos(ang), 0f, 0.062f * Mathf.Sin(ang)),
                                new Vector3(0.072f, 0.032f, 0.052f), 8, 5);
                        }
                        Ell(b, B, new Vector3(0f, 0.026f, 0f), new Vector3(0.026f, 0.018f, 0.026f), 6, 4);
                    });
                }
            });
        }

        private static void Head(GoodsMeshBuilder b, Pal p, Fl f, Vector3 pos, Quaternion rot, float s,
            bool dry = false, bool fine = true)
        {
            switch (f)
            {
                case Fl.Rose: Rose(b, p, pos, rot, s, dry); break;
                case Fl.Tulip: Tulip(b, p, pos, rot, s, dry); break;
                case Fl.Sunflower: Sunflower(b, p, pos, rot, s, dry); break;
                default: Hydrangea(b, p, pos, rot, s, dry, fine); break;
            }
        }

        // ------------------------------------------------------------------ goods

        private static Quaternion Facing(Vector3 dir) => Quaternion.FromToRotation(Vector3.up, dir.normalized);

        private static void BunchStem(GoodsMeshBuilder b, Pal p, Vector3 from, Vector3 to, float r = 0.011f)
        {
            Vector3 bow = new Vector3((to.x - from.x) * 0.1f, 0f, (to.z - from.z) * 0.1f);
            Tube(b, p.I("stem"), t => Vector3.Lerp(from, to, t) + bow * Mathf.Sin(t * Mathf.PI), Vector3.forward + Vector3.right * 0.37f, r, r * 0.85f, 6, 6);
        }

        /// <summary>One unit of a flower good: a little hand-tied bunch with leaves and a twine band.</summary>
        private static Model Bunch(Fl f)
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            var foot = new Vector3(0f, -0.06f, 0f);

            Vector3[] at;
            float[] sc;
            Vector3[] dir;
            switch (f)
            {
                case Fl.Rose:
                    at = new[] { new Vector3(0f, 0.34f, 0f), new Vector3(-0.11f, 0.27f, 0.05f), new Vector3(0.11f, 0.24f, -0.05f) };
                    sc = new[] { 0.13f, 0.12f, 0.115f };
                    dir = new[] { new Vector3(0f, 1f, 0.1f), new Vector3(-0.5f, 1f, 0.2f), new Vector3(0.5f, 1f, -0.15f) };
                    break;
                case Fl.Tulip:
                    at = new[] { new Vector3(0f, 0.34f, 0f), new Vector3(-0.10f, 0.27f, 0.04f), new Vector3(0.10f, 0.25f, -0.04f) };
                    sc = new[] { 0.20f, 0.19f, 0.19f };
                    dir = new[] { Vector3.up, new Vector3(-0.3f, 1f, 0.1f), new Vector3(0.3f, 1f, -0.1f) };
                    break;
                case Fl.Sunflower:
                    at = new[] { new Vector3(0f, 0.30f, 0f), new Vector3(0.12f, 0.20f, 0.05f) };
                    sc = new[] { 0.15f, 0.13f };
                    dir = new[] { new Vector3(0f, 1f, 0.45f), new Vector3(0.4f, 1f, 0.4f) };
                    break;
                default:
                    at = new[] { new Vector3(0f, 0.28f, 0f), new Vector3(0.13f, 0.18f, 0.05f) };
                    sc = new[] { 0.22f, 0.20f };
                    dir = new[] { Vector3.up, new Vector3(0.45f, 1f, 0.1f) };
                    break;
            }

            for (int i = 0; i < at.Length; i++) BunchStem(b, p, foot, at[i] - dir[i].normalized * 0.01f);

            // Leaves.
            switch (f)
            {
                case Fl.Rose:
                    Leaf(b, p.I("leaf"), new Vector3(0f, 0.10f, 0f), 60f, 40f, 0.15f, 0.08f);
                    Leaf(b, p.I("leaf"), new Vector3(0f, 0.12f, 0f), -120f, 40f, 0.15f, 0.08f);
                    Leaf(b, p.I("leaf"), new Vector3(0f, 0.08f, 0f), 170f, 45f, 0.13f, 0.07f);
                    break;
                case Fl.Tulip:
                    Leaf(b, p.I("leaf"), new Vector3(0f, 0.03f, 0f), 70f, 62f, 0.30f, 0.08f, 0.5f);
                    Leaf(b, p.I("leaf"), new Vector3(0f, 0.03f, 0f), -110f, 62f, 0.30f, 0.08f, 0.5f);
                    Leaf(b, p.I("leaf"), new Vector3(0f, 0.03f, 0f), 160f, 66f, 0.26f, 0.07f, 0.5f);
                    break;
                case Fl.Sunflower:
                    Leaf(b, p.I("leaf"), new Vector3(0f, 0.08f, 0f), 80f, 34f, 0.20f, 0.14f);
                    Leaf(b, p.I("leaf"), new Vector3(0f, 0.08f, 0f), -100f, 34f, 0.20f, 0.14f);
                    Leaf(b, p.I("leaf"), new Vector3(0f, 0.10f, 0f), 170f, 38f, 0.17f, 0.12f);
                    break;
                default:
                    for (int i = 0; i < 4; i++)
                        Leaf(b, p.I("leaf"), new Vector3(0f, 0.05f + i * 0.01f, 0f), 20f + i * 90f, 32f, 0.20f, 0.16f);
                    break;
            }

            for (int i = 0; i < at.Length; i++)
                Head(b, p, f, at[i], Facing(dir[i]) * Quaternion.Euler(0f, i * 70f, 0f), sc[i], false, true);

            // Twine band holding the stems.
            b.Revolve(p.I("rope"), 20, GoodsMeshBuilder.Spline(new[]
            {
                V(0.030f, 0.010f), V(0.042f, 0.010f), V(0.046f, 0.022f), V(0.042f, 0.040f), V(0.030f, 0.040f), V(0.030f, 0.010f)
            }, 3));

            b.Fit(0.30f * GoodsModels.Scale, 0.34f * GoodsModels.Scale);
            return new Model { Mesh = b, Pal = p };
        }

        private static Model WateringCan()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            int can = p.I("can"), dark = p.I("can_dark"), lite = p.I("glass");

            // Body, with a recessed top.
            b.Revolve(can, 44, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.10f, 0f), V(0.125f, 0.012f), V(0.130f, 0.05f), V(0.128f, 0.16f), V(0.120f, 0.205f),
                V(0.100f, 0.215f), V(0.070f, 0.205f), V(0f, 0.200f)
            }, 4));
            // Rim bands.
            foreach (float y in new[] { 0.03f, 0.185f })
                b.Revolve(dark, 36, GoodsMeshBuilder.Spline(new[]
                {
                    V(0.124f, y - 0.014f), V(0.138f, y - 0.014f), V(0.142f, y), V(0.138f, y + 0.014f), V(0.124f, y + 0.014f), V(0.124f, y - 0.014f)
                }, 3));
            // Spout, rising out and up, with a flared sprinkler rose.
            Vector3 s0 = new Vector3(0.11f, 0.05f, 0f), s1 = new Vector3(0.36f, 0.26f, 0f);
            Tube(b, can, t => Vector3.Lerp(s0, s1, t) + new Vector3(0f, -0.03f * Mathf.Sin(t * Mathf.PI) + 0.02f * t * t, 0f),
                Vector3.forward, 0.024f, 0.017f, 10, 10);
            Put(b, s1, Quaternion.FromToRotation(Vector3.up, (s1 - s0).normalized), 1f, () =>
            {
                b.Revolve(dark, 24, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, -0.004f), V(0.030f, -0.004f), V(0.048f, 0.026f), V(0.056f, 0.060f), V(0f, 0.064f)
                }, 4));
                for (int i = 0; i < 7; i++)
                {
                    float a = i < 6 ? i * Mathf.PI / 3f : 0f;
                    float r = i < 6 ? 0.030f : 0f;
                    Ell(b, lite, new Vector3(r * Mathf.Cos(a), 0.063f, r * Mathf.Sin(a)), new Vector3(0.0085f, 0.004f, 0.0085f), 8, 5);
                }
            });
            // Back handle (a loop) and the carrying arc over the top.
            Tube(b, dark, t =>
            {
                float a = Mathf.PI * t;
                return new Vector3(-0.125f - 0.10f * Mathf.Sin(a), 0.105f + 0.085f * Mathf.Cos(a) * 1.0f, 0f);
            }, Vector3.forward, 0.016f, 0.016f, 12, 8);
            Tube(b, dark, t => new Vector3(Mathf.Lerp(0.07f, -0.125f, t), 0.205f + 0.115f * Mathf.Sin(Mathf.PI * t), 0f),
                Vector3.forward, 0.013f, 0.013f, 12, 8);

            b.Fit(0.30f * GoodsModels.Scale, 0.34f * GoodsModels.Scale);
            return new Model { Mesh = b, Pal = p };
        }

        private static float ConeR(float s) => Mathf.Lerp(0.03f, 0.20f, Mathf.Pow(s, 0.9f));

        private static Model Bouquet()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();

            // The wrap: a cone of paper with a scalloped edge and soft pleats, double sided.
            int paper = p.I("paper");
            b.Surface(paper, 48, 10, (i, j) =>
            {
                float a = i / 48f * Mathf.PI * 2f, s = j / 10f;
                float top = 0.30f + 0.028f * Mathf.Cos(5f * a);
                float r = ConeR(s) * (1f + 0.05f * Mathf.Cos(6f * a) * s);
                return new Vector3(r * Mathf.Cos(a), s * top, r * Mathf.Sin(a));
            }, closed: false, flip: false, backFace: 0.006f);

            // A pink flap folded over the front (+z).
            int pink = p.I("paper_pink");
            b.Surface(pink, 20, 10, (i, j) =>
            {
                float a = Mathf.PI * 0.5f + (i / 20f * 2f - 1f) * 0.95f, s = j / 10f;
                float top = 0.275f + 0.022f * Mathf.Cos(5f * a);
                float r = ConeR(s) * (1f + 0.05f * Mathf.Cos(6f * a) * s) * 1.016f;
                return new Vector3(r * Mathf.Cos(a), s * top, r * Mathf.Sin(a));
            }, closed: false, flip: false, backFace: 0.004f);

            // Ribbon band with a bow at the front.
            int rib = p.I("ribbon");
            float R = ConeR(0.10f / 0.30f);
            b.Revolve(rib, 36, GoodsMeshBuilder.Spline(new[]
            {
                V(R - 0.012f, 0.082f), V(R + 0.008f, 0.082f), V(R + 0.016f, 0.090f), V(R + 0.016f, 0.116f),
                V(R + 0.008f, 0.124f), V(R - 0.012f, 0.124f), V(R - 0.012f, 0.082f)
            }, 3));
            float bz = R + 0.022f;
            Put(b, new Vector3(-0.052f, 0.108f, bz), Quaternion.Euler(0f, 0f, 24f), 1f, () => Ell(b, rib, Vector3.zero, new Vector3(0.060f, 0.040f, 0.016f), 12, 8));
            Put(b, new Vector3(0.052f, 0.108f, bz), Quaternion.Euler(0f, 0f, -24f), 1f, () => Ell(b, rib, Vector3.zero, new Vector3(0.060f, 0.040f, 0.016f), 12, 8));
            Put(b, new Vector3(-0.024f, 0.050f, bz), Quaternion.Euler(0f, 0f, 12f), 1f, () => Ell(b, rib, Vector3.zero, new Vector3(0.016f, 0.062f, 0.009f), 8, 6));
            Put(b, new Vector3(0.024f, 0.052f, bz), Quaternion.Euler(0f, 0f, -14f), 1f, () => Ell(b, rib, Vector3.zero, new Vector3(0.016f, 0.056f, 0.009f), 8, 6));
            Ell(b, rib, new Vector3(0f, 0.106f, bz + 0.006f), new Vector3(0.024f, 0.024f, 0.020f), 10, 8);

            // Leaves ring the rim; stems stub out of the bottom.
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f + 0.3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Leaf(b, p.I("leaf"), dir * 0.15f + new Vector3(0f, 0.27f, 0f), Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 44f, 0.17f, 0.09f);
            }
            b.Revolve(p.I("stem"), 14, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, -0.045f), V(0.020f, -0.045f), V(0.026f, -0.03f), V(0.028f, 0.01f), V(0f, 0.01f)
            }, 3));

            // The blooms.
            Head(b, p, Fl.Hydrangea, new Vector3(-0.085f, 0.255f, 0.00f), Facing(new Vector3(-0.35f, 1f, 0f)), 0.20f, false, true);
            Head(b, p, Fl.Sunflower, new Vector3(0.00f, 0.300f, -0.06f), Facing(new Vector3(0f, 1f, 0.40f)), 0.13f);
            Head(b, p, Fl.Rose, new Vector3(0.09f, 0.245f, 0.04f), Facing(new Vector3(0.4f, 1f, 0.2f)), 0.115f);
            Head(b, p, Fl.Rose, new Vector3(-0.01f, 0.225f, 0.11f), Facing(new Vector3(0f, 1f, 0.6f)), 0.11f);
            Head(b, p, Fl.Tulip, new Vector3(0.12f, 0.255f, -0.07f), Facing(new Vector3(0.5f, 1f, -0.25f)), 0.17f);
            Head(b, p, Fl.Tulip, new Vector3(-0.14f, 0.255f, -0.06f), Facing(new Vector3(-0.5f, 1f, -0.2f)), 0.17f);

            b.Fit(0.36f * GoodsModels.Scale, 0.36f * GoodsModels.Scale);
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ flower beds

        private static readonly float[] StemH = { 0.50f, 0.46f, 0.95f, 0.34f };
        private static readonly float[] HeadS = { 0.20f, 0.27f, 0.25f, 0.34f };
        private static readonly float[] LeafLen = { 0.18f, 0.40f, 0.30f, 0.24f };
        private static readonly float[] LeafWid = { 0.09f, 0.09f, 0.20f, 0.17f };
        private static readonly string[] BudKey = { "rose_a", "tulip_a", "sun_petal", "hyd_a" };

        private static void Plant(GoodsMeshBuilder b, Pal p, Fl f, int state, Vector3 basePos, float yaw, System.Random rnd)
        {
            int fi = (int)f;
            float H = StemH[fi] * (0.85f + 0.30f * (float)rnd.NextDouble());
            float R() => (float)rnd.NextDouble();
            string leaf = state == 2 ? "dry_leaf" : "leaf";

            if (state == 0)
            {
                float h = H * 0.28f;
                Tube(b, p.I("stem"), t => basePos + Vector3.up * (h * t), Vector3.right, 0.018f, 0.014f, 3, 6);
                var tip = basePos + Vector3.up * h;
                Put(b, tip + Vector3.up * 0.02f, Quaternion.identity, 1f, () =>
                    Ell(b, p.I(BudKey[fi]), Vector3.zero, new Vector3(0.035f, 0.05f, 0.035f), 10, 8));
                for (int i = 0; i < 4; i++)
                    Leaf(b, p.I("leaf"), basePos + Vector3.up * (h * 0.4f), yaw + i * 90f + R() * 24f, 36f + R() * 16f,
                        LeafLen[fi] * 0.55f, LeafWid[fi] * 0.55f, 0.25f);
                return;
            }

            if (state == 1)
            {
                var dir = Quaternion.Euler(R() * 8f - 4f, yaw, R() * 8f - 4f) * Vector3.up;
                Tube(b, p.I("stem"), t => basePos + dir * (H * t), Vector3.right + Vector3.forward * 0.3f, 0.020f, 0.015f, 5, 6);
                switch (f)
                {
                    case Fl.Rose:
                        for (int i = 0; i < 4; i++)
                            Leaf(b, p.I(leaf), basePos + dir * (H * (0.25f + 0.13f * i)), yaw + i * 100f, 42f, 0.17f, 0.09f);
                        break;
                    case Fl.Tulip:
                        for (int i = 0; i < 3; i++)
                            Leaf(b, p.I(leaf), basePos + Vector3.up * 0.02f, yaw + i * 120f + R() * 20f, 58f, 0.40f, 0.09f, 0.45f);
                        break;
                    case Fl.Sunflower:
                        for (int i = 0; i < 3; i++)
                            Leaf(b, p.I(leaf), basePos + dir * (H * (0.30f + 0.18f * i)), yaw + i * 130f, 34f, 0.30f, 0.20f);
                        break;
                    default:
                        for (int i = 0; i < 5; i++)
                            Leaf(b, p.I(leaf), basePos + Vector3.up * (0.04f + 0.03f * (i % 2)), yaw + i * 72f, 30f, 0.24f, 0.17f);
                        break;
                }
                var headDir = f == Fl.Sunflower ? Quaternion.Euler(24f, yaw, 0f) * Vector3.up : dir;
                Head(b, p, f, basePos + dir * H, Facing(headDir) * Quaternion.Euler(0f, R() * 360f, 0f), HeadS[fi], false, false);
                return;
            }

            // Dry: the stem folds over, the head hangs, the leaves go brown and flop.
            var lat = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Func<float, Vector3> P = t => basePos + lat * (0.55f * H * t * t) +
                Vector3.up * (0.9f * H * Mathf.Sin(t * Mathf.PI * 0.5f) * (1f - 0.35f * t * t));
            Tube(b, p.I("dry_stem"), P, Vector3.Cross(lat, Vector3.up), 0.020f, 0.015f, 8, 6);
            var tan = (P(1f) - P(0.95f)).normalized;
            var hang = (tan + Vector3.down * 0.7f).normalized;
            for (int i = 0; i < 3; i++)
                Leaf(b, p.I(leaf), basePos + Vector3.up * (0.04f + 0.03f * i), yaw + i * 120f + R() * 30f, 14f,
                    LeafLen[fi] * 0.8f, LeafWid[fi] * 0.8f, 0.9f);
            Head(b, p, f, P(1f), Facing(hang) * Quaternion.Euler(0f, R() * 360f, 0f), HeadS[fi] * 0.92f, true, false);
        }

        private static Model BedModel(Fl f, int state)
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            float hx = BedSize.x * 0.5f, hz = BedSize.y * 0.5f;

            Slab(b, p.I(state == 2 ? "soil_dry" : "soil"), new Vector3(0f, 0.10f, 0f), new Vector3(hx - 0.08f, 0.10f, hz - 0.08f), 0.30f);
            int wood = p.I("wood");
            foreach (float sx in new[] { -1f, 1f })
                Slab(b, wood, new Vector3(sx * (hx - 0.05f), 0.11f, 0f), new Vector3(0.05f, 0.12f, hz), 0.25f);
            foreach (float sz in new[] { -1f, 1f })
                Slab(b, wood, new Vector3(0f, 0.11f, sz * (hz - 0.05f)), new Vector3(hx, 0.12f, 0.05f), 0.25f);
            int post = p.I("wood_dark");
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Slab(b, post, new Vector3(sx * (hx - 0.05f), 0.15f, sz * (hz - 0.05f)), new Vector3(0.075f, 0.15f, 0.075f), 0.25f);

            var rnd = new System.Random(11 + (int)f * 7);
            if (state == 2)
            {
                int crack = p.I("crack");
                for (int i = 0; i < 9; i++)
                {
                    float x = ((float)rnd.NextDouble() - 0.5f) * 1.3f, z = ((float)rnd.NextDouble() - 0.5f) * 1.9f;
                    float yaw = (float)rnd.NextDouble() * 180f;
                    Slab(b, crack, new Vector3(x, SoilTop - 0.006f, z), new Vector3(0.20f, 0.008f, 0.014f), 0.4f, yaw, 0f, 0f, 12, 6);
                    Slab(b, crack, new Vector3(x + 0.06f * Mathf.Cos(yaw), SoilTop - 0.006f, z + 0.1f * Mathf.Sin(yaw)),
                        new Vector3(0.10f, 0.008f, 0.012f), 0.4f, yaw + 40f, 0f, 0f, 12, 6);
                }
            }

            bool big = f == Fl.Sunflower;
            float[] xs = big ? new[] { -0.38f, 0.38f } : new[] { -0.50f, 0f, 0.50f };
            float[] zs = { -0.80f, 0f, 0.80f };
            foreach (float x in xs)
                foreach (float z in zs)
                {
                    var at = new Vector3(x + ((float)rnd.NextDouble() - 0.5f) * 0.10f, SoilTop - 0.01f,
                        z + ((float)rnd.NextDouble() - 0.5f) * 0.10f);
                    Plant(b, p, f, state, at, (float)rnd.NextDouble() * 360f, rnd);
                }
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ the well

        private static Model WellModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            int stone = p.I("stone");

            // Stone ring, with courses of slightly proud bricks.
            b.Revolve(stone, 56, GoodsMeshBuilder.Spline(new[]
            {
                V(0.52f, 0f), V(0.86f, 0f), V(0.90f, 0.06f), V(0.90f, 0.58f), V(0.85f, 0.70f), V(0.66f, 0.76f),
                V(0.54f, 0.68f), V(0.52f, 0.62f), V(0.52f, 0f)
            }, 4), (q, u, v) =>
            {
                float rad = Mathf.Sqrt(q.x * q.x + q.z * q.z);
                if (rad < 0.8f || q.y < 0.04f || q.y > 0.64f) return q;
                float band = Mathf.Floor(q.y / 0.13f);
                float ph = u * 16f + (((int)band & 1) == 1 ? 0.5f : 0f);
                float f = Mathf.Abs(ph - Mathf.Floor(ph) - 0.5f) * 2f;
                float yy = q.y / 0.13f - band;
                float k = 1f + 0.03f * Mathf.Clamp01(1.6f * (0.85f - f)) * Mathf.Clamp01(5f * Mathf.Min(yy, 1f - yy));
                return new Vector3(q.x * k, q.y, q.z * k);
            });
            // Darker coping ring on top.
            b.Revolve(p.I("stone_dark"), 48, GoodsMeshBuilder.Spline(new[]
            {
                V(0.56f, 0.70f), V(0.84f, 0.72f), V(0.88f, 0.78f), V(0.82f, 0.84f), V(0.60f, 0.84f), V(0.56f, 0.78f), V(0.56f, 0.70f)
            }, 3));
            // Water.
            b.Revolve(p.I("water"), 36, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0.50f), V(0.50f, 0.50f), V(0.54f, 0.46f), V(0f, 0.44f)
            }, 3));

            // Posts, barrel, crank and roof.
            int wood = p.I("wood"), dark = p.I("wood_dark");
            foreach (float sx in new[] { -1f, 1f })
            {
                Slab(b, wood, new Vector3(sx * 0.74f, 1.70f, 0f), new Vector3(0.075f, 1.05f, 0.075f), 0.25f);
                Slab(b, dark, new Vector3(sx * 0.74f, 0.80f, 0f), new Vector3(0.10f, 0.05f, 0.10f), 0.3f);
            }
            Put(b, new Vector3(0f, 1.30f, 0f), Quaternion.Euler(0f, 0f, 90f), 1f, () =>
                b.Revolve(wood, 24, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, -0.76f), V(0.07f, -0.76f), V(0.09f, -0.72f), V(0.09f, 0.72f), V(0.07f, 0.76f), V(0f, 0.76f)
                }, 3)));
            Put(b, new Vector3(0f, 1.30f, 0f), Quaternion.Euler(0f, 0f, 90f), 1f, () =>
                b.Revolve(p.I("rope"), 28, GoodsMeshBuilder.Spline(new[]
                {
                    V(0.08f, -0.40f), V(0.125f, -0.38f), V(0.135f, -0.30f), V(0.135f, 0.30f), V(0.125f, 0.38f), V(0.08f, 0.40f), V(0.08f, -0.40f)
                }, 3)));
            // Crank on the east post side.
            Slab(b, dark, new Vector3(0.88f, 1.20f, 0f), new Vector3(0.028f, 0.16f, 0.028f), 0.3f);
            Put(b, new Vector3(0.88f, 1.04f, 0f), Quaternion.Euler(0f, 0f, 90f), 1f, () =>
                b.Revolve(p.I("brass"), 16, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, -0.09f), V(0.04f, -0.09f), V(0.045f, -0.07f), V(0.045f, 0.07f), V(0.04f, 0.09f), V(0f, 0.09f)
                }, 3)));

            int roof = p.I("roof");
            foreach (float sz in new[] { -1f, 1f })
                Slab(b, roof, new Vector3(0f, 2.80f, sz * 0.35f), new Vector3(0.96f, 0.05f, 0.40f), 0.25f, 0f, sz * 30f, 0f, 28, 10);
            Ell(b, dark, new Vector3(0f, 3.00f, 0f), new Vector3(1.00f, 0.075f, 0.075f), 24, 8);

            // Bucket on its rope.
            int tin = p.I("tin");
            Put(b, new Vector3(0f, 0.80f, 0f), Quaternion.identity, 1f, () =>
            {
                b.Revolve(tin, 28, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, 0f), V(0.11f, 0f), V(0.15f, 0.20f), V(0.14f, 0.21f), V(0.125f, 0.19f), V(0.095f, 0.03f), V(0f, 0.03f)
                }, 3));
                b.Revolve(p.I("tin_band"), 28, GoodsMeshBuilder.Spline(new[]
                {
                    V(0.122f, 0.07f), V(0.132f, 0.07f), V(0.139f, 0.11f), V(0.131f, 0.11f), V(0.122f, 0.07f)
                }, 3));
                Tube(b, tin, t => new Vector3(0.145f * Mathf.Cos(Mathf.PI * t), 0.20f + 0.15f * Mathf.Sin(Mathf.PI * t), 0f),
                    Vector3.forward, 0.010f, 0.010f, 10, 6);
            });
            Tube(b, p.I("rope"), t => new Vector3(0f, Mathf.Lerp(1.17f, 1.15f, t), 0f) + new Vector3(0f, 0f, 0f),
                Vector3.right, 0.014f, 0.014f, 2, 6);
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ shop furniture

        private static Model BucketModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            b.Revolve(p.I("tin"), 28, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.13f, 0f), V(0.17f, 0.24f), V(0.18f, 0.27f), V(0f, 0.27f)
            }, 4));
            b.Revolve(p.I("tin_band"), 28, GoodsMeshBuilder.Spline(new[]
            {
                V(0.142f, 0.07f), V(0.153f, 0.07f), V(0.162f, 0.15f), V(0.151f, 0.15f), V(0.142f, 0.07f)
            }, 3));
            b.Revolve(p.I("tin"), 28, GoodsMeshBuilder.Spline(new[]
            {
                V(0.171f, 0.250f), V(0.186f, 0.250f), V(0.190f, 0.262f), V(0.186f, 0.274f), V(0.171f, 0.274f), V(0.171f, 0.250f)
            }, 3));
            return new Model { Mesh = b, Pal = p };
        }

        private static readonly float[] TierTop = { 0.28f, 0.60f, 0.92f };
        private static readonly float[] TierFront = { -0.65f, -0.20f, 0.25f };
        private const float StandBack = 0.65f;

        private static Model WallStandModel(float width, bool mitre)
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            for (int i = 0; i < 3; i++)
            {
                float depth = StandBack - TierFront[i], cz = (StandBack + TierFront[i]) * 0.5f;
                Func<Vector3, Vector3> mod = null;
                if (mitre)
                    mod = q =>
                    {
                        float fz = Mathf.InverseLerp(depth * 0.5f, -depth * 0.5f, q.z);
                        q.x *= Mathf.Lerp(1f, 0.74f, fz);
                        return q;
                    };
                Slab(b, p.I("cream"), new Vector3(0f, TierTop[i] * 0.5f, cz), new Vector3(width * 0.5f, TierTop[i] * 0.5f, depth * 0.5f), 0.20f, modify: mod);
                Slab(b, p.I("wood_light"), new Vector3(0f, TierTop[i] + 0.012f, cz), new Vector3(width * 0.5f + 0.02f, 0.028f, depth * 0.5f + 0.02f), 0.30f, modify: mod);
                Slab(b, p.I("pink"), new Vector3(0f, TierTop[i] - 0.08f, TierFront[i] - 0.010f), new Vector3(width * 0.5f * (mitre ? 0.70f : 0.94f), 0.035f, 0.018f), 0.4f);
            }
            Slab(b, p.I("wood_dark"), new Vector3(0f, 0.04f, 0f), new Vector3(width * 0.5f + 0.03f, 0.04f, StandBack + 0.03f), 0.3f);
            return new Model { Mesh = b, Pal = p };
        }

        private static readonly float[] CentreHalf = { 1.00f, 0.72f, 0.44f };

        private static Model CentreStandModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            for (int i = 0; i < 3; i++)
            {
                float h = CentreHalf[i];
                Slab(b, p.I("cream"), new Vector3(0f, TierTop[i] * 0.5f, 0f), new Vector3(h, TierTop[i] * 0.5f, h), 0.22f);
                Slab(b, p.I("wood_light"), new Vector3(0f, TierTop[i] + 0.012f, 0f), new Vector3(h + 0.02f, 0.028f, h + 0.02f), 0.30f);
                foreach (float sz in new[] { -1f, 1f })
                {
                    Slab(b, p.I("pink"), new Vector3(0f, TierTop[i] - 0.08f, sz * (h + 0.008f)), new Vector3(h * 0.9f, 0.035f, 0.018f), 0.4f);
                    Slab(b, p.I("pink"), new Vector3(sz * (h + 0.008f), TierTop[i] - 0.08f, 0f), new Vector3(0.018f, 0.035f, h * 0.9f), 0.4f);
                }
            }
            Slab(b, p.I("wood_dark"), new Vector3(0f, 0.04f, 0f), new Vector3(CentreHalf[0] + 0.03f, 0.04f, CentreHalf[0] + 0.03f), 0.3f);
            return new Model { Mesh = b, Pal = p };
        }

        private static Model CounterModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            const float W = 3.2f;
            Slab(b, p.I("cream"), new Vector3(0f, 0.50f, 0f), new Vector3(W * 0.5f, 0.50f, 0.42f), 0.16f);
            Slab(b, p.I("wood_dark"), new Vector3(0f, 0.05f, 0f), new Vector3(W * 0.5f + 0.02f, 0.05f, 0.45f), 0.3f);
            Slab(b, p.I("wood_light"), new Vector3(0f, 1.04f, -0.02f), new Vector3(W * 0.5f + 0.08f, 0.045f, 0.50f), 0.28f);
            // Painted front panels, pink inside cream frames.
            foreach (float x in new[] { -1.05f, 0f, 1.05f })
            {
                Slab(b, p.I("cream"), new Vector3(x, 0.53f, -0.425f), new Vector3(0.50f, 0.33f, 0.02f), 0.3f);
                Slab(b, p.I("pink"), new Vector3(x, 0.53f, -0.440f), new Vector3(0.44f, 0.27f, 0.02f), 0.35f);
            }

            // Wrapping paper roll and a ribbon spool on the counter.
            var top = new Vector3(0f, 1.085f, 0f);
            Put(b, top + new Vector3(-1.15f, 0.12f, 0.02f), Quaternion.Euler(0f, 18f, 90f), 1f, () =>
                b.Revolve(p.I("paper"), 28, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, -0.26f), V(0.11f, -0.26f), V(0.12f, -0.24f), V(0.12f, 0.24f), V(0.11f, 0.26f), V(0f, 0.26f)
                }, 3)));
            Put(b, top + new Vector3(-1.15f, 0.12f, 0.02f), Quaternion.Euler(0f, 18f, 90f), 1f, () =>
                b.Revolve(p.I("paper_pink"), 28, GoodsMeshBuilder.Spline(new[]
                {
                    V(0.115f, -0.10f), V(0.126f, -0.09f), V(0.126f, 0.09f), V(0.115f, 0.10f), V(0.115f, -0.10f)
                }, 3)));
            Put(b, top + new Vector3(-0.62f, 0.05f, 0.05f), Quaternion.identity, 1f, () =>
            {
                b.Revolve(p.I("ribbon"), 24, GoodsMeshBuilder.Spline(new[]
                {
                    V(0.02f, 0f), V(0.16f, 0f), V(0.17f, 0.012f), V(0.17f, 0.088f), V(0.16f, 0.10f), V(0.02f, 0.10f), V(0.02f, 0f)
                }, 3));
                b.Revolve(p.I("wood"), 20, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, -0.01f), V(0.19f, -0.01f), V(0.19f, 0.012f), V(0.03f, 0.014f), V(0.03f, 0.086f), V(0.19f, 0.088f), V(0.19f, 0.11f), V(0f, 0.11f)
                }, 3));
            });
            // A little brass bell.
            Put(b, top + new Vector3(0.50f, 0f, -0.06f), Quaternion.identity, 1f, () =>
                b.Revolve(p.I("brass"), 24, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, 0.004f), V(0.095f, 0.004f), V(0.10f, 0.02f), V(0.085f, 0.07f), V(0.05f, 0.11f), V(0.012f, 0.125f), V(0f, 0.13f)
                }, 4)));
            // A glass vase of roses.
            Put(b, top + new Vector3(1.20f, 0f, 0.04f), Quaternion.identity, 1f, () =>
            {
                b.Revolve(p.I("glass"), 28, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, 0f), V(0.09f, 0f), V(0.115f, 0.03f), V(0.10f, 0.14f), V(0.06f, 0.24f), V(0.07f, 0.30f), V(0f, 0.30f)
                }, 4));
                var heads = new[] { new Vector3(0f, 0.52f, 0f), new Vector3(-0.10f, 0.45f, 0.04f), new Vector3(0.10f, 0.43f, -0.03f) };
                var dirs = new[] { Vector3.up, new Vector3(-0.5f, 1f, 0.2f), new Vector3(0.5f, 1f, -0.1f) };
                for (int i = 0; i < 3; i++)
                {
                    var from = new Vector3(0f, 0.12f, 0f);
                    Tube(b, p.I("stem"), t => Vector3.Lerp(from, heads[i], t), Vector3.forward + Vector3.right * 0.4f, 0.009f, 0.008f, 4, 6);
                    Rose(b, p, heads[i], Facing(dirs[i]), 0.105f);
                }
            });
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ assembling

        /// <summary>Spots (local x, tier y, local z) where a stand's buckets sit, for the stand types.</summary>
        private static List<(Vector3 at, float scale)> WallSpots(float width, bool corner)
        {
            var l = new List<(Vector3, float)>();
            int[] counts = corner ? new[] { 3, 2, 2 } : new[] { 5, 4, 3 };
            for (int t = 0; t < 3; t++)
            {
                int n = counts[t];
                float span = corner ? (t == 0 ? 0.84f : 0.50f) : (t == 0 ? 1.76f : (t == 1 ? 1.98f : 1.40f));
                for (int i = 0; i < n; i++)
                {
                    float x = n == 1 ? 0f : Mathf.Lerp(-span * 0.5f, span * 0.5f, i / (float)(n - 1));
                    l.Add((new Vector3(x, TierTop[t] + 0.02f, TierFront[t] + 0.25f), 1f));
                }
            }
            return l;
        }

        private static List<(Vector3 at, float scale)> CentreSpots()
        {
            var l = new List<(Vector3, float)>();
            float[] ring = { 0.84f, 0.57f };
            for (int t = 0; t < 2; t++)
            {
                float r = ring[t];
                var pts = new List<Vector2>
                {
                    new Vector2(-r, -r), new Vector2(0f, -r), new Vector2(r, -r), new Vector2(r, 0f),
                    new Vector2(r, r), new Vector2(0f, r), new Vector2(-r, r), new Vector2(-r, 0f)
                };
                foreach (var v in pts)
                    l.Add((new Vector3(v.x, TierTop[t] + 0.02f, v.y), t == 0 ? 1f : 0.88f));
            }
            l.Add((new Vector3(0f, TierTop[2] + 0.02f, 0f), 1.25f));
            return l;
        }

        private static void FillSpots(Transform stand, List<(Vector3 at, float scale)> spots, Mesh bucket, Material[] bucketMats,
            Func<int, int> flowerOf)
        {
            for (int i = 0; i < spots.Count; i++)
            {
                var (at, sc) = spots[i];
                var bk = new GameObject($"Bucket_{i}");
                bk.transform.SetParent(stand, false);
                bk.transform.localPosition = at;
                bk.transform.localScale = Vector3.one * sc;
                bk.AddComponent<MeshFilter>().sharedMesh = bucket;
                var r = bk.AddComponent<MeshRenderer>();
                r.sharedMaterials = bucketMats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                string id = FlowerIds[flowerOf(i) % FlowerIds.Length];
                var fill = GoodsModels.Instance(id, stand, at + Vector3.up * (0.27f * sc + 0.07f * sc));
                fill.name = "Fill_" + id;
                fill.transform.localScale = Vector3.one * (0.62f * sc);
                fill.transform.localRotation = Quaternion.Euler(0f, 37f * i, 0f);
            }
        }

        // ------------------------------------------------------------------ asset plumbing

        private static readonly Dictionary<string, (Mesh mesh, Material[] mats)> _built =
            new Dictionary<string, (Mesh, Material[])>();

        private static (Mesh mesh, Material[] mats) Bake(string path, string name, Model m)
        {
            var mesh = ButcherModels.SaveMeshAt(path, name, m.Mesh);
            var mats = m.Pal.Mats;
            if (mats.Length > mesh.subMeshCount) Array.Resize(ref mats, mesh.subMeshCount);
            var s = mesh.bounds.size;
            Debug.Log($"[Florist] {name}: {mesh.vertexCount} verts, size {s.x:0.000} x {s.y:0.000} x {s.z:0.000}");
            return (mesh, mats);
        }

        private static (Mesh mesh, Material[] mats) Get(string key, Func<Model> make)
        {
            if (_built.TryGetValue(key, out var hit) && hit.mesh != null) return hit;
            var r = Bake($"{MeshFolder}/{key}.asset", key, make());
            _built[key] = r;
            return r;
        }

        private static GameObject Obj(string name, Transform parent, Vector3 pos, float yaw, (Mesh mesh, Material[] mats) m,
            bool shadow = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = m.mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = m.mats;
            if (!shadow) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ public spawners

        public static GameObject Good(string id, Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var go = GoodsModels.Instance(id, parent, localPos);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        private static readonly float[] SingleS = { 0.22f, 0.30f, 0.22f, 0.30f };

        /// <summary>One loose flower (head, stem and two leaves), used for the minigame art.</summary>
        private static Model SingleModel(Fl f)
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            Tube(b, p.I("stem"), t => new Vector3(0f, Mathf.Lerp(-0.45f, 0f, t), 0f), Vector3.right, 0.014f, 0.012f, 6, 6);
            Leaf(b, p.I("leaf"), new Vector3(0f, -0.26f, 0f), 70f, 40f, 0.20f, 0.10f);
            Leaf(b, p.I("leaf"), new Vector3(0f, -0.20f, 0f), -110f, 40f, 0.18f, 0.09f);
            Head(b, p, f, Vector3.zero, Quaternion.identity, SingleS[(int)f], false, true);
            return new Model { Mesh = b, Pal = p };
        }

        public static GameObject Flower(Fl f, Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj($"Flower_{FlowerIds[(int)f]}", parent, pos, yaw,
                Get($"Flower_{FlowerIds[(int)f]}", () => SingleModel(f)), false);

        public static GameObject Bed(Fl f, int state, Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj($"Bed_{FlowerIds[(int)f]}_{BedStates[state]}", parent, pos, yaw,
                Get($"Bed_{FlowerIds[(int)f]}_{BedStates[state]}", () => BedModel(f, state)), false);

        public static GameObject Well(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("Well", parent, pos, yaw, Get("Well", WellModel));

        public static GameObject Counter(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("FloristCounter", parent, pos, yaw, Get("FloristCounter", CounterModel));

        public const float WallStandWidth = 2.2f, CornerStandWidth = 1.5f;

        public static GameObject WallStand(Transform parent, Vector3 pos, float yaw = 0f)
        {
            var go = Obj("WallStand", parent, pos, yaw, Get("WallStand", () => WallStandModel(WallStandWidth, false)));
            var bk = Get("Bucket", BucketModel);
            FillSpots(go.transform, WallSpots(WallStandWidth, false), bk.mesh, bk.mats, i => i * 3 + i / 5);
            return go;
        }

        public static GameObject CornerStand(Transform parent, Vector3 pos, float yaw = 0f)
        {
            var go = Obj("CornerStand", parent, pos, yaw, Get("CornerStand", () => WallStandModel(CornerStandWidth, true)));
            var bk = Get("Bucket", BucketModel);
            FillSpots(go.transform, WallSpots(CornerStandWidth, true), bk.mesh, bk.mats, i => i * 3 + 1 + i / 3);
            return go;
        }

        public static GameObject CentreStand(Transform parent, Vector3 pos, float yaw = 0f)
        {
            var go = Obj("CentreStand", parent, pos, yaw, Get("CentreStand", CentreStandModel));
            var bk = Get("Bucket", BucketModel);
            FillSpots(go.transform, CentreSpots(), bk.mesh, bk.mats, i => i + i / 4);
            return go;
        }

        // ------------------------------------------------------------------ build all

        private static GameObject SavePrefab(GameObject go, string name)
        {
            ButcherModels.EnsureFolder(PrefabFolder);
            return PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{name}.prefab");
        }

        /// <summary>Builds every florist mesh/material/prefab asset. Safe to rerun.</summary>
        [MenuItem("Tycoon/Build Florist Models")]
        public static void BuildAll()
        {
            ButcherModels.EnsureFolder(MeshFolder);
            ButcherModels.EnsureFolder(PrefabFolder);
            ButcherModels.EnsureFolder(GoodsModels.MeshFolder);
            ButcherModels.EnsureFolder(GoodsModels.PrefabFolder);
            _built.Clear();

            // Goods: same layout as GoodsModels.BuildAll (mesh in Meshes/Goods, prefab in Prefabs/Goods).
            for (int i = 0; i < GoodIds.Length; i++)
            {
                string id = GoodIds[i];
                Model model = id == "watering_can" ? WateringCan() : id == "bouquet" ? Bouquet() : Bunch((Fl)i);
                var (mesh, mats) = Bake(GoodsModels.MeshPath(id), "Good_" + id, model);
                var go = new GameObject("Good_" + id);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(go, GoodsModels.PrefabPath(id));
                UnityEngine.Object.DestroyImmediate(go);
                Debug.Log($"[Florist] Good_{id}: use stackHeight {mesh.bounds.size.y:0.00}");
            }
            BuildSeedGood();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var holder = new GameObject("FloristBuild").transform;
            for (int f = 0; f < 4; f++)
                for (int s = 0; s < 3; s++)
                    SavePrefab(Bed((Fl)f, s, holder, Vector3.zero), $"Bed_{FlowerIds[f]}_{BedStates[s]}");
            SavePrefab(Well(holder, Vector3.zero), "Well");
            SavePrefab(Counter(holder, Vector3.zero), "FloristCounter");
            SavePrefab(WallStand(holder, Vector3.zero), "WallStand");
            SavePrefab(CornerStand(holder, Vector3.zero), "CornerStand");
            SavePrefab(CentreStand(holder, Vector3.zero), "CentreStand");
            UnityEngine.Object.DestroyImmediate(holder.gameObject);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FLORISTMODELS_OK");
        }
    }
}
