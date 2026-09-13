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
        [Tooltip("Height above the customer's feet.")]
        public float height = 2.05f;

        [Tooltip("World size of one canvas pixel. 0.01 makes a 160px bubble 1.6 units wide.")]
        public float worldScale = 0.01f;

        private Canvas _canvas;
        private RectTransform _root;
        private Image _iconImage;
        private Image _patienceFill;
        private Text _countLabel;
        private Camera _camera;

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
            _root.sizeDelta = new Vector2(170f, 150f);
            _root.localScale = Vector3.one * worldScale;

            var panel = UIFactory.CreatePanel("Panel", _root, new Color(1f, 1f, 1f, 0.96f));
            panel.rectTransform.anchorMin = Vector2.zero;
            panel.rectTransform.anchorMax = Vector2.one;
            panel.rectTransform.offsetMin = Vector2.zero;
            panel.rectTransform.offsetMax = new Vector2(0f, -26f);

            // Little tail so the bubble reads as coming from the customer.
            var tail = UIFactory.CreatePanel("Tail", _root, new Color(1f, 1f, 1f, 0.96f));
            tail.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            tail.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            tail.rectTransform.pivot = new Vector2(0.5f, 0f);
            tail.rectTransform.sizeDelta = new Vector2(34f, 34f);
            tail.rectTransform.anchoredPosition = new Vector2(0f, -14f);

            var iconGo = UIFactory.CreateRect("Icon", _root);
            _iconImage = iconGo.gameObject.AddComponent<Image>();
            _iconImage.sprite = UIFactory.Circle;
            _iconImage.raycastTarget = false;
            iconGo.anchorMin = iconGo.anchorMax = new Vector2(0.32f, 0.62f);
            iconGo.pivot = new Vector2(0.5f, 0.5f);
            iconGo.sizeDelta = new Vector2(74f, 74f);

            _countLabel = UIFactory.CreateText("Count", _root, "x1", 58);
            _countLabel.color = new Color(0.13f, 0.15f, 0.18f);
            _countLabel.fontStyle = FontStyle.Bold;
            _countLabel.rectTransform.anchorMin = _countLabel.rectTransform.anchorMax =
                new Vector2(0.71f, 0.62f);
            _countLabel.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _countLabel.rectTransform.sizeDelta = new Vector2(90f, 80f);

            var track = UIFactory.CreatePanel("PatienceTrack", _root, new Color(0.85f, 0.87f, 0.9f, 1f));
            track.rectTransform.anchorMin = new Vector2(0.5f, 0.24f);
            track.rectTransform.anchorMax = new Vector2(0.5f, 0.24f);
            track.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            track.rectTransform.sizeDelta = new Vector2(120f, 18f);

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

        public void Show(ItemDefinition item, int remaining)
        {
            Build();
            if (item == null) { SetVisible(false); return; }

            if (item.icon != null)
            {
                _iconImage.sprite = item.icon;
                _iconImage.color = Color.white;
            }
            else
            {
                _iconImage.sprite = UIFactory.Circle;
                _iconImage.color = item.color;
            }

            _countLabel.text = "x" + Mathf.Max(0, remaining);
            SetVisible(true);
        }

        /// <summary>1 = just arrived, 0 = about to walk out in disgust.</summary>
        public void SetPatience(float normalised)
        {
            if (_patienceFill == null) return;
            float t = Mathf.Clamp01(normalised);
            _patienceFill.fillAmount = t;
            _patienceFill.color = t > 0.5f ? new Color(0.35f, 0.78f, 0.45f)
                : t > 0.25f ? new Color(0.95f, 0.75f, 0.25f)
                : new Color(0.9f, 0.35f, 0.3f);
        }

        public void SetVisible(bool visible)
        {
            if (_canvas != null) _canvas.gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf) return;
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            // Fixed camera angle, so matching its rotation is all the billboarding needed.
            _root.rotation = _camera.transform.rotation;
            _root.localPosition = Vector3.up * height;
        }
    }
}
