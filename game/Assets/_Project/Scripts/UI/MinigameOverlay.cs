using Tycoon.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The frame every serving minigame is built in (the florist's bouquet now, the hairdresser's
    /// haircut next): a full-screen overlay opened from a physical square, never from a HUD button.
    ///
    /// It does the parts that must be the same for every minigame and that the pause panel and the
    /// paint menu already settled:
    ///   - while open the world is frozen (Time.timeScale 0) and the joystick is gone, and both come
    ///     back on close - also if the HUD is torn down mid-game (OnDisable), so the game can never be
    ///     left frozen;
    ///   - it lives in the HUD's safe area, so it stays clear of the notch and the home bar;
    ///   - the design canvas is a phone portrait, 1080 x 1920 units, and the card is scaled down to
    ///     fit whatever the safe area is (a landscape browser window gets a smaller card, not a
    ///     cropped one), the same way the paint menu fits;
    ///   - the dim layer swallows every tap, so the pause button and the world behind never get one;
    ///   - one <see cref="MinigamePad"/> turns touch AND mouse into press / drag / release at design
    ///     coordinates (y down, like the mock-ups), so a minigame is written once for both;
    ///   - time inside a minigame is unscaled (<see cref="Tick"/> gets Time.unscaledDeltaTime).
    /// A minigame supplies <see cref="BuildContent"/> and its own Open / scoring, and calls
    /// <see cref="Show"/> / <see cref="Hide"/>.
    /// </summary>
    public abstract class MinigameOverlay : MonoBehaviour
    {
        public const float DesignWidth = 1080f;
        public const float DesignHeight = 1920f;

        /// <summary>The minigame on screen, if any. Only one can be.</summary>
        public static MinigameOverlay Current { get; private set; }

        private static readonly Color Dim = new Color(0.02f, 0.04f, 0.06f, 0.72f);

        private RectTransform _safeArea;
        private GameObject _joystick;
        private bool _built;

        /// <summary>The dim layer that covers the safe area.</summary>
        protected RectTransform Root { get; private set; }

        /// <summary>The 1080 x 1920 design card everything is laid out on.</summary>
        protected RectTransform Card { get; private set; }

        public bool IsOpen { get; private set; }

        protected void Attach(RectTransform safeArea, GameObject joystick)
        {
            _safeArea = safeArea;
            _joystick = joystick;
        }

        protected abstract void BuildContent(RectTransform card);

        /// <summary>
        /// The size of a second, landscape arrangement of the same content, if the minigame has one. Each
        /// frame the card takes whichever arrangement (portrait 1080 x 1920, or this) fits the safe area
        /// larger, so a rotating tablet switches live.
        /// </summary>
        protected virtual Vector2? LandscapeDesign => null;

        /// <summary>Called when the arrangement changes; also once with false when the screen is first shown.</summary>
        protected virtual void ApplyOrientation(bool landscape) { }

        private bool _landscape;

        /// <summary>Called every frame while open, with unscaled seconds.</summary>
        protected virtual void Tick(float dt) { }

        /// <summary>
        /// Builds the screen now, hidden. Building it draws the HUD plates in software (about 2 s in the
        /// editor the first time), so a square that opens a minigame calls this when it comes into play,
        /// not when the player is waiting for the minigame.
        /// </summary>
        public void Prewarm() => EnsureBuilt();

        protected void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var dim = UIFactory.CreatePanel(GetType().Name + "Dim", _safeArea, Dim);
            Root = dim.rectTransform;
            UIFactory.Stretch(Root);
            // Over the edges of the safe area too, so the world does not show round the notch.
            Root.offsetMin = new Vector2(-400f, -400f);
            Root.offsetMax = new Vector2(400f, 400f);
            dim.raycastTarget = true;

            Card = UIFactory.CreateRect("Card", Root);
            Card.anchorMin = Card.anchorMax = new Vector2(0.5f, 0.5f);
            Card.pivot = new Vector2(0.5f, 0.5f);
            Card.sizeDelta = new Vector2(DesignWidth, DesignHeight);

            BuildContent(Card);
            Root.gameObject.SetActive(false);
        }

        protected void Show()
        {
            EnsureBuilt();
            if (Current != null && Current != this) Current.Hide();

            IsOpen = true;
            Current = this;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            FitCard();

            // Freeze the world, as the pause panel does: no customer runs out of patience and no
            // flower blooms while the player is busy in here.
            Time.timeScale = 0f;
            if (_joystick != null) _joystick.SetActive(false);
            PlayerInputSource.Joystick = Vector2.zero;
        }

        protected void Hide()
        {
            if (!IsOpen) return;
            IsOpen = false;
            if (Current == this) Current = null;
            Root.gameObject.SetActive(false);

            Time.timeScale = 1f;
            if (_joystick != null) _joystick.SetActive(true);
            PlayerInputSource.Joystick = Vector2.zero;
        }

        protected virtual void OnDisable()
        {
            // Never leave the game frozen because the HUD went away.
            if (IsOpen)
            {
                IsOpen = false;
                if (Current == this) Current = null;
                Time.timeScale = 1f;
                if (_joystick != null) _joystick.SetActive(true);
            }
        }

        private void Update()
        {
            if (!IsOpen) return;
            FitCard();
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// Scales the design card into the safe area: never bigger than designed, as large as fits.
        /// Re-run every frame so a rotation or a resized browser window is followed at once.
        /// </summary>
        private void FitCard()
        {
            var r = Root.rect;
            // The root reaches 400 units past the safe area on every side (see EnsureBuilt).
            float w = r.width - 800f, h = r.height - 800f;
            if (w <= 0f || h <= 0f) return;
            float fit = Mathf.Min(1f, h / DesignHeight, w / DesignWidth);
            bool landscape = false;
            var land = LandscapeDesign;
            if (land.HasValue)
            {
                float fitL = Mathf.Min(1f, h / land.Value.y, w / land.Value.x);
                landscape = fitL > fit * 1.0001f;
                if (landscape) fit = fitL;
            }
            if (landscape != _landscape) { _landscape = landscape; ApplyOrientation(landscape); }
            if (!Mathf.Approximately(Card.localScale.x, fit)) Card.localScale = Vector3.one * fit;
        }

        // ---- layout helpers, in design units (x right, y DOWN from the card's top-left) ------

        /// <summary>Centres <paramref name="rect"/> on (cx, cy) with size (w, h).</summary>
        protected static void Place(RectTransform rect, float cx, float cy, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(cx, -cy);
            rect.sizeDelta = new Vector2(w, h);
        }

        protected static void Move(RectTransform rect, float cx, float cy) =>
            rect.anchoredPosition = new Vector2(cx, -cy);

        /// <summary>A layer the size of the card, for one step's contents.</summary>
        protected static RectTransform Layer(string name, RectTransform parent)
        {
            var layer = UIFactory.CreateRect(name, parent);
            UIFactory.Stretch(layer);
            return layer;
        }

        /// <summary>A framed HUD plate given by its top-left corner, as the mock-ups give them.</summary>
        protected static RectTransform Plate(string name, RectTransform parent, float left, float top, float w, float h,
            float radius, HudArt.Look look, out Image art)
        {
            var plate = UIFactory.CreatePlate(name, parent, new Vector2(w, h), radius, look, out art);
            Place(plate, left + w * 0.5f, top + h * 0.5f, w, h);
            return plate;
        }

        protected static Text Label(string name, RectTransform parent, string text, int size, float cx, float cy,
            float w = 800f, float h = 0f, TextAnchor anchor = TextAnchor.MiddleCenter, float tracking = 0.05f)
        {
            var label = UIFactory.CreateLabel(name, parent, text, size, anchor, tracking);
            Place(label.rectTransform, cx, cy, w, h > 0f ? h : size * 1.6f);
            return label;
        }

        protected static Image Picture(string name, RectTransform parent, Sprite sprite, float cx, float cy, float w, float h)
        {
            var rect = UIFactory.CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            Place(rect, cx, cy, w, h);
            return image;
        }

        /// <summary>A texture drawn 1:1 with its top-left at (left, top) - the mock-ups' Put().</summary>
        protected static Image Put(string name, RectTransform parent, Sprite sprite, float left, float top)
        {
            var size = sprite.rect.size;
            return Picture(name, parent, sprite, left + size.x * 0.5f, top + size.y * 0.5f, size.x, size.y);
        }

        /// <summary>A touch/mouse surface over (left, top, w, h) of the card.</summary>
        protected MinigamePad Pad(RectTransform parent, float left, float top, float w, float h)
        {
            var rect = UIFactory.CreateRect("Pad", parent);
            Place(rect, left + w * 0.5f, top + h * 0.5f, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            var pad = rect.gameObject.AddComponent<MinigamePad>();
            // The pad's coordinates are the design coordinates of the group it sits in (full card size,
            // moved as a whole when the arrangement changes), not of the card.
            pad.card = parent;
            return pad;
        }
    }

    /// <summary>
    /// Press, drag and release on a minigame, reported in the card's design coordinates (x right,
    /// y down, 1080 x 1920) whatever the card's current scale. Touch and mouse both arrive here
    /// through the event system, so nothing in a minigame cares which it was.
    /// </summary>
    public class MinigamePad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform card;

        public event System.Action<Vector2> Down;
        public event System.Action<Vector2> Drag;
        public event System.Action<Vector2> Up;

        /// <summary>Only one finger drives the game; a second touch is ignored.</summary>
        private int _pointer = int.MinValue;

        private Vector2 ToDesign(Vector2 screen, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(card, screen, cam, out var local);
            return new Vector2(local.x + MinigameOverlay.DesignWidth * 0.5f, MinigameOverlay.DesignHeight * 0.5f - local.y);
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_pointer != int.MinValue) return;
            _pointer = e.pointerId;
            Down?.Invoke(ToDesign(e.position, e.pressEventCamera));
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _pointer) return;
            Drag?.Invoke(ToDesign(e.position, e.pressEventCamera));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _pointer) return;
            _pointer = int.MinValue;
            Up?.Invoke(ToDesign(e.position, e.pressEventCamera));
        }

        private void OnDisable() => _pointer = int.MinValue;
    }
}
