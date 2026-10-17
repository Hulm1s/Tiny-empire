using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The little speech bubble over a flower bed that says what it needs: a seed packet (plant me), a
    /// water drop (water me) or a pink bloom with a sparkle (pick me). Hidden while the bed is growing.
    ///
    /// Built like <see cref="OrderBubble"/> (the same white panel with a dark rim and a tail) and kept
    /// as lean as <see cref="AttentionMarker"/>: a world-space canvas that is only switched on while
    /// there is something to say and the bed is on screen, billboarded to the camera. It knows nothing
    /// about beds; the bed tells it which <see cref="Kind"/> to show.
    /// </summary>
    public class BedBubble : MonoBehaviour
    {
        public enum Kind { None, Seed, Water, Ready }

        [Tooltip("Height above the bed's origin. The bed is flat, so this is where the bubble hangs.")]
        public float height = 1.55f;

        [Tooltip("World size of one canvas pixel: a 200 px bubble is about a metre wide.")]
        public float worldScale = 0.0048f;

        private Canvas _canvas;
        private RectTransform _root;
        private Image _icon;
        private Kind _kind = Kind.None;
        private bool _shown;
        private float _checkTimer;

        private const float CheckInterval = 0.2f;

        private void Awake()
        {
            Build();
            _checkTimer = Random.value * CheckInterval;
        }

        private void Build()
        {
            if (_canvas != null) return;

            var stale = transform.Find("~BedBubble");
            if (stale != null) DestroyImmediate(stale.gameObject);

            var go = new GameObject("~BedBubble");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            _root = (RectTransform)go.transform;
            _root.sizeDelta = new Vector2(200f, 176f);
            _root.localScale = Vector3.one * worldScale;

            var white = new Color(1f, 1f, 1f, 0.97f);
            var rim = new Color(0.16f, 0.18f, 0.22f, 0.92f);

            // Tail first, so the panel draws over its top half and the two read as one shape.
            var tailRim = UIFactory.CreatePanel("TailRim", _root, rim);
            Corner(tailRim.rectTransform, 36f, 24f);
            var tail = UIFactory.CreatePanel("Tail", _root, white);
            Corner(tail.rectTransform, 26f, 30f);

            var border = UIFactory.CreatePanel("Border", _root, rim);
            Fill(border.rectTransform, 26f, 0f);
            var panel = UIFactory.CreatePanel("Panel", _root, white);
            Fill(panel.rectTransform, 26f, 6f);

            var icon = UIFactory.CreateRect("Icon", _root);
            _icon = icon.gameObject.AddComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.57f);
            icon.pivot = new Vector2(0.5f, 0.5f);
            icon.sizeDelta = new Vector2(138f, 138f);

            _canvas.gameObject.SetActive(false);
        }

        private static void Corner(RectTransform rect, float size, float y)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        private static void Fill(RectTransform rect, float bottom, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, bottom + inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>What to show. Cheap to call every frame: nothing changes unless the kind does.</summary>
        public void Show(Kind kind)
        {
            if (kind == _kind) return;
            _kind = kind;
            Build();

            string id = kind == Kind.Seed ? "seed" : kind == Kind.Water ? "drop" : kind == Kind.Ready ? "ready" : null;
            if (id != null) _icon.sprite = IconFactory.GardenIcon(id);
            _checkTimer = 0f;   // re-evaluate visibility now
        }

        private void LateUpdate()
        {
            if (_root == null) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer <= 0f)
            {
                _checkTimer = CheckInterval;
                bool show = _kind != Kind.None && WorldUi.IsVisible(transform.position, 0.3f);
                if (show != _shown)
                {
                    _shown = show;
                    _canvas.gameObject.SetActive(show);
                }
            }

            if (!_shown) return;
            var camera = WorldUi.Camera;
            if (camera != null) _root.rotation = camera.transform.rotation;
        }
    }
}
