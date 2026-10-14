using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The butcher shop's 3D art, in the same style as the approved goods / cash / farm art:
    /// pig, pig pen building, delivery van, cutting table, display counter, and the goods raw
    /// meat, steak and sausage.
    ///
    /// Everything is built from <see cref="GoodsMeshBuilder"/> (rounded superellipsoids and
    /// revolves, one submesh per material) or, for the pen building, the same <c>LevelBuildKit.Box</c>
    /// greybox the coop and cow shed are made from. Meshes are saved as assets under
    /// Meshes/Butcher (goods: Meshes/Goods) and every material is an ASSET (Bt_* / Good_*) -
    /// a runtime Material renders magenta under URP.
    ///
    /// NOT wired in: nothing references these yet. Goods follow the GoodsModels contract (pivot at
    /// the centre of the stack cell, ~0.34 x 1.6 footprint, no shadow); the measured model heights
    /// are what an ItemDefinition.stackHeight should be (<see cref="MeasuredHeight"/>). Because
    /// LevelBuildKit.Item looks up <c>GoodsModels.PrefabPath(id)</c>, an item with id "meat",
    /// "steak" or "sausage" picks its prefab up automatically.
    ///
    /// Orientation: pig, van and counter all face their local +z (the pig's snout, the van's
    /// bonnet); the counter's customer side is -z, its staff side +z.
    /// </summary>
    public static class ButcherModels
    {
        public const string MeshFolder = "Assets/_Project/Meshes/Butcher";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Butcher";

        public static readonly string[] GoodIds = { "meat", "steak", "sausage" };

        /// <summary>Local position of the counter's goods tray surface (top of the tray).</summary>
        public const float CounterTrayY = 1.01f;

        // ------------------------------------------------------------------ materials

        private static Material M(string name, Color c, float smooth, float metallic = 0f)
        {
            var m = LevelBuildKit.Mat(name, c, smooth);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Color C(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        /// <summary>Alpha-blended URP Lit material asset, no shadow casting (a see-through pane).</summary>
        private static Material Glass(string name, Color c, float smooth)
        {
            var m = M(name, c, smooth);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material White() => M("Bt_White", C(0.97f, 0.95f, 0.92f), 0.45f);
        private static Material Red() => M("Bt_Red", C(0.86f, 0.22f, 0.28f), 0.5f);
        private static Material Dark() => M("Bt_Dark", C(0.20f, 0.21f, 0.24f), 0.3f);
        private static Material GlassBlue() => M("Bt_VanGlass", C(0.50f, 0.78f, 0.95f), 0.85f);
        private static Material Tyre() => M("Bt_Tyre", C(0.11f, 0.11f, 0.12f), 0.15f);
        private static Material Steel() => M("Bt_Steel", C(0.80f, 0.82f, 0.86f), 0.65f, 0.15f);
        private static Material Lamp() => M("Bt_Lamp", C(1.00f, 0.88f, 0.40f), 0.7f);
        private static Material Wood() => M("Bt_Wood", C(0.80f, 0.60f, 0.36f), 0.25f);
        private static Material WoodDark() => M("Bt_WoodDark", C(0.55f, 0.38f, 0.22f), 0.25f);
        private static Material WoodLight() => M("Bt_WoodLight", C(0.90f, 0.74f, 0.50f), 0.3f);
        private static Material Meat() => M("Bt_Meat", C(0.93f, 0.36f, 0.42f), 0.65f);
        private static Material Fat() => M("Bt_Fat", C(1.00f, 0.92f, 0.84f), 0.4f);
        private static Material Bone() => M("Bt_Bone", C(1.00f, 0.97f, 0.88f), 0.4f);
        private static Material PigSkin() => M("Bt_PigSkin", C(1.00f, 0.71f, 0.75f), 0.45f);
        private static Material PigSnout() => M("Bt_PigSnout", C(0.99f, 0.58f, 0.66f), 0.5f);
        private static Material PigEar() => M("Bt_PigEar", C(0.96f, 0.55f, 0.63f), 0.4f);
        private static Material PigDark() => M("Bt_PigDark", C(0.20f, 0.10f, 0.13f), 0.3f);
        private static Material PigHoof() => M("Bt_PigHoof", C(0.45f, 0.28f, 0.28f), 0.2f);

        // ------------------------------------------------------------------ helpers

        private struct Model
        {
            public GoodsMeshBuilder Mesh;
            public Material[] Materials;
        }

        /// <summary>A soft rounded box (superellipsoid close to a cuboid), polar axis Y.</summary>
        private static List<Vector3> Slab(GoodsMeshBuilder b, int sub, Vector3 c, Vector3 h, float e = 0.25f,
            float yaw = 0f, float pitch = 0f, float roll = 0f, int nu = 24, int nv = 14,
            Func<Vector3, Vector3> modify = null) =>
            b.Super(sub, c, h, e, e, nu, nv, false, yaw, pitch, roll, modify);

        private static Vector2 V(float r, float y) => new Vector2(r, y);

        private static List<Vector2> Dense(int n, Func<float, Vector2> f)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= n; i++) l.Add(f(i / (float)n));
            return l;
        }

        /// <summary>
        /// A small oval laid on the upward-facing surface of a body, tilted to the slope there and
        /// half sunk into it (the way the bread's slashes sit). <paramref name="pts"/> is the point
        /// list that <see cref="GoodsMeshBuilder.Super"/> returned for the body.
        /// </summary>
        private static void Streak(GoodsMeshBuilder b, List<Vector3> pts, int sub, float x, float z,
            Vector3 half, float yaw, float sink = 0.003f)
        {
            float Top(float px, float pz)
            {
                float best = float.MinValue, bd = float.MaxValue;
                foreach (var p in pts)
                {
                    if (p.y < 0f) continue;
                    float d = (p.x - px) * (p.x - px) + (p.z - pz) * (p.z - pz);
                    if (d < bd) { bd = d; best = p.y; }
                }
                return best;
            }
            float y = Top(x, z);
            float dx = (Top(x + 0.012f, z) - Top(x - 0.012f, z)) / 0.024f;
            float dz = (Top(x, z + 0.012f) - Top(x, z - 0.012f)) / 0.024f;
            var n = new Vector3(-dx, 1f, -dz).normalized;
            var tilt = Quaternion.FromToRotation(Vector3.up, n);
            int from = b.VertexCount;
            b.Ellipsoid(sub, Vector3.zero, half, 14, 8, yaw: yaw);
            b.Transform(from, Matrix4x4.TRS(new Vector3(x, y - sink, z), tilt, Vector3.one));
        }

        // ================================================================== GOODS

        private static Model RawMeat()
        {
            // A cartoon joint: plump pink-red body, cream fat cap, marbling, and a bone sticking out.
            var b = new GoodsMeshBuilder();
            var half = new Vector3(0.170f, 0.105f, 0.125f);
            Vector3 Shape(Vector3 q)
            {
                // Narrower at the bone end, fuller at the other.
                float t = Mathf.Clamp01(0.5f - q.x / (2f * half.x));   // 1 at -x (bone end)
                float k = 1f - 0.22f * t;
                q.y *= k; q.z *= k;
                if (q.y < 0f) q.y *= 0.8f;
                return q;
            }
            var pts = b.Super(0, Vector3.zero, half, 0.70f, 0.80f, 56, 30, alongX: true, modify: Shape);

            // Fat cap along the front-top edge.
            b.Super(1, new Vector3(0.025f, 0.062f, 0.050f), new Vector3(0.125f, 0.050f, 0.078f), 0.6f, 0.7f,
                36, 20, alongX: true, roll: -18f);

            // Marbling streaks across the top.
            Streak(b, pts, 1, -0.05f, -0.01f, new Vector3(0.050f, 0.007f, 0.011f), 28f);
            Streak(b, pts, 1, 0.05f, -0.05f, new Vector3(0.045f, 0.007f, 0.010f), -22f);
            Streak(b, pts, 1, 0.095f, 0.0f, new Vector3(0.035f, 0.007f, 0.009f), 40f);

            // Bone: shaft and two knuckles, out of the narrow end.
            int from = b.VertexCount;
            b.Revolve(2, 20, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.036f, 0f), V(0.030f, 0.05f), V(0.028f, 0.16f), V(0f, 0.16f)
            }, 4));
            b.Transform(from, Matrix4x4.TRS(new Vector3(-0.11f, 0f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one));
            b.Ellipsoid(2, new Vector3(-0.275f, 0f, 0.034f), new Vector3(0.040f, 0.040f, 0.040f), 14, 10);
            b.Ellipsoid(2, new Vector3(-0.275f, 0f, -0.034f), new Vector3(0.040f, 0.040f, 0.040f), 14, 10);

            b.Fit(0.30f * GoodsModels.Scale, 0.34f * GoodsModels.Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Meat_Flesh", C(0.93f, 0.36f, 0.42f), 0.65f),
                    M("Good_Meat_Fat", C(1.00f, 0.92f, 0.84f), 0.4f),
                    M("Good_Meat_Bone", C(1.00f, 0.97f, 0.88f), 0.4f)
                }
            };
        }

        private static Model Steak()
        {
            // A thick-cut steak: cream fat rim, seared brown top, grill marks, a bone ring.
            var b = new GoodsMeshBuilder();
            Vector3 Kidney(Vector3 q)
            {
                float k = Mathf.Clamp01(1f - Mathf.Abs(q.x) / 0.21f);
                if (q.z > 0f) q.z -= 0.034f * k * (q.z / 0.15f);
                return q;
            }
            b.Super(0, new Vector3(0f, -0.014f, 0f), new Vector3(0.215f, 0.062f, 0.165f), 0.55f, 0.80f, 56, 30,
                modify: Kidney);
            var pts = b.Super(1, new Vector3(0f, 0.004f, 0f), new Vector3(0.190f, 0.070f, 0.140f), 0.55f, 0.80f, 56, 30,
                modify: Kidney);

            for (int i = -1; i <= 1; i++)
                Streak(b, pts, 2, i * 0.072f, 0f, new Vector3(0.105f, 0.0075f, 0.0125f), 58f, 0.0035f);
            Streak(b, pts, 3, -0.105f, -0.045f, new Vector3(0.045f, 0.016f, 0.045f), 0f, 0.002f);

            b.Fit(0.26f, 0.34f * GoodsModels.Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Steak_Fat", C(1.00f, 0.93f, 0.80f), 0.4f),
                    M("Good_Steak_Seared", C(0.82f, 0.38f, 0.24f), 0.5f),
                    M("Good_Steak_Grill", C(0.30f, 0.11f, 0.06f), 0.3f),
                    M("Good_Steak_Bone", C(1.00f, 0.98f, 0.90f), 0.4f)
                }
            };
        }

        /// <summary>One plump sausage lying along X, ends rounded, with an optional gentle bend.</summary>
        private static void SausageBody(GoodsMeshBuilder b, Vector3 pos, float yaw, float bend, float len = 0.36f,
            float r = 0.062f)
        {
            int from = b.VertexCount;
            var prof = Dense(24, t =>
            {
                float u = t * 2f - 1f;
                float rr = r * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(u), 2.6f)), 1f / 2.6f);
                return V(rr, u * len * 0.5f);
            });
            b.Revolve(0, 28, prof, (q, u, v) =>
            {
                float s = q.y / (len * 0.5f);
                q.z += bend * (1f - s * s);
                return q;
            });
            // Darker pinched ends.
            b.Ellipsoid(1, new Vector3(0f, len * 0.5f - 0.004f, bend * 0f), new Vector3(0.020f, 0.016f, 0.020f), 10, 8);
            b.Ellipsoid(1, new Vector3(0f, -len * 0.5f + 0.004f, 0f), new Vector3(0.020f, 0.016f, 0.020f), 10, 8);
            b.Transform(from, Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, 90f), Vector3.one));
        }

        private static Model Sausage()
        {
            // The classic pile of three: two on the bottom, one in the groove.
            var b = new GoodsMeshBuilder();
            SausageBody(b, new Vector3(0f, 0f, -0.064f), 2f, 0.020f);
            SausageBody(b, new Vector3(0.012f, 0f, 0.064f), -3f, -0.018f);
            SausageBody(b, new Vector3(-0.010f, 0.108f, 0.000f), 0f, 0.012f);
            b.Fit(0.30f * GoodsModels.Scale, 0.34f * GoodsModels.Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Sausage_Skin", C(0.88f, 0.42f, 0.30f), 0.65f),
                    M("Good_Sausage_Tip", C(0.58f, 0.22f, 0.14f), 0.4f)
                }
            };
        }

        private static Model GoodModel(string id)
        {
            switch (id)
            {
                case "meat": return RawMeat();
                case "steak": return Steak();
                case "sausage": return Sausage();
            }
            throw new ArgumentException(id);
        }

        private static readonly Dictionary<string, float> _heights = new Dictionary<string, float>();

        /// <summary>The model's fitted height = the stackHeight an ItemDefinition for it should use.</summary>
        public static float MeasuredHeight(string id)
        {
            if (_heights.TryGetValue(id, out float h)) return h;
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GoodsModels.MeshPath(id));
            return mesh != null ? mesh.bounds.size.y : 0.3f;
        }

        // ================================================================== ANIMAL

        private static Model PigModel()
        {
            var b = new GoodsMeshBuilder();
            const int Skin = 0, Snout = 1, Ear = 2, DarkS = 3, Hoof = 4;

            // Plump body and a round head with a deep chin, a touch pear-shaped.
            b.Super(Skin, new Vector3(0f, 0.52f, -0.06f), new Vector3(0.34f, 0.32f, 0.50f), 0.88f, 0.92f, 48, 30);
            b.Ellipsoid(Skin, new Vector3(0f, 0.53f, 0.50f), new Vector3(0.26f, 0.24f, 0.24f), 36, 24);

            // Snout: a short stubby disc facing +z, with two nostrils.
            int from = b.VertexCount;
            b.Revolve(Snout, 28, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, -0.04f), V(0.095f, -0.04f), V(0.125f, -0.02f), V(0.128f, 0.025f), V(0.105f, 0.05f), V(0f, 0.052f)
            }, 4));
            b.Transform(from, Matrix4x4.TRS(new Vector3(0f, 0.47f, 0.70f), Quaternion.Euler(90f, 0f, 0f), Vector3.one));
            b.Ellipsoid(DarkS, new Vector3(-0.046f, 0.475f, 0.752f), new Vector3(0.019f, 0.030f, 0.014f), 10, 8);
            b.Ellipsoid(DarkS, new Vector3(0.046f, 0.475f, 0.752f), new Vector3(0.019f, 0.030f, 0.014f), 10, 8);

            // Eyes and floppy ears folded forward over the brow.
            b.Ellipsoid(DarkS, new Vector3(-0.150f, 0.62f, 0.64f), new Vector3(0.032f, 0.040f, 0.026f), 10, 8);
            b.Ellipsoid(DarkS, new Vector3(0.150f, 0.62f, 0.64f), new Vector3(0.032f, 0.040f, 0.026f), 10, 8);
            b.Ellipsoid(Ear, new Vector3(-0.175f, 0.74f, 0.52f), new Vector3(0.100f, 0.030f, 0.125f), 20, 12,
                yaw: 0f, pitch: -28f, roll: 28f);
            b.Ellipsoid(Ear, new Vector3(0.175f, 0.74f, 0.52f), new Vector3(0.100f, 0.030f, 0.125f), 20, 12,
                yaw: 0f, pitch: -28f, roll: -28f);

            // Four short legs ending in dark hooves.
            foreach (float lx in new[] { -0.20f, 0.20f })
                foreach (float lz in new[] { 0.28f, -0.38f })
                {
                    b.Revolve(Skin, 18, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0.05f), V(0.088f, 0.05f), V(0.090f, 0.12f), V(0.080f, 0.32f), V(0f, 0.34f)
                    }, 4), (q, u, v) => q + new Vector3(lx, 0f, lz));
                    b.Revolve(Hoof, 18, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0f), V(0.082f, 0f), V(0.095f, 0.018f), V(0.092f, 0.058f), V(0f, 0.058f)
                    }, 3), (q, u, v) => q + new Vector3(lx, 0f, lz));
                }

            // Curly tail: a spiral of little beads at the back.
            for (int k = 0; k < 9; k++)
            {
                float t = k / 8f * Mathf.PI * 2.1f;
                var p = new Vector3(0.050f * Mathf.Sin(t), 0.64f + 0.020f * k - 0.003f * k * k * 0.3f,
                    -0.58f - 0.050f * (1f - Mathf.Cos(t)));
                float rr = Mathf.Lerp(0.028f, 0.016f, k / 8f);
                b.Ellipsoid(Ear, p, new Vector3(rr, rr, rr), 8, 6);
            }

            // A hair under 1:1 with the cow so a pen of them looks right.
            b.Transform(0, Matrix4x4.Scale(Vector3.one * 0.88f));
            return new Model
            {
                Mesh = b,
                Materials = new[] { PigSkin(), PigSnout(), PigEar(), PigDark(), PigHoof() }
            };
        }

        // ================================================================== VAN

        private static Model VanModel()
        {
            // Web budget: the first approved pass was ~27k vertices (every slab 24 x 14, every wheel 28-gon).
            // This is the same van with every part at the lowest resolution that still reads the same at
            // game scale: big slabs 16 x 8, small panels 8 x 4 or 12 x 6, wheels 16-gons. Shapes, colours,
            // livery and proportions are untouched.
            var b = new GoodsMeshBuilder();
            const int Wh = 0, Rd = 1, Dk = 2, Gl = 3, Ty = 4, St = 5, Lp = 6, Wd = 7, Mt = 8, Ft = 9, Pk = 10, Sn = 11, Bx = 12;

            // Chassis and cargo box: floor, two walls, bulkhead, roof - open at the back so the meat shows.
            Slab(b, Dk, new Vector3(0f, 0.52f, 0f), new Vector3(0.84f, 0.10f, 1.80f), 0.3f, nu: 16, nv: 8);
            Slab(b, Bx, new Vector3(-0.80f, 1.30f, -0.72f), new Vector3(0.06f, 0.74f, 1.10f), 0.3f, nu: 12, nv: 6);
            Slab(b, Bx, new Vector3(0.80f, 1.30f, -0.72f), new Vector3(0.06f, 0.74f, 1.10f), 0.3f, nu: 12, nv: 6);
            Slab(b, Bx, new Vector3(0f, 1.30f, 0.36f), new Vector3(0.86f, 0.74f, 0.07f), 0.3f, nu: 12, nv: 6);
            Slab(b, Bx, new Vector3(0f, 2.06f, -0.70f), new Vector3(0.90f, 0.075f, 1.16f), 0.3f, nu: 16, nv: 8);
            Slab(b, Wd, new Vector3(0f, 0.65f, -0.72f), new Vector3(0.78f, 0.04f, 1.08f), 0.3f, nu: 8, nv: 4);

            // Cab: bonnet/body and the glasshouse on top, windscreen and side windows.
            Slab(b, Wh, new Vector3(0f, 0.90f, 1.12f), new Vector3(0.86f, 0.36f, 0.74f), 0.35f, nu: 16, nv: 8);
            Slab(b, Wh, new Vector3(0f, 1.50f, 0.86f), new Vector3(0.80f, 0.36f, 0.46f), 0.40f, nu: 16, nv: 8);
            Slab(b, Gl, new Vector3(0f, 1.52f, 1.32f), new Vector3(0.66f, 0.22f, 0.03f), 0.35f, pitch: -16f, nu: 8, nv: 4);
            Slab(b, Gl, new Vector3(-0.795f, 1.52f, 0.86f), new Vector3(0.03f, 0.20f, 0.34f), 0.35f, nu: 8, nv: 4);
            Slab(b, Gl, new Vector3(0.795f, 1.52f, 0.86f), new Vector3(0.03f, 0.20f, 0.34f), 0.35f, nu: 8, nv: 4);

            // Front end: grille, bumpers, headlights, mirrors; tail lights at the back.
            Slab(b, Dk, new Vector3(0f, 0.82f, 1.865f), new Vector3(0.40f, 0.12f, 0.035f), 0.3f, nu: 8, nv: 4);
            Slab(b, Dk, new Vector3(0f, 0.62f, 1.85f), new Vector3(0.86f, 0.09f, 0.09f), 0.4f, nu: 8, nv: 4);
            Slab(b, Dk, new Vector3(0f, 0.62f, -1.84f), new Vector3(0.86f, 0.09f, 0.08f), 0.4f, nu: 8, nv: 4);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Ellipsoid(Lp, new Vector3(0.60f * s, 0.98f, 1.83f), new Vector3(0.15f, 0.10f, 0.06f), 8, 4);
                Slab(b, Rd, new Vector3(0.805f * s, 0.95f, -1.835f), new Vector3(0.05f, 0.12f, 0.03f), 0.4f, nu: 8, nv: 4);
                Slab(b, Dk, new Vector3(0.98f * s, 1.42f, 1.10f), new Vector3(0.045f, 0.11f, 0.07f), 0.4f, nu: 8, nv: 4);
                Slab(b, Dk, new Vector3(0.90f * s, 1.38f, 1.10f), new Vector3(0.08f, 0.015f, 0.015f), 0.4f, nu: 8, nv: 4);

                // Butcher livery: a red stripe down the cargo box and cab, a plate with a pig on it.
                Slab(b, Rd, new Vector3(0.865f * s, 1.02f, -0.72f), new Vector3(0.020f, 0.11f, 1.10f), 0.5f, nu: 8, nv: 4);
                Slab(b, Rd, new Vector3(0.865f * s, 0.88f, 1.12f), new Vector3(0.020f, 0.07f, 0.74f), 0.5f, nu: 8, nv: 4);

                int from = b.VertexCount;
                b.Revolve(Ft, 16, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, 0f), V(0.30f, 0f), V(0.318f, 0.010f), V(0.318f, 0.022f), V(0.30f, 0.032f), V(0f, 0.034f)
                }, 1));
                b.Transform(from, Matrix4x4.TRS(new Vector3(0.855f * s, 1.58f, -0.72f),
                    Quaternion.Euler(0f, 0f, -90f * s), Vector3.one));
                float x0 = 0.895f * s;
                b.Ellipsoid(Pk, new Vector3(x0, 1.56f, -0.72f), new Vector3(0.035f, 0.185f, 0.185f), 12, 8);
                b.Ellipsoid(Sn, new Vector3(x0 + 0.030f * s, 1.51f, -0.72f), new Vector3(0.035f, 0.075f, 0.095f), 8, 6);
                b.Ellipsoid(Dk, new Vector3(x0 + 0.055f * s, 1.51f, -0.755f), new Vector3(0.012f, 0.025f, 0.014f), 4, 3);
                b.Ellipsoid(Dk, new Vector3(x0 + 0.055f * s, 1.51f, -0.685f), new Vector3(0.012f, 0.025f, 0.014f), 4, 3);
                b.Ellipsoid(Dk, new Vector3(x0 + 0.030f * s, 1.64f, -0.795f), new Vector3(0.014f, 0.030f, 0.024f), 4, 3);
                b.Ellipsoid(Dk, new Vector3(x0 + 0.030f * s, 1.64f, -0.645f), new Vector3(0.014f, 0.030f, 0.024f), 4, 3);
                b.Ellipsoid(Pk, new Vector3(x0, 1.74f, -0.855f), new Vector3(0.030f, 0.075f, 0.050f), 6, 4, roll: 0f, pitch: 28f);
                b.Ellipsoid(Pk, new Vector3(x0, 1.74f, -0.585f), new Vector3(0.030f, 0.075f, 0.050f), 6, 4, roll: 0f, pitch: -28f);

                // Wheels: tyre and hub, rear and front.
                foreach (float wz in new[] { -1.15f, 1.22f })
                {
                    int w0 = b.VertexCount;
                    b.Revolve(Ty, 16, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, -0.13f), V(0.30f, -0.13f), V(0.355f, -0.09f), V(0.365f, 0f),
                        V(0.355f, 0.09f), V(0.30f, 0.13f), V(0f, 0.13f)
                    }, 1));
                    b.Revolve(St, 8, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, -0.142f), V(0.17f, -0.142f), V(0.19f, -0.13f), V(0.19f, 0.13f), V(0.17f, 0.142f), V(0f, 0.142f)
                    }, 1));
                    b.Transform(w0, Matrix4x4.TRS(new Vector3(0.80f * s, 0.365f, wz), Quaternion.Euler(0f, 0f, 90f), Vector3.one));
                }

                // Rear door, swung open 100 degrees.
                {
                    float psi = 90f - s * 10f;
                    var dir = new Vector3(Mathf.Cos(psi * Mathf.Deg2Rad), 0f, -Mathf.Sin(psi * Mathf.Deg2Rad));
                    var hinge = new Vector3(0.855f * s, 1.30f, -1.87f);
                    Slab(b, Bx, hinge + dir * 0.43f, new Vector3(0.43f, 0.72f, 0.04f), 0.3f, yaw: psi, nu: 8, nv: 4);
                    Slab(b, Rd, hinge + dir * 0.43f + new Vector3(0f, -0.20f, 0f) + Quaternion.Euler(0f, psi, 0f) * new Vector3(0f, 0f, -0.045f),
                        new Vector3(0.40f, 0.09f, 0.012f), 0.5f, yaw: psi, nu: 8, nv: 4);
                }
            }

            // Crates of meat in the back: wood boxes heaped with joints.
            void Crate(Vector3 c)
            {
                Slab(b, Wd, c, new Vector3(0.30f, 0.17f, 0.25f), 0.15f, nu: 8, nv: 4);
                float top = c.y + 0.14f;
                b.Ellipsoid(Mt, new Vector3(c.x - 0.12f, top + 0.04f, c.z + 0.05f), new Vector3(0.14f, 0.09f, 0.12f), 6, 4, yaw: 20f);
                b.Ellipsoid(Mt, new Vector3(c.x + 0.12f, top + 0.05f, c.z - 0.05f), new Vector3(0.13f, 0.09f, 0.11f), 6, 4, yaw: -30f);
                b.Ellipsoid(Ft, new Vector3(c.x, top + 0.11f, c.z), new Vector3(0.09f, 0.045f, 0.07f), 6, 4, yaw: 40f);
            }
            Crate(new Vector3(-0.40f, 0.85f, -1.34f));
            Crate(new Vector3(0.32f, 0.85f, -1.30f));
            Crate(new Vector3(-0.40f, 0.85f, -0.74f));
            Crate(new Vector3(-0.40f, 1.19f, -1.34f));

            // A small cartoon van, not a lorry: a little longer than the barn is wide.
            b.Transform(0, Matrix4x4.Scale(Vector3.one * 0.72f));
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    White(), Red(), Dark(), GlassBlue(), Tyre(), Steel(), Lamp(), Wood(), Meat(), Fat(), PigSkin(), PigSnout(),
                    M("Bt_VanBox", C(1.00f, 0.76f, 0.79f), 0.5f)
                }
            };
        }

        // ================================================================== CUTTING TABLE

        private static Model TableModel()
        {
            var b = new GoodsMeshBuilder();
            const int Lt = 0, Wd = 1, Dk = 2, St = 3, Mt = 4, Ft = 5, Bn = 6, Sm = 7;

            // A thick end-grain block top on an apron, four chunky legs and a low shelf.
            var top = Slab(b, Lt, new Vector3(0f, 0.92f, 0f), new Vector3(0.92f, 0.12f, 0.58f), 0.30f, nu: 40, nv: 22);
            Slab(b, Wd, new Vector3(0f, 0.72f, 0f), new Vector3(0.84f, 0.07f, 0.50f), 0.25f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Slab(b, Dk, new Vector3(0.78f * sx, 0.37f, 0.44f * sz), new Vector3(0.075f, 0.37f, 0.075f), 0.3f);
            Slab(b, Wd, new Vector3(0f, 0.22f, 0f), new Vector3(0.80f, 0.03f, 0.46f), 0.25f);

            // Knife marks worn into the top.
            var rnd = new System.Random(5);
            for (int i = 0; i < 6; i++)
            {
                float x = (float)(rnd.NextDouble() * 1.0 - 0.5) * 0.9f;
                float z = (float)(rnd.NextDouble() * 0.6 - 0.3);
                Streak(b, top, Sm, x, z, new Vector3(0.10f + (float)rnd.NextDouble() * 0.08f, 0.004f, 0.006f),
                    (float)rnd.NextDouble() * 70f - 35f, 0.001f);
            }

            // The cleaver, buried in the block at the right: blade, hanging hole and a wooden grip.
            int from = b.VertexCount;
            Slab(b, St, new Vector3(0f, 0.115f, 0f), new Vector3(0.175f, 0.115f, 0.013f), 0.22f);
            b.Ellipsoid(Dk, new Vector3(-0.115f, 0.16f, 0f), new Vector3(0.022f, 0.022f, 0.016f), 10, 8);
            int g0 = b.VertexCount;
            b.Revolve(Dk, 14, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.032f, 0f), V(0.036f, 0.02f), V(0.030f, 0.12f), V(0.034f, 0.23f), V(0f, 0.24f)
            }, 4));
            b.Transform(g0, Matrix4x4.TRS(new Vector3(0.165f, 0.168f, 0f), Quaternion.Euler(0f, 0f, -90f), Vector3.one));
            b.Transform(from, Matrix4x4.TRS(new Vector3(0.50f, 1.00f, 0.0f), Quaternion.Euler(0f, 45f, -7f), Vector3.one));

            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    WoodLight(), Wood(), WoodDark(), Steel(), Meat(), Fat(), Bone(),
                    M("Bt_WoodCut", C(0.62f, 0.44f, 0.26f), 0.2f)
                }
            };
        }

        // ================================================================== MEAT GRINDER

        /// <summary>
        /// A crank grinder bolted to a small bench: steel body along z with the nozzle at the FRONT (-z, the
        /// camera side), a funnel hopper on top with a joint in it, a side crank with a red knob, and a
        /// sausage coil lying on a steel tray under the nozzle. Same bench woods as the cutting table.
        /// </summary>
        private static Model GrinderModel()
        {
            var b = new GoodsMeshBuilder();
            const int Lt = 0, Wd = 1, Dk = 2, St = 3, Mt = 4, Ft = 5, Rd = 6, Sg = 7;
            const float top = 1.04f;

            // Bench: block top on an apron, four legs, a low shelf (a smaller sibling of the cutting table).
            Slab(b, Lt, new Vector3(0f, 0.92f, 0f), new Vector3(0.64f, 0.12f, 0.52f), 0.30f, nu: 24, nv: 12);
            Slab(b, Wd, new Vector3(0f, 0.72f, 0f), new Vector3(0.56f, 0.07f, 0.44f), 0.25f, nu: 16, nv: 8);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Slab(b, 8, new Vector3(0.54f * sx, 0.37f, 0.40f * sz), new Vector3(0.075f, 0.37f, 0.075f), 0.3f, nu: 12, nv: 6);
            Slab(b, Wd, new Vector3(0f, 0.22f, 0f), new Vector3(0.52f, 0.03f, 0.40f), 0.25f, nu: 12, nv: 6);

            // Clamp plate and the body: a steel barrel lying along z.
            Slab(b, Dk, new Vector3(0f, top + 0.03f, 0.12f), new Vector3(0.22f, 0.03f, 0.24f), 0.3f, nu: 12, nv: 6);
            const float axisY = top + 0.30f, axisZ = 0.12f;
            int from = b.VertexCount;
            b.Revolve(St, 28, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, -0.27f), V(0.14f, -0.27f), V(0.175f, -0.24f), V(0.18f, -0.20f), V(0.18f, 0.20f), V(0.175f, 0.24f),
                V(0.14f, 0.27f), V(0f, 0.27f)
            }, 3));
            b.Transform(from, Matrix4x4.TRS(new Vector3(0f, axisY, axisZ), Quaternion.Euler(90f, 0f, 0f), Vector3.one));

            // Front: die plate and nozzle collar, dark so the opening reads.
            from = b.VertexCount;
            b.Revolve(Dk, 24, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.14f, 0f), V(0.15f, 0.02f), V(0.15f, 0.06f), V(0.11f, 0.08f), V(0f, 0.08f)
            }, 3));
            b.Transform(from, Matrix4x4.TRS(new Vector3(0f, axisY, axisZ - 0.27f), Quaternion.Euler(-90f, 0f, 0f), Vector3.one));

            // Hopper: a funnel on top, a dark mouth, and a joint of meat dropped in.
            from = b.VertexCount;
            b.Revolve(St, 24, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.08f, 0f), V(0.10f, 0.04f), V(0.22f, 0.26f), V(0.235f, 0.285f), V(0.20f, 0.285f), V(0f, 0.27f)
            }, 2));
            b.Transform(from, Matrix4x4.TRS(new Vector3(0f, axisY + 0.12f, axisZ + 0.02f), Quaternion.identity, Vector3.one));
            b.Ellipsoid(Mt, new Vector3(0.01f, axisY + 0.42f, axisZ + 0.02f), new Vector3(0.14f, 0.09f, 0.11f), 14, 10, yaw: 25f);
            b.Ellipsoid(Ft, new Vector3(-0.04f, axisY + 0.485f, axisZ + 0.02f), new Vector3(0.07f, 0.035f, 0.06f), 10, 6, yaw: 25f);

            // Crank: axle out of the side, an arm hanging from it, a red knob on the end.
            from = b.VertexCount;
            b.Revolve(Dk, 12, GoodsMeshBuilder.Spline(new[] { V(0f, 0f), V(0.035f, 0f), V(0.035f, 0.17f), V(0f, 0.17f) }, 1));
            b.Transform(from, Matrix4x4.TRS(new Vector3(0.17f, axisY, axisZ), Quaternion.Euler(0f, 0f, -90f), Vector3.one));
            Slab(b, St, new Vector3(0.36f, axisY - 0.09f, axisZ), new Vector3(0.022f, 0.12f, 0.03f), 0.3f, nu: 8, nv: 4);
            from = b.VertexCount;
            b.Revolve(Rd, 14, GoodsMeshBuilder.Spline(new[] { V(0f, 0f), V(0.04f, 0f), V(0.045f, 0.03f), V(0.04f, 0.14f), V(0.03f, 0.16f), V(0f, 0.17f) }, 2));
            b.Transform(from, Matrix4x4.TRS(new Vector3(0.36f, axisY - 0.21f, axisZ), Quaternion.Euler(0f, 0f, -90f), Vector3.one));

            // Tray under the nozzle, with a coil of sausage curling out onto it.
            Slab(b, St, new Vector3(0f, top + 0.02f, -0.34f), new Vector3(0.30f, 0.02f, 0.20f), 0.35f, nu: 16, nv: 8);
            const int beads = 24;
            for (int k = 0; k < beads; k++)
            {
                float t = k / (float)(beads - 1);
                float a = t * Mathf.PI * 3.3f;
                float rad = Mathf.Lerp(0.0f, 0.15f, Mathf.Min(1f, t * 2.2f));
                var p = new Vector3(rad * Mathf.Sin(a), Mathf.Lerp(axisY - 0.02f, top + 0.095f, Mathf.Clamp01(t * 2.5f)),
                    Mathf.Lerp(axisZ - 0.30f, -0.34f, Mathf.Clamp01(t * 2.5f)) + rad * Mathf.Cos(a) * 0.7f);
                b.Ellipsoid(Sg, p, new Vector3(0.062f, 0.052f, 0.062f), 8, 6);
            }

            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    WoodLight(), Wood(), Dark(), Steel(), Meat(), Fat(), Red(),
                    M("Bt_Sausage", C(0.86f, 0.38f, 0.26f), 0.5f), WoodDark()
                }
            };
        }

        // ================================================================== DISPLAY COUNTER

        private static Model CounterModel()
        {
            var b = new GoodsMeshBuilder();
            const int Tile = 0, Rd = 1, Dk = 2, Tray = 3, St = 4, Gl = 5, Wd = 6;

            // Cabinet in white tile with a red stripe, a dark kick plate, a worktop shelf behind.
            Slab(b, Tile, new Vector3(0f, 0.52f, 0f), new Vector3(1.58f, 0.40f, 0.54f), 0.18f);
            Slab(b, Dk, new Vector3(0f, 0.06f, -0.02f), new Vector3(1.52f, 0.06f, 0.50f), 0.3f);
            Slab(b, Rd, new Vector3(0f, 0.52f, -0.545f), new Vector3(1.56f, 0.12f, 0.016f), 0.5f);
            Slab(b, Wd, new Vector3(0f, 0.935f, 0f), new Vector3(1.62f, 0.025f, 0.58f), 0.3f);   // worktop lip

            // Pale tray the goods sit on.
            Slab(b, Tray, new Vector3(0f, 0.985f, 0f), new Vector3(1.52f, 0.025f, 0.50f), 0.3f);

            // Steel frame: front and back rails, four corner posts, top rails.
            foreach (float sz in new[] { -0.50f, 0.50f })
                Slab(b, St, new Vector3(0f, 1.025f, sz), new Vector3(1.55f, 0.025f, 0.025f), 0.4f);
            foreach (float sx in new[] { -1.53f, 1.53f })
            {
                Slab(b, St, new Vector3(sx, 1.30f, 0.50f), new Vector3(0.03f, 0.30f, 0.03f), 0.4f);
                Slab(b, St, new Vector3(sx, 1.30f, -0.50f), new Vector3(0.03f, 0.03f, 0.03f), 0.4f);
                Slab(b, St, new Vector3(sx, 1.575f, -0.28f), new Vector3(0.03f, 0.03f, 0.03f), 0.4f);
                Slab(b, St, new Vector3(sx, 1.425f, -0.39f), new Vector3(0.025f, 0.03f, 0.25f), 0.4f, pitch: 0f);
                Slab(b, St, new Vector3(sx, 1.59f, 0.11f), new Vector3(0.03f, 0.025f, 0.41f), 0.4f);
            }
            Slab(b, St, new Vector3(0f, 1.59f, -0.28f), new Vector3(1.55f, 0.025f, 0.025f), 0.4f);
            Slab(b, St, new Vector3(0f, 1.59f, 0.50f), new Vector3(1.55f, 0.025f, 0.025f), 0.4f);

            // Glass: sloping front, flat top, two end panes (back left open for the staff).
            float dz = 0.22f, dy = 0.55f;
            float len = Mathf.Sqrt(dz * dz + dy * dy);
            Slab(b, Gl, new Vector3(0f, 1.305f, -0.39f), new Vector3(1.52f, len * 0.5f, 0.010f), 0.4f,
                pitch: Mathf.Atan2(dz, dy) * Mathf.Rad2Deg);
            Slab(b, Gl, new Vector3(0f, 1.595f, 0.11f), new Vector3(1.52f, 0.010f, 0.39f), 0.4f);
            Slab(b, Gl, new Vector3(-1.53f, 1.30f, 0.07f), new Vector3(0.010f, 0.275f, 0.43f), 0.4f);
            Slab(b, Gl, new Vector3(1.53f, 1.30f, 0.07f), new Vector3(0.010f, 0.275f, 0.43f), 0.4f);

            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Bt_Tile", C(0.96f, 0.94f, 0.90f), 0.55f), Red(), Dark(),
                    M("Bt_Tray", C(1.00f, 0.88f, 0.88f), 0.5f), Steel(),
                    Glass("Bt_CaseGlass", C(0.80f, 0.93f, 1.00f, 0.24f), 0.95f), WoodLight()
                }
            };
        }

        // ================================================================== PIG-HEAD EMBLEM (pen sign)

        private static Model EmblemModel()
        {
            // A round cream plate with a pink pig face on it, facing -z, for the pen building's sign.
            var b = new GoodsMeshBuilder();
            const int Plate = 0, Pk = 1, Sn = 2, Dk = 3;
            int from = b.VertexCount;
            b.Revolve(Plate, 40, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.40f, 0f), V(0.425f, 0.012f), V(0.425f, 0.03f), V(0.40f, 0.04f), V(0f, 0.042f)
            }, 4));
            b.Transform(from, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(-90f, 0f, 0f), Vector3.one));
            // Axis Y -> -z: the plate's base is at z 0, its face at z -0.04.
            b.Ellipsoid(Pk, new Vector3(0f, 0f, -0.06f), new Vector3(0.27f, 0.25f, 0.045f), 32, 20);
            b.Ellipsoid(Pk, new Vector3(-0.19f, 0.20f, -0.065f), new Vector3(0.09f, 0.08f, 0.03f), 16, 10, roll: 30f);
            b.Ellipsoid(Pk, new Vector3(0.19f, 0.20f, -0.065f), new Vector3(0.09f, 0.08f, 0.03f), 16, 10, roll: -30f);
            b.Ellipsoid(Sn, new Vector3(0f, -0.06f, -0.095f), new Vector3(0.12f, 0.085f, 0.04f), 24, 14);
            b.Ellipsoid(Dk, new Vector3(-0.045f, -0.06f, -0.130f), new Vector3(0.018f, 0.030f, 0.012f), 8, 6);
            b.Ellipsoid(Dk, new Vector3(0.045f, -0.06f, -0.130f), new Vector3(0.018f, 0.030f, 0.012f), 8, 6);
            b.Ellipsoid(Dk, new Vector3(-0.10f, 0.08f, -0.095f), new Vector3(0.028f, 0.035f, 0.016f), 10, 8);
            b.Ellipsoid(Dk, new Vector3(0.10f, 0.08f, -0.095f), new Vector3(0.028f, 0.035f, 0.016f), 10, 8);
            return new Model
            {
                Mesh = b,
                Materials = new[] { Fat(), PigSkin(), PigSnout(), PigDark() }
            };
        }

        // ================================================================== asset plumbing

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static Mesh SaveMeshAt(string path, string name, GoodsMeshBuilder b)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
            var fresh = b.ToMesh(name);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                // Clear first: a stale index buffer from a model with other submesh ranges would
                // otherwise survive and mismatch the new vertices.
                existing.Clear();
                EditorUtility.CopySerialized(fresh, existing);
                UnityEngine.Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(fresh, path);
            return fresh;
        }

        private static readonly Dictionary<string, (Mesh mesh, Material[] mats)> _built =
            new Dictionary<string, (Mesh, Material[])>();

        private static (Mesh mesh, Material[] mats) Get(string key, Func<Model> make)
        {
            if (_built.TryGetValue(key, out var hit) && hit.mesh != null) return hit;
            var model = make();
            var mesh = SaveMeshAt($"{MeshFolder}/{key}.asset", key, model.Mesh);
            var result = (mesh, model.Materials);
            _built[key] = result;
            var s = mesh.bounds.size;
            Debug.Log($"[Butcher] {key}: {mesh.vertexCount} verts, size {s.x:0.00} x {s.y:0.00} x {s.z:0.00}");
            return result;
        }

        private static GameObject Obj(string name, Transform parent, Vector3 pos, float yaw,
            (Mesh mesh, Material[] mats) m, bool shadow = true)
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

        public static GameObject Pig(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("Pig", parent, pos, yaw, Get("Pig", PigModel));

        public static GameObject Van(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("DeliveryVan", parent, pos, yaw, Get("DeliveryVan", VanModel));

        public static GameObject CuttingTable(Transform parent, Vector3 pos, float yaw = 0f)
        {
            var go = Obj("CuttingTable", parent, pos, yaw, Get("CuttingTable", TableModel));
            // A joint and a steak waiting on the block - the real goods, so they match what is carried.
            const float top = 1.04f;
            var meat = Good("meat", go.transform, new Vector3(-0.42f, top + MeasuredHeight("meat") * 0.5f, 0.10f), 20f);
            var steak = Good("steak", go.transform, new Vector3(0.02f, top + MeasuredHeight("steak") * 0.5f, -0.20f), -15f);
            meat.name = "TableMeat"; steak.name = "TableSteak";
            return go;
        }

        public static GameObject Grinder(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("MeatGrinder", parent, pos, yaw, Get("MeatGrinder", GrinderModel));

        public static GameObject DisplayCounter(Transform parent, Vector3 pos, float yaw = 0f)
        {
            var go = Obj("DisplayCounter", parent, pos, yaw, Get("DisplayCounter", CounterModel));
            // An empty at the tray surface: goods are parented here (pivot = cell centre).
            var tray = new GameObject("Tray");
            tray.transform.SetParent(go.transform, false);
            tray.transform.localPosition = new Vector3(0f, CounterTrayY, 0f);
            return go;
        }

        /// <summary>A goods prefab instance (Good_meat / Good_steak / Good_sausage).</summary>
        public static GameObject Good(string id, Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var go = GoodsModels.Instance(id, parent, localPos);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        /// <summary>
        /// The pig pen: a barn matching the coop / cow shed (same 3.0 x 1.8 x 2.2 body, 3.4 x 0.3 x 2.6 roof,
        /// same grid squares), pink with a pig sign over the door, and the cow-sized fenced pen on its
        /// east side with a feed trough, mud and <paramref name="pigs"/> pigs. Built for the farm's
        /// fifth column at the building's origin; only visuals, no stations or colliders.
        /// </summary>
        public static GameObject PigPen(Transform parent, Vector3 pos, int pigs = 3)
        {
            var root = new GameObject("PigPen");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            PigPenInto(root.transform, pigs);
            return root;
        }

        /// <summary>
        /// The pig pen's parts (Shell, Pen) built straight into <paramref name="rootT"/>. The farm's
        /// workshop builder uses this so the pen is a workshop root like the coop's, with the same
        /// "Shell" and "Pen" children the layout audit measures. Returns the pigs' holder, which is
        /// what ProducerMachine.unitVisuals shows the first N of.
        /// </summary>
        public static Transform PigPenInto(Transform rootT, int pigs)
        {
            var root = rootT.gameObject;

            var bodyColor = new Color(0.93f, 0.66f, 0.64f);
            var body = LevelBuildKit.Mat("Body_PigPen", bodyColor);
            var roof = LevelBuildKit.Mat("Roof_PigPen", bodyColor * 0.65f);
            var trim = LevelBuildKit.Mat("Trim_PigPen", new Color(0.98f, 0.90f, 0.82f), 0.3f);
            var door = LevelBuildKit.Mat("Door_PigPen", new Color(0.55f, 0.36f, 0.26f), 0.2f);
            var glass = LevelBuildKit.Mat("Glass_PigPen", new Color(0.38f, 0.30f, 0.30f), 0.5f);

            var shell = new GameObject("Shell");
            shell.transform.SetParent(root.transform, false);
            var t = shell.transform;
            LevelBuildKit.Box("Body", t, new Vector3(0f, 0.9f, 0f), new Vector3(3.0f, 1.8f, 2.2f), body);
            LevelBuildKit.Box("Roof", t, new Vector3(0f, 1.95f, 0f), new Vector3(3.4f, 0.3f, 2.6f), roof);
            // Cream eave trim round the roof edge, and a plinth at the foot of the walls.
            LevelBuildKit.Box("Eave", t, new Vector3(0f, 1.78f, 0f), new Vector3(3.08f, 0.08f, 2.28f), trim, castShadow: false);
            LevelBuildKit.Box("Plinth", t, new Vector3(0f, 0.10f, 0f), new Vector3(3.08f, 0.20f, 2.28f),
                LevelBuildKit.Mat("Plinth_PigPen", new Color(0.62f, 0.52f, 0.48f)), castShadow: false);

            // Front (-z): stable door in a trim frame, two small windows, and the pig sign above.
            LevelBuildKit.Box("DoorFrame", t, new Vector3(0f, 0.70f, -1.105f), new Vector3(1.18f, 1.40f, 0.03f), trim, castShadow: false);
            LevelBuildKit.Box("Door", t, new Vector3(0f, 0.66f, -1.12f), new Vector3(0.98f, 1.28f, 0.04f), door, castShadow: false);
            LevelBuildKit.Box("DoorSplit", t, new Vector3(0f, 0.66f, -1.145f), new Vector3(0.98f, 0.05f, 0.02f), trim, castShadow: false);
            foreach (float sx in new[] { -1f, 1f })
            {
                LevelBuildKit.Box("WinFrame", t, new Vector3(1.05f * sx, 1.05f, -1.105f), new Vector3(0.58f, 0.52f, 0.03f), trim, castShadow: false);
                LevelBuildKit.Box("Win", t, new Vector3(1.05f * sx, 1.05f, -1.12f), new Vector3(0.46f, 0.40f, 0.03f), glass, castShadow: false);
            }
            var emblem = Obj("Sign", t, new Vector3(0f, 1.50f, -1.12f), 0f, Get("PigEmblem", EmblemModel), false);
            emblem.transform.localScale = Vector3.one * 0.72f;

            // The fenced pen on the east side: the cow shed's pasture footprint.
            var pen = new GameObject("Pen");
            pen.transform.SetParent(root.transform, false);
            pen.transform.localPosition = new Vector3(2.7f, 0f, 0f);
            Vector2 size = new Vector2(2.8f, 4.2f);
            var mud = LevelBuildKit.Mat("Pen_Mud", new Color(0.56f, 0.43f, 0.31f));
            var puddle = LevelBuildKit.Mat("Pen_Puddle", new Color(0.40f, 0.30f, 0.22f), 0.55f);
            var post = LevelBuildKit.Mat("Pen_Post", new Color(0.72f, 0.58f, 0.4f));
            var rail = LevelBuildKit.Mat("Pen_Rail", new Color(0.85f, 0.73f, 0.55f));

            LevelBuildKit.Box("Ground", pen.transform, new Vector3(0f, 0.02f, 0f), new Vector3(size.x, 0.04f, size.y), mud, castShadow: false);
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    LevelBuildKit.Box($"Post_{sx}_{sz}", pen.transform, new Vector3(hx * sx, 0.35f, hz * sz),
                        new Vector3(0.12f, 0.7f, 0.12f), post, castShadow: false);
            LevelBuildKit.Box("RailN", pen.transform, new Vector3(0f, 0.45f, hz), new Vector3(size.x, 0.08f, 0.07f), rail, castShadow: false);
            LevelBuildKit.Box("RailS", pen.transform, new Vector3(0f, 0.45f, -hz), new Vector3(size.x, 0.08f, 0.07f), rail, castShadow: false);
            LevelBuildKit.Box("RailE", pen.transform, new Vector3(hx, 0.45f, 0f), new Vector3(0.07f, 0.08f, size.y), rail, castShadow: false);

            // Mud wallows and a wooden trough of corn against the building wall.
            LevelBuildKit.Cylinder("Puddle1", pen.transform, new Vector3(0.35f, 0.045f, -0.9f), new Vector3(1.1f, 0.01f, 0.8f), puddle, false);
            LevelBuildKit.Cylinder("Puddle2", pen.transform, new Vector3(0.1f, 0.045f, 1.25f), new Vector3(0.7f, 0.01f, 0.55f), puddle, false);
            var troughMat = LevelBuildKit.Mat("Pen_Trough", new Color(0.64f, 0.46f, 0.28f), 0.25f);
            var corn = LevelBuildKit.Mat("Pen_TroughCorn", new Color(0.98f, 0.80f, 0.22f), 0.4f);
            LevelBuildKit.Box("Trough", pen.transform, new Vector3(-hx + 0.30f, 0.17f, 0.2f), new Vector3(0.50f, 0.30f, 1.5f), troughMat, castShadow: false);
            LevelBuildKit.Box("TroughCorn", pen.transform, new Vector3(-hx + 0.30f, 0.33f, 0.2f), new Vector3(0.36f, 0.06f, 1.34f), corn, castShadow: false);

            var holder = new GameObject("Animals");
            holder.transform.SetParent(pen.transform, false);
            for (int i = 0; i < pigs; i++)
            {
                float z = pigs <= 1 ? 0f : Mathf.Lerp(-hz * 0.55f, hz * 0.55f, i / (float)(pigs - 1));
                float stagger = pigs > 3 ? (i % 2 == 0 ? -0.4f : 0.4f) : (i % 2 == 0 ? 0.25f : -0.2f);
                var pig = Pig(holder.transform, new Vector3(0.62f + stagger, 0f, z), (i * 83 + 200) % 360);
                pig.name = $"Pig_{i}";
                pig.AddComponent<Tycoon.Characters.LivestockIdle>().kind = Tycoon.Characters.LivestockIdle.Kind.Pig;
            }
            return holder.transform;
        }

        // ------------------------------------------------------------------ build all

        private static GameObject SavePrefab(GameObject go, string name)
        {
            EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{name}.prefab");
            return prefab;
        }

        /// <summary>Builds every butcher mesh/material/prefab asset. Safe to rerun.</summary>
        [MenuItem("Tycoon/Build Butcher Models")]
        public static void BuildAll()
        {
            EnsureFolder(MeshFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(GoodsModels.MeshFolder);
            EnsureFolder(GoodsModels.PrefabFolder);
            _built.Clear();

            // Goods: same layout as GoodsModels.BuildAll (mesh in Meshes/Goods, prefab in Prefabs/Goods).
            foreach (string id in GoodIds)
            {
                var model = GoodModel(id);
                var mesh = SaveMeshAt(GoodsModels.MeshPath(id), "Good_" + id, model.Mesh);
                var go = new GameObject("Good_" + id);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = model.Materials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(go, GoodsModels.PrefabPath(id));
                UnityEngine.Object.DestroyImmediate(go);
                _heights[id] = mesh.bounds.size.y;
                var s = mesh.bounds.size;
                Debug.Log($"[Butcher] Good_{id}: {mesh.vertexCount} verts, size {s.x:0.000} x {s.y:0.000} x {s.z:0.000} (use stackHeight {s.y:0.00})");
            }

            var holder = new GameObject("ButcherBuild").transform;
            SavePrefab(Pig(holder, Vector3.zero), "Pig");
            SavePrefab(Van(holder, Vector3.zero), "DeliveryVan");
            SavePrefab(CuttingTable(holder, Vector3.zero), "CuttingTable");
            SavePrefab(Grinder(holder, Vector3.zero), "MeatGrinder");
            SavePrefab(DisplayCounter(holder, Vector3.zero), "DisplayCounter");
            SavePrefab(PigPen(holder, Vector3.zero, 3), "PigPen");
            UnityEngine.Object.DestroyImmediate(holder.gameObject);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("BUTCHERMODELS_OK");
        }
    }
}
