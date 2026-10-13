using System.IO;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The 3D cash props, built from primitives and material ASSETS (never runtime Materials, which
    /// turn magenta under URP): a bundle of bills, a 2 x 2-per-layer stack of them, and the ATM.
    /// The bundle's look (green, gold paper band, "$") is the approved art, baked into two texture
    /// assets by <see cref="CashArt"/> and put on a flat box mesh.
    ///
    /// <see cref="CashPreview"/> renders them in an empty scene to ../cash-sheet-3d.png so they can
    /// be judged before anything is wired in. Run: Tycoon > Export Cash 3D Preview, or
    /// <c>-executeMethod Tycoon.EditorTools.CashPreview.Export</c> (needs a GPU: no -nographics).
    /// </summary>
    public static class CashModels
    {
        /// <summary>Bundle length (x), thickness (y) and width (z), in metres.</summary>
        public static readonly Vector3 BundleSize = new Vector3(0.50f, 0.16f, 0.32f);

        private const string ArtFolder = "Assets/_Project/Art/Cash";

        private static Texture2D SaveTexture(Texture2D tex, string name)
        {
            if (!AssetDatabase.IsValidFolder(ArtFolder))
                AssetDatabase.CreateFolder("Assets/_Project/Art", "Cash");
            string path = $"{ArtFolder}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material TexturedMat(string name, Texture2D tex)
        {
            var m = LevelBuildKit.Mat(name, Color.white, 0.15f);
            m.SetTexture("_BaseMap", tex);
            m.SetTexture("_MainTex", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material BillTop() =>
            TexturedMat("Cash_BillTop", SaveTexture(CashArt.BundleTopTexture(), "CashBundleTop"));

        public static Material BillSide() =>
            TexturedMat("Cash_BillSide", SaveTexture(CashArt.BundleSideTexture(), "CashBundleSide"));

        private static GameObject _bundlePrefab;

        /// <summary>Forgets the prefab made this build, so the next build writes it afresh.</summary>
        public static void Begin() => _bundlePrefab = null;

        /// <summary>
        /// The bundle as a prefab asset (Prefabs/CashBundle.prefab), made once per build. The stacks and
        /// piles instantiate it, so every bundle uses the material and texture ASSETS - a Material made at
        /// runtime would render magenta under URP.
        /// </summary>
        public static GameObject BundlePrefab()
        {
            if (_bundlePrefab != null) return _bundlePrefab;

            var temp = Bundle("CashBundle", null, Vector3.zero);
            const string path = "Assets/_Project/Prefabs/CashBundle.prefab";
            _bundlePrefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return _bundlePrefab;
        }

        public static Material Band() => LevelBuildKit.Mat("Cash_Band", new Color(0.96f, 0.80f, 0.36f), 0.3f);

        /// <summary>One bundle: a flat stack-of-bills box, the gold band wrapped round it, the art on top.</summary>
        public static GameObject Bundle(string name, Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var side = BillSide();
            var top = BillTop();
            var band = Band();
            var s = BundleSize;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            LevelBuildKit.Box("Stack", root.transform, new Vector3(0f, s.y * 0.5f, 0f), s, side, castShadow: false);
            // The band wraps the sides a hair proud of the stack, so it reads as paper round it.
            LevelBuildKit.Box("Band", root.transform, new Vector3(0f, s.y * 0.5f, 0f),
                new Vector3(s.x * 0.18f, s.y + 0.01f, s.z + 0.012f), band, castShadow: false);
            // The art: a quad lying on the top face.
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "TopArt";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, s.y + 0.0045f, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(s.x, s.z, 1f);
            var r = quad.GetComponent<Renderer>();
            r.sharedMaterial = top;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return root;
        }

        /// <summary>A stack of bundles: 2 x 2 per layer, <paramref name="layers"/> layers, no cap.</summary>
        public static GameObject Stack(string name, Transform parent, Vector3 localPos, int bundles)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            var s = BundleSize;
            float gap = 0.015f;
            for (int i = 0; i < bundles; i++)
            {
                int layer = i / 4, slot = i % 4;
                float x = (slot % 2 == 0 ? -1f : 1f) * (s.x * 0.5f + gap * 0.5f);
                float z = (slot / 2 == 0 ? -1f : 1f) * (s.z * 0.5f + gap * 0.5f);
                // A touch of hand-stacked wobble, deterministic per bundle.
                float yaw = ((i * 37) % 7 - 3) * 1.2f;
                Bundle("Bundle" + i, root.transform, new Vector3(x, layer * s.y, z), yaw);
            }
            return root;
        }

        /// <summary>The ATM, front toward -z, standing on the ground at the origin of its parent.</summary>
        public static GameObject Atm(string name, Transform parent, Vector3 localPos)
        {
            var body = LevelBuildKit.Mat("Atm_Body", new Color(0.30f, 0.50f, 0.78f), 0.35f);
            var darkBlue = LevelBuildKit.Mat("Atm_Plinth", new Color(0.16f, 0.26f, 0.42f), 0.2f);
            var gold = LevelBuildKit.Mat("Atm_Gold", new Color(0.98f, 0.76f, 0.22f), 0.45f);
            var dark = LevelBuildKit.Mat("Atm_Dark", new Color(0.08f, 0.07f, 0.07f), 0.2f);
            var glass = LevelBuildKit.Mat("Atm_Screen", new Color(0.36f, 0.95f, 0.55f), 0.7f);
            var glassDim = LevelBuildKit.Mat("Atm_ScreenDim", new Color(0.12f, 0.50f, 0.32f), 0.6f);
            var white = LevelBuildKit.Mat("Atm_Key", new Color(0.93f, 0.95f, 0.98f), 0.4f);
            var green = LevelBuildKit.Mat("Atm_KeyGreen", new Color(0.35f, 0.82f, 0.30f), 0.4f);
            var bill = LevelBuildKit.Mat("Cash_BillFlat", new Color(0.55f, 0.85f, 0.42f), 0.2f);

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            // Named "Shell" so the layout audit measures the machine's footprint like a building's.
            var shell = new GameObject("Shell");
            shell.transform.SetParent(root.transform, false);
            var t = shell.transform;

            LevelBuildKit.Box("Plinth", t, new Vector3(0f, 0.06f, 0f), new Vector3(1.35f, 0.12f, 1.05f), darkBlue, true);
            LevelBuildKit.Box("Body", t, new Vector3(0f, 0.97f, 0.02f), new Vector3(1.15f, 1.7f, 0.80f), body, true);
            // Gold header sign with a coin on it, and a dark band under it.
            // A slim header, not a roof: just proud of the body so it reads as a sign.
            LevelBuildKit.Box("Sign", t, new Vector3(0f, 1.92f, 0.02f), new Vector3(1.19f, 0.20f, 0.84f), gold);
            var coin = LevelBuildKit.Cylinder("Coin", t, new Vector3(0f, 1.92f, -0.425f), new Vector3(0.17f, 0.02f, 0.17f), dark);
            coin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var coinFace = LevelBuildKit.Cylinder("CoinFace", t, new Vector3(0f, 1.92f, -0.44f), new Vector3(0.13f, 0.02f, 0.13f), gold);
            coinFace.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            LevelBuildKit.Box("CoinSign", t, new Vector3(0f, 1.92f, -0.455f), new Vector3(0.03f, 0.09f, 0.01f), dark);
            LevelBuildKit.Box("SignL", t, new Vector3(-0.40f, 1.92f, -0.425f), new Vector3(0.18f, 0.11f, 0.02f), dark);
            LevelBuildKit.Box("SignR", t, new Vector3(0.40f, 1.92f, -0.425f), new Vector3(0.18f, 0.11f, 0.02f), dark);

            // Screen: dark bezel, bright glass, two bars and a big dollar-ish block.
            LevelBuildKit.Box("Bezel", t, new Vector3(0f, 1.50f, -0.395f), new Vector3(0.92f, 0.52f, 0.06f), dark);
            LevelBuildKit.Box("Glass", t, new Vector3(0f, 1.50f, -0.43f), new Vector3(0.82f, 0.42f, 0.02f), glassDim);
            LevelBuildKit.Box("GlassGlow", t, new Vector3(0f, 1.55f, -0.445f), new Vector3(0.74f, 0.28f, 0.01f), glass);
            LevelBuildKit.Box("Dollar", t, new Vector3(-0.22f, 1.50f, -0.455f), new Vector3(0.12f, 0.24f, 0.01f), glassDim);
            LevelBuildKit.Box("Bar1", t, new Vector3(0.14f, 1.57f, -0.455f), new Vector3(0.28f, 0.05f, 0.01f), glassDim);
            LevelBuildKit.Box("Bar2", t, new Vector3(0.14f, 1.46f, -0.455f), new Vector3(0.28f, 0.05f, 0.01f), glassDim);

            // Keypad ledge with 3 x 3 keys and a green enter key.
            LevelBuildKit.Box("Ledge", t, new Vector3(0f, 1.06f, -0.52f), new Vector3(1.0f, 0.06f, 0.36f), darkBlue);
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    LevelBuildKit.Box("Key", t, new Vector3(-0.30f + col * 0.14f, 1.115f, -0.43f - row * 0.11f),
                        new Vector3(0.10f, 0.04f, 0.08f), white, castShadow: false);
            LevelBuildKit.Box("Enter", t, new Vector3(0.32f, 1.115f, -0.54f), new Vector3(0.20f, 0.04f, 0.26f), green, castShadow: false);

            // Cash tray with a bill sticking out of it.
            LevelBuildKit.Box("Tray", t, new Vector3(0.10f, 0.62f, -0.405f), new Vector3(0.70f, 0.16f, 0.06f), dark);
            LevelBuildKit.Box("Bill", t, new Vector3(0.10f, 0.625f, -0.48f), new Vector3(0.46f, 0.02f, 0.14f), bill, castShadow: false);
            LevelBuildKit.Box("CardSlot", t, new Vector3(-0.32f, 0.62f, -0.405f), new Vector3(0.22f, 0.05f, 0.04f), dark);
            return root;
        }
    }

    public static class CashPreview
    {
        private const string OutputPath = "../cash-sheet-3d.png";

        [MenuItem("Tycoon/Export Cash 3D Preview")]
        public static void Export()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("CashPreview").transform;

            var grass = LevelBuildKit.Mat("Cash_PreviewGrass", new Color(0.42f, 0.66f, 0.30f), 0.05f);
            LevelBuildKit.Box("Ground", root, new Vector3(0f, -0.05f, 0f), new Vector3(300f, 0.1f, 300f), grass);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Along the camera's right axis, so the row reads left to right on screen.
            Vector3 right = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
            CashModels.Stack("Stack1", root, right * -5.6f, 4);
            CashModels.Stack("Stack3", root, right * -3.6f, 12);
            CashModels.Stack("Stack8", root, right * -1.2f, 32);
            // The ATM and a normal interaction square in front of it.
            Vector3 atmPos = right * 2.6f;
            CashModels.Atm("Atm", root, atmPos);
            var station = LevelBuildKit.Station<DepositStation>("preview.atm", "AtmSquare", root,
                atmPos + new Vector3(0f, 0f, -1.85f), new Vector2(2.2f, 1.8f), "ATM", new Color(0.35f, 0.8f, 1f));
            var square = station.GetComponent<InteractionSquare>();
            var build = typeof(InteractionSquare).GetMethod("Build",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            build?.Invoke(square, null);
            // Close-up of one bundle, far from the rest.
            CashModels.Bundle("Single", root, new Vector3(0f, 0f, 60f));

            var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            RenderCam(root, rt, new Rect(0f, 0f, 1f, 1f), right * 0.3f + new Vector3(0f, 1.0f, 0f), 5.0f);
            RenderCam(root, rt, new Rect(0.01f, 0.67f, 0.26f, 0.32f), new Vector3(0f, 0.1f, 60f), 0.42f);

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[CashPreview] -> {path}");
            Debug.Log("CASHPREVIEW_OK");
            AssetDatabase.SaveAssets();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void RenderCam(Transform root, RenderTexture rt, Rect viewport, Vector3 focus, float ortho)
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
            if (ortho > 1f) go.tag = "MainCamera";
            // Edit mode never runs the square's Update, so tick it by hand to colour and turn it.
            foreach (var sq in Object.FindObjectsByType<InteractionSquare>(FindObjectsSortMode.None))
            {
                foreach (var m in new[] { "Update", "LateUpdate" })
                    typeof(InteractionSquare).GetMethod(m, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(sq, null);
            }
            Canvas.ForceUpdateCanvases();
            cam.targetTexture = rt;
            cam.rect = viewport;
            cam.Render();
            cam.targetTexture = null;
        }
    }
}
