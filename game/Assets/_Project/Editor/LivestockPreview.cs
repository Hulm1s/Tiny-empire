using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Renders the new chicken and cow beside the approved pig to ../livestock-sheet-3d.png with the
    /// game camera angle (pitch 50, yaw 45, orthographic): the three animals large, a chicken pen and
    /// a cow pen at 5 animals each (the farm's two-row arrangement), and a strip at roughly phone
    /// scale. Needs a GPU (no -nographics).
    /// Run: Tycoon > Export Livestock 3D Preview, or
    /// <c>-executeMethod Tycoon.EditorTools.LivestockPreview.Export</c>.
    /// </summary>
    public static class LivestockPreview
    {
        private const string OutputPath = "../livestock-sheet-3d.png";
        private const int W = 1920, H = 1300;

        private static Vector3 _right, _fwd;

        private static Transform BuildPen(Transform root, Vector3 at, bool cows, int count)
        {
            var pen = new GameObject(cows ? "Pasture" : "Pen").transform;
            pen.SetParent(root, false);
            pen.localPosition = at;
            Vector2 size = cows ? new Vector2(2.8f, 4.2f) : new Vector2(2.4f, 3.6f);
            var dirt = LevelBuildKit.Mat(cows ? "Pen_Pasture" : "Pen_Dirt",
                cows ? new Color(0.46f, 0.62f, 0.32f) : new Color(0.62f, 0.53f, 0.38f));
            var post = LevelBuildKit.Mat("Pen_Post", new Color(0.72f, 0.58f, 0.4f));
            var rail = LevelBuildKit.Mat("Pen_Rail", new Color(0.85f, 0.73f, 0.55f));
            LevelBuildKit.Box("Ground", pen, new Vector3(0f, 0.02f, 0f), new Vector3(size.x, 0.04f, size.y), dirt, castShadow: false);
            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    LevelBuildKit.Box($"Post_{sx}_{sz}", pen, new Vector3(hx * sx, 0.35f, hz * sz), new Vector3(0.12f, 0.7f, 0.12f), post, castShadow: false);
            LevelBuildKit.Box("RailN", pen, new Vector3(0f, 0.45f, hz), new Vector3(size.x, 0.08f, 0.07f), rail, castShadow: false);
            LevelBuildKit.Box("RailS", pen, new Vector3(0f, 0.45f, -hz), new Vector3(size.x, 0.08f, 0.07f), rail, castShadow: false);
            LevelBuildKit.Box("RailE", pen, new Vector3(hx, 0.45f, 0f), new Vector3(0.07f, 0.08f, size.y), rail, castShadow: false);
            if (!cows)
            {
                var nest = LevelBuildKit.Mat("Pen_Nest", new Color(0.75f, 0.62f, 0.42f));
                var straw = LevelBuildKit.Mat("Pen_Straw", new Color(0.93f, 0.85f, 0.5f));
                for (int i = 0; i < 3; i++)
                {
                    float z = Mathf.Lerp(-hz * 0.6f, hz * 0.6f, i / 2f);
                    LevelBuildKit.Box($"Nest_{i}", pen, new Vector3(-hx + 0.3f, 0.17f, z), new Vector3(0.5f, 0.34f, 0.6f), nest, castShadow: false);
                    LevelBuildKit.Box($"Straw_{i}", pen, new Vector3(-hx + 0.3f, 0.36f, z), new Vector3(0.42f, 0.06f, 0.5f), straw, castShadow: false);
                }
            }
            // Same placement rule as LevelBuildKit.BuildPen.
            for (int i = 0; i < count; i++)
            {
                float z = count <= 1 ? 0f : Mathf.Lerp(-hz * 0.55f, hz * 0.55f, i / (float)(count - 1));
                float stagger = count > 3 ? (i % 2 == 0 ? -0.4f : 0.4f) : 0f;
                var p = new Vector3(0.5f + stagger, 0f, z);
                float yaw = (i * 53) % 360;
                if (cows) LivestockModels.Cow(pen, p, yaw); else LivestockModels.Chicken(pen, p, yaw);
            }
            return pen;
        }

        [MenuItem("Tycoon/Export Livestock 3D Preview")]
        public static void Export()
        {
            ButcherModels.BuildAll();
            LivestockModels.BuildAll();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("LivestockPreview").transform;

            var grass = LevelBuildKit.Mat("Cash_PreviewGrass", new Color(0.42f, 0.66f, 0.30f), 0.05f);
            LevelBuildKit.Box("Ground", root, new Vector3(0f, -0.05f, 0f), new Vector3(2000f, 0.1f, 2000f), grass);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            _right = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
            _fwd = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            Vector3 Slot(int i) => _right * (i * 60f);

            // Band 1: chicken, cow, pig, each facing the camera-ish (yaw 205 like the pig sheet).
            var chickAt = Slot(0); LivestockModels.Chicken(root, chickAt, 205f);
            var cowAt = Slot(1); LivestockModels.Cow(root, cowAt, 205f);
            var pigAt = Slot(2); ButcherModels.Pig(root, pigAt, 205f);
            // Side profiles too (yaw 90 faces screen-right-ish).
            var chick2At = Slot(3); LivestockModels.Chicken(root, chick2At, 100f);
            var cow2At = Slot(4); LivestockModels.Cow(root, cow2At, 100f);

            // Band 2: two pens at 5 animals.
            var chickPenAt = Slot(5);
            BuildPen(root, chickPenAt, false, 5);
            var cowPenAt = Slot(6);
            BuildPen(root, cowPenAt, true, 5);

            // Band 3: phone scale strip.
            var stripAt = _fwd * 400f;
            float sx = -12.0f;
            float Next(float w) { float v = sx + w * 0.5f; sx += w; return v; }
            BuildPen(root, stripAt + _right * Next(3.2f), false, 5);
            BuildPen(root, stripAt + _right * Next(3.6f), true, 5);
            LivestockModels.Chicken(root, stripAt + _right * Next(1.0f), 205f);
            LivestockModels.Cow(root, stripAt + _right * Next(1.8f), 205f);
            ButcherModels.Pig(root, stripAt + _right * Next(1.8f), 205f);
            LivestockModels.Chicken(root, stripAt + _right * Next(1.0f), 130f);
            LivestockModels.Cow(root, stripAt + _right * Next(1.8f), 130f);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            // Band 1 (y 800..1300): five animals, 384 px each.
            Cell(root, rt, new RectInt(0, 800, 384, 500), chickAt + Vector3.up * 0.35f, 0.85f);
            Cell(root, rt, new RectInt(384, 800, 384, 500), cowAt + Vector3.up * 0.55f, 1.15f);
            Cell(root, rt, new RectInt(768, 800, 384, 500), pigAt + Vector3.up * 0.40f, 1.0f);
            Cell(root, rt, new RectInt(1152, 800, 384, 500), chick2At + Vector3.up * 0.35f, 0.85f);
            Cell(root, rt, new RectInt(1536, 800, 384, 500), cow2At + Vector3.up * 0.55f, 1.15f);
            // Band 2 (y 330..800): pens.
            Cell(root, rt, new RectInt(0, 330, 960, 470), chickPenAt + new Vector3(0f, 0.2f, 0f), 2.5f);
            Cell(root, rt, new RectInt(960, 330, 960, 470), cowPenAt + new Vector3(0f, 0.2f, 0f), 2.9f);
            // Band 3 (330 px) at ~56 px per metre.
            Cell(root, rt, new RectInt(0, 0, W, 330), stripAt + Vector3.up * 0.5f, 330f / (2f * 56f));

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[LivestockPreview] -> {path}");
            Debug.Log("LIVESTOCKPREVIEW_OK");
            AssetDatabase.SaveAssets();
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Cell(Transform root, RenderTexture rt, RectInt px, Vector3 focus, float ortho)
        {
            var go = new GameObject("Cam");
            go.transform.SetParent(root, false);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ortho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.66f, 0.30f);
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
