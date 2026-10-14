using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Renders the meat grinder to ../grinder-sheet-3d.png: alone, next to the cutting table, and at roughly
    /// phone scale. Game camera angle (pitch 50, yaw 45 on an unturned level). Needs a GPU (no -nographics).
    /// Run: <c>-executeMethod Tycoon.EditorTools.GrinderPreview.Export</c>.
    /// </summary>
    public static class GrinderPreview
    {
        private const int W = 1500, H = 1000;

        [MenuItem("Tycoon/Export Grinder 3D Preview")]
        public static void Export()
        {
            ButcherModels.BuildAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GrinderPreview").transform;
            var grass = LevelBuildKit.Mat("Cash_PreviewGrass", new Color(0.42f, 0.66f, 0.30f), 0.05f);
            LevelBuildKit.Box("Ground", root, new Vector3(0f, -0.05f, 0f), new Vector3(2000f, 0.1f, 2000f), grass);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Vector3 right = Quaternion.Euler(0f, 45f, 0f) * Vector3.right;
            Vector3 fwd = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;

            var a = right * 0f;
            ButcherModels.Grinder(root, a, 0f);
            var b = right * 60f;
            ButcherModels.Grinder(root, b - right * 1.0f, 0f);
            ButcherModels.CuttingTable(root, b + right * 1.2f, 0f);
            var c = fwd * 200f;
            ButcherModels.Grinder(root, c - right * 1.4f, 0f);
            ButcherModels.CuttingTable(root, c + right * 1.0f, 0f);
            ButcherModels.Pig(root, c + right * 3.0f, 205f);
            ButcherModels.Good("sausage", root, c + right * 4.2f + Vector3.up * 0.15f);

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            Cell(root, rt, new RectInt(0, 300, 700, 700), a + Vector3.up * 0.75f, 1.55f);
            Cell(root, rt, new RectInt(700, 300, 800, 700), b + Vector3.up * 0.75f, 2.2f);
            Cell(root, rt, new RectInt(0, 0, W, 300), c + Vector3.up * 0.9f, 300f / (2f * 56f));

            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.GetFullPath("../grinder-sheet-3d.png"), tex.EncodeToPNG());
            Debug.Log("GRINDERPREVIEW_OK");
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
            cam.targetTexture = rt;
            cam.rect = new Rect(px.x / (float)rt.width, px.y / (float)rt.height, px.width / (float)rt.width, px.height / (float)rt.height);
            cam.Render();
            cam.targetTexture = null;
            Object.DestroyImmediate(go);
        }
    }
}
