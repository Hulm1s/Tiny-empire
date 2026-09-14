using System.Text;
using Tycoon.Stations;
using Tycoon.Upkeep;
using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// Floating world-space label above a station or machine.
    ///
    /// In this genre the player is never told anything through menus - every number they need
    /// hovers over the thing it describes. One component composes the label from whichever
    /// parts are wired up, so it works over a crop field, an oven or a locked gate.
    /// </summary>
    [ExecuteAlways]
    public class InfoSign : MonoBehaviour
    {
        [Header("Sources (all optional)")]
        public StationBase station;
        public ProducerMachine machine;
        public Durability durability;
        public ItemBuffer buffer;

        [Header("Layout")]
        public float height = 2.4f;

        [Tooltip("World height of a glyph is roughly fontSize * characterSize / 10. The camera " +
                 "shows about 17 world units top to bottom, so 0.05 here reads as small, tidy " +
                 "text on a phone rather than a billboard across the whole screen.")]
        public float characterSize = 0.055f;

        public int fontSize = 80;

        [Tooltip("Hide the sign entirely when there is nothing worth saying.")]
        public bool hideWhenEmpty = true;

        private TextMesh _text;
        private Transform _pivot;
        private readonly StringBuilder _builder = new StringBuilder(64);

        /// <summary>
        /// Seconds between recomposes. Every line of this sign is built with string
        /// interpolation, so composing at frame rate turns a handful of signs into a steady
        /// stream of garbage - and WebGL pays for that in collection pauses, which is exactly
        /// the stutter it is supposed to be helping the player avoid.
        /// </summary>
        private const float RefreshInterval = 0.2f;

        private float _refreshTimer;
        private bool _visible = true;

        private void Awake()
        {
            EnsureText();
            _refreshTimer = Random.value * RefreshInterval;
        }

        private void EnsureText()
        {
            if (_text != null) return;

            var existing = transform.Find("~Sign");
            if (existing != null)
            {
                _pivot = existing;
                _text = existing.GetComponent<TextMesh>();
                if (_text != null) return;
            }

            var go = new GameObject("~Sign");
            go.hideFlags = HideFlags.DontSave;
            _pivot = go.transform;
            _pivot.SetParent(transform, false);
            _pivot.localPosition = Vector3.up * height;

            _text = go.AddComponent<TextMesh>();
            _text.font = UIFactory.Font;
            _text.characterSize = characterSize;
            _text.fontSize = fontSize;
            _text.anchor = TextAnchor.LowerCenter;
            _text.alignment = TextAlignment.Center;
            _text.color = Color.white;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = UIFactory.Font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void LateUpdate()
        {
            EnsureText();
            if (_text == null) return;

            _refreshTimer -= Time.deltaTime;
            bool refresh = _refreshTimer <= 0f;
            if (refresh)
            {
                _refreshTimer = RefreshInterval;
                _visible = WorldUi.IsVisible(transform.position);
            }

            // A sign nobody can see needs neither billboarding nor a new string.
            if (!_visible)
            {
                if (_text.gameObject.activeSelf) _text.gameObject.SetActive(false);
                return;
            }

            _pivot.localPosition = Vector3.up * height;
            // The camera angle is fixed, so matching its rotation is all the billboarding needed.
            var camera = WorldUi.Camera;
            if (camera != null) _pivot.rotation = camera.transform.rotation;

            if (!refresh) return;

            string content = Compose();
            if (_text.text != content) _text.text = content;

            bool show = !hideWhenEmpty || !string.IsNullOrEmpty(content);
            if (_text.gameObject.activeSelf != show) _text.gameObject.SetActive(show);

            var tint = Tint();
            if (_text.color != tint) _text.color = tint;
        }

        private string Compose()
        {
            _builder.Clear();

            if (station != null)
            {
                string status = station.StatusText;
                if (!string.IsNullOrEmpty(status)) _builder.Append(status);
            }

            if (machine != null)
            {
                string blockage = machine.CurrentBlockage switch
                {
                    ProducerMachine.Blockage.Broken => "BROKEN",
                    ProducerMachine.Blockage.NoInput => "needs input",
                    ProducerMachine.Blockage.OutputFull => "full",
                    _ => $"{Mathf.RoundToInt(machine.Progress01 * 100f)}%"
                };
                Append(blockage);
            }

            if (buffer != null) Append($"{buffer.Count}/{buffer.capacity}");

            if (durability != null && durability.NeedsAttention)
                Append($"[{Mathf.RoundToInt(durability.Fraction * 100f)}%]");

            return _builder.ToString();
        }

        private void Append(string part)
        {
            if (string.IsNullOrEmpty(part)) return;
            if (_builder.Length > 0) _builder.Append('\n');
            _builder.Append(part);
        }

        private Color Tint()
        {
            if (durability != null && durability.IsBroken) return new Color(1f, 0.35f, 0.3f);
            if (machine != null && machine.CurrentBlockage == ProducerMachine.Blockage.Broken)
                return new Color(1f, 0.35f, 0.3f);
            if (durability != null && durability.NeedsAttention) return new Color(1f, 0.72f, 0.3f);
            if (station != null && !station.IsOperational) return new Color(0.72f, 0.76f, 0.8f);
            return Color.white;
        }
    }
}
