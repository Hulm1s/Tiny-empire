using Tycoon.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// Floating thumbstick: the ring appears wherever the thumb lands rather than sitting in a
    /// fixed corner. On a phone held one-handed that removes the "hunt for the stick" problem
    /// entirely, and it costs nothing on desktop where the keyboard fallback is used instead.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Ring that marks the origin of the drag.")]
        public RectTransform ring;

        [Tooltip("Knob that follows the thumb inside the ring.")]
        public RectTransform knob;

        [Tooltip("Drag distance, in reference-canvas pixels, that equals full speed.")]
        public float radius = 170f;

        [Tooltip("Fraction of the radius ignored, to stop tiny jitters creeping the character.")]
        [Range(0f, 0.5f)] public float deadZone = 0.08f;

        private Canvas _canvas;
        private int _activePointerId = int.MinValue;
        private Vector2 _origin;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            SetVisible(false);
        }

        private void OnDisable()
        {
            _activePointerId = int.MinValue;
            PlayerInputSource.Joystick = Vector2.zero;
            SetVisible(false);
        }

        private float ScaleFactor => _canvas != null ? _canvas.scaleFactor : 1f;

        public void OnPointerDown(PointerEventData eventData)
        {
            // First finger down wins; later fingers are ignored so the stick cannot be hijacked.
            if (_activePointerId != int.MinValue) return;

            _activePointerId = eventData.pointerId;
            _origin = eventData.position;

            if (ring != null) ring.position = _origin;
            if (knob != null) knob.position = _origin;

            SetVisible(true);
            PlayerInputSource.Joystick = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;

            Vector2 delta = eventData.position - _origin;
            float radiusPx = Mathf.Max(1f, radius * ScaleFactor);

            Vector2 clamped = Vector2.ClampMagnitude(delta, radiusPx);
            if (knob != null) knob.position = _origin + clamped;

            Vector2 value = clamped / radiusPx;
            PlayerInputSource.Joystick = value.magnitude < deadZone ? Vector2.zero : value;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _activePointerId) return;

            _activePointerId = int.MinValue;
            PlayerInputSource.Joystick = Vector2.zero;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (ring != null) ring.gameObject.SetActive(visible);
            if (knob != null) knob.gameObject.SetActive(visible);
        }

        /// <summary>
        /// Builds the joystick hierarchy under an existing canvas. Called by the HUD so no
        /// scene needs to contain prefab wiring for it.
        /// </summary>
        public static VirtualJoystick Create(RectTransform parent, Sprite circle)
        {
            var areaGo = new GameObject("JoystickArea", typeof(RectTransform), typeof(Image), typeof(VirtualJoystick));
            var area = (RectTransform)areaGo.transform;
            area.SetParent(parent, false);

            // Covers the lower part of the screen: enough room for the thumb, while leaving the
            // top clear so HUD buttons stay tappable.
            area.anchorMin = new Vector2(0f, 0f);
            area.anchorMax = new Vector2(1f, 0.62f);
            area.offsetMin = Vector2.zero;
            area.offsetMax = Vector2.zero;

            var areaImage = areaGo.GetComponent<Image>();
            areaImage.color = new Color(0f, 0f, 0f, 0f); // invisible but raycast-able
            areaImage.raycastTarget = true;

            var ring = CreateCircle("Ring", area, circle, 320f, new Color(1f, 1f, 1f, 0.16f));
            var knob = CreateCircle("Knob", area, circle, 130f, new Color(1f, 1f, 1f, 0.55f));

            var joystick = areaGo.GetComponent<VirtualJoystick>();
            joystick.ring = ring;
            joystick.knob = knob;
            return joystick;
        }

        private static RectTransform CreateCircle(string name, RectTransform parent, Sprite sprite, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }
    }
}
