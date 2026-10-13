using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The 3D goods: egg, milk bottle, bread loaf, yogurt cup, corn cob, hay bale, apple.
    ///
    /// Built from <see cref="GoodsMeshBuilder"/> (rounded revolves / superellipsoids), saved as
    /// mesh ASSETS under Meshes/Goods and as prefabs under Prefabs/Goods, with material ASSETS
    /// (Good_*; runtime materials render magenta under URP on the web).
    ///
    /// NOT wired in yet: nothing assigns <c>ItemDefinition.visualPrefab</c>. To adopt, point each
    /// item's visualPrefab at <see cref="PrefabPath"/>(id).
    ///
    /// Contract for CarryStack / ShelfDisplay when they do adopt it: each prefab's PIVOT is the
    /// centre of its stack cell - exactly like the tinted cube it replaces (a cube is centred on
    /// its slot) - the model fits the item's stackHeight (<see cref="Heights"/>) and a ~0.34 m
    /// footprint, unit scale, no collider, no shadow casting.
    /// </summary>
    public static class GoodsModels
    {
        /// <summary>
        /// Filip wanted the goods 60% bigger than the first pass: every model is fitted to
        /// <c>Scale</c> x its old size, and the stack heights below (and the items' stackHeight in
        /// FarmSceneBuilder) are the old ones x Scale, so stacks still close up exactly.
        /// </summary>
        public const float Scale = 1.6f;

        public const string MeshFolder = "Assets/_Project/Meshes/Goods";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Goods";

        /// <summary>Ids match <c>ItemDefinition.id</c>. Value = the item's stackHeight in FarmSceneBuilder.</summary>
        public static readonly string[] Ids = { "egg", "milk", "bread", "yogurt", "corn", "hay", "apples" };

        public static readonly Dictionary<string, float> Heights = new Dictionary<string, float>
        {
            { "egg", 0.42f }, { "milk", 0.48f }, { "bread", 0.48f }, { "yogurt", 0.45f },
            { "corn", 0.48f }, { "hay", 0.51f }, { "apples", 0.45f }
        };

        public static string PrefabPath(string id) => $"{PrefabFolder}/Good_{id}.prefab";
        public static string MeshPath(string id) => $"{MeshFolder}/Good_{id}.asset";

        // ------------------------------------------------------------------ materials

        private static Material M(string name, Color c, float smooth, float metallic = 0f)
        {
            var m = LevelBuildKit.Mat(name, c, smooth);
            m.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b);

        // ------------------------------------------------------------------ build

        /// <summary>One model: its geometry and the materials of its submeshes, in order.</summary>
        private struct Model
        {
            public GoodsMeshBuilder Mesh;
            public Material[] Materials;
        }

        private static Model Build(string id)
        {
            switch (id)
            {
                case "egg": return Egg();
                case "milk": return Milk();
                case "bread": return Bread();
                case "yogurt": return Yogurt();
                case "corn": return Corn();
                case "hay": return Hay();
                case "apples": return Apple();
            }
            throw new ArgumentException(id);
        }

        private static List<Vector2> Dense(int n, Func<float, Vector2> f)
        {
            var l = new List<Vector2>();
            for (int i = 0; i <= n; i++) l.Add(f(i / (float)n));
            return l;
        }

        private static Model Egg()
        {
            var b = new GoodsMeshBuilder();
            const float H = 0.26f, R = 0.098f;
            Vector2 Prof(float t)
            {
                float u = t * 2f - 1f;
                return new Vector2(R * Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * (1f - 0.22f * u), (t - 0.5f) * H);
            }
            b.Revolve(0, 40, Dense(28, Prof));

            // A few freckles, half sunk into the shell.
            var rnd = new System.Random(7);
            for (int i = 0; i < 6; i++)
            {
                float t = Mathf.Lerp(0.35f, 0.9f, (float)rnd.NextDouble());
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                var p = Prof(t);
                b.Ellipsoid(1, new Vector3(p.x * Mathf.Cos(a), p.y, p.x * Mathf.Sin(a)),
                    new Vector3(0.0085f, 0.0065f, 0.0085f), 8, 6);
            }
            b.Fit(0.26f * Scale, 0.30f * Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Egg_Shell", C(1.00f, 0.95f, 0.84f), 0.6f),
                    M("Good_Egg_Speckle", C(0.74f, 0.55f, 0.36f), 0.3f)
                }
            };
        }

        private static Model Milk()
        {
            var b = new GoodsMeshBuilder();
            // Body, shoulder and neck as one soft bottle.
            var body = GoodsMeshBuilder.Spline(new[]
            {
                new Vector2(0f, -0.15f), new Vector2(0.060f, -0.15f), new Vector2(0.090f, -0.138f),
                new Vector2(0.098f, -0.105f), new Vector2(0.098f, 0.000f), new Vector2(0.094f, 0.040f),
                new Vector2(0.078f, 0.072f), new Vector2(0.056f, 0.094f), new Vector2(0.046f, 0.110f),
                new Vector2(0.046f, 0.128f), new Vector2(0f, 0.128f)
            }, 5);
            b.Revolve(0, 44, body);

            // The blue label, a hair proud of the glass.
            b.Revolve(1, 44, GoodsMeshBuilder.Spline(new[]
            {
                new Vector2(0.0975f, -0.098f), new Vector2(0.1005f, -0.092f), new Vector2(0.1015f, -0.055f),
                new Vector2(0.1005f, -0.018f), new Vector2(0.0975f, -0.012f)
            }, 4));

            // Cap.
            b.Revolve(2, 36, GoodsMeshBuilder.Spline(new[]
            {
                new Vector2(0f, 0.152f), new Vector2(0.040f, 0.152f), new Vector2(0.054f, 0.145f),
                new Vector2(0.058f, 0.130f), new Vector2(0.058f, 0.112f), new Vector2(0.053f, 0.106f),
                new Vector2(0f, 0.106f)
            }, 5));
            b.Fit(0.30f * Scale, 0.30f * Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Milk_Glass", C(0.97f, 0.98f, 1.00f), 0.85f),
                    M("Good_Milk_Label", C(0.27f, 0.55f, 0.95f), 0.6f),
                    M("Good_Milk_Cap", C(0.17f, 0.38f, 0.82f), 0.6f)
                }
            };
        }

        private static Model Bread()
        {
            var b = new GoodsMeshBuilder();
            var half = new Vector3(0.175f, 0.150f, 0.105f);
            Vector3 Shape(Vector3 q)
            {
                // Flat-ish underside, full domed top: reads as a baked loaf, not a pill.
                if (q.y < 0f) q.y *= 0.55f;
                float domeK = 1f - 0.10f * Mathf.Clamp01(q.y / half.y);
                q.z *= domeK;
                return q;
            }
            var pts = b.Super(0, Vector3.zero, half, 0.62f, 0.62f, 72, 36, alongX: true, modify: Shape);

            float Top(float x, float z)
            {
                float best = float.MinValue, bd = float.MaxValue;
                foreach (var p in pts)
                {
                    if (p.y < 0f) continue;
                    float d = (p.x - x) * (p.x - x) + (p.z - z) * (p.z - z);
                    if (d < bd) { bd = d; best = p.y; }
                }
                return best;
            }

            // Three pale scoring slashes across the crown, following the surface.
            for (int i = -1; i <= 1; i++)
            {
                float x = i * 0.085f;
                float y = Top(x, 0f);
                float dx = (Top(x + 0.012f, 0f) - Top(x - 0.012f, 0f)) / 0.024f;
                float dz = (Top(x, 0.012f) - Top(x, -0.012f)) / 0.024f;
                var n = new Vector3(-dx, 1f, -dz).normalized;
                var tilt = Quaternion.FromToRotation(Vector3.up, n);
                int from = b.VertexCount;
                b.Ellipsoid(1, Vector3.zero, new Vector3(0.050f, 0.0105f, 0.0135f), 14, 8, yaw: 52f);
                b.Transform(from, Matrix4x4.TRS(new Vector3(x, y - 0.003f, 0f), tilt, Vector3.one));
            }
            b.Fit(0.28f * Scale, 0.34f * Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Bread_Crust", C(0.88f, 0.54f, 0.19f), 0.4f),
                    M("Good_Bread_Slash", C(1.00f, 0.89f, 0.63f), 0.3f)
                }
            };
        }

        private static Model Yogurt()
        {
            var b = new GoodsMeshBuilder();
            float CupR(float y) => Mathf.Lerp(0.067f, 0.092f, Mathf.InverseLerp(-0.12f, 0.09f, y));

            b.Revolve(0, 44, GoodsMeshBuilder.Spline(new[]
            {
                new Vector2(0f, -0.14f), new Vector2(0.048f, -0.14f), new Vector2(0.061f, -0.132f),
                new Vector2(0.067f, -0.118f), new Vector2(CupR(0.0f), 0f), new Vector2(0.092f, 0.090f),
                new Vector2(0.098f, 0.104f), new Vector2(0.100f, 0.112f), new Vector2(0f, 0.112f)
            }, 5));

            // Fruity pink label band.
            b.Revolve(1, 44, GoodsMeshBuilder.Spline(new[]
            {
                new Vector2(CupR(-0.088f) + 0.0004f, -0.088f), new Vector2(CupR(-0.082f) + 0.0032f, -0.082f),
                new Vector2(CupR(-0.03f) + 0.0036f, -0.03f), new Vector2(CupR(0.02f) + 0.0036f, 0.02f),
                new Vector2(CupR(0.050f) + 0.0032f, 0.050f), new Vector2(CupR(0.056f) + 0.0004f, 0.056f)
            }, 4));

            // Foil lid with a raised centre and a peel tab.
            b.Revolve(2, 44, GoodsMeshBuilder.Spline(new[]
            {
                new Vector2(0f, 0.140f), new Vector2(0.050f, 0.140f), new Vector2(0.068f, 0.134f),
                new Vector2(0.092f, 0.131f), new Vector2(0.108f, 0.124f), new Vector2(0.110f, 0.112f),
                new Vector2(0.104f, 0.102f), new Vector2(0f, 0.102f)
            }, 5));
            b.Super(2, new Vector3(0.118f, 0.126f, 0f), new Vector3(0.026f, 0.0045f, 0.017f), 0.5f, 0.5f, 16, 10,
                yaw: 0f, roll: 8f);
            b.Fit(0.28f * Scale, 0.30f * Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Yogurt_Cup", C(0.97f, 0.97f, 0.99f), 0.7f),
                    M("Good_Yogurt_Band", C(0.96f, 0.38f, 0.58f), 0.6f),
                    M("Good_Yogurt_Lid", C(0.88f, 0.90f, 0.95f), 0.6f)
                }
            };
        }

        private static Model Corn()
        {
            var b = new GoodsMeshBuilder();
            const int Cols = 12, Rows = 11;
            const float y0 = -0.10f, len = 0.27f, R = 0.068f;

            Vector2 Prof(float t)
            {
                float envelope = Mathf.Pow(Mathf.Max(0f, 4f * t * (1f - t)), 0.30f);
                return new Vector2(R * envelope * (1f - 0.30f * t * t), y0 + t * len);
            }
            Vector3 Kernels(Vector3 pos, float u, float v)
            {
                if (v < 0.07f || v > 0.96f) return pos;
                float rowF = v * Rows;
                int row = Mathf.FloorToInt(rowF);
                float fy = rowF - row - 0.5f;
                float p = u * Cols + ((row & 1) == 1 ? 0.5f : 0f);
                float fx = p - Mathf.Floor(p) - 0.5f;
                float bump = Mathf.Sqrt(Mathf.Max(0f, 1f - (fx * fx + fy * fy) / 0.2f));
                float fade = Mathf.Clamp01((0.97f - v) / 0.10f);
                float k = 1f + 0.085f * bump * fade;
                return new Vector3(pos.x * k, pos.y, pos.z * k);
            }
            b.Revolve(0, 72, Dense(66, Prof), Kernels);

            // Husk: three lance-shaped leaves cupping the base and flaring open at the tips.
            for (int k = 0; k < 3; k++)
            {
                float phi = k * Mathf.PI * 2f / 3f + 0.4f;
                float lift = k * 0.0035f;
                b.Surface(1, 10, 16, (i, j) =>
                {
                    float w = i / 10f * 2f - 1f;
                    float s = j / 16f;
                    float y = -0.13f + 0.185f * s;
                    float rd = Mathf.Lerp(0.034f, 0.074f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(s / 0.45f)))
                               + 0.050f * Mathf.Pow(s, 2.4f) + lift + 0.004f * w * w * s;
                    float half = 1.0f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.8f))), 0.85f);
                    float a = phi + w * half;
                    return new Vector3(rd * Mathf.Cos(a), y, rd * Mathf.Sin(a));
                }, closed: false, backFace: 0.0035f);
            }

            // Stem stub.
            b.Revolve(2, 20, GoodsMeshBuilder.Spline(new[]
            {
                new Vector2(0f, -0.165f), new Vector2(0.026f, -0.165f), new Vector2(0.020f, -0.14f),
                new Vector2(0.016f, -0.115f), new Vector2(0f, -0.115f)
            }, 4));
            b.Fit(0.30f * Scale, 0.34f * Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Corn_Kernel", C(1.00f, 0.80f, 0.14f), 0.55f),
                    M("Good_Corn_Husk", C(0.40f, 0.69f, 0.22f), 0.3f),
                    M("Good_Corn_Stem", C(0.56f, 0.40f, 0.18f), 0.2f)
                }
            };
        }

        private static Model Hay()
        {
            var b = new GoodsMeshBuilder();
            const float A = 0.165f, Bz = 0.125f, Cy = 0.140f, E1 = 0.40f, E2 = 0.50f;

            Vector3 Straw(Vector3 q)
            {
                // Tufted straw: shallow ridges, running along each face's long direction.
                var n = new Vector3(
                    Mathf.Sign(q.x) * Mathf.Pow(Mathf.Abs(q.x / A), 3f),
                    Mathf.Sign(q.y) * Mathf.Pow(Mathf.Abs(q.y / Cy), 3f),
                    Mathf.Sign(q.z) * Mathf.Pow(Mathf.Abs(q.z / Bz), 3f));
                n = n.sqrMagnitude > 1e-6f ? n.normalized : Vector3.up;
                float d = 0.0035f * (n.y * n.y * Mathf.Sin(q.z * 110f + q.x * 8f)
                                   + n.z * n.z * Mathf.Sin(q.y * 110f + q.x * 8f)
                                   + n.x * n.x * Mathf.Sin(q.y * 120f + q.z * 30f));
                return q + n * d;
            }
            b.Super(0, Vector3.zero, new Vector3(A, Cy, Bz), E1, E2, 72, 40, alongX: true, modify: Straw);

            // Two twine straps, wrapped round the cross-section.
            foreach (float xc in new[] { -0.072f, 0.072f })
            {
                const float w = 0.014f;
                b.Surface(1, 64, 10, (i, j) =>
                {
                    float t = j / 10f;
                    float x = xc - w + 2f * w * t;
                    float g = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(x) / A, 2f / E1)), E1 * 0.5f);
                    float off = 1f + 0.075f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.4f);
                    float a = i / 64f * Mathf.PI * 2f;
                    return new Vector3(x,
                        Cy * g * off * GoodsMeshBuilder.SE(Mathf.Sin(a), E2),
                        Bz * g * off * GoodsMeshBuilder.SE(Mathf.Cos(a), E2));
                }, closed: false);
            }

            // A few loose blades sticking out.
            var rnd = new System.Random(11);
            for (int i = 0; i < 9; i++)
            {
                float x = (float)(rnd.NextDouble() * 2 - 1) * 0.14f;
                float z = (rnd.NextDouble() < 0.5 ? -1f : 1f) * (0.07f + (float)rnd.NextDouble() * 0.04f);
                float top = Cy * 0.86f * Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(Mathf.Abs(x) / A, 2f / E1)), E1 * 0.5f);
                b.Ellipsoid(2, new Vector3(x, top + 0.018f, z), new Vector3(0.0032f, 0.030f, 0.0032f), 6, 6,
                    yaw: (float)rnd.NextDouble() * 360f, pitch: 18f + (float)rnd.NextDouble() * 30f);
            }
            b.Fit(0.30f * Scale, 0.34f * Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Hay_Straw", C(0.93f, 0.77f, 0.33f), 0.15f),
                    M("Good_Hay_Twine", C(0.42f, 0.27f, 0.12f), 0.2f),
                    M("Good_Hay_Blade", C(0.99f, 0.89f, 0.52f), 0.2f)
                }
            };
        }

        private static Model Apple()
        {
            var b = new GoodsMeshBuilder();
            const float R = 0.130f, Hh = 0.150f;
            float TopY = Hh - 0.075f;

            Vector2 Prof(float t)
            {
                float u = t * Mathf.PI;
                float r = R * Mathf.Sin(u) * (1f + 0.10f * Mathf.Cos(u));
                float y = Hh * Mathf.Cos(u)
                          - 0.075f * Mathf.Exp(-Mathf.Pow(u / 0.36f, 2f))
                          + 0.020f * Mathf.Exp(-Mathf.Pow((Mathf.PI - u) / 0.45f, 2f));
                return new Vector2(r, y);
            }
            b.Revolve(0, 44, Dense(36, Prof));

            // Bent stem sprouting from the dimple.
            float s0 = TopY - 0.012f;
            b.Revolve(1, 14, GoodsMeshBuilder.Spline(new[]
            {
                new Vector2(0f, s0), new Vector2(0.0085f, s0), new Vector2(0.0072f, s0 + 0.03f),
                new Vector2(0.0075f, s0 + 0.062f), new Vector2(0.0f, s0 + 0.066f)
            }, 4), (q, u, v) =>
            {
                float h = (q.y - s0) / 0.066f;
                return new Vector3(q.x + 0.022f * h * h, q.y, q.z);
            });

            // One leaf, curling up and away from the stem.
            var origin = new Vector3(0.012f, s0 + 0.038f, 0f);
            b.Surface(2, 8, 14, (i, j) =>
            {
                float w = i / 8f * 2f - 1f;
                float s = j / 14f;
                float half = 0.024f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * Mathf.Pow(s, 0.75f))), 0.9f);
                var p = origin + new Vector3(0.095f * s, 0.030f * Mathf.Sin(s * Mathf.PI * 0.8f) - 0.012f * s * s, 0f);
                p.z += w * half;
                p.y += 0.012f * w * w * (1f - s * 0.5f);
                return p;
            }, closed: false, flip: true, backFace: 0.0025f);
            b.Fit(0.27f * Scale, 0.32f * Scale);
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Good_Apple_Skin", C(0.86f, 0.12f, 0.10f), 0.8f),
                    M("Good_Apple_Stem", C(0.36f, 0.22f, 0.10f), 0.2f),
                    M("Good_Apple_Leaf", C(0.32f, 0.68f, 0.20f), 0.4f)
                }
            };
        }

        // ------------------------------------------------------------------ assets

        /// <summary>Builds every model and (re)writes its mesh asset and prefab. Safe to rerun.</summary>
        [MenuItem("Tycoon/Build Goods Models")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Meshes")) AssetDatabase.CreateFolder("Assets/_Project", "Meshes");
            if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/_Project/Meshes", "Goods");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs")) AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "Goods");

            foreach (string id in Ids)
            {
                var model = Build(id);
                var fresh = model.Mesh.ToMesh("Good_" + id);

                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(id));
                Mesh mesh;
                if (existing != null)
                {
                    EditorUtility.CopySerialized(fresh, existing);
                    UnityEngine.Object.DestroyImmediate(fresh);
                    mesh = existing;
                    EditorUtility.SetDirty(mesh);
                }
                else
                {
                    AssetDatabase.CreateAsset(fresh, MeshPath(id));
                    mesh = fresh;
                }

                var go = new GameObject("Good_" + id);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = model.Materials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath(id));
                UnityEngine.Object.DestroyImmediate(go);

                var size = mesh.bounds.size;
                Debug.Log($"[Goods] {id}: {mesh.vertexCount} verts, size {size.x:0.000} x {size.y:0.000} x {size.z:0.000} (stackHeight {Heights[id]:0.00})");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("GOODS_OK");
        }

        public static GameObject Instance(string id, Transform parent, Vector3 localPos)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(id)));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            return go;
        }
    }

    /// <summary>
    /// Renders the goods in an empty scene to ../goods-sheet-3d.png: singles on top, 3-high stacks
    /// below (stacked exactly the way CarryStack does it: heights add, pivots at cell centres), and a
    /// small zoomed-out inset for judging them at game scale. Needs a GPU (no -nographics).
    /// Run: Tycoon > Export Goods 3D Preview, or
    /// <c>-executeMethod Tycoon.EditorTools.GoodsPreview.Export</c>.
    /// </summary>
    public static class GoodsPreview
    {
        private const string OutputPath = "../goods-sheet-3d.png";

        [MenuItem("Tycoon/Export Goods 3D Preview")]
        public static void Export()
        {
            GoodsModels.BuildAll();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GoodsPreview").transform;

            var grass = LevelBuildKit.Mat("Cash_PreviewGrass", new Color(0.42f, 0.66f, 0.30f), 0.05f);
            LevelBuildKit.Box("Ground", root, new Vector3(0f, -0.05f, 0f), new Vector3(300f, 0.1f, 300f), grass);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Vector3 right = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
            Vector3 fwd = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            const float spacing = 0.98f;
            Vector3 singlesOrigin = fwd * 0f;
            Vector3 stacksOrigin = fwd * -4f;

            for (int i = 0; i < GoodsModels.Ids.Length; i++)
            {
                string id = GoodsModels.Ids[i];
                float h = GoodsModels.Heights[id];
                float x = (i - 3) * spacing;
                GoodsModels.Instance(id, root, singlesOrigin + right * x + Vector3.up * (h * 0.5f));
                for (int k = 0; k < 3; k++)
                    GoodsModels.Instance(id, root, stacksOrigin + right * x + Vector3.up * (h * 0.5f + k * h));
            }

            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            Render(root, rt, new Rect(0f, 0f, 1f, 1f), new Vector3(0f, 0f, 40f), 0.5f, false);
            Render(root, rt, new Rect(0f, 0.5f, 1f, 0.5f), singlesOrigin + Vector3.up * 0.25f, 1.05f);
            Render(root, rt, new Rect(0f, 0f, 0.78f, 0.5f), stacksOrigin + Vector3.up * 0.55f, 1.3f);
            // Game-scale inset: both rows small.
            Render(root, rt, new Rect(0.80f, 0.04f, 0.19f, 0.30f), (singlesOrigin + stacksOrigin) * 0.5f, 3.2f, false);

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[GoodsPreview] -> {path}");
            Debug.Log("GOODSPREVIEW_OK");
            AssetDatabase.SaveAssets();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Render(Transform root, RenderTexture rt, Rect viewport, Vector3 focus,
            float ortho, bool clear = true)
        {
            var go = new GameObject("Cam");
            go.transform.SetParent(root, false);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ortho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.66f, 0.30f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 80f;
            var rot = Quaternion.Euler(50f, 45f, 0f);
            go.transform.rotation = rot;
            go.transform.position = focus - rot * Vector3.forward * 24f;
            if (clear) go.tag = "MainCamera";
            cam.targetTexture = rt;
            cam.rect = viewport;
            cam.Render();
            cam.targetTexture = null;
        }
    }
}
