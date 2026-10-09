using Tycoon.Audio;
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
        private RectTransform _board;
        private Text _soundLabel;
        private Text _arrowLabel;
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
            // --- the medallion that opens it ----------------------------------------------
            var openButton = UIFactory.CreateMedallionButton("PauseButton", safeArea, 110f);
            var buttonRect = openButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 1f);
            buttonRect.pivot = new Vector2(1f, 1f);
            buttonRect.anchoredPosition = new Vector2(-28f, -28f);
            openButton.onClick.AddListener(() => SetOpen(true));

            // --- the panel ----------------------------------------------------------------
            var dim = UIFactory.CreatePanel("PauseDim", safeArea, new Color(0.03f, 0.06f, 0.09f, 0.82f));
            _panel = dim.rectTransform;
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.one;
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
            dim.raycastTarget = true; // swallow taps so the world never gets them while paused

            // The framed board, centred. 780 x 920 units on a phone; scaled down in SetOpen on a
            // screen too short to hold it.
            _board = UIFactory.CreatePlate("Board", _panel, new Vector2(780f, 1090f), 56f,
                HudArt.Looks.Board, out _);
            _board.anchorMin = _board.anchorMax = new Vector2(0.5f, 0.5f);
            _board.pivot = new Vector2(0.5f, 0.5f);
            _board.anchoredPosition = Vector2.zero;

            // The gold title plate straddling the top edge, like the logo's ribbon.
            var title = UIFactory.CreatePlate("TitlePlate", _board, new Vector2(520f, 124f), 42f,
                HudArt.Looks.Gold, out _);
            title.anchorMin = title.anchorMax = new Vector2(0.5f, 0.5f);
            title.anchoredPosition = new Vector2(0f, 553f);
            var titleText = UIFactory.CreateLabel("Title", title, "PAUSED", 62, TextAnchor.MiddleCenter, 0.14f);
            UIFactory.Stretch(titleText.rectTransform);

            var resume = MakeRow("Resume", "RESUME", HudArt.Looks.Gold, 300f, out _);
            resume.onClick.AddListener(() => SetOpen(false));

            var sound = MakeRow("Sound", "", HudArt.Looks.Blue, 130f, out _soundLabel);
            sound.onClick.AddListener(ToggleSound);

            var arrow = MakeRow("Arrow", "", HudArt.Looks.Green, -40f, out _arrowLabel);
            arrow.onClick.AddListener(ToggleArrow);

            // Red, so the one irreversible button is never mistaken for the others.
            var delete = MakeRow("Delete", "DELETE SAVE", HudArt.Looks.Red, -210f, out _deleteLabel);
            delete.onClick.AddListener(OnDeletePressed);

            var hint = UIFactory.CreateLabel("Hint", _board,
                "Progress saves automatically on this device.", 22);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            hint.rectTransform.anchoredPosition = new Vector2(0f, -375f);
            hint.rectTransform.sizeDelta = new Vector2(700f, 50f);
            hint.color = new Color(1f, 1f, 1f, 0.6f);

            ApplySound(PlayerPrefs.GetInt(SoundPrefKey, 1) == 1);
            _arrowLabel.text = Tycoon.Tasks.GuideArrow.Enabled ? "ARROW: ON" : "ARROW: OFF";
            _panel.gameObject.SetActive(false);
        }

        private Button MakeRow(string name, string content, HudArt.Look look, float y, out Text label)
        {
            var button = UIFactory.CreatePlateButton(name, _board, new Vector2(620f, 130f), 46f, look,
                content, 48, out label);

            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            return button;
        }

        private void SetOpen(bool open)
        {
            _open = open;
            _panel.gameObject.SetActive(open);

            // Fit the board to the screen: full size on a phone, smaller on a short window.
            if (open && _board != null)
            {
                var area = _panel.rect;
                float fit = Mathf.Min(1f, area.height / 1170f, area.width / 840f);
                _board.localScale = new Vector3(fit, fit, 1f);
            }

            // Freeze the world. Everything in the game is driven by Time.deltaTime, so this
            // stops production, decay, customers and spoilage together.
            Time.timeScale = open ? 0f : 1f;

            // The joystick sits under the panel and would otherwise keep steering the player.
            if (_joystick != null) _joystick.SetActive(!open);
            PlayerInputSource.Joystick = Vector2.zero;

            if (!open) DisarmDelete();
            if (open) GameRoot.Instance?.Save();
        }

        private void ToggleSound()
        {
            bool on = !(AudioListener.volume > 0.5f);
            ApplySound(on);

            // The button's own click fired while the sound was still off, so it was silent.
            if (on) SoundFx.Play(Sfx.Click);
        }

        private void ToggleArrow()
        {
            ApplyArrow(!Tycoon.Tasks.GuideArrow.Enabled);
        }

        private void ApplyArrow(bool on)
        {
            Tycoon.Tasks.GuideArrow.Enabled = on;
            if (_arrowLabel != null) _arrowLabel.text = on ? "ARROW: ON" : "ARROW: OFF";
        }

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
                _deleteLabel.text = "TAP TO CONFIRM";
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
