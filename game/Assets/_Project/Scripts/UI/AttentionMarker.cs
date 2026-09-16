using Tycoon.Upkeep;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The bouncing exclamation mark over something that is about to stop working.
    ///
    /// A coop wearing out used to announce itself only as a line of text in the corner of the
    /// screen, which is the wrong place: the player is looking at the farm, the problem is a
    /// particular building, and a list in the HUD makes them read a name and then go and find
    /// it. A marker pinned to the building itself is the thing they are already looking at.
    ///
    /// It escalates rather than switching on. A faint amber mark appears well before anything
    /// breaks, hardens into an orange one that bobs and tilts, and finally goes red and hurries
    /// up. Nothing flashes: at this size, on a phone, a blinking marker is an irritation the
    /// player learns to tune out, which is the exact opposite of what it is for.
    ///
    /// <para>Reuse:</para> the marker itself knows nothing about durability. Point it at one
    /// and it follows condition; leave that empty and drive <see cref="Severity"/> from
    /// anywhere - a register with a queue going cold, a machine out of stock, a business that
    /// needs restocking - and it behaves identically.
    /// </summary>
    public class AttentionMarker : MonoBehaviour
    {
        [Header("Source (optional)")]
        [Tooltip("Follows this component's condition. Leave empty and set Severity yourself.")]
        public Durability durability;

        [Tooltip("Condition at which the marker first appears, faintly.")]
        [Range(0f, 1f)] public float appearBelow = 0.6f;

        [Tooltip("Condition at or below which the marker is at full strength.")]
        [Range(0f, 1f)] public float urgentBelow = 0.2f;

        [Header("Placement")]
        public float height = 4.05f;

        public float worldScale = 0.011f;

        /// <summary>
        /// 0 = nothing to say, 1 = deal with this now. Written by <see cref="durability"/>
        /// when one is set, otherwise by whoever wants the marker shown.
        /// </summary>
        public float Severity { get; set; }

        private Canvas _canvas;
        private RectTransform _root;
        private RectTransform _badge;
        private Image _body;
        private Image _rim;
        private Text _bang;

        private bool _shown;
        private float _phase;
        private float _eased;
        private Color _lastBody;
        private float _checkTimer;

        private const float CheckInterval = 0.2f;

        private static readonly Color Mild = new Color(0.96f, 0.78f, 0.28f);
        private static readonly Color Strong = new Color(0.97f, 0.55f, 0.18f);
        private static readonly Color Urgent = new Color(0.93f, 0.28f, 0.24f);

        private void Awake()
        {
            Build();
            _checkTimer = Random.value * CheckInterval;
            // Out of step with its neighbours, so a row of worn coops does not bob in unison
            // like a chorus line.
            _phase = Random.value * 10f;
        }

        private void Build()
        {
            if (_canvas != null) return;

            var stale = transform.Find("~Attention");
            if (stale != null) DestroyImmediate(stale.gameObject);

            var go = new GameObject("~Attention");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            _root = (RectTransform)go.transform;
            _root.sizeDelta = new Vector2(96f, 128f);
            _root.localScale = Vector3.one * worldScale;
            _root.localPosition = Vector3.up * height;

            // The badge is a child of the canvas root rather than the root itself, so the bob
            // and the tilt can be applied without touching the billboarding on the root.
            _badge = UIFactory.CreateRect("Badge", _root);
            _badge.anchorMin = _badge.anchorMax = new Vector2(0.5f, 0.5f);
            _badge.pivot = new Vector2(0.5f, 0.5f);
            _badge.sizeDelta = new Vector2(88f, 104f);

            _rim = UIFactory.CreatePanel("Rim", _badge, new Color(0.13f, 0.15f, 0.19f, 0.95f));
            Stretch(_rim.rectTransform, 0f);

            _body = UIFactory.CreatePanel("Body", _badge, Mild);
            Stretch(_body.rectTransform, 6f);

            // A drawn bang rather than the emoji: the legacy font has no glyph for it, so a
            // literal exclamation-mark character would render as an empty box.
            _bang = UIFactory.CreateText("Bang", _badge, "!", 84);
            Stretch(_bang.rectTransform, 0f);
            _bang.fontStyle = FontStyle.Bold;
            _bang.color = Color.white;

            var outline = _bang.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.1f, 0.08f, 0.06f, 0.6f);
            outline.effectDistance = new Vector2(3f, 3f);

            _canvas.gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>
        /// Condition mapped onto 0-1 urgency. Full condition says nothing at all; the mark
        /// fades in across the warning band and is at full strength once it is nearly broken.
        /// </summary>
        private float SeverityFromDurability()
        {
            if (durability == null) return Severity;
            if (durability.IsBroken) return 1f;

            float condition = durability.Fraction;
            if (condition >= appearBelow) return 0f;

            float span = Mathf.Max(0.01f, appearBelow - urgentBelow);
            return Mathf.Clamp01((appearBelow - condition) / span);
        }

        private void LateUpdate()
        {
            if (_root == null) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer <= 0f)
            {
                _checkTimer = CheckInterval;

                float wanted = SeverityFromDurability();
                if (durability != null) Severity = wanted;

                // A repaired building drops out of the band immediately, which is exactly the
                // "fix it and the warning goes" the player expects - no fade, no lingering.
                bool show = Severity > 0.001f && WorldUi.IsVisible(transform.position, 0.35f);
                if (show != _shown)
                {
                    _shown = show;
                    _canvas.gameObject.SetActive(show);
                    // Coming back, start from the current urgency rather than easing up from
                    // nothing while the player is already looking at it.
                    if (show) _eased = Severity;
                }
            }

            if (!_shown) return;

            var camera = WorldUi.Camera;
            if (camera != null) _root.rotation = camera.transform.rotation;

            Animate();
        }

        private void Animate()
        {
            _eased = Mathf.Lerp(_eased, Severity, Time.deltaTime * 3f);
            float s = Mathf.Clamp01(_eased);

            // Everything speeds up together as it gets worse, so urgency is read from the
            // movement before the colour has even registered.
            float speed = Mathf.Lerp(3.2f, 6.4f, s);
            _phase += Time.deltaTime * speed;

            float bob = Mathf.Abs(Mathf.Sin(_phase)) * Mathf.Lerp(7f, 20f, s);
            float tilt = Mathf.Sin(_phase * 0.5f) * Mathf.Lerp(4f, 13f, s);
            float pulse = 1f + Mathf.Sin(_phase * 1.3f) * Mathf.Lerp(0.03f, 0.1f, s);

            _badge.anchoredPosition = new Vector2(0f, bob);
            _badge.localRotation = Quaternion.Euler(0f, 0f, tilt);
            _badge.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.12f, s) * pulse;

            // Amber, then orange, then red. Only written when it has actually moved a step,
            // because assigning a colour rebuilds the canvas.
            Color wanted = s < 0.5f
                ? Color.Lerp(Mild, Strong, s * 2f)
                : Color.Lerp(Strong, Urgent, (s - 0.5f) * 2f);

            if (Mathf.Abs(wanted.r - _lastBody.r) > 0.01f ||
                Mathf.Abs(wanted.g - _lastBody.g) > 0.01f ||
                Mathf.Abs(wanted.b - _lastBody.b) > 0.01f)
            {
                _lastBody = wanted;
                _body.color = wanted;
            }
        }
    }
}
