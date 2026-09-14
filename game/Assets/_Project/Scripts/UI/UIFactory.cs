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

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _frames =
            new System.Collections.Generic.Dictionary<string, Sprite>();

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

        /// <summary>
        /// A dashed outline with rounded corners, sized to one particular card.
        ///
        /// Built whole rather than from four stretched edges, because the dashes have to run
        /// round the corners to look like a bay marked out on the ground - four straight strips
        /// meet in square corners and read as a box someone drew badly. There are only a handful
        /// of card sizes in a level, so each one's frame is drawn once and kept.
        /// </summary>
        public static Sprite DashedFrame(int width, int height)
        {
            string key = width + "x" + height;
            if (_frames.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = BuildDashedFrame(width, height);
            _frames[key] = sprite;
            return sprite;
        }

        private static Sprite BuildDashedFrame(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "UIDashedFrame" };
            var pixels = new Color32[width * height];

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float radius = Mathf.Min(halfW, halfH) * 0.34f;
            float stroke = Mathf.Max(4f, Mathf.Min(width, height) * 0.035f);

            // Half-extents of the straight runs, i.e. the box the corner arcs are swept around.
            float a = halfW - radius;
            float b = halfH - radius;

            // One quarter of the way round: up the right side, round one corner, in along the top.
            float quarter = b + radius * Mathf.PI * 0.5f + a;
            float perimeter = quarter * 4f;

            // Round the pattern to fit a whole number of times, so the dashes meet cleanly
            // instead of leaving a stub where the outline closes.
            float wanted = Mathf.Max(14f, Mathf.Min(width, height) * 0.13f);
            int count = Mathf.Max(8, Mathf.RoundToInt(perimeter / wanted));
            float pattern = perimeter / count;
            float dash = pattern * 0.58f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f - halfW;
                    float py = y + 0.5f - halfH;
                    float ax = Mathf.Abs(px);
                    float ay = Mathf.Abs(py);

                    // Distance to the rounded-rectangle outline: negative inside, zero on it.
                    float qx = ax - a;
                    float qy = ay - b;
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                               Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                    float onStroke = Mathf.Clamp01(stroke * 0.5f - Mathf.Abs(distance) + 0.5f);
                    if (onStroke <= 0f) continue;

                    // How far round the outline this pixel sits. Worked out in one quadrant and
                    // mirrored, which keeps the dashes symmetrical on all four sides.
                    float inQuadrant;
                    if (qx > 0f && qy > 0f) inQuadrant = b + radius * Mathf.Atan2(qy, qx);
                    else if (ay <= b) inQuadrant = ay;
                    else inQuadrant = b + radius * Mathf.PI * 0.5f + (a - ax);

                    float along;
                    if (px >= 0f && py >= 0f) along = inQuadrant;
                    else if (px < 0f && py >= 0f) along = 2f * quarter - inQuadrant;
                    else if (px < 0f) along = 2f * quarter + inQuadrant;
                    else along = 4f * quarter - inQuadrant;

                    float phase = Mathf.Repeat(along, pattern);
                    // Feather both ends of every dash rather than cutting them off square.
                    float onDash = Mathf.Clamp01(Mathf.Min(phase, dash - phase) + 0.5f);
                    if (onDash <= 0f) continue;

                    byte alpha = (byte)(Mathf.Clamp01(onStroke * onDash) * 255f);
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
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
