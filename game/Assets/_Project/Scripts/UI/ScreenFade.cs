using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// A full-screen black overlay whose only job is its alpha, used to hide the jump when the
    /// player travels between locations.
    ///
    /// Not a button and never blocks a tap: it is state display, like the money readout, so it
    /// does not break the "no HUD action buttons" rule. It lives on the HUD canvas, drawn above
    /// everything else.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        public static ScreenFade Instance { get; private set; }

        private Image _image;
        private float _alpha;
        private float _target;
        private float _speed;

        public float Alpha => _alpha;

        /// <summary>Builds the overlay as the last child of <paramref name="canvas"/>.</summary>
        public static ScreenFade Create(RectTransform canvas)
        {
            var rect = UIFactory.CreateRect("ScreenFade", canvas);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-8f, -8f);
            rect.offsetMax = new Vector2(8f, 8f);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = false;
            image.enabled = false;

            var fade = rect.gameObject.AddComponent<ScreenFade>();
            fade._image = image;
            Instance = fade;
            return fade;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Moves to the given alpha over the given time. Finishes when it gets there.</summary>
        public IEnumerator FadeTo(float alpha, float seconds)
        {
            _target = Mathf.Clamp01(alpha);
            _speed = Mathf.Abs(_target - _alpha) / Mathf.Max(0.01f, seconds);

            while (!Mathf.Approximately(_alpha, _target)) yield return null;
        }

        private void Update()
        {
            if (Mathf.Approximately(_alpha, _target)) return;

            // Unscaled, so a paused game cannot strand the player behind a black screen.
            _alpha = Mathf.MoveTowards(_alpha, _target, _speed * Time.unscaledDeltaTime);
            _image.enabled = _alpha > 0.001f;
            _image.color = new Color(0f, 0f, 0f, _alpha);
        }
    }
}
