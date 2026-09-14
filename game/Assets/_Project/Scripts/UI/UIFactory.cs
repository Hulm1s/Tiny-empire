using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// Small helpers for building UI from code.
    ///
    /// The HUD is generated at runtime rather than authored as a prefab so that every level -
    /// including ones added months from now - gets a correct, safe-area-aware HUD without any
    /// scene wiring at all.
    /// </summary>
    public static class UIFactory
    {
        private static Sprite _circle;
        private static Sprite _roundedBox;
        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    // Built-in legacy font. Used instead of TextMeshPro because TMP needs its
                    // "Essential Resources" imported into the project, which cannot be done
                    // from a headless build.
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (_circle == null) _circle = BuildCircle(128);
                return _circle;
            }
        }

        public static Sprite RoundedBox
        {
            get
            {
                if (_roundedBox == null) _roundedBox = BuildRoundedBox(64, 18);
                return _roundedBox;
            }
        }

        private static Sprite BuildCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UICircle" };
            float r = size * 0.5f;
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r + 0.5f;
                    float dy = y - r + 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // One-pixel feather so the edge is not jagged on a high-DPI phone screen.
                    float a = Mathf.Clamp01(r - d);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildRoundedBox(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UIRoundedBox" };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0f);
                    float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
        }

        public static RectTransform CreateRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image CreatePanel(string name, RectTransform parent, Color color)
        {
            var rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = RoundedBox;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// A tappable pill with a label. Returns the Button so the caller can wire onClick and
        /// the Text so it can relabel it (a confirm step, a toggle state).
        /// </summary>
        public static Button CreateButton(string name, RectTransform parent, string content,
            Color color, int fontSize, out Text label)
        {
            var rect = CreateRect(name, parent);

            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = RoundedBox;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = true;

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            label = CreateText("Label", rect, content, fontSize);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.fontStyle = FontStyle.Bold;

            return button;
        }

        public static Text CreateText(string name, RectTransform parent, string content, int fontSize,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rect = CreateRect(name, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Font;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
