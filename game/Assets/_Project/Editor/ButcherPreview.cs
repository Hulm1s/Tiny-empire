using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Renders the butcher art in an empty scene to ../butcher-sheet-3d.png, each asset alone in its
    /// own cell with the game camera angle (pitch 50, yaw 45, orthographic), then a strip at roughly
    /// phone scale on grass. Needs a GPU (no -nographics).
    /// Run: Tycoon > Export Butcher 3D Preview, or
    /// <c>-executeMethod Tycoon.EditorTools.ButcherPreview.Export</c>.
    /// </summary>
    public static class ButcherPreview
    {
        private const string OutputPath = "../butcher-sheet-3d.png";
        private const int W = 1920, H = 1510;

        private static Vector3 _right, _fwd;

        [MenuItem("Tycoon/Export Butcher 3D Preview")]
        public static void Export()
        {
            ButcherModels.BuildAll();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("ButcherPreview").transform;

            var grass = LevelBuildKit.Mat("Cash_PreviewGrass", new Color(0.42f, 0.66f, 0.30f), 0.05f);
            LevelBuildKit.Box("Ground", root, new Vector3(0f, -0.05f, 0f), new Vector3(2000f, 0.1f, 2000f), grass);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            _right = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
            _fwd = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;

            // Each big cell's asset sits in its own slot far from the others along the screen's x axis.
            Vector3 Slot(int i) => _right * (i * 60f);
            int slot = 0;

            // --- band 1: pig, pen, van
            var pigAt = Slot(slot++);
            ButcherModels.Pig(root, pigAt, 205f);

            var penAt = Slot(slot++);
            ButcherModels.PigPen(root, penAt, 3);

            var vanAt = Slot(slot++);
            ButcherModels.Van(root, vanAt, 0f);
            var vanFrontAt = Slot(slot++);
            ButcherModels.Van(root, vanFrontAt, 205f);

            // --- band 2: table, counter, three goods
            var tableAt = Slot(slot++);
            ButcherModels.CuttingTable(root, tableAt, 0f);

            var counterAt = Slot(slot++);
            var counter = ButcherModels.DisplayCounter(root, counterAt, 0f);
            var tray = counter.transform.Find("Tray");
            
            for (int i = 0; i < 6; i++)
            {
                float x = -1.30f + (i % 3) * 0.50f;
                float z = i < 3 ? -0.14f : 0.24f;
                float h = ButcherModels.MeasuredHeight("steak");
                ButcherModels.Good("steak", tray, new Vector3(x, h * 0.5f, z), 8f * (i - 2));
            }
            for (int i = 0; i < 6; i++)
            {
                float x = 0.30f + (i % 3) * 0.48f;
                float z = i < 3 ? -0.12f : 0.25f;
                float h = ButcherModels.MeasuredHeight("sausage");
                ButcherModels.Good("sausage", tray, new Vector3(x, h * 0.5f, z), 90f + 6f * i);
            }

            var goodAt = new Vector3[3];
            for (int g = 0; g < 3; g++)
            {
                goodAt[g] = Slot(slot++);
                string id = ButcherModels.GoodIds[g];
                float h = ButcherModels.MeasuredHeight(id);
                ButcherModels.Good(id, root, goodAt[g] + _right * -0.40f + Vector3.up * (h * 0.5f));
                for (int k = 0; k < 3; k++)
                    ButcherModels.Good(id, root, goodAt[g] + _right * 0.42f + Vector3.up * (h * 0.5f + k * h));
            }

            // --- band 3: everything at game scale, in a row on the grass
            var stripAt = new Vector3(0f, 0f, 0f) + _fwd * 400f;
            float sx = -15.0f;
            float Next(float w) { float v = sx + w * 0.5f; sx += w; return v; }
            ButcherModels.PigPen(root, stripAt + _right * (Next(8.4f) - 1.2f), 3);
            ButcherModels.Van(root, stripAt + _right * Next(4.0f), 0f);
            ButcherModels.CuttingTable(root, stripAt + _right * Next(3.0f), 0f);
            var c2 = ButcherModels.DisplayCounter(root, stripAt + _right * Next(4.4f), 0f);
            var tray2 = c2.transform.Find("Tray");
            for (int i = 0; i < 3; i++)
            {
                ButcherModels.Good("steak", tray2, new Vector3(-1.0f + i * 0.5f, ButcherModels.MeasuredHeight("steak") * 0.5f, 0f), 6f * i);
                ButcherModels.Good("sausage", tray2, new Vector3(0.4f + i * 0.5f, ButcherModels.MeasuredHeight("sausage") * 0.5f, 0f), 90f);
            }
            ButcherModels.Pig(root, stripAt + _right * Next(1.8f), 205f);
            ButcherModels.Pig(root, stripAt + _right * Next(1.8f) + _fwd * 0.3f, 160f);
            foreach (string id in ButcherModels.GoodIds)
                ButcherModels.Good(id, root, stripAt + _right * Next(0.9f) + Vector3.up * (ButcherModels.MeasuredHeight(id) * 0.5f));

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            // Band 1 (top 700 px): y 810..1510
            Cell(root, rt, new RectInt(0, 810, 480, 700), pigAt + Vector3.up * 0.45f, 1.45f);
            Cell(root, rt, new RectInt(480, 810, 760, 700), penAt + new Vector3(0f, 0.7f, 0f) + _right * 1.2f, 4.2f);
            Cell(root, rt, new RectInt(1240, 810, 680, 700), vanAt + Vector3.up * 0.65f, 1.95f);
            // Van front inset.
            Cell(root, rt, new RectInt(1690, 815, 220, 230), vanFrontAt + Vector3.up * 0.65f, 1.5f);
            // Band 2 (480 px): y 330..810
            Cell(root, rt, new RectInt(0, 330, 440, 480), tableAt + Vector3.up * 0.6f, 1.45f);
            Cell(root, rt, new RectInt(440, 330, 720, 480), counterAt + Vector3.up * 0.75f, 2.05f);
            for (int g = 0; g < 3; g++)
                Cell(root, rt, new RectInt(1160 + g * 253, 330, 253, 480), goodAt[g] + Vector3.up * 0.4f, 1.5f);
            // Band 3 (330 px): y 0..330 at ~56 px per metre.
            Cell(root, rt, new RectInt(0, 0, W, 330), stripAt + Vector3.up * 1.0f, 330f / (2f * 56f));

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Debug.Log($"[ButcherPreview] -> {path}");
            Debug.Log("BUTCHERPREVIEW_OK");
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
