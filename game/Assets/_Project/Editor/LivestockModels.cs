using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The chicken and the cow, drawn in the same rounded, cute style as the approved pig
    /// (<see cref="ButcherModels"/>) and goods: <see cref="GoodsMeshBuilder"/> superellipsoids and
    /// revolves, one submesh per material, every material an ASSET (Lv_*) - a runtime Material
    /// renders magenta under URP. Meshes go to Meshes/Livestock, prefabs to Prefabs/Livestock.
    ///
    /// Both face their local +z like the pig, with the pivot on the ground under the body centre.
    /// Sized against the pig (which is ~1.2 m long at scale 0.88): the cow a touch bigger, the
    /// chicken ~0.5 m, so a pen of them reads right from the orthographic game camera.
    ///
    /// Used by LevelBuildKit.BuildPen for the coops and cow sheds (with LivestockIdle on each animal).
    /// </summary>
    public static class LivestockModels
    {
        public const string MeshFolder = "Assets/_Project/Meshes/Livestock";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Livestock";

        private struct Model
        {
            public GoodsMeshBuilder Mesh;
            public Material[] Materials;
        }

        private static Material M(string name, Color c, float smooth)
        {
            var m = LevelBuildKit.Mat(name, c, smooth);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b);
        private static Vector2 V(float r, float y) => new Vector2(r, y);

        /// <summary>
        /// A flattened oval lying on a body's surface in direction <paramref name="dir"/> from its
        /// centre (a cow's patch, a feather), tilted to the surface and half sunk into it.
        /// <paramref name="pts"/> is what <see cref="GoodsMeshBuilder.Super"/> returned for the body.
        /// </summary>
        private static void Patch(GoodsMeshBuilder b, List<Vector3> pts, Vector3 centre, Vector3 half, int sub,
            Vector3 dir, float radiusX, float radiusZ, float yaw, float sink = 0.012f)
        {
            dir.Normalize();
            Vector3 best = pts[0];
            float bd = float.MinValue;
            foreach (var p in pts)
            {
                float d = Vector3.Dot((p - centre).normalized, dir);
                if (d > bd) { bd = d; best = p; }
            }
            // Surface normal of an ellipsoid-ish body at that point.
            var o = best - centre;
            var n = new Vector3(o.x / (half.x * half.x), o.y / (half.y * half.y), o.z / (half.z * half.z)).normalized;
            var tilt = Quaternion.FromToRotation(Vector3.up, n) * Quaternion.Euler(0f, yaw, 0f);
            int from = b.VertexCount;
            b.Ellipsoid(sub, Vector3.zero, new Vector3(radiusX, 0.03f, radiusZ), 14, 8);
            b.Transform(from, Matrix4x4.TRS(best - n * sink, tilt, Vector3.one));
        }

        // ================================================================== CHICKEN

        private static Model ChickenModel()
        {
            var b = new GoodsMeshBuilder();
            const int Fth = 0, Comb = 1, Beak = 2, Eye = 3, Leg = 4, Wing = 5;

            // Plump egg-shaped body, breast forward and up, sitting just off the ground.
            var bodyC = new Vector3(0f, 0.34f, -0.02f);
            var bodyH = new Vector3(0.235f, 0.235f, 0.285f);
            b.Super(Fth, bodyC, bodyH, 0.9f, 0.92f, 28, 18, pitch: -8f);

            // Round head on a short neck, with a comb, beak, wattle and eyes.
            b.Ellipsoid(Fth, new Vector3(0f, 0.60f, 0.20f), new Vector3(0.135f, 0.14f, 0.135f), 22, 14);
            for (int k = 0; k < 3; k++)
            {
                float h = k == 1 ? 0.085f : 0.065f;
                b.Ellipsoid(Comb, new Vector3(0f, 0.735f + (k == 1 ? 0.012f : 0f), 0.145f + k * 0.055f),
                    new Vector3(0.036f, h, 0.042f), 12, 8);
            }
            // Beak: a short cone out of the face.
            int from = b.VertexCount;
            b.Revolve(Beak, 14, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.050f, 0f), V(0.040f, 0.04f), V(0.016f, 0.085f), V(0f, 0.10f)
            }, 3));
            b.Transform(from, Matrix4x4.TRS(new Vector3(0f, 0.585f, 0.30f), Quaternion.Euler(90f, 0f, 0f), Vector3.one));
            b.Ellipsoid(Comb, new Vector3(0f, 0.505f, 0.295f), new Vector3(0.028f, 0.05f, 0.028f), 8, 6);
            b.Ellipsoid(Eye, new Vector3(-0.098f, 0.625f, 0.265f), new Vector3(0.020f, 0.028f, 0.018f), 8, 6);
            b.Ellipsoid(Eye, new Vector3(0.098f, 0.625f, 0.265f), new Vector3(0.020f, 0.028f, 0.018f), 8, 6);

            // Wings: flattened ovals hugging the sides, tips swept back.
            b.Ellipsoid(Wing, new Vector3(-0.205f, 0.350f, -0.04f), new Vector3(0.042f, 0.125f, 0.175f), 16, 10,
                pitch: 10f, roll: 6f);
            b.Ellipsoid(Wing, new Vector3(0.205f, 0.350f, -0.04f), new Vector3(0.042f, 0.125f, 0.175f), 16, 10,
                pitch: 10f, roll: -6f);

            // Tail: three upturned feathers fanning out at the back.
            for (int k = -1; k <= 1; k++)
            {
                b.Ellipsoid(Fth, new Vector3(0.06f * k, 0.455f - Mathf.Abs(k) * 0.02f, -0.34f - (k == 0 ? 0.03f : 0f)),
                    new Vector3(0.032f, 0.105f, 0.050f), 12, 8, roll: -16f * k, pitch: -48f);
            }

            // Two short orange legs and flat feet (three toes each as one oval + two small ones).
            foreach (float lx in new[] { -0.10f, 0.10f })
            {
                b.Revolve(Leg, 8, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, 0.02f), V(0.024f, 0.02f), V(0.020f, 0.10f), V(0.022f, 0.20f), V(0f, 0.22f)
                }, 2), (q, u, v) => q + new Vector3(lx, 0f, 0.03f));
                b.Ellipsoid(Leg, new Vector3(lx, 0.022f, 0.075f), new Vector3(0.050f, 0.022f, 0.085f), 10, 6);
                b.Ellipsoid(Leg, new Vector3(lx - 0.035f * Mathf.Sign(lx), 0.020f, 0.075f), new Vector3(0.020f, 0.018f, 0.060f), 8, 4,
                    yaw: -20f * Mathf.Sign(lx));
            }

            b.Transform(0, Matrix4x4.Scale(Vector3.one * 0.78f));
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Lv_Feather", C(1.00f, 0.97f, 0.90f), 0.45f),
                    M("Lv_Comb", C(0.92f, 0.28f, 0.30f), 0.5f),
                    M("Lv_Beak", C(1.00f, 0.72f, 0.20f), 0.4f),
                    M("Lv_Eye", C(0.18f, 0.10f, 0.10f), 0.3f),
                    M("Lv_Leg", C(0.98f, 0.62f, 0.16f), 0.35f),
                    M("Lv_Wing", C(0.96f, 0.86f, 0.70f), 0.4f)
                }
            };
        }

        // ================================================================== COW

        private static Model CowModel()
        {
            var b = new GoodsMeshBuilder();
            const int Hide = 0, Spot = 1, Muzzle = 2, Dark = 3, Horn = 4, Hoof = 5, Ear = 6;

            // Barrel body, a little pear-shaped: deeper at the shoulder than the rump.
            var bodyC = new Vector3(0f, 0.74f, -0.06f);
            var bodyH = new Vector3(0.36f, 0.34f, 0.58f);
            var bodyPts = b.Super(Hide, bodyC, bodyH, 0.85f, 0.90f, 40, 24);

            // Big friendly head, carried forward and low, with a wide pink muzzle.
            var headC = new Vector3(0f, 0.86f, 0.62f);
            b.Super(Hide, headC, new Vector3(0.24f, 0.23f, 0.25f), 0.85f, 0.88f, 28, 18);
            b.Super(Muzzle, new Vector3(0f, 0.78f, 0.84f), new Vector3(0.175f, 0.125f, 0.12f), 0.8f, 0.85f, 24, 14);
            b.Ellipsoid(Dark, new Vector3(-0.065f, 0.80f, 0.945f), new Vector3(0.022f, 0.030f, 0.014f), 8, 6);
            b.Ellipsoid(Dark, new Vector3(0.065f, 0.80f, 0.945f), new Vector3(0.022f, 0.030f, 0.014f), 8, 6);
            b.Ellipsoid(Dark, new Vector3(-0.168f, 0.93f, 0.765f), new Vector3(0.034f, 0.044f, 0.028f), 10, 8);
            b.Ellipsoid(Dark, new Vector3(0.168f, 0.93f, 0.765f), new Vector3(0.034f, 0.044f, 0.028f), 10, 8);

            // Ears out to the side, pink inside, and two stubby horns.
            b.Ellipsoid(Hide, new Vector3(-0.265f, 0.99f, 0.56f), new Vector3(0.115f, 0.040f, 0.065f), 14, 8,
                yaw: 15f, roll: 22f);
            b.Ellipsoid(Hide, new Vector3(0.265f, 0.99f, 0.56f), new Vector3(0.115f, 0.040f, 0.065f), 14, 8,
                yaw: -15f, roll: -22f);
            b.Ellipsoid(Ear, new Vector3(-0.265f, 0.985f, 0.575f), new Vector3(0.085f, 0.034f, 0.045f), 12, 6,
                yaw: 15f, roll: 22f);
            b.Ellipsoid(Ear, new Vector3(0.265f, 0.985f, 0.575f), new Vector3(0.085f, 0.034f, 0.045f), 12, 6,
                yaw: -15f, roll: -22f);
            b.Ellipsoid(Horn, new Vector3(-0.115f, 1.10f, 0.56f), new Vector3(0.036f, 0.075f, 0.036f), 10, 8, roll: 22f);
            b.Ellipsoid(Horn, new Vector3(0.115f, 1.10f, 0.56f), new Vector3(0.036f, 0.075f, 0.036f), 10, 8, roll: -22f);

            // Black patches: a big one over the back, a smaller one on the flank, one round the eye.
            Patch(b, bodyPts, bodyC, bodyH, Spot, new Vector3(0.1f, 1f, 0.35f), 0.19f, 0.25f, 15f);
            Patch(b, bodyPts, bodyC, bodyH, Spot, new Vector3(1f, 0.1f, -0.5f), 0.14f, 0.19f, -20f);
            Patch(b, bodyPts, bodyC, bodyH, Spot, new Vector3(-1f, 0.2f, 0.25f), 0.17f, 0.21f, 30f);
            Patch(b, bodyPts, bodyC, bodyH, Spot, new Vector3(-0.2f, 0.9f, -0.85f), 0.12f, 0.15f, 0f);

            // Pink udder tucked under the rump.
            b.Ellipsoid(Muzzle, new Vector3(0f, 0.45f, -0.26f), new Vector3(0.150f, 0.115f, 0.160f), 18, 12);

            // Four sturdy legs ending in dark hooves.
            foreach (float lx in new[] { -0.22f, 0.22f })
                foreach (float lz in new[] { 0.34f, -0.42f })
                {
                    b.Revolve(Hide, 14, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0.08f), V(0.092f, 0.08f), V(0.098f, 0.16f), V(0.088f, 0.50f), V(0f, 0.52f)
                    }, 3), (q, u, v) => q + new Vector3(lx, 0f, lz));
                    b.Revolve(Hoof, 14, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0f), V(0.090f, 0f), V(0.104f, 0.025f), V(0.100f, 0.085f), V(0f, 0.085f)
                    }, 2), (q, u, v) => q + new Vector3(lx, 0f, lz));
                }

            // Tail: a thin rope hanging off the rump ending in a dark tuft.
            for (int k = 0; k < 6; k++)
            {
                float t = k / 5f;
                float r = 0.026f;
                b.Ellipsoid(Hide, new Vector3(0.04f * Mathf.Sin(t * 3f), 0.98f - 0.34f * t, -0.64f - 0.07f * Mathf.Sin(t * 2.4f)),
                    new Vector3(r, 0.075f, r), 8, 6);
            }
            b.Ellipsoid(Dark, new Vector3(0.04f * Mathf.Sin(3f), 0.58f, -0.70f), new Vector3(0.055f, 0.09f, 0.055f), 10, 8);

            b.Transform(0, Matrix4x4.Scale(Vector3.one * 0.74f));
            return new Model
            {
                Mesh = b,
                Materials = new[]
                {
                    M("Lv_CowHide", C(1.00f, 0.97f, 0.93f), 0.45f),
                    M("Lv_CowSpot", C(0.30f, 0.24f, 0.24f), 0.4f),
                    M("Lv_CowMuzzle", C(1.00f, 0.72f, 0.74f), 0.5f),
                    M("Lv_CowDark", C(0.18f, 0.10f, 0.12f), 0.3f),
                    M("Lv_CowHorn", C(1.00f, 0.92f, 0.74f), 0.4f),
                    M("Lv_CowHoof", C(0.40f, 0.30f, 0.30f), 0.25f),
                    M("Lv_CowEar", C(1.00f, 0.66f, 0.70f), 0.4f)
                }
            };
        }

        // ------------------------------------------------------------------ assets

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static Mesh SaveMesh(string key, GoodsMeshBuilder b)
        {
            EnsureFolder(MeshFolder);
            string path = $"{MeshFolder}/{key}.asset";
            var fresh = b.ToMesh(key);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
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
            var mesh = SaveMesh(key, model.Mesh);
            var result = (mesh, model.Materials);
            _built[key] = result;
            var s = mesh.bounds.size;
            Debug.Log($"[Livestock] {key}: {mesh.vertexCount} verts, size {s.x:0.00} x {s.y:0.00} x {s.z:0.00}");
            return result;
        }

        private static GameObject Obj(string name, Transform parent, Vector3 pos, float yaw, (Mesh mesh, Material[] mats) m)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = m.mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = m.mats;
            return go;
        }

        public static GameObject Chicken(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("Chicken", parent, pos, yaw, Get("Chicken", ChickenModel));

        public static GameObject Cow(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("Cow", parent, pos, yaw, Get("Cow", CowModel));

        /// <summary>Builds the livestock mesh/material/prefab assets. Safe to rerun.</summary>
        [MenuItem("Tycoon/Build Livestock Models")]
        public static void BuildAll()
        {
            EnsureFolder(MeshFolder);
            EnsureFolder(PrefabFolder);
            _built.Clear();
            var holder = new GameObject("LivestockBuild").transform;
            foreach (var go in new[] { Chicken(holder, Vector3.zero), Cow(holder, Vector3.zero) })
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{go.name}.prefab");
            UnityEngine.Object.DestroyImmediate(holder.gameObject);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("LIVESTOCKMODELS_OK");
        }
    }
}
