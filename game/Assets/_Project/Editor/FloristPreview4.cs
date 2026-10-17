using System.IO;
using Tycoon.Config;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Pictures of the florist's HUD stock panel (<see cref="StockPanel"/>) at the two phone shapes: 390 x 844
    /// (portrait) and 844 x 390 (landscape), with grey stand-ins for the things it must not collide with (task
    /// card, money plaque, pause button, the joystick's reach). The canvas is laid out with the same numbers the
    /// HUD uses (HudRoot: reference 1080 x 1920 matched 0.35 in portrait; height 900 on a short wide screen), so
    /// the panel is the real class at the real size. Needs a GPU. Run:
    /// <c>-executeMethod Tycoon.EditorTools.FloristPreview4.Export</c>.
    /// </summary>
    public static class FloristPreview4
    {
        private const string OutDir = "C:/Users/ADMINI~1/AppData/Local/Temp/claude/D--iosGame/b50262dc-8611-42da-8650-85247e0c766c/scratchpad/florist4-preview/";

        public static void Export()
        {
            Directory.CreateDirectory(OutDir);
            Shoot("portrait-390x844", 390, 844, safeBottomPx: 34f, safeTopPx: 47f, wide: false, all: false);
            Shoot("portrait-390x844-all-four", 390, 844, safeBottomPx: 34f, safeTopPx: 47f, wide: false, all: true);
            Shoot("landscape-844x390", 844, 390, safeBottomPx: 21f, safeTopPx: 0f, wide: true, all: false);
            Shoot("landscape-844x390-all-four", 844, 390, safeBottomPx: 21f, safeTopPx: 0f, wide: true, all: true);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static ItemBuffer Stock(Transform parent, string id, int count)
        {
            var go = new GameObject("Stock_" + id);
            go.transform.SetParent(parent, false);
            var buffer = go.AddComponent<ItemBuffer>();
            buffer.item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Configs/Item_{id}.asset");
            buffer.capacity = 12;
            buffer.Add(count);
            return buffer;
        }

        private static RectTransform Block(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color colour, string label)
        {
            var img = UIFactory.CreatePanel(name, parent, colour);
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            var t = UIFactory.CreateText("Label", r, label, 34);
            t.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one;
            t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            return r;
        }

        private static void Shoot(string name, int w, int h, float safeBottomPx, float safeTopPx, bool wide, bool all)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // The scale HudRoot's CanvasScaler settles on.
            float scale = wide
                ? h / (h < 600 ? 900f : 1080f)
                : Mathf.Exp(Mathf.Lerp(Mathf.Log(w / 1080f), Mathf.Log(h / 1920f), 0.35f));
            float unitsW = w / scale, unitsH = h / scale;

            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var crt = (RectTransform)canvasGo.transform;
            crt.sizeDelta = new Vector2(unitsW, unitsH);
            crt.localScale = Vector3.one * 0.01f;

            var backdrop = UIFactory.CreatePanel("Backdrop", crt, new Color(0.42f, 0.66f, 0.30f));
            backdrop.rectTransform.anchorMin = Vector2.zero; backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;

            // The safe area, inset for the notch and the home bar.
            var safe = UIFactory.CreateRect("SafeArea", crt);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            safe.offsetMin = new Vector2(0f, safeBottomPx / scale);
            safe.offsetMax = new Vector2(0f, -safeTopPx / scale);

            // Stand-ins (grey, labelled) for what the panel must stay clear of.
            var grey = new Color(0.78f, 0.80f, 0.84f, 0.75f);
            bool topRow = wide;   // the HUD puts the task card on the top row when the screen is wide enough
            Block(safe, "TaskCard", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, topRow ? -28f : -196f), new Vector2(600f, 250f), grey, "TASKS");
            Block(safe, "MoneyPlaque", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(420f, 150f), grey, "MONEY");
            Block(safe, "Pause", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(110f, 110f), grey, "II");
            Block(safe, "Joystick", new Vector2(0.28f, 0.22f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340f, 340f), new Color(1f, 1f, 1f, 0.22f), "STICK");

            var stocks = new GameObject("Stocks").transform;
            var buffers = all
                ? new[] { Stock(stocks, "rose", 7), Stock(stocks, "tulip", 0), Stock(stocks, "sunflower", 12), Stock(stocks, "hydrangea", 3) }
                : new[] { Stock(stocks, "rose", 7), Stock(stocks, "tulip", 0) };

            var panel = StockPanel.Create(safe);
            panel.Preview(buffers);
            Canvas.ForceUpdateCanvases();

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = unitsH * 0.01f * 0.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            var rt = new RenderTexture(w * 2, h * 2, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(OutDir + name + ".png", tex.EncodeToPNG());
            Debug.Log($"[FloristPreview4] {name}: canvas {unitsW:0} x {unitsH:0} units, scale {scale:0.000} -> {OutDir}{name}.png");
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
        }
    }
}
