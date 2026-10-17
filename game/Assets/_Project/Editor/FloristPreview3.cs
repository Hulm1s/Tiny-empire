using System.IO;
using System.Linq;
using Tycoon.Core;
using Tycoon.Stations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Florist round 3 pictures for Filip, rendered from the BUILT scene (nothing is saved back):
    /// the meadow with its squares laid over the ground (top-down and at the game camera). Needs a GPU (no -nographics).
    /// Run: <c>-executeMethod Tycoon.EditorTools.FloristPreview3.ExportMeadow</c> .
    /// </summary>
    public static class FloristPreview3
    {
        private const string OutDir = "C:/Users/ADMINI~1/AppData/Local/Temp/claude/D--iosGame/b50262dc-8611-42da-8650-85247e0c766c/scratchpad/florist3-preview/";
        private const string ScenePath = "Assets/_Project/Scenes/Farm.unity";

        private static Font _font;

        private static Transform OpenTown()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var town = Object.FindObjectsByType<Location>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(l => l.locationId == "town").transform;
            town.gameObject.SetActive(true);
            var florist = town.Find("Florist");
            foreach (var t in florist.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Roses": case "Tulips": case "Sunflowers": case "Hydrangeas":
                    case "Well": case "SeedBox": case "Working": case "Painted": case "Shop": case "Hiregardener":
                        t.gameObject.SetActive(true);
                        break;
                    case "Grubby": case "Broken":
                        t.gameObject.SetActive(false);
                        break;
                }
            }
            return town;
        }

        private static GameObject Label(string text, Quaternion rot, Vector3 pos, float height, Color col)
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label");
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font; tm.text = text; tm.fontSize = 80; tm.characterSize = height / 8f;
            tm.anchor = TextAnchor.MiddleCenter; tm.color = col;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            go.transform.rotation = rot; go.transform.position = pos;
            return go;
        }

        private static void Shoot(Vector3 focus, Quaternion rot, float ortho, int w, int h, string file)
        {
            var go = new GameObject("PreviewCam");
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = ortho;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.42f, 0.66f, 0.30f);
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 120f;
            go.transform.rotation = rot;
            go.transform.position = focus - rot * Vector3.forward * 50f;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes(OutDir + file, tex.EncodeToPNG());
            Debug.Log("[FloristPreview3] -> " + OutDir + file);
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }

        // ------------------------------------------------------------------ the meadow

        public static void ExportMeadow()
        {
            var town = OpenTown();
            var mat = new Material(Shader.Find("Sprites/Default"));
            var overlays = new System.Collections.Generic.List<GameObject>();

            foreach (var st in town.GetComponentsInChildren<StationBase>(true))
            {
                Vector3 local = town.InverseTransformPoint(st.transform.position);
                if (local.z < 8.4f || local.z > 17.3f) continue;
                var box = st.GetComponent<BoxCollider>();
                Color c; string text;
                if (st is BedStation) { c = new Color(0.55f, 0.85f, 0.45f, 0.55f); text = "BED"; }
                else if (st.name == "FetchWater") { c = new Color(0.30f, 0.62f, 1f, 0.65f); text = "WATER"; }
                else if (st.name == "FetchSeeds") { c = new Color(1f, 0.62f, 0.20f, 0.65f); text = "SEEDS"; }
                else if (st.name == "Hiregardener") { c = new Color(0.30f, 0.85f, 0.35f, 0.65f); text = "HIRE GARDENER"; }
                else if (st.name.StartsWith("Unlock")) { c = new Color(1f, 0.85f, 0.35f, 0.6f); text = st.label.ToUpperInvariant(); }
                else continue;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.GetComponent<MeshRenderer>().sharedMaterial = new Material(mat) { color = c };
                quad.transform.SetParent(town, false);
                quad.transform.localPosition = new Vector3(local.x, 0.12f, local.z);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = new Vector3(box.size.x, box.size.z, 1f);
                overlays.Add(quad);
                overlays.Add(Label(text, town.rotation * Quaternion.Euler(90f, 0f, 0f), town.TransformPoint(new Vector3(local.x, 0.3f, local.z)), 0.34f, Color.black));
            }

            Vector3 focus = town.TransformPoint(new Vector3(-7f, 0f, 12.7f));
            Shoot(focus, Quaternion.Euler(90f, 45f, 0f), 5.3f, 1500, 1060, "meadow-topdown.png");
            // At the game camera the squares are foreshortened; labels face the camera.
            foreach (var o in overlays) if (o.name == "Label") o.transform.rotation = Quaternion.Euler(50f, 45f, 0f);
            Shoot(focus + Vector3.up * 0.4f, Quaternion.Euler(50f, 45f, 0f), 8.2f, 1600, 1200, "meadow-game-camera.png");

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
