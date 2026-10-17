using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Fl = Tycoon.EditorTools.FloristModels.Fl;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Renders the florist art in an empty scene to ../florist-sheet-3d.png, each asset alone in its
    /// own cell with the game camera angle (pitch 50, yaw 45, orthographic): the six goods (single and
    /// stacked), the twelve flower beds (growing / blooming / dry), the well, the workers, the shop
    /// pieces and finally the whole florist laid out from Filip's sketch. (The bouquet minigame is 2D
    /// now and has its own export, <see cref="BouquetMock"/>.) Needs a GPU (no -nographics).
    /// Run: Tycoon > Export Florist 3D Preview, or
    /// <c>-executeMethod Tycoon.EditorTools.FloristPreview.Export</c>.
    /// </summary>
    public static class FloristPreview
    {
        private const string OutputPath = "../florist-sheet-3d.png";
        private const int W = 2400, H = 3360;

        private static Vector3 _right, _fwd;
        private static readonly Color GrassCol = new Color(0.42f, 0.66f, 0.30f);

        [MenuItem("Tycoon/Export Florist 3D Preview")]
        public static void Export()
        {
            FloristModels.BuildAll();
            var root = NewStage(out _right, out _fwd);

            Vector3 Slot(int i) => _right * (i * 60f);
            int slot = 0;

            // ---------------------------------------------------------------- goods (top band)
            var goodAt = new Vector3[FloristModels.GoodIds.Length];
            for (int g = 0; g < goodAt.Length; g++)
            {
                goodAt[g] = Slot(slot++);
                string id = FloristModels.GoodIds[g];
                float h = Height(id);
                FloristModels.Good(id, root, goodAt[g] + _right * -0.42f + Vector3.up * (h * 0.5f), 20f);
                for (int k = 0; k < 2; k++)
                    FloristModels.Good(id, root, goodAt[g] + _right * 0.45f + Vector3.up * (h * 0.5f + k * h), 25f * k);
            }

            // ---------------------------------------------------------------- beds
            var bedAt = new Vector3[4, 3];
            for (int f = 0; f < 4; f++)
                for (int s = 0; s < 3; s++)
                {
                    bedAt[f, s] = Slot(slot++);
                    FloristModels.Bed((Fl)f, s, root, bedAt[f, s]);
                }

            // ---------------------------------------------------------------- well, workers, game-scale goods
            var wellAt = Slot(slot++);
            FloristModels.Well(root, wellAt);

            var workersAt = Slot(slot++);
            var florist = Worker(root, workersAt + _right * -0.9f, 200f, CharacterLibrary.Cashier, "Florist",
                new Dictionary<int, Color>
                {
                    { CharacterLibrary.ShirtSlot, new Color(1.00f, 0.62f, 0.74f) },
                    { CharacterLibrary.TrouserSlot, new Color(0.97f, 0.92f, 0.82f) },
                    { CharacterLibrary.ShoeSlot, new Color(0.55f, 0.36f, 0.30f) },
                    { 4, new Color(1.00f, 0.80f, 0.86f) },
                    { 5, new Color(1.00f, 0.80f, 0.86f) }
                }, "bouquet");
            var gardener = Worker(root, workersAt + _right * 0.9f, 160f, CharacterLibrary.Farmer, "Gardener",
                new Dictionary<int, Color>
                {
                    { CharacterLibrary.ShirtSlot, new Color(0.46f, 0.78f, 0.42f) },
                    { CharacterLibrary.TrouserSlot, new Color(0.30f, 0.46f, 0.74f) },
                    { CharacterLibrary.ShoeSlot, new Color(0.40f, 0.28f, 0.18f) }
                }, "watering_can");

            var scaleAt = Slot(slot++);
            float sx = -3.9f;
            foreach (string id in FloristModels.GoodIds)
            {
                FloristModels.Good(id, root, scaleAt + _right * sx + Vector3.up * (Height(id) * 0.5f), 20f);
                sx += 0.95f;
            }
            FloristModels.Bed(Fl.Rose, 1, root, scaleAt + _right * 3.2f + _fwd * 0.0f);
            FloristModels.Well(root, scaleAt + _right * 6.0f);

            // ---------------------------------------------------------------- shop pieces
            var counterAt = Slot(slot++);
            FloristModels.Counter(root, counterAt);
            var wallAt = Slot(slot++);
            FloristModels.WallStand(root, wallAt);
            var cornerAt = Slot(slot++);
            FloristModels.CornerStand(root, cornerAt);
            var centreAt = Slot(slot++);
            FloristModels.CentreStand(root, centreAt);

            // ---------------------------------------------------------------- the whole florist
            var layoutAt = _fwd * 400f;
            BuildLayout(root, layoutAt);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            const int top = H;
            // Band 1: goods, y 2940..3360, six cells of 400.
            for (int g = 0; g < 6; g++)
                Cell(root, rt, new RectInt(g * 400, top - 420, 400, 420), goodAt[g] + Vector3.up * 0.42f, 0.85f);

            // Beds, four rows of 330 under the goods, three columns of 500.
            for (int f = 0; f < 4; f++)
                for (int s = 0; s < 3; s++)
                    Cell(root, rt, new RectInt(s * 500, top - 420 - 330 * (f + 1), 500, 330), bedAt[f, s] + Vector3.up * 0.30f, 1.40f);

            // Right column, x 1500..2400.
            Cell(root, rt, new RectInt(1500, top - 420 - 520, 900, 520), wellAt + Vector3.up * 1.0f, 1.95f);
            Cell(root, rt, new RectInt(1500, top - 420 - 520 - 420, 900, 420), workersAt + Vector3.up * 0.62f, 1.1f);
            Cell(root, rt, new RectInt(1500, top - 420 - 1320, 900, 380), scaleAt + _right * 1.2f + Vector3.up * 0.4f, 3.6f);

            // Shop pieces: 520 high.
            int sy = top - 420 - 1320 - 520;
            Cell(root, rt, new RectInt(0, sy, 800, 520), counterAt + Vector3.up * 0.7f, 1.7f);
            Cell(root, rt, new RectInt(800, sy, 560, 520), wallAt + Vector3.up * 0.7f, 1.95f);
            Cell(root, rt, new RectInt(1360, sy, 480, 520), cornerAt + Vector3.up * 0.7f, 1.75f);
            Cell(root, rt, new RectInt(1840, sy, 560, 520), centreAt + Vector3.up * 0.75f, 1.95f);

            // The whole layout at game scale (64 px per metre), 1100 high.
            Cell(root, rt, new RectInt(0, 0, W, 1100), layoutAt + new Vector3(0f, 0.6f, 0f) + _fwd * 3.3f, 1100f / (2f * 64f));

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[FloristPreview] -> {path}");

            AssetDatabase.SaveAssets();
            Debug.Log("FLORISTPREVIEW_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ stage

        internal static Transform NewStage(out Vector3 right, out Vector3 fwd)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("FloristPreview").transform;

            var grass = LevelBuildKit.Mat("Cash_PreviewGrass", GrassCol, 0.05f);
            LevelBuildKit.Box("Ground", root, new Vector3(0f, -0.05f, 0f), new Vector3(8000f, 0.1f, 8000f), grass);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            right = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
            fwd = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            return root;
        }

        private static float Height(string id)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GoodsModels.MeshPath(id));
            return mesh != null ? mesh.bounds.size.y : 0.45f;
        }

        // ------------------------------------------------------------------ workers

        private static Material TintAsset(Material src, string name, Color c)
        {
            string path = $"Assets/_Project/Materials/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = Object.Instantiate(src);
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// A villager role recoloured with material ASSETS (never runtime copies), holding a good in
        /// its hands at the shared hand anchor.
        /// </summary>
        internal static Transform Worker(Transform parent, Vector3 pos, float yaw, string role, string tag,
            Dictionary<int, Color> tints, string carry)
        {
            var t = CharacterLibrary.Spawn(role, parent, tag);
            if (t == null) { Debug.LogWarning($"[FloristPreview] no model for {role}"); return null; }
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);

            var r = t.GetComponentInChildren<SkinnedMeshRenderer>();
            if (r != null)
            {
                var mats = r.sharedMaterials;
                foreach (var kv in tints)
                    if (kv.Key < mats.Length) mats[kv.Key] = TintAsset(mats[kv.Key], $"Fl_{tag}_{kv.Key}", kv.Value);
                r.sharedMaterials = mats;
            }

            var anim = t.GetComponent<Animator>();
            if (anim != null)
            {
                anim.Rebind();
                anim.Update(0.4f);
            }

            if (!string.IsNullOrEmpty(carry))
            {
                var g = FloristModels.Good(carry, t, CharacterLibrary.HandAnchor);
                g.name = "Carried_" + carry;
            }
            return t;
        }

        // ------------------------------------------------------------------ the florist, from the sketch

        private static Material Mat(string n, Color c, float s = 0.2f) => LevelBuildKit.Mat("FlPrev_" + n, c, s);

        private static float YawFacing(Vector3 front) => Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg;

        /// <summary>
        /// Shop (south, z -3.5..3.5) with its entrance bottom-left and exit bottom-right, the counter
        /// and serve spot, the central stand, wall stands with angled corner pieces; the fenced
        /// meadow (north) with eight beds and the well. North up, x east, as in the sketch.
        /// </summary>
        private static void BuildLayout(Transform root, Vector3 at)
        {
            var g = new GameObject("FloristLayout").transform;
            g.SetParent(root, false);
            g.position = at;

            var floor = Mat("Floor", new Color(0.90f, 0.76f, 0.56f), 0.3f);
            var meadow = Mat("Meadow", new Color(0.50f, 0.76f, 0.34f), 0.05f);
            var wall = Mat("Wall", new Color(1.00f, 0.95f, 0.88f), 0.4f);
            var cap = Mat("WallCap", new Color(1.00f, 0.74f, 0.82f), 0.4f);
            var mat = Mat("DoorMat", new Color(0.46f, 0.78f, 0.52f), 0.2f);
            var matOut = Mat("DoorMatOut", new Color(0.98f, 0.66f, 0.30f), 0.2f);
            var serve = Mat("Serve", new Color(0.30f, 0.82f, 0.40f), 0.2f);
            var post = Mat("Post", new Color(0.72f, 0.58f, 0.40f), 0.2f);
            var rail = Mat("Rail", new Color(0.86f, 0.74f, 0.56f), 0.2f);
            var path = Mat("Path", new Color(0.88f, 0.84f, 0.76f), 0.3f);

            LevelBuildKit.Box("ShopFloor", g, new Vector3(0f, 0.03f, 0f), new Vector3(12.2f, 0.06f, 7.2f), floor, castShadow: false);
            LevelBuildKit.Box("MeadowGround", g, new Vector3(0f, 0.02f, 7.2f), new Vector3(12.6f, 0.04f, 7.4f), meadow, castShadow: false);

            void Wall(string n, float x0, float x1, float z0, float z1)
            {
                var c = new Vector3((x0 + x1) * 0.5f, 0.35f, (z0 + z1) * 0.5f);
                var s = new Vector3(Mathf.Max(0.25f, x1 - x0), 0.7f, Mathf.Max(0.25f, z1 - z0));
                LevelBuildKit.Box(n, g, c, s, wall, castShadow: false);
                LevelBuildKit.Box(n + "Cap", g, c + Vector3.up * 0.38f, new Vector3(s.x + 0.04f, 0.07f, s.z + 0.04f), cap, castShadow: false);
            }
            // South wall: entrance bottom-left, exit bottom-right.
            Wall("SouthA", -6.1f, -3.9f, -3.6f, -3.4f);
            Wall("SouthB", -2.3f, 2.3f, -3.6f, -3.4f);
            Wall("SouthC", 3.9f, 6.1f, -3.6f, -3.4f);
            // Side walls and the back wall with the door to the meadow.
            Wall("West", -6.1f, -5.9f, -3.6f, 3.6f);
            Wall("East", 5.9f, 6.1f, -3.6f, 3.6f);
            Wall("NorthA", -6.1f, -0.9f, 3.4f, 3.6f);
            Wall("NorthB", 0.9f, 6.1f, 3.4f, 3.6f);
            LevelBuildKit.Box("DoorMatIn", g, new Vector3(-3.1f, 0.07f, -3.0f), new Vector3(1.4f, 0.03f, 1.0f), mat, castShadow: false);
            LevelBuildKit.Box("DoorMatOut", g, new Vector3(3.1f, 0.07f, -3.0f), new Vector3(1.4f, 0.03f, 1.0f), matOut, castShadow: false);
            LevelBuildKit.Box("BackDoorMat", g, new Vector3(0f, 0.07f, 3.0f), new Vector3(1.5f, 0.03f, 0.8f), matOut, castShadow: false);

            // Counter with the serve spot behind it.
            FloristModels.Counter(g, new Vector3(0f, 0.06f, 1.2f), 0f);
            LevelBuildKit.Box("ServeSpot", g, new Vector3(0f, 0.08f, 2.3f), new Vector3(1.2f, 0.03f, 1.2f), serve, castShadow: false);

            // The square stand in the middle, wall runs with angled corner pieces.
            FloristModels.CentreStand(g, new Vector3(0f, 0.06f, -1.1f), 0f);
            FloristModels.WallStand(g, new Vector3(-5.225f, 0.06f, 0f), -90f);
            FloristModels.WallStand(g, new Vector3(5.225f, 0.06f, 0f), 90f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var front = new Vector3(-sx * 0.7071f, 0f, -sz * 0.7071f);
                    var pos = new Vector3(sx * 6f, 0.06f, sz * 3.5f) + front * 1.4f;
                    FloristModels.CornerStand(g, pos, YawFacing(front));
                }

            // Meadow: fence, path, well, eight beds (roses + tulips west, sunflowers + hydrangeas east).
            const float fz0 = 3.6f, fz1 = 10.8f, fx = 6.2f;
            for (float z = fz0; z <= fz1 + 0.01f; z += 1.2f)
                foreach (float sx in new[] { -1f, 1f })
                    LevelBuildKit.Box("FencePost", g, new Vector3(sx * fx, 0.35f, z), new Vector3(0.14f, 0.7f, 0.14f), post, castShadow: false);
            for (float x = -fx; x <= fx + 0.01f; x += 1.2f)
                LevelBuildKit.Box("FencePostN", g, new Vector3(x, 0.35f, fz1), new Vector3(0.14f, 0.7f, 0.14f), post, castShadow: false);
            foreach (float sx in new[] { -1f, 1f })
                LevelBuildKit.Box("FenceRailSide", g, new Vector3(sx * fx, 0.45f, (fz0 + fz1) * 0.5f), new Vector3(0.07f, 0.08f, fz1 - fz0), rail, castShadow: false);
            LevelBuildKit.Box("FenceRailN", g, new Vector3(0f, 0.45f, fz1), new Vector3(fx * 2f, 0.08f, 0.07f), rail, castShadow: false);

            for (int i = 0; i < 4; i++)
                LevelBuildKit.Cylinder("Stone", g, new Vector3(0f, 0.045f, 4.1f + i * 0.62f), new Vector3(0.7f, 0.02f, 0.5f), path, false);
            FloristModels.Well(g, new Vector3(0f, 0.04f, 7.3f));

            int[,] state = { { 1, 1 }, { 0, 1 }, { 1, 2 }, { 1, 0 } };   // per flower: row A, row B
            float[] rowZ = { 5.4f, 8.9f };
            float[] colX = { 2.5f, 4.7f };
            for (int f = 0; f < 4; f++)
            {
                float side = f < 2 ? -1f : 1f;
                int row = f % 2;   // rose/sunflower on the south row, tulip/hydrangea north
                for (int c = 0; c < 2; c++)
                {
                    int st = c == 0 ? state[f, 0] : state[f, 1];
                    FloristModels.Bed((Fl)f, st, g, new Vector3(side * colX[c], 0.04f, rowZ[row]), 0f);
                }
            }

            // People: the florist behind the counter, the gardener by the well.
            Worker(g, new Vector3(0f, 0.06f, 2.3f), 180f, CharacterLibrary.Cashier, "Florist",
                new Dictionary<int, Color>
                {
                    { CharacterLibrary.ShirtSlot, new Color(1.00f, 0.62f, 0.74f) },
                    { CharacterLibrary.TrouserSlot, new Color(0.97f, 0.92f, 0.82f) },
                    { CharacterLibrary.ShoeSlot, new Color(0.55f, 0.36f, 0.30f) },
                    { 4, new Color(1.00f, 0.80f, 0.86f) },
                    { 5, new Color(1.00f, 0.80f, 0.86f) }
                }, null);
            Worker(g, new Vector3(1.5f, 0.04f, 6.2f), 200f, CharacterLibrary.Farmer, "Gardener",
                new Dictionary<int, Color>
                {
                    { CharacterLibrary.ShirtSlot, new Color(0.46f, 0.78f, 0.42f) },
                    { CharacterLibrary.TrouserSlot, new Color(0.30f, 0.46f, 0.74f) },
                    { CharacterLibrary.ShoeSlot, new Color(0.40f, 0.28f, 0.18f) }
                }, "watering_can");
        }

        // ------------------------------------------------------------------ rendering

        /// <summary>Renders the scene with the game camera into a rectangle of the target.</summary>
        internal static void Cell(Transform root, RenderTexture rt, RectInt px, Vector3 focus, float ortho)
        {
            var go = new GameObject("Cam");
            go.transform.SetParent(root, false);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ortho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = GrassCol;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 90f;
            var rot = Quaternion.Euler(50f, 45f, 0f);
            go.transform.rotation = rot;
            go.transform.position = focus - rot * Vector3.forward * 40f;
            go.tag = "MainCamera";
            cam.targetTexture = rt;
            cam.rect = new Rect(px.x / (float)rt.width, px.y / (float)rt.height, px.width / (float)rt.width, px.height / (float)rt.height);
            cam.Render();
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
        }
    }
}
