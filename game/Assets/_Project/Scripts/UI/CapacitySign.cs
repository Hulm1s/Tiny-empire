using Tycoon.Config;
using Tycoon.Stations;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The board over a production building: what it holds, how many are working it, and
    /// whether it has stopped because the shelf is full.
    ///
    /// A full output buffer silently halts a coop. Before this there was nothing in the
    /// world that said so - the player just noticed, eventually, that the eggs had stopped.
    /// The bar going red is the whole point; the counts are the supporting detail.
    ///
    /// Built from the same sprites, palette and corner radius as the interaction squares
    /// and order bubbles, so the farm keeps speaking one visual language.
    /// </summary>
    public class CapacitySign : MonoBehaviour
    {
        [Header("What to report")]
        public ItemBuffer output;
        public ProducerMachine machine;

        [Header("Placement")]
        [Tooltip("Height above the building's origin. Kept low: the camera looks across the " +
                 "farm, so a sign hung high is drawn over the ground well behind it.")]
        public float height = 2.65f;

        public float worldScale = 0.0115f;

        private Canvas _canvas;
        private RectTransform _root;
        private Image _panel;
        private Image _barTrack;
        private Image _barFill;
        private Image _icon;
        private Text _count;
        private Text _units;

        private int _lastCount = -1, _lastCapacity = -1, _lastUnits = -1;
        private float _lastFill = -1f;
        private Color _lastFillColour;
        private bool _visible = true;
        private float _timer;

        private const float Interval = 0.2f;

        private static readonly Color Good = new Color(0.36f, 0.78f, 0.45f);
        private static readonly Color Warn = new Color(0.95f, 0.75f, 0.25f);
        private static readonly Color Stopped = new Color(0.92f, 0.34f, 0.30f);

        private void Awake()
        {
            Build();
            _timer = Random.value * Interval;
        }

        private void Build()
        {
            if (_canvas != null) return;

            var stale = transform.Find("~Capacity");
            if (stale != null) DestroyImmediate(stale.gameObject);

            var go = new GameObject("~Capacity");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(transform, false);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            _root = (RectTransform)go.transform;
            _root.sizeDelta = new Vector2(230f, 148f);
            _root.localScale = Vector3.one * worldScale;
            _root.localPosition = Vector3.up * height;

            var border = UIFactory.CreatePanel("Border", _root, new Color(0.13f, 0.15f, 0.19f, 0.92f));
            Stretch(border.rectTransform, 0f);

            _panel = UIFactory.CreatePanel("Panel", _root, new Color(0.97f, 0.97f, 0.96f, 0.97f));
            Stretch(_panel.rectTransform, 5f);

            var iconRect = UIFactory.CreateRect("Icon", _root);
            _icon = iconRect.gameObject.AddComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.19f, 0.71f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(52f, 52f);

            _count = UIFactory.CreateText("Count", _root, "0/0", 44);
            _count.color = new Color(0.12f, 0.14f, 0.17f);
            _count.fontStyle = FontStyle.Bold;
            _count.rectTransform.anchorMin = _count.rectTransform.anchorMax = new Vector2(0.58f, 0.71f);
            _count.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _count.rectTransform.sizeDelta = new Vector2(130f, 56f);

            _barTrack = UIFactory.CreatePanel("BarTrack", _root, new Color(0.82f, 0.83f, 0.86f));
            var track = _barTrack.rectTransform;
            track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.40f);
            track.pivot = new Vector2(0.5f, 0.5f);
            track.sizeDelta = new Vector2(158f, 16f);

            _barFill = UIFactory.CreatePanel("BarFill", track, Good);
            Stretch(_barFill.rectTransform, 0f);
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _barFill.fillAmount = 0f;

            // How many animals are actually working in there, which is the other half of
            // "why is this coop slow" and is otherwise only countable by eye in the pen.
            _units = UIFactory.CreateText("Units", _root, "", 30);
            _units.color = new Color(0.33f, 0.36f, 0.40f);
            _units.fontStyle = FontStyle.Bold;
            _units.rectTransform.anchorMin = _units.rectTransform.anchorMax = new Vector2(0.5f, 0.17f);
            _units.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _units.rectTransform.sizeDelta = new Vector2(170f, 32f);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private void LateUpdate()
        {
            if (_root == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = Interval;

            bool onScreen = WorldUi.IsVisible(transform.position, 0.35f);
            if (onScreen != _visible)
            {
                _visible = onScreen;
                _canvas.gameObject.SetActive(onScreen);
            }
            if (!onScreen) return;

            var camera = WorldUi.Camera;
            if (camera != null) _root.rotation = camera.transform.rotation;

            Refresh();
        }

        private void Refresh()
        {
            if (output == null) return;

            int count = output.Count;
            int capacity = Mathf.Max(1, output.capacity);
            int units = machine != null ? machine.units : 0;

            if (count != _lastCount || capacity != _lastCapacity)
            {
                _lastCount = count;
                _lastCapacity = capacity;

                // The word matters more than the numbers when production has stopped.
                _count.text = count >= capacity ? "FULL" : $"{count}/{capacity}";
                _count.fontSize = count >= capacity ? 40 : 44;

                var item = output.item;
                _icon.sprite = IconFactory.For(item);
                _icon.enabled = _icon.sprite != null;
                _icon.color = item != null && !IconFactory.HasArtwork(item) ? item.color : Color.white;
            }

            if (units != _lastUnits && machine != null)
            {
                _lastUnits = units;
                _units.text = $"{units}/{machine.maxUnits} working";
            }

            float fill = Mathf.Clamp01(count / (float)capacity);
            float stepped = Mathf.Round(fill * 32f) / 32f;
            if (!Mathf.Approximately(stepped, _lastFill))
            {
                _lastFill = stepped;
                _barFill.fillAmount = stepped;
            }

            // Green while there is room, amber as it fills, red once it has stopped the
            // building. Writing colour every tick would rebuild the canvas for nothing.
            Color wanted = fill >= 0.999f ? Stopped : fill > 0.75f ? Warn : Good;
            if (wanted != _lastFillColour)
            {
                _lastFillColour = wanted;
                _barFill.color = wanted;
            }
        }
    }
}
