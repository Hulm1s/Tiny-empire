using Tycoon.Config;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The speech bubble over a customer's head showing what they came in for, e.g. an egg
    /// icon and "x4", with a patience bar that drains while they wait.
    ///
    /// Built as a world-space canvas so it sits in the scene above the customer but still
    /// uses ordinary UI sprites and text, and so swapping the placeholder dot for real art is
    /// just assigning a sprite on the ItemDefinition.
    /// </summary>
    public class OrderBubble : MonoBehaviour
    {
        [Tooltip("Height above the customer's feet. The bubble hangs centred on this, so it has " +
                 "to clear a head at about 1.2 m.")]
        public float height = 2.25f;

        [Tooltip("World size of one canvas pixel. 0.01 makes a 160px bubble 1.6 units wide.")]
        public float worldScale = 0.01f;

        private Canvas _canvas;
        private RectTransform _root;
        private Image _iconImage;
        private Image _patienceFill;
        private Text _countLabel;
        private float _lastPatience = -1f;

        private void Awake() => Build();

        private void Build()
        {
            if (_canvas != null) return;

            var go = new GameObject("~OrderBubble");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * height;

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            _root = (RectTransform)go.transform;
            // Bigger than it was. The old bubble gave the product about twenty screen pixels,
            // and since egg and milk are both near-white, a small tinted dot made them
            // genuinely indistinguishable. The icon now gets roughly double that.
            _root.sizeDelta = new Vector2(200f, 176f);
            _root.localScale = Vector3.one * worldScale;

            // Tail first, so the panel draws over its top half and the two read as one shape.
            var tail = UIFactory.CreatePanel("Tail", _root, new Color(1f, 1f, 1f, 0.97f));
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            tail.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            tail.rectTransform.sizeDelta = new Vector2(30f, 30f);
            tail.rectTransform.anchoredPosition = new Vector2(0f, 28f);
            tail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // Dark rim, matching the outline on the icons and the border on the squares, so a
            // bubble and an interaction square look like parts of the same game.
            var border = UIFactory.CreatePanel("Border", _root, new Color(0.16f, 0.18f, 0.22f, 0.9f));
            Fill(border.rectTransform, bottomInset: 26f, inset: 0f);

            var panel = UIFactory.CreatePanel("Panel", _root, new Color(1f, 1f, 1f, 0.97f));
            Fill(panel.rectTransform, bottomInset: 26f, inset: 5f);

            var iconGo = UIFactory.CreateRect("Icon", _root);
            _iconImage = iconGo.gameObject.AddComponent<Image>();
            _iconImage.sprite = UIFactory.Circle;
            _iconImage.raycastTarget = false;
            _iconImage.preserveAspect = true;
            iconGo.anchorMin = iconGo.anchorMax = new Vector2(0.31f, 0.63f);
            iconGo.pivot = new Vector2(0.5f, 0.5f);
            iconGo.sizeDelta = new Vector2(94f, 94f);

            // Secondary to the icon, deliberately: the player should know what is being asked
            // for before they know how many.
            _countLabel = UIFactory.CreateText("Count", _root, "x1", 62);
            _countLabel.color = new Color(0.13f, 0.15f, 0.18f);
            _countLabel.fontStyle = FontStyle.Bold;
            _countLabel.rectTransform.anchorMin = _countLabel.rectTransform.anchorMax =
                new Vector2(0.72f, 0.63f);
            _countLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _countLabel.rectTransform.sizeDelta = new Vector2(96f, 84f);

            var track = UIFactory.CreatePanel("PatienceTrack", _root, new Color(0.85f, 0.87f, 0.9f, 1f));
            track.rectTransform.anchorMin = new Vector2(0.5f, 0.27f);
            track.rectTransform.anchorMax = new Vector2(0.5f, 0.27f);
            track.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(148f, 16f);

            var fillGo = UIFactory.CreateRect("PatienceFill", track.rectTransform);
            _patienceFill = fillGo.gameObject.AddComponent<Image>();
            _patienceFill.sprite = UIFactory.RoundedBox;
            _patienceFill.type = Image.Type.Sliced;
            _patienceFill.raycastTarget = false;
            _patienceFill.color = new Color(0.35f, 0.78f, 0.45f);
            fillGo.anchorMin = new Vector2(0f, 0f);
            fillGo.anchorMax = new Vector2(1f, 1f);
            fillGo.offsetMin = Vector2.zero;
            fillGo.offsetMax = Vector2.zero;

            _patienceFill.fillMethod = Image.FillMethod.Horizontal;
            _patienceFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _patienceFill.type = Image.Type.Filled;
            _patienceFill.fillAmount = 1f;

            SetVisible(false);
        }

        /// <summary>
        /// Shows an order. Entirely driven by the item itself - there is no per-product code
        /// path here, so a product added later gets a correct bubble for free.
        /// </summary>
        public void Show(ItemDefinition item, int remaining)
        {
            Build();
            if (item == null) { SetVisible(false); return; }

            _iconImage.sprite = IconFactory.For(item);
            // Drawn icons carry their own colours; the plain-disc fallback for a product with
            // no artwork still has to be tinted to mean anything.
            _iconImage.color = IconFactory.HasArtwork(item) ? Color.white : item.color;

            _countLabel.text = "x" + Mathf.Max(0, remaining);
            SetVisible(true);
        }

        /// <summary>Stretches to the bubble's body, leaving the bottom strip for the tail.</summary>
        private static void Fill(RectTransform rect, float bottomInset, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, bottomInset + inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        /// <summary>1 = just arrived, 0 = about to walk out in disgust.</summary>
        public void SetPatience(float normalised)
        {
            if (_patienceFill == null) return;

            // Called every frame by the customer, and both of these writes rebuild the bubble's
            // canvas. The bar is 120 pixels wide, so quantising to sixty steps is finer than
            // anyone can see and leaves most frames with nothing to do.
            float t = Mathf.Clamp01(normalised);
            float stepped = Mathf.Round(t * 60f) / 60f;
            if (!Mathf.Approximately(stepped, _lastPatience))
            {
                _lastPatience = stepped;
                _patienceFill.fillAmount = stepped;

                var wanted = t > 0.5f ? new Color(0.35f, 0.78f, 0.45f)
                    : t > 0.25f ? new Color(0.95f, 0.75f, 0.25f)
                    : new Color(0.9f, 0.35f, 0.3f);
                if (_patienceFill.color != wanted) _patienceFill.color = wanted;
            }
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;

            var camera = WorldUi.Camera;
            if (camera == null) return;

            // Fixed camera angle, so matching its rotation is all the billboarding needed.
            _root.rotation = camera.transform.rotation;
            _root.localPosition = Vector3.up * height;
        }
    }
}
