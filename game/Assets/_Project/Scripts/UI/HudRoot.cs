using System.Collections.Generic;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using Tycoon.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The on-screen HUD, built entirely from code and spawned before the first scene loads.
    ///
    /// Doing it this way means a brand new level scene needs no canvas, no event system and no
    /// prefab references - drop in a ground plane and some stations and the game is playable.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    public class HudRoot : MonoBehaviour
    {
        public static HudRoot Instance { get; private set; }

        private Text _moneyLabel;
        private RectTransform _moneyPanel;
        private Text _alertLabel;
        private Canvas _canvas;
        private RectTransform _safeArea;
        private Rect _lastSafeArea;
        private int _lastScreenW, _lastScreenH;
        private float _safeAreaTimer;

        /// <summary>
        /// How often the safe area is re-read. A rotation or a collapsing toolbar can move it at
        /// any time, but it is a browser query, so it is not asked for every frame.
        /// </summary>
        private const float SafeAreaInterval = 0.5f;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Implemented in Plugins/WebGL/SafeArea.jslib. Fills top, right, bottom, left in canvas
        // pixels; returns 0 if the page has no probe element.
        [DllImport("__Internal")]
        private static extern int Tycoon_GetSafeInsets(float[] insets);

        private readonly float[] _insets = new float[4];
#endif

        private float _moneyPulse;
        private readonly List<string> _alerts = new List<string>();

        /// <summary>Seconds between sweeps of the businesses for problems.</summary>
        private const float AlertInterval = 0.25f;

        private float _alertTimer;

        /// <summary>
        /// Corner readout of frame time and player state. Invaluable when the only way to
        /// inspect a Web build is to look at a screenshot of it, so it stays in the code -
        /// flip this to true whenever something needs diagnosing on a real device.
        /// </summary>
        public static bool ShowDebug = false;

        private Text _debugLabel;
        private float _fpsSmoothed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("~HUD");
            DontDestroyOnLoad(go);
            go.AddComponent<HudRoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Append ?debug=1 to the URL to bring the readout back on a real device without
            // rebuilding. Off by default, so players never see it.
            if (!ShowDebug && Application.absoluteURL.Contains("debug=1")) ShowDebug = true;

            EnsureEventSystem();
            BuildCanvas();
            BuildMoneyReadout();
            BuildAlertReadout();
            var joystick = VirtualJoystick.Create(_safeArea, UIFactory.Circle);
            if (ShowDebug) BuildDebugReadout();

            // Built last so the panel sits above the joystick area and swallows its taps.
            PauseMenu.Create(_safeArea, joystick.gameObject);

            // The paint menu opens from a square in the supermarket and is not built until it
            // does. Above the pause panel's button, below the fade.
            PaintMenu.Create(_safeArea, joystick.gameObject);

            // Last, so it covers the whole screen including the pause panel. It is the full
            // canvas rather than the safe area: a fade that stopped short of the notch would
            // leave a strip of the old location showing.
            ScreenFade.Create((RectTransform)_canvas.transform);

            var wallet = GameRoot.Money;
            if (wallet != null)
            {
                wallet.Changed += OnMoneyChanged;
                OnMoneyChanged(wallet.Balance, 0d);
            }
        }

        private void OnDestroy()
        {
            var wallet = GameRoot.Money;
            if (wallet != null) wallet.Changed -= OnMoneyChanged;
            if (Instance == this) Instance = null;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;

            var go = new GameObject("~EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }

        private void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Portrait reference resolution: the game is designed to be held one-handed.
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // Bias toward matching width so the HUD keeps its proportions on tall phones.
            scaler.matchWidthOrHeight = 0.35f;

            _safeArea = UIFactory.CreateRect("SafeArea", (RectTransform)canvasGo.transform);
            _safeArea.anchorMin = Vector2.zero;
            _safeArea.anchorMax = Vector2.one;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
            ApplySafeArea();
        }

        private void BuildMoneyReadout()
        {
            var panel = UIFactory.CreatePanel("MoneyPanel", _safeArea, new Color(0.05f, 0.09f, 0.14f, 0.72f));
            _moneyPanel = panel.rectTransform;
            _moneyPanel.anchorMin = new Vector2(0.5f, 1f);
            _moneyPanel.anchorMax = new Vector2(0.5f, 1f);
            _moneyPanel.pivot = new Vector2(0.5f, 1f);
            _moneyPanel.anchoredPosition = new Vector2(0f, -28f);
            _moneyPanel.sizeDelta = new Vector2(420f, 116f);

            _moneyLabel = UIFactory.CreateText("MoneyLabel", _moneyPanel, "$0", 64);
            _moneyLabel.rectTransform.anchorMin = Vector2.zero;
            _moneyLabel.rectTransform.anchorMax = Vector2.one;
            _moneyLabel.rectTransform.offsetMin = Vector2.zero;
            _moneyLabel.rectTransform.offsetMax = Vector2.zero;
            _moneyLabel.color = new Color(1f, 0.92f, 0.55f);
            _moneyLabel.fontStyle = FontStyle.Bold;
        }

        private void BuildAlertReadout()
        {
            _alertLabel = UIFactory.CreateText("Alerts", _safeArea, "", 40, TextAnchor.UpperLeft);
            var rect = _alertLabel.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(28f, -160f);
            rect.sizeDelta = new Vector2(700f, 300f);
            _alertLabel.color = new Color(1f, 0.6f, 0.45f);
        }

        private void BuildDebugReadout()
        {
            _debugLabel = UIFactory.CreateText("Debug", _safeArea, "", 28, TextAnchor.LowerLeft);
            var rect = _debugLabel.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(24f, 24f);
            rect.sizeDelta = new Vector2(760f, 240f);
            _debugLabel.color = new Color(1f, 1f, 1f, 0.75f);
        }

        private void RefreshDebug()
        {
            if (_debugLabel == null) return;

            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _fpsSmoothed = Mathf.Lerp(_fpsSmoothed <= 0f ? 1f / dt : _fpsSmoothed, 1f / dt, 0.1f);

            var motor = FindFirstObjectByType<Tycoon.Player.PlayerMotor>();
            var carry = motor != null ? motor.GetComponent<Tycoon.Player.CarryStack>() : null;
            var rig = FindFirstObjectByType<Tycoon.Core.IsometricCameraRig>();

            string position = motor != null
                ? $"{motor.transform.position.x:0.0},{motor.transform.position.y:0.0},{motor.transform.position.z:0.0}"
                : "no player";

            string held = carry == null ? "-" : carry.Describe();

            string tracking = rig == null ? "no rig" : (rig.target != null ? "tracking" : "NO TARGET");

            _debugLabel.text =
                $"{_fpsSmoothed:0} fps  dt {Time.deltaTime * 1000f:0}ms\n" +
                $"pos {position}\n" +
                $"carry {held}   cam {tracking}\n" +
                $"safe L{_lastSafeArea.xMin:0} B{_lastSafeArea.yMin:0} R{Screen.width - _lastSafeArea.xMax:0} T{Screen.height - _lastSafeArea.yMax:0}";
        }

        private void OnMoneyChanged(double balance, double delta)
        {
            if (_moneyLabel == null) return;
            _moneyLabel.text = MoneyFormat.Short(balance);
            if (delta > 0d) _moneyPulse = 1f;
        }

        private void Update()
        {
            if (_safeArea != null) RefreshSafeArea();

            if (_moneyPanel != null && _moneyPulse > 0f)
            {
                _moneyPulse = Mathf.Max(0f, _moneyPulse - Time.unscaledDeltaTime * 4f);
                float scale = 1f + 0.07f * _moneyPulse;
                _moneyPanel.localScale = new Vector3(scale, scale, 1f);
            }

            _alertTimer -= Time.unscaledDeltaTime;
            if (_alertTimer <= 0f)
            {
                _alertTimer = AlertInterval;
                RefreshAlerts();
            }

            RefreshDebug();
        }

        /// <summary>
        /// The part of the screen free of the notch, the status bar and the home indicator, in
        /// screen pixels with the origin at the bottom left (the same space as Screen.safeArea).
        ///
        /// In a WebGL build Screen.safeArea is always the whole screen, because Unity cannot see
        /// the browser's insets, so they are read from the page instead - see SafeArea.jslib.
        /// Everywhere else (the editor, a future native build) Screen.safeArea is correct.
        /// </summary>
        private Rect SafeRect()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            float w = Screen.width;
            float h = Screen.height;
            if (w > 0f && h > 0f && Tycoon_GetSafeInsets(_insets) != 0)
            {
                float top = Mathf.Max(0f, _insets[0]);
                float right = Mathf.Max(0f, _insets[1]);
                float bottom = Mathf.Max(0f, _insets[2]);
                float left = Mathf.Max(0f, _insets[3]);

                // Never let a nonsense reading shrink the HUD away to nothing.
                if (left + right < w * 0.5f && top + bottom < h * 0.5f)
                    return new Rect(left, bottom, w - left - right, h - top - bottom);
            }
#endif
            return Screen.safeArea;
        }

        /// <summary>Re-reads the safe area twice a second, or at once if the screen resized.</summary>
        private void RefreshSafeArea()
        {
            _safeAreaTimer -= Time.unscaledDeltaTime;
            bool resized = Screen.width != _lastScreenW || Screen.height != _lastScreenH;
            if (_safeAreaTimer > 0f && !resized) return;

            _safeAreaTimer = SafeAreaInterval;
            if (resized || SafeRect() != _lastSafeArea) ApplySafeArea();
        }

        /// <summary>
        /// Keeps the HUD clear of the notch and the home indicator. Critical on iPhone, where
        /// a joystick drawn under the home bar is a joystick that swipes the app away.
        /// </summary>
        private void ApplySafeArea()
        {
            _lastSafeArea = SafeRect();
            _lastScreenW = Screen.width;
            _lastScreenH = Screen.height;

            float w = Screen.width;
            float h = Screen.height;
            if (w <= 0f || h <= 0f) return;

            Vector2 min = new Vector2(_lastSafeArea.xMin / w, _lastSafeArea.yMin / h);
            Vector2 max = new Vector2(_lastSafeArea.xMax / w, _lastSafeArea.yMax / h);

            _safeArea.anchorMin = min;
            _safeArea.anchorMax = max;
            _safeArea.offsetMin = Vector2.zero;
            _safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>Registered by anything that wants the player's attention.</summary>
        public void ReportAlert(string message)
        {
            if (!string.IsNullOrEmpty(message) && !_alerts.Contains(message)) _alerts.Add(message);
        }

        /// <summary>
        /// Asks every running business whether it needs the player, and redraws the list.
        ///
        /// Polled a few times a second rather than every frame: these are slow conditions - a
        /// jam, a full basket - and composing the lines at frame rate cost more than the whole
        /// rest of the upkeep system put together.
        /// </summary>
        private void RefreshAlerts()
        {
            if (_alertLabel == null) return;

            _alerts.Clear();

            var beacons = Tycoon.Upkeep.AlertBeacon.Active;
            for (int i = 0; i < beacons.Count; i++)
            {
                var beacon = beacons[i];
                if (beacon == null) continue;

                beacon.Refresh();
                ReportAlert(beacon.CurrentAlert);
            }

            string wanted = _alerts.Count == 0 ? "" : string.Join("\n", _alerts);
            if (_alertLabel.text != wanted) _alertLabel.text = wanted;
        }
    }
}
