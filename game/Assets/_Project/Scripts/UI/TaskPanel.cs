using Tycoon.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The task card in the top-left corner: up to three lines of what to do next, each with a
    /// coloured dot, and two small tabs that choose which list the arrow follows.
    ///
    /// Drawn in the same framed style as the money plaque and the pause menu (see
    /// <see cref="HudArt"/>): a dark plaque with a gold rim, outlined white lettering like the
    /// loading-screen tips, the selected tab filled in its colour and the other left dark. A red
    /// disc on the Problems tab carries the count.
    ///
    /// The card only displays what <see cref="TaskBoard"/> hands it, and only redraws when the
    /// board has swept again. The tabs are the one place the HUD has buttons for gameplay, and
    /// they exist because the arrow can only follow one list at a time. During the tutorial
    /// they are hidden and the card shows the green steps instead.
    /// </summary>
    public class TaskPanel : MonoBehaviour
    {
        private const float CardWidth = 600f;
        private const float CardRadius = 34f;
        private const float Pad = 14f;
        private const float TabHeight = 54f;
        private const float TabRadius = 24f;
        private const float RowHeight = 56f;
        private const int FontSize = 34;

        private static readonly Color Green = new Color(0.42f, 0.84f, 0.38f);
        private static readonly Color Red = new Color(0.95f, 0.32f, 0.27f);
        private static readonly Color Gold = new Color(1f, 0.80f, 0.25f);

        private const float TabGap = 10f;
        private const float MinimizeSize = 54f;
        private const string CollapsedPref = "tycoon.taskPanelCollapsed";

        // Both tabs equal, with the minimize button taking the top-right corner of the same row.
        private const float TabWidth = (CardWidth - 2f * Pad - 2f * TabGap - MinimizeSize) * 0.5f;
        private static readonly Vector2 ProblemsSize = new Vector2(TabWidth, TabHeight);
        private static readonly Vector2 GoalsSize = new Vector2(TabWidth, TabHeight);

        private RectTransform _card;
        private Image _cardArt;
        private Button _problemsTab;
        private Button _goalsTab;
        private Image _problemsArt;
        private Image _goalsArt;
        private Text _problemsLabel;
        private Text _goalsLabel;
        private Text _header;
        private RectTransform _badge;
        private Text _badgeLabel;

        // Last look applied, so the sprites are only swapped when something actually changed.
        private int _problemsLook = -1;
        private int _goalsLook = -1;
        private float _cardHeight;

        private Button _minimize;
        private bool _collapsed;

        private readonly Image[] _dots = new Image[TaskBoard.MaxLines];
        private readonly Text[] _lines = new Text[TaskBoard.MaxLines];

        public RectTransform Card => _card;

        public static TaskPanel Create(RectTransform safeArea)
        {
            var panel = safeArea.gameObject.AddComponent<TaskPanel>();
            panel.Build(safeArea);
            return panel;
        }

        private void Build(RectTransform safeArea)
        {
            _cardHeight = 200f;
            _card = UIFactory.CreatePlate("TaskCard", safeArea, new Vector2(CardWidth, _cardHeight), CardRadius,
                HudArt.Looks.Plaque, out _cardArt);
            _card.anchorMin = _card.anchorMax = new Vector2(0f, 1f);
            _card.pivot = new Vector2(0f, 1f);
            // Below the money readout, which ends 144 units down, with a clear gap so the two
            // read as separate pieces, and clear of the pause button.
            _card.anchoredPosition = new Vector2(28f, -196f);

            _problemsTab = MakeTab("ProblemsTab", new Vector2(Pad, -Pad), ProblemsSize,
                out _problemsArt, out _problemsLabel);
            _problemsTab.onClick.AddListener(() => TaskBoard.Instance?.SetFollow(TaskKind.Problem));
            _problemsLabel.text = "Problems";

            // The count, as a red disc pinned to the tab's top-right corner. The disc sits 2.5
            // units above its sprite's centre, so the anchor is nudged down to match.
            var badge = UIFactory.CreateRect("Badge", _problemsTab.GetComponent<RectTransform>());
            _badge = badge;
            badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
            badge.sizeDelta = new Vector2(62f, 62f);
            badge.anchoredPosition = new Vector2(-22f, -8.5f);
            var disc = badge.gameObject.AddComponent<Image>();
            disc.sprite = HudArt.Badge(62f);
            disc.raycastTarget = false;
            _badgeLabel = UIFactory.CreateLabel("Count", badge, "", 26);
            UIFactory.Stretch(_badgeLabel.rectTransform);
            _badgeLabel.rectTransform.anchoredPosition = new Vector2(0f, 2.5f);

            _goalsTab = MakeTab("GoalsTab", new Vector2(Pad + TabWidth + TabGap, -Pad), GoalsSize,
                out _goalsArt, out _goalsLabel);
            _goalsTab.onClick.AddListener(() => TaskBoard.Instance?.SetFollow(TaskKind.Goal));
            _goalsLabel.text = "Goals";

            // Collapse / expand, top-right of the card. Collapsed, the card keeps its tabs (and the
            // problem count) or, in the tutorial, its one green step.
            _collapsed = PlayerPrefs.GetInt(CollapsedPref, 0) == 1;
            _minimize = UIFactory.CreateMedallionButton("MinimizeButton", _card, MinimizeSize,
                _collapsed ? HudArt.Glyph.Plus : HudArt.Glyph.Minus);
            var minRect = _minimize.GetComponent<RectTransform>();
            minRect.anchorMin = minRect.anchorMax = new Vector2(1f, 1f);
            minRect.pivot = new Vector2(1f, 1f);
            minRect.anchoredPosition = new Vector2(-Pad, -Pad);
            _minimize.onClick.AddListener(ToggleCollapsed);

            _header = UIFactory.CreateLabel("Header", _card, "", 30, TextAnchor.MiddleLeft, 0.08f);
            PlaceTopLeft(_header.rectTransform, new Vector2(Pad + 8f, -Pad),
                new Vector2(CardWidth - 2f * Pad - MinimizeSize - TabGap, TabHeight));
            _header.color = Green;

            for (int i = 0; i < TaskBoard.MaxLines; i++)
            {
                float y = -(Pad + TabHeight + 8f + i * RowHeight);

                var dotRect = UIFactory.CreateRect($"Dot{i}", _card);
                PlaceTopLeft(dotRect, new Vector2(Pad + 10f, y - (RowHeight - 26f) * 0.5f), new Vector2(26f, 26f));
                var dot = dotRect.gameObject.AddComponent<Image>();
                dot.sprite = UIFactory.Circle;
                dot.raycastTarget = false;
                // A dark ring, like the dots on the sheet.
                var ring = dotRect.gameObject.AddComponent<Outline>();
                ring.effectColor = new Color(0.10f, 0.078f, 0.063f, 0.9f);
                ring.effectDistance = new Vector2(2.5f, -2.5f);
                _dots[i] = dot;

                var line = UIFactory.CreateLabel($"Line{i}", _card, "", FontSize, TextAnchor.MiddleLeft);
                PlaceTopLeft(line.rectTransform, new Vector2(Pad + 50f, y), new Vector2(CardWidth - Pad * 2f - 50f, RowHeight));
                line.resizeTextForBestFit = true;
                line.resizeTextMinSize = 22;
                line.resizeTextMaxSize = FontSize;
                line.horizontalOverflow = HorizontalWrapMode.Wrap;
                line.verticalOverflow = VerticalWrapMode.Truncate;
                _lines[i] = line;
            }
        }

        private void Start()
        {
            var board = TaskBoard.Instance;
            if (board != null) board.Rebuilt += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            var board = TaskBoard.Instance;
            if (board != null) board.Rebuilt -= Refresh;
        }

        // ---- building blocks --------------------------------------------------------------

        private Button MakeTab(string name, Vector2 topLeft, Vector2 size, out Image art, out Text label)
        {
            var holder = UIFactory.CreatePlate(name, _card, size, TabRadius, HudArt.Looks.Tab, out art, true);
            PlaceTopLeft(holder, topLeft, size);

            label = UIFactory.CreateLabel("Label", holder, "", 30, TextAnchor.MiddleCenter, 0.10f);
            UIFactory.Stretch(label.rectTransform);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 20;
            label.resizeTextMaxSize = 30;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            var button = holder.gameObject.AddComponent<Button>();
            button.targetGraphic = art;
            button.transition = Selectable.Transition.SpriteSwap;
            UIFactory.SkinButton(button, art, size, TabRadius, HudArt.Looks.Tab);
            button.onClick.AddListener(() => Tycoon.Audio.SoundFx.Play(Tycoon.Audio.Sfx.Click));
            holder.gameObject.AddComponent<PressShift>().label = label.rectTransform;
            return button;
        }

        private static void PlaceTopLeft(RectTransform rect, Vector2 topLeft, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
        }

        // ---- drawing ----------------------------------------------------------------------

        private static Color DotColour(TaskKind kind) =>
            kind == TaskKind.Tutorial ? Green : kind == TaskKind.Problem ? Red : Gold;

        private void Refresh()
        {
            var board = TaskBoard.Instance;
            if (board == null) return;

            bool tutorial = board.InTutorial;

            _problemsTab.gameObject.SetActive(!tutorial);
            _goalsTab.gameObject.SetActive(!tutorial);
            _header.gameObject.SetActive(tutorial && !_collapsed);

            if (tutorial)
                SetText(_header, $"FIRST STEPS  {Mathf.Min(board.TutorialStep + 1, TutorialProgress.StepCount)}/{TutorialProgress.StepCount}");
            else
                DrawTabs(board);

            var shown = board.Shown;
            int rows = Mathf.Min(shown.Count, TaskBoard.MaxLines);

            // What is on show. Collapsed: nothing under the tabs, or just the single green step
            // in the tutorial (which has no tabs to keep).
            int visibleRows = _collapsed ? (tutorial ? 1 : 0) : Mathf.Max(1, rows);

            // The lines normally sit under the tab row. A collapsed tutorial has no tab row, so its
            // one step moves up beside the minimize button instead.
            bool beside = tutorial && _collapsed;
            float top = beside ? Pad : Pad + TabHeight + 8f;
            float lineWidth = CardWidth - Pad * 2f - 50f - (beside ? MinimizeSize + TabGap : 0f);

            for (int i = 0; i < TaskBoard.MaxLines; i++)
            {
                float y = -(top + i * RowHeight);
                _dots[i].rectTransform.anchoredPosition = new Vector2(Pad + 10f, y - (RowHeight - 26f) * 0.5f);
                _lines[i].rectTransform.anchoredPosition = new Vector2(Pad + 50f, y);
                _lines[i].rectTransform.sizeDelta = new Vector2(lineWidth, RowHeight);

                bool used = i < visibleRows && (i < rows || (i == 0 && rows == 0));
                _dots[i].gameObject.SetActive(used);
                _lines[i].gameObject.SetActive(used);
                if (!used) continue;

                if (i < rows)
                {
                    var item = shown[i];
                    float alpha = item.dim ? 0.5f : 1f;

                    var dot = DotColour(item.kind);
                    dot.a = alpha;
                    _dots[i].color = dot;

                    SetText(_lines[i], item.label);
                    _lines[i].color = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    // Nothing on this list: say so, rather than leave an empty card.
                    _dots[i].color = new Color(0.7f, 0.7f, 0.7f, 0.6f);
                    SetText(_lines[i], board.Followed == TaskKind.Problem ? "All clear!" : "Nothing to buy right now");
                    _lines[i].color = new Color(1f, 1f, 1f, 0.6f);
                }
            }

            float height = visibleRows == 0
                ? Pad + TabHeight + Pad
                : top + visibleRows * RowHeight + Pad;
            if (!Mathf.Approximately(_cardHeight, height))
            {
                _cardHeight = height;
                _card.sizeDelta = new Vector2(CardWidth, height);
                UIFactory.SetPlate(_cardArt, _card.sizeDelta, CardRadius, HudArt.Looks.Plaque);
            }
        }

        private void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            PlayerPrefs.SetInt(CollapsedPref, _collapsed ? 1 : 0);
            PlayerPrefs.Save();
            UIFactory.SetGlyph(_minimize, MinimizeSize, _collapsed ? HudArt.Glyph.Plus : HudArt.Glyph.Minus);
            Refresh();
        }

        private void DrawTabs(TaskBoard board)
        {
            int problems = board.ProblemCount;
            _badge.gameObject.SetActive(problems > 0);
            if (problems > 0) SetText(_badgeLabel, problems > 9 ? "9+" : problems.ToString());

            bool onProblems = board.Followed == TaskKind.Problem;
            StyleTab(_problemsTab, _problemsArt, _problemsLabel, ProblemsSize, ref _problemsLook,
                HudArt.Looks.Red, Red, onProblems, problems > 0);
            StyleTab(_goalsTab, _goalsArt, _goalsLabel, GoalsSize, ref _goalsLook,
                HudArt.Looks.Gold, Gold, !onProblems, true);
        }

        /// <summary>
        /// The tab the arrow follows is filled in its colour with white lettering; the other is
        /// the dark tab with coloured lettering. A problems tab with nothing in it goes quiet.
        /// </summary>
        private static void StyleTab(Button button, Image art, Text label, Vector2 size, ref int applied,
            HudArt.Look coloured, Color text, bool selected, bool lively)
        {
            int want = selected ? 1 : 0;
            if (applied != want)
            {
                applied = want;
                UIFactory.SkinButton(button, art, size, TabRadius, selected ? coloured : HudArt.Looks.Tab);
            }

            Color c = selected ? Color.white : text;
            if (!lively) c.a = 0.55f;
            label.color = c;
        }

        private static void SetText(Text text, string value)
        {
            if (text.text != value) text.text = value;
        }
    }
}
