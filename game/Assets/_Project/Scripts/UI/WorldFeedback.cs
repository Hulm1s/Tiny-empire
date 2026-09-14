using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// Short-lived labels that pop up in the world: "+8 Corn", "+$25", "FIXED".
    ///
    /// Repeated events merge into one rising label rather than spawning a new one per unit.
    /// That is both better to read - "+8 Corn" instead of eight separate "+1 Corn" - and much
    /// cheaper, because a drip transfer fires several times a second.
    /// </summary>
    public static class WorldFeedback
    {
        private static readonly Dictionary<string, FloatingLabel> Merging =
            new Dictionary<string, FloatingLabel>();

        /// <summary>Seconds a label stays open to absorb further events of the same kind.</summary>
        private const float MergeWindow = 0.7f;

        /// <summary>A one-off message with no counting, e.g. "FIXED" or "UNLOCKED".</summary>
        public static void Show(Vector3 worldPosition, string message, Color color)
        {
            var label = FloatingLabel.Create(worldPosition, color);
            label.SetMessage(message);
        }

        /// <summary>
        /// A counted message. Calls sharing a key inside the merge window add to the running
        /// total instead of spawning another label.
        /// </summary>
        public static void Add(string key, Vector3 worldPosition, int amount, string suffix, Color color)
        {
            if (Merging.TryGetValue(key, out var existing))
            {
                if (existing != null && existing.CanAbsorb(MergeWindow))
                {
                    existing.Absorb(amount, worldPosition);
                    return;
                }
                Merging.Remove(key);
            }

            var label = FloatingLabel.Create(worldPosition, color);
            label.SetCounted(amount, suffix);
            label.MergeKey = key;
            Merging[key] = label;
        }

        /// <summary>A counted money message, rendered as "+$25" rather than "+25 $".</summary>
        public static void AddMoney(string key, Vector3 worldPosition, int amount)
        {
            if (Merging.TryGetValue(key, out var existing))
            {
                if (existing != null && existing.CanAbsorb(MergeWindow))
                {
                    existing.Absorb(amount, worldPosition);
                    return;
                }
                Merging.Remove(key);
            }

            var label = FloatingLabel.Create(worldPosition, new Color(1f, 0.88f, 0.4f));
            label.SetMoney(amount);
            label.MergeKey = key;
            Merging[key] = label;
        }

        internal static void Forget(string key)
        {
            if (!string.IsNullOrEmpty(key)) Merging.Remove(key);
        }
    }

    /// <summary>One rising, fading world-space label. Created by <see cref="WorldFeedback"/>.</summary>
    public class FloatingLabel : MonoBehaviour
    {
        private const float Lifetime = 1.25f;
        private const float RiseSpeed = 1.15f;

        private Text _text;
        private RectTransform _root;
        private float _age;
        private int _amount;
        private string _suffix;
        private bool _isMoney;
        private Color _color;

        internal string MergeKey;

        public bool CanAbsorb(float window) => _age <= window;

        internal static FloatingLabel Create(Vector3 worldPosition, Color color)
        {
            var go = new GameObject("~Feedback");

            // Add the Canvas *before* positioning. Adding a Canvas swaps the plain Transform
            // for a RectTransform, which resets the position - so setting it first sends every
            // popup to the world origin instead of to the action that triggered it.
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var root = (RectTransform)go.transform;
            root.position = worldPosition;
            root.sizeDelta = new Vector2(260f, 70f);
            root.localScale = Vector3.one * 0.012f;

            var label = go.AddComponent<FloatingLabel>();
            label._root = root;
            label._color = color;

            var text = UIFactory.CreateText("Text", root, "", 60);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            label._text = text;

            return label;
        }

        public void SetMessage(string message)
        {
            _suffix = null;
            if (_text != null) _text.text = message;
        }

        public void SetCounted(int amount, string suffix)
        {
            _amount = amount;
            _suffix = suffix;
            _isMoney = false;
            Refresh();
        }

        public void SetMoney(int amount)
        {
            _amount = amount;
            _suffix = null;
            _isMoney = true;
            Refresh();
        }

        public void Absorb(int amount, Vector3 worldPosition)
        {
            _amount += amount;
            _age = 0f;
            // Follow the action rather than staying where the first unit happened.
            transform.position = new Vector3(worldPosition.x, transform.position.y, worldPosition.z);
            Refresh();

            // A small kick so a growing number is visibly alive.
            if (_root != null) _root.localScale = Vector3.one * 0.015f;
        }

        private void Refresh()
        {
            if (_text == null) return;
            if (_isMoney) _text.text = $"+${_amount}";
            else _text.text = string.IsNullOrEmpty(_suffix) ? $"+{_amount}" : $"+{_amount} {_suffix}";
        }

        private void Update()
        {
            _age += Time.deltaTime;

            transform.position += Vector3.up * RiseSpeed * Time.deltaTime;

            if (_root != null)
            {
                _root.localScale = Vector3.Lerp(_root.localScale, Vector3.one * 0.012f,
                    Time.deltaTime * 8f);
            }

            if (_text != null)
            {
                float fade = Mathf.Clamp01(1f - _age / Lifetime);
                // Hold full opacity for the first half, then fade out.
                _text.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(fade * 2f));
            }

            if (_age >= Lifetime)
            {
                WorldFeedback.Forget(MergeKey);
                Destroy(gameObject);
            }
        }

        private void LateUpdate()
        {
            // Popups are created several times a second during a transfer, so they share the
            // cached camera rather than each running a tag search on their first frame.
            var camera = WorldUi.Camera;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }
}
