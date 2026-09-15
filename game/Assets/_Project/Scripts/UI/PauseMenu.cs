using Tycoon.Core;
using Tycoon.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The pause panel: sound, progress reset, and a way to stop the world while you look at it.
    ///
    /// This is the one place the game is allowed to be a menu. Everything the player *does* to
    /// the farm happens by standing somewhere in the world; this only holds settings and the
    /// destructive option, which has no sensible physical place.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        private const string SoundPrefKey = "tycoon.sound";

        private RectTransform _panel;
        private Text _soundLabel;
        private Text _deleteLabel;
        private GameObject _joystick;

        private bool _open;
        private bool _deleteArmed;
        private float _deleteArmedUntil;

        public bool IsOpen => _open;

        /// <summary>Builds the button and the panel under the HUD's safe area.</summary>
        public static PauseMenu Create(RectTransform safeArea, GameObject joystick)
        {
            var menu = safeArea.gameObject.AddComponent<PauseMenu>();
            menu._joystick = joystick;
            menu.Build(safeArea);
            return menu;
        }

        private void Build(RectTransform safeArea)
        {
            // --- the button that opens it ------------------------------------------------
            var openButton = UIFactory.CreateButton("PauseButton", safeArea, "II",
                new Color(0.08f, 0.13f, 0.19f, 0.8f), 48, out _);
            var buttonRect = openButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(-28f, -28f);
            buttonRect.sizeDelta = new Vector2(110f, 110f);
            openButton.onClick.AddListener(() => SetOpen(true));

            // --- the panel ----------------------------------------------------------------
            var dim = UIFactory.CreatePanel("PauseDim", safeArea, new Color(0.03f, 0.06f, 0.09f, 0.82f));
            _panel = dim.rectTransform;
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.one;
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
            dim.raycastTarget = true; // swallow taps so the world never gets them while paused

            var title = UIFactory.CreateText("Title", _panel, "PAUSED", 84);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.82f);
            title.rectTransform.sizeDelta = new Vector2(700f, 120f);
            title.fontStyle = FontStyle.Bold;

            float y = 0.63f;
            var resume = MakeRow("Resume", "RESUME", new Color(0.98f, 0.83f, 0.35f),
                new Color(0.15f, 0.18f, 0.1f), ref y, out _);
            resume.onClick.AddListener(() => SetOpen(false));

            var sound = MakeRow("Sound", "", new Color(0.25f, 0.45f, 0.62f), Color.white, ref y, out _soundLabel);
            sound.onClick.AddListener(ToggleSound);

            var delete = MakeRow("Delete", "DELETE SAVE", new Color(0.55f, 0.18f, 0.16f), Color.white,
                ref y, out _deleteLabel);
            delete.onClick.AddListener(OnDeletePressed);

            var hint = UIFactory.CreateText("Hint", _panel,
                "Progress saves automatically on this device.", 32);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.16f);
            hint.rectTransform.sizeDelta = new Vector2(820f, 60f);
            hint.color = new Color(1f, 1f, 1f, 0.55f);

            ApplySound(PlayerPrefs.GetInt(SoundPrefKey, 1) == 1);
            _panel.gameObject.SetActive(false);
        }

        private Button MakeRow(string name, string content, Color background, Color textColor,
            ref float y, out Text label)
        {
            var button = UIFactory.CreateButton(name, _panel, content, background, 52, out label);
            label.color = textColor;

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 130f);

            y -= 0.13f;
            return button;
        }

        private void SetOpen(bool open)
        {
            _open = open;
            _panel.gameObject.SetActive(open);

            // Freeze the world. Everything in the game is driven by Time.deltaTime, so this
            // stops production, decay, customers and spoilage together.
            Time.timeScale = open ? 0f : 1f;

            // The joystick sits under the panel and would otherwise keep steering the player.
            if (_joystick != null) _joystick.SetActive(!open);
            PlayerInputSource.Joystick = Vector2.zero;

            if (!open) DisarmDelete();
            if (open) GameRoot.Instance?.Save();
        }

        private void ToggleSound() => ApplySound(!(AudioListener.volume > 0.5f));

        private void ApplySound(bool on)
        {
            AudioListener.volume = on ? 1f : 0f;
            PlayerPrefs.SetInt(SoundPrefKey, on ? 1 : 0);
            PlayerPrefs.Save();
            if (_soundLabel != null) _soundLabel.text = on ? "SOUND: ON" : "SOUND: OFF";
        }

        /// <summary>
        /// Two taps to wipe a save. Deleting progress is the only irreversible thing in the
        /// game, so it does not happen on a single mis-tap.
        /// </summary>
        private void OnDeletePressed()
        {
            if (!_deleteArmed)
            {
                _deleteArmed = true;
                _deleteArmedUntil = Time.unscaledTime + 4f;
                _deleteLabel.text = "TAP AGAIN TO CONFIRM";
                return;
            }

            DisarmDelete();
            // Close before reloading. The HUD survives the scene load, so leaving the menu up
            // means the player is staring at PAUSED over a farm that has already reset.
            SetOpen(false);
            Time.timeScale = 1f;
            GameRoot.Instance?.ResetProgress();
        }

        private void DisarmDelete()
        {
            _deleteArmed = false;
            if (_deleteLabel != null) _deleteLabel.text = "DELETE SAVE";
        }

        private void Update()
        {
            if (_deleteArmed && Time.unscaledTime > _deleteArmedUntil) DisarmDelete();
        }

        private void OnDisable()
        {
            // Never leave the game frozen because the HUD went away.
            if (_open) Time.timeScale = 1f;
        }
    }
}
