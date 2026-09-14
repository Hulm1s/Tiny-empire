using System.Collections.Generic;
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
            rect.sizeDelta = new Vector2(760f, 200f);
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
                $"carry {held}   cam {tracking}";
        }

        private void OnMoneyChanged(double balance, double delta)
        {
            if (_moneyLabel == null) return;
            _moneyLabel.text = MoneyFormat.Short(balance);
            if (delta > 0d) _moneyPulse = 1f;
        }

        private void Update()
        {
            if (_safeArea != null && Screen.safeArea != _lastSafeArea) ApplySafeArea();

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
        /// Keeps the HUD clear of the notch and the home indicator. Critical on iPhone, where
        /// a joystick drawn under the home bar is a joystick that swipes the app away.
        /// </summary>
        private void ApplySafeArea()
        {
            _lastSafeArea = Screen.safeArea;

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
