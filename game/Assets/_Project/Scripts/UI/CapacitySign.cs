using Tycoon.Config;
using Tycoon.Stations;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The board over a production building. It answers one question: does this place need me?
    ///
    /// It used to report the output basket - "8/12 eggs" - which turned out to be the one
    /// number the player already has. The collect square directly below the building shows the
    /// same count, in the place where the player would act on it. What is genuinely invisible
    /// is the feed: a coop with an empty hopper looks exactly like a coop with a full one until
    /// the eggs quietly stop, and by then the run to the corn field is already overdue.
    ///
    /// So the board leads with feed, as a bar that goes green to amber to red, and keeps one
    /// small line underneath for the other reason a building stops - a full basket, which is
    /// the only case where the answer is "collect" rather than "feed".
    ///
    /// Nothing here is specific to chickens. A cow shed gets the same board off the same
    /// component with hay in place of corn, and so will anything else with a hopper.
    /// </summary>
    public class CapacitySign : MonoBehaviour
    {
        [Header("What to report")]
        [Tooltip("The feed hopper. This is the headline: how close the animals are to going hungry.")]
        public ItemBuffer input;

        [Tooltip("The output basket. Only surfaced when it is full, because that also stops production.")]
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
        private Text _headline;
        private Text _footnote;

        private int _lastPercent = -1;
        private int _lastUnits = -1;
        private bool _lastStarved;
        private bool _lastOutputFull;
        private float _lastFill = -1f;
        private Color _lastFillColour;
        private Sprite _lastIcon;
        private bool _visible = true;
        private float _timer;

        private const float Interval = 0.2f;

        /// <summary>Below this the hopper is amber; below <see cref="Critical"/> it is red.</summary>
        private const float Low = 0.5f;

        private const float Critical = 0.2f;

        private static readonly Color Good = new Color(0.36f, 0.78f, 0.45f);
        private static readonly Color Warn = new Color(0.95f, 0.75f, 0.25f);
        private static readonly Color Empty = new Color(0.92f, 0.34f, 0.30f);
        private static readonly Color Ink = new Color(0.12f, 0.14f, 0.17f);
        private static readonly Color Quiet = new Color(0.33f, 0.36f, 0.40f);

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

            // The feed itself, not an arrow: a corn cob over a coop and a hay bale over a cow
            // shed say which building needs which run without a word of text.
            var iconRect = UIFactory.CreateRect("Icon", _root);
            _icon = iconRect.gameObject.AddComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.19f, 0.71f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(52f, 52f);

            _headline = UIFactory.CreateText("Headline", _root, "", 40);
            _headline.color = Ink;
            _headline.fontStyle = FontStyle.Bold;
            _headline.rectTransform.anchorMin = _headline.rectTransform.anchorMax = new Vector2(0.60f, 0.71f);
            _headline.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _headline.rectTransform.sizeDelta = new Vector2(140f, 56f);

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

            // Normally the headcount, which is the other half of "why is this coop slow".
            // Taken over by the basket when a full one has stopped the building, because that
            // is the more urgent of the two and they can never both be the answer.
            _footnote = UIFactory.CreateText("Footnote", _root, "", 30);
            _footnote.color = Quiet;
            _footnote.fontStyle = FontStyle.Bold;
            _footnote.rectTransform.anchorMin = _footnote.rectTransform.anchorMax = new Vector2(0.5f, 0.17f);
            _footnote.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _footnote.rectTransform.sizeDelta = new Vector2(200f, 32f);
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
            // A building with no hopper - a well, a machine that makes something from nothing -
            // has no feed to report, so it falls back to reporting its basket.
            float fill = input != null && input.capacity > 0 ? input.Fill : 1f;
            bool starved = input != null && input.IsEmpty;
            int percent = Mathf.Clamp(Mathf.RoundToInt(fill * 100f), 0, 100);

            // Rounded to fives. The hopper drains one unit at a time out of ten, so the number
            // would otherwise sit still for seconds and then jump; and every write to a Text
            // rebuilds this canvas.
            percent = Mathf.RoundToInt(percent / 5f) * 5;

            if (percent != _lastPercent || starved != _lastStarved)
            {
                _lastPercent = percent;
                _lastStarved = starved;

                // The word beats the number when the hopper has actually run out: 0% FED reads
                // as a statistic, NO FEED reads as a job.
                _headline.text = input == null ? "" : starved ? "NO FEED" : percent + "% FED";
                _headline.fontSize = starved ? 36 : 40;
                _headline.color = starved ? Empty : Ink;
            }

            var feedItem = input != null ? input.item : (output != null ? output.item : null);
            Sprite sprite = IconFactory.For(feedItem);
            if (sprite != _lastIcon)
            {
                _lastIcon = sprite;
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
                _icon.color = feedItem != null && !IconFactory.HasArtwork(feedItem)
                    ? feedItem.color
                    : Color.white;
            }

            bool outputFull = output != null && output.IsFull;
            int units = machine != null ? machine.units : 0;

            if (outputFull != _lastOutputFull || units != _lastUnits)
            {
                _lastOutputFull = outputFull;
                _lastUnits = units;

                if (outputFull)
                {
                    _footnote.text = "FULL - COLLECT";
                    _footnote.color = Empty;
                }
                else
                {
                    _footnote.text = machine != null ? $"{units}/{machine.maxUnits} working" : "";
                    _footnote.color = Quiet;
                }
            }

            float stepped = Mathf.Round(fill * 32f) / 32f;
            if (!Mathf.Approximately(stepped, _lastFill))
            {
                _lastFill = stepped;
                _barFill.fillAmount = stepped;
            }

            // Green while there is plenty, amber as it runs down, red once the animals are
            // nearly out. Writing colour every tick would rebuild the canvas for nothing.
            Color wanted = fill <= Critical ? Empty : fill <= Low ? Warn : Good;
            if (wanted != _lastFillColour)
            {
                _lastFillColour = wanted;
                _barFill.color = wanted;
            }
        }
    }
}
