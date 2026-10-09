using Tycoon.Audio;
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

            // Every button in the game clicks. Added here so no menu has to remember to.
            button.onClick.AddListener(() => SoundFx.Play(Sfx.Click));

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

        // ---- the framed HUD (see HudArt) ---------------------------------------------------

        private static readonly Color LabelEdge = new Color(0.10f, 0.078f, 0.063f, 1f);

        private static Sprite PlateSprite(Vector2 size, float radius, HudArt.Look look, bool pressed) =>
            HudArt.Panel(size.x + 2f * HudArt.Margin, size.y + 2f * HudArt.Margin + 6f, radius, look, pressed);

        /// <summary>
        /// A framed plate. The returned rect is the VISIBLE plate, <paramref name="size"/> units,
        /// so layout maths is about what the player sees; the art child reaches out past it for
        /// the shadow.
        /// </summary>
        public static RectTransform CreatePlate(string name, RectTransform parent, Vector2 size, float radius,
            HudArt.Look look, out Image art, bool raycast = false)
        {
            var holder = CreateRect(name, parent);
            holder.sizeDelta = size;

            var artRect = CreateRect("Art", holder);
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.offsetMin = new Vector2(-HudArt.Margin, -HudArt.Margin - 6f);
            artRect.offsetMax = new Vector2(HudArt.Margin, HudArt.Margin);

            art = artRect.gameObject.AddComponent<Image>();
            art.sprite = PlateSprite(size, radius, look, false);
            art.raycastTarget = raycast;
            return holder;
        }

        /// <summary>Re-skins a plate made by <see cref="CreatePlate"/> for a new size or look.</summary>
        public static void SetPlate(Image art, Vector2 size, float radius, HudArt.Look look)
        {
            art.sprite = PlateSprite(size, radius, look, false);
        }

        /// <summary>Re-skins a framed button: new look for both its resting and its pressed plate.</summary>
        public static void SkinButton(Button button, Image art, Vector2 size, float radius, HudArt.Look look)
        {
            art.sprite = PlateSprite(size, radius, look, false);
            button.spriteState = new SpriteState
            {
                pressedSprite = PlateSprite(size, radius, look, true),
                highlightedSprite = art.sprite,
                selectedSprite = art.sprite,
                disabledSprite = art.sprite
            };
        }

        /// <summary>
        /// White lettering with a hard dark edge, like the loading-screen tips. Tracking spaces the
        /// letters out; the effect is added before the outline so the edge follows the letters.
        /// </summary>
        public static Text CreateLabel(string name, RectTransform parent, string content, int fontSize,
            TextAnchor anchor = TextAnchor.MiddleCenter, float tracking = 0f)
        {
            var text = CreateText(name, parent, content, fontSize, anchor);
            text.fontStyle = FontStyle.Bold;

            if (tracking > 0f) text.gameObject.AddComponent<LetterSpacing>().tracking = tracking;

            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = LabelEdge;
            float edge = Mathf.Clamp(fontSize * 0.055f, 2f, 4f);
            outline.effectDistance = new Vector2(edge, -edge);
            return text;
        }

        /// <summary>A framed button: sprite swap to a pressed plate, label dips, clicks like every button.</summary>
        public static Button CreatePlateButton(string name, RectTransform parent, Vector2 size, float radius,
            HudArt.Look look, string content, int fontSize, out Text label)
        {
            var holder = CreatePlate(name, parent, size, radius, look, out Image art, true);
            label = CreateLabel("Label", holder, content, fontSize, TextAnchor.MiddleCenter, 0.12f);
            Stretch(label.rectTransform);

            var button = holder.gameObject.AddComponent<Button>();
            button.targetGraphic = art;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                pressedSprite = PlateSprite(size, radius, look, true),
                highlightedSprite = art.sprite,
                selectedSprite = art.sprite,
                disabledSprite = art.sprite
            };
            button.onClick.AddListener(() => SoundFx.Play(Sfx.Click));
            holder.gameObject.AddComponent<PressShift>().label = label.rectTransform;
            return button;
        }

        /// <summary>The round pause button: a medallion whose visible disc is <paramref name="diameter"/> units.</summary>
        public static Button CreateMedallionButton(string name, RectTransform parent, float diameter,
            HudArt.Glyph glyph = HudArt.Glyph.Pause)
        {
            var holder = CreateRect(name, parent);
            holder.sizeDelta = new Vector2(diameter, diameter);

            // The sprite is 21 units larger than the disc to hold the shadow, and the disc sits
            // 2.5 units above its centre.
            float full = diameter + 21f;
            var artRect = CreateRect("Art", holder);
            artRect.anchorMin = artRect.anchorMax = new Vector2(0.5f, 0.5f);
            artRect.sizeDelta = new Vector2(full, full);
            artRect.anchoredPosition = new Vector2(0f, -2.5f);

            var art = artRect.gameObject.AddComponent<Image>();
            art.sprite = HudArt.Medallion(full, false, glyph);
            art.raycastTarget = true;

            var button = holder.gameObject.AddComponent<Button>();
            button.targetGraphic = art;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                pressedSprite = HudArt.Medallion(full, true, glyph),
                highlightedSprite = art.sprite,
                selectedSprite = art.sprite,
                disabledSprite = art.sprite
            };
            button.onClick.AddListener(() => SoundFx.Play(Sfx.Click));
            return button;
        }

        /// <summary>Swaps the glyph on a medallion made by <see cref="CreateMedallionButton"/>.</summary>
        public static void SetGlyph(Button button, float diameter, HudArt.Glyph glyph)
        {
            float full = diameter + 21f;
            var art = (Image)button.targetGraphic;
            art.sprite = HudArt.Medallion(full, false, glyph);
            button.spriteState = new SpriteState
            {
                pressedSprite = HudArt.Medallion(full, true, glyph),
                highlightedSprite = art.sprite,
                selectedSprite = art.sprite,
                disabledSprite = art.sprite
            };
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }

    /// <summary>Dips a button's label a few units while it is held, to match the pressed plate.</summary>
    public class PressShift : MonoBehaviour,
        UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler,
        UnityEngine.EventSystems.IPointerExitHandler
    {
        public RectTransform label;
        private bool _down;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e) => Set(true);
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => Set(false);
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => Set(false);
        private void OnDisable() => Set(false);

        private void Set(bool down)
        {
            if (label == null || down == _down) return;
            _down = down;
            label.anchoredPosition = new Vector2(0f, down ? -5f : 0f);
        }
    }
}
