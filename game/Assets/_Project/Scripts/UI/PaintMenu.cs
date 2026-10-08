using System;
using System.Collections;
using Tycoon.Core;
using Tycoon.Player;
using Tycoon.Stations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The paint menu: wall colours, wall pattern and floor, with a live preview.
    ///
    /// The second place in the game that is a menu rather than a square, and for the same reason
    /// as the pause panel - nothing physical stands for "this exact shade of green". Getting
    /// here is still a physical act: it opens when the player has stood in the PAINT square
    /// (<see cref="PaintStation"/>), and closing it leaves them standing in the shop. While it is
    /// open the world is frozen and the joystick is gone, exactly as for the pause panel.
    ///
    /// The shop itself is the preview. Every change is shown on the real walls and floor behind
    /// the panel straight away (<see cref="MarketDecor.Preview"/>) and thrown away by Cancel.
    /// Money is only taken on Confirm.
    ///
    /// Built from code like the rest of the HUD, and lazily: nothing is made until the first
    /// time it opens, because most players will never see it before the market exists.
    /// </summary>
    public class PaintMenu : MonoBehaviour
    {
        public static PaintMenu Instance { get; private set; }

        // Layout, in reference pixels of the 1080 x 1920 canvas. The card is a fixed size and
        // is scaled down to fit when the screen is smaller than that, which a landscape
        // browser window is.
        private const float CardWidth = 960f;
        private const float CardHeight = 1780f;

        private static readonly Color Dim = new Color(0.03f, 0.06f, 0.09f, 0.78f);
        private static readonly Color CardColour = new Color(0.09f, 0.14f, 0.2f, 0.98f);
        private static readonly Color Well = new Color(0.14f, 0.2f, 0.28f, 1f);
        private static readonly Color Accent = new Color(0.98f, 0.83f, 0.35f);
        private static readonly Color Ink = new Color(0.15f, 0.18f, 0.1f);
        private static readonly Color Quiet = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color Bad = new Color(0.95f, 0.45f, 0.4f);
        private static readonly Color Disabled = new Color(0.36f, 0.4f, 0.45f);

        private RectTransform _safeArea;
        private GameObject _joystick;
        private RectTransform _root;
        private RectTransform _card;
        private bool _built;
        private bool _open;

        private PaintStation _station;
        private MarketDecor _decor;
        private DecorLook _original;
        private DecorLook _edit;

        // Tabs.
        private RectTransform[] _tabs;
        private Image[] _tabButtons;
        private int _tab;

        // Colours tab.
        private ColourPicker _primary, _secondary, _tertiary;

        // Patterns tab.
        private Image[] _patternBorders;
        private Texture2D[] _thumbs;
        private bool[] _thumbDirty;
        private Text _hint;
        private RawImage _strip;
        private Texture2D _stripTexture;
        private Coroutine _baking;

        // Floor tab.
        private Image[] _floorButtons;
        private RawImage _floorBig;
        private ColourPicker _floorPicker;
        private RectTransform _woodRow;
        private Image[] _woodSwatches;

        // Bottom strip.
        private RawImage _cube;
        private Texture2D _cubeTexture;
        private RawImage _floorSmall;
        private Texture2D _floorTexture;
        private Text _status;
        private Button _confirm;
        private Image _confirmImage;
        private Text _confirmLabel;

        private Color32[] _wallPixels;
        private bool _previewDirty;
        private bool _floorDirty;

        public bool IsOpen => _open;

        public static PaintMenu Create(RectTransform safeArea, GameObject joystick)
        {
            var menu = safeArea.gameObject.AddComponent<PaintMenu>();
            menu._safeArea = safeArea;
            menu._joystick = joystick;
            Instance = menu;
            return menu;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnDisable()
        {
            // Never leave the game frozen because the HUD went away.
            if (_open) Time.timeScale = 1f;
        }

        // ================================================================ opening and closing

        public void Open(PaintStation station)
        {
            if (_open || station == null || station.decor == null) return;

            EnsureBuilt();

            _station = station;
            _decor = station.decor;
            _original = _decor.Look;
            _edit = _original;

            _wallPixels = _wallPixels ?? new Color32[PatternFactory.Size * PatternFactory.Size];

            SyncControls();
            SelectTab(0);
            _previewDirty = true;
            _floorDirty = true;
            for (int i = 0; i < _thumbDirty.Length; i++) _thumbDirty[i] = true;

            SetOpen(true);
            ApplyPreview();
        }

        private void Close(bool keepLook)
        {
            if (!keepLook && _decor != null) _decor.CancelPreview();
            if (_baking != null) { StopCoroutine(_baking); _baking = null; }

            SetOpen(false);
            _station = null;
            _decor = null;
        }

        private void SetOpen(bool open)
        {
            _open = open;
            _root.gameObject.SetActive(open);

            // Freeze the world, as the pause panel does: the shop must not change under a
            // player who is choosing colours.
            Time.timeScale = open ? 0f : 1f;

            if (_joystick != null) _joystick.SetActive(!open);
            PlayerInputSource.Joystick = Vector2.zero;
        }

        private void OnCancel() => Close(keepLook: false);

        private void OnConfirm()
        {
            if (_station == null || _decor == null) return;

            bool changed = !_decor.HasPainted || !_edit.SameAs(_original);
            if (!changed) { Close(keepLook: false); return; }

            double cost = _station.CurrentPrice;
            var wallet = GameRoot.Money;
            if (wallet == null || !wallet.CanAfford(cost)) return;

            wallet.TrySpend(cost);
            _decor.Commit(_edit);

            Vector3 at = _station.transform.position + Vector3.up * 2f;
            Close(keepLook: true);

            WorldFeedback.Show(at, "PAINTED!", new Color(1f, 0.85f, 0.35f));
            GameRoot.Instance?.Save();
        }

        // ================================================================ building the UI

        private void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            var dim = UIFactory.CreatePanel("PaintDim", _safeArea, Dim);
            _root = dim.rectTransform;
            Stretch(_root);
            // Swallows every tap so the world and the pause button never get them.
            dim.raycastTarget = true;

            var card = UIFactory.CreatePanel("PaintCard", _root, CardColour);
            _card = card.rectTransform;
            _card.anchorMin = _card.anchorMax = new Vector2(0.5f, 0.5f);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(CardWidth, CardHeight);
            card.raycastTarget = true;

            var title = Label(_card, "PAINT & DECORATE", 62, 0f, 22f, CardWidth, 90f, Color.white);
            title.fontStyle = FontStyle.Bold;

            BuildTabs();
            BuildColoursTab();
            BuildPatternsTab();
            BuildFloorTab();
            BuildBottom();

            _root.gameObject.SetActive(false);
        }

        private void BuildTabs()
        {
            string[] names = { "COLOURS", "PATTERNS", "FLOOR" };
            _tabs = new RectTransform[3];
            _tabButtons = new Image[3];

            const float tabY = 126f, tabW = 300f, tabH = 100f, gap = 20f;
            float x0 = (CardWidth - (tabW * 3 + gap * 2)) * 0.5f;

            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var button = UIFactory.CreateButton("Tab" + names[i], _card, names[i], Well, 44, out _);
                Place(button.GetComponent<RectTransform>(), x0 + i * (tabW + gap), tabY, tabW, tabH);
                button.onClick.AddListener(() => SelectTab(index));
                _tabButtons[i] = button.GetComponent<Image>();

                var content = UIFactory.CreateRect("Content" + names[i], _card);
                Place(content, 0f, 250f, CardWidth, 1010f);
                _tabs[i] = content;
            }
        }

        private void SelectTab(int index)
        {
            _tab = index;
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].gameObject.SetActive(i == index);
                _tabButtons[i].color = i == index ? Accent : Well;
                var label = _tabButtons[i].GetComponentInChildren<Text>();
                if (label != null) label.color = i == index ? Ink : Color.white;
            }

            if (index == 1) StartBaking();
            if (index == 1 || index == 2) _previewDirty = true;
        }

        // ---------------------------------------------------------------- colours

        private void BuildColoursTab()
        {
            var tab = _tabs[0];
            _primary = ColourPicker.Create(tab, "PRIMARY", "The main wall colour", 10f, 0f);
            _secondary = ColourPicker.Create(tab, "SECONDARY", "The second wall colour", 10f, 335f);
            _tertiary = ColourPicker.Create(tab, "TERTIARY", "Lines, rail and skirting", 10f, 670f);

            _primary.Changed += () => { _edit.primary = _primary.Value; Touched(); };
            _secondary.Changed += () => { _edit.secondary = _secondary.Value; Touched(); };
            _tertiary.Changed += () => { _edit.tertiary = _tertiary.Value; Touched(); };
        }

        // ---------------------------------------------------------------- patterns

        private void BuildPatternsTab()
        {
            var tab = _tabs[1];

            const int columns = 5;
            const float cell = 176f, gap = 14f, cellH = 240f;
            float x0 = (CardWidth - (columns * cell + (columns - 1) * gap)) * 0.5f;

            _patternBorders = new Image[PatternFactory.PatternCount];
            _thumbs = new Texture2D[PatternFactory.PatternCount];
            _thumbDirty = new bool[PatternFactory.PatternCount];

            for (int p = 0; p < PatternFactory.PatternCount; p++)
            {
                int pattern = p;
                int column = p % columns, row = p / columns;
                float x = x0 + column * (cell + gap);
                float y = 6f + row * (cellH + 6f);

                var button = UIFactory.CreateButton("Pattern" + p, tab, "", Well, 20, out _);
                var rect = button.GetComponent<RectTransform>();
                Place(rect, x, y, cell, cell);
                _patternBorders[p] = button.GetComponent<Image>();
                button.onClick.AddListener(() => ChoosePattern(pattern));

                _thumbs[p] = new Texture2D(64, 64, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };

                var inner = UIFactory.CreateRect("Thumb", rect);
                Place(inner, 10f, 10f, cell - 20f, cell - 20f);
                var raw = inner.gameObject.AddComponent<RawImage>();
                raw.texture = _thumbs[p];
                raw.raycastTarget = false;

                var name = Label(tab, PatternFactory.PatternNames[p], 26, x - 6f, y + cell + 2f,
                    cell + 12f, 48f, Color.white);
                name.fontStyle = FontStyle.Bold;
            }

            _hint = Label(tab, "", 30, 20f, 508f, CardWidth - 40f, 44f, Quiet);

            // The wall, two repeats wide, big enough to judge a pattern by.
            var stripRect = UIFactory.CreateRect("WallStrip", tab);
            Place(stripRect, 30f, 566f, CardWidth - 60f, 420f);
            _strip = stripRect.gameObject.AddComponent<RawImage>();
            _strip.raycastTarget = false;
            _stripTexture = PatternFactory.NewTexture("PaintStrip");
            _strip.texture = _stripTexture;
        }

        private void ChoosePattern(int pattern)
        {
            _edit.pattern = pattern;
            Touched();
            RefreshPatternSelection();
        }

        private void RefreshPatternSelection()
        {
            for (int p = 0; p < _patternBorders.Length; p++)
                _patternBorders[p].color = p == _edit.pattern ? Accent : Well;

            _hint.text = PatternFactory.PatternHints[Mathf.Clamp(_edit.pattern, 0, PatternFactory.PatternCount - 1)];
        }

        /// <summary>
        /// Draws every pattern's map once, one a frame. A pattern is drawn the first time it is
        /// wanted and costs about the same as a frame of the whole game, so doing all ten at
        /// once would be a visible stall in a single-threaded web build.
        /// </summary>
        private void StartBaking()
        {
            if (_baking != null) return;
            bool anyMissing = false;
            for (int p = 0; p < PatternFactory.PatternCount; p++)
                if (!PatternFactory.IsWallMapReady(p)) anyMissing = true;
            if (anyMissing) _baking = StartCoroutine(BakeAll());
        }

        private IEnumerator BakeAll()
        {
            for (int p = 0; p < PatternFactory.PatternCount; p++)
            {
                if (!PatternFactory.IsWallMapReady(p))
                {
                    PatternFactory.WallMap(p);
                    _thumbDirty[p] = true;
                    yield return null;
                }
            }
            _baking = null;
        }

        // ---------------------------------------------------------------- floor

        private void BuildFloorTab()
        {
            var tab = _tabs[2];

            _floorButtons = new Image[3];
            const float w = 296f, gap = 20f;
            float x0 = (CardWidth - (w * 3 + gap * 2)) * 0.5f;
            for (int k = 0; k < 3; k++)
            {
                int kind = k;
                var button = UIFactory.CreateButton("Floor" + PatternFactory.FloorNames[k], tab,
                    PatternFactory.FloorNames[k], Well, 44, out _);
                Place(button.GetComponent<RectTransform>(), x0 + k * (w + gap), 10f, w, 110f);
                button.onClick.AddListener(() => ChooseFloor(kind));
                _floorButtons[k] = button.GetComponent<Image>();
            }

            var swatch = UIFactory.CreateRect("FloorSwatch", tab);
            Place(swatch, 120f, 140f, CardWidth - 240f, 330f);
            _floorBig = swatch.gameObject.AddComponent<RawImage>();
            _floorBig.raycastTarget = false;

            _floorPicker = ColourPicker.Create(tab, "FLOOR COLOUR", "Tiles and solid floors", 10f, 500f);
            _floorPicker.Changed += () => { _edit.floorColor = _floorPicker.Value; Touched(); _floorDirty = true; };

            // Wood is a short run of browns rather than a free choice: pale oak to walnut.
            var row = UIFactory.CreateRect("WoodRow", tab);
            _woodRow = row;
            Place(row, 0f, 500f, CardWidth, 330f);

            Label(row, "WOOD SHADE", 40, 20f, 20f, 400f, 52f, Color.white).alignment = TextAnchor.MiddleLeft;
            Label(row, "Light oak to walnut", 28, 20f, 72f, 500f, 40f, Quiet).alignment = TextAnchor.MiddleLeft;

            const int swatches = 6;
            _woodSwatches = new Image[swatches];
            const float size = 128f, spacing = 22f;
            float sx = (CardWidth - (size * swatches + spacing * (swatches - 1))) * 0.5f;
            for (int i = 0; i < swatches; i++)
            {
                int index = i;
                float t = i / (float)(swatches - 1);
                var button = UIFactory.CreateButton("Wood" + i, row, "", Well, 20, out _);
                var rect = button.GetComponent<RectTransform>();
                Place(rect, sx + i * (size + spacing), 150f, size, size);
                _woodSwatches[i] = button.GetComponent<Image>();
                button.onClick.AddListener(() => ChooseWood(index / (float)(swatches - 1)));

                var inner = UIFactory.CreatePanel("Shade", rect, PatternFactory.WoodColor(t));
                Place(inner.rectTransform, 12f, 12f, size - 24f, size - 24f);
            }
        }

        private void ChooseFloor(int kind)
        {
            _edit.floorKind = kind;
            Touched();
            _floorDirty = true;
            RefreshFloorControls();
        }

        private void ChooseWood(float t)
        {
            _edit.woodShade = t;
            Touched();
            _floorDirty = true;
            RefreshFloorControls();
        }

        private void RefreshFloorControls()
        {
            for (int k = 0; k < _floorButtons.Length; k++)
            {
                _floorButtons[k].color = k == _edit.floorKind ? Accent : Well;
                var label = _floorButtons[k].GetComponentInChildren<Text>();
                if (label != null) label.color = k == _edit.floorKind ? Ink : Color.white;
            }

            bool wood = _edit.floorKind == PatternFactory.FloorWood;
            _woodRow.gameObject.SetActive(wood);
            _floorPicker.SetVisible(!wood);

            int nearest = Mathf.RoundToInt(_edit.woodShade * (_woodSwatches.Length - 1));
            for (int i = 0; i < _woodSwatches.Length; i++)
                _woodSwatches[i].color = i == nearest ? Accent : Well;
        }

        // ---------------------------------------------------------------- the bottom strip

        private void BuildBottom()
        {
            // The wall as a cube and the floor as a swatch, on every tab.
            var cube = UIFactory.CreateRect("CubePreview", _card);
            Place(cube, 90f, 1270f, 280f, 280f);
            _cube = cube.gameObject.AddComponent<RawImage>();
            _cube.raycastTarget = false;
            _cubeTexture = new Texture2D(160, 160, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            _cube.texture = _cubeTexture;

            Label(_card, "WALL", 30, 90f, 1548f, 280f, 40f, Quiet);

            var floor = UIFactory.CreateRect("FloorPreview", _card);
            Place(floor, 470f, 1290f, 240f, 240f);
            _floorSmall = floor.gameObject.AddComponent<RawImage>();
            _floorSmall.raycastTarget = false;
            _floorTexture = PatternFactory.NewTexture("PaintFloor");

            Label(_card, "FLOOR", 30, 450f, 1548f, 280f, 40f, Quiet);

            _status = Label(_card, "", 34, 20f, 1596f, CardWidth - 40f, 60f, Color.white);
            _status.fontStyle = FontStyle.Bold;

            var cancel = UIFactory.CreateButton("Cancel", _card, "CANCEL", new Color(0.3f, 0.36f, 0.43f), 52, out _);
            Place(cancel.GetComponent<RectTransform>(), 30f, 1664f, 430f, 96f);
            cancel.onClick.AddListener(OnCancel);

            _confirm = UIFactory.CreateButton("Confirm", _card, "CONFIRM", Accent, 52, out _confirmLabel);
            Place(_confirm.GetComponent<RectTransform>(), CardWidth - 460f, 1664f, 430f, 96f);
            _confirm.onClick.AddListener(OnConfirm);
            _confirmImage = _confirm.GetComponent<Image>();
            _confirmLabel.color = Ink;
        }

        // ================================================================ keeping it in step

        /// <summary>The controls take their values from the look being edited.</summary>
        private void SyncControls()
        {
            _primary.Set(_edit.primary);
            _secondary.Set(_edit.secondary);
            _tertiary.Set(_edit.tertiary);
            _floorPicker.Set(_edit.floorColor);
            RefreshPatternSelection();
            RefreshFloorControls();
        }

        /// <summary>Something in the look changed; redraw what shows it on the next frame.</summary>
        private void Touched()
        {
            _previewDirty = true;
            for (int i = 0; i < _thumbDirty.Length; i++) _thumbDirty[i] = true;
        }

        private void Update()
        {
            if (!_open) return;

            FitCard();

            if (_previewDirty)
            {
                _previewDirty = false;
                ApplyPreview();
            }

            if (_floorDirty)
            {
                _floorDirty = false;
                RedrawFloor();
            }

            if (_tab == 1) RefreshThumbs();
            RefreshStatus();
        }

        private void FitCard()
        {
            float fit = Mathf.Min(1f, _root.rect.height / (CardHeight + 30f),
                                  _root.rect.width / (CardWidth + 10f));
            if (fit > 0f && !Mathf.Approximately(_card.localScale.x, fit))
                _card.localScale = Vector3.one * fit;
        }

        /// <summary>Redraws the wall pixels, the cube, the big strip and the real walls behind the panel.</summary>
        private void ApplyPreview()
        {
            _decor.Preview(_edit);

            PatternFactory.RecolorWall(_edit.pattern, _edit.primary, _edit.secondary, _edit.tertiary,
                _wallPixels);

            bool banded = PatternFactory.IsBanded(_edit.pattern);
            PatternFactory.PaintCube(_wallPixels, banded, _cubeTexture);

            if (_tab == 1)
            {
                _stripTexture.SetPixels32(_wallPixels);
                _stripTexture.Apply(true, false);

                // Two repeats across; a banded wall is a whole wall tall, so only the foot of
                // it shows, up past the rail.
                float tall = banded ? 1.5f / PatternFactory.BandedHeight : 1.5f / PatternFactory.TileMeters;
                _strip.uvRect = new Rect(0f, 0f, 2f, tall);
            }
        }

        private void RedrawFloor()
        {
            Color tint = _edit.floorKind == PatternFactory.FloorWood
                ? PatternFactory.WoodColor(_edit.woodShade)
                : _edit.floorColor;

            if (_edit.floorKind == PatternFactory.FloorSolid)
            {
                _floorBig.texture = Texture2D.whiteTexture;
                _floorSmall.texture = Texture2D.whiteTexture;
                _floorBig.color = tint;
                _floorSmall.color = tint;
                _floorBig.uvRect = _floorSmall.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
            else
            {
                PatternFactory.PaintFloor(_edit.floorKind, tint, _floorTexture);
                _floorBig.texture = _floorTexture;
                _floorSmall.texture = _floorTexture;
                _floorBig.color = Color.white;
                _floorSmall.color = Color.white;
                // The big one is twice as wide as it is tall, so two repeats across and one up.
                _floorBig.uvRect = new Rect(0f, 0f, 2f, 1f);
                _floorSmall.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
        }

        private int _thumbCursor;

        /// <summary>A couple of thumbnails a frame, so a colour drag never waits on all ten.</summary>
        private void RefreshThumbs()
        {
            int budget = 2;
            for (int n = 0; n < PatternFactory.PatternCount && budget > 0; n++)
            {
                int p = (_thumbCursor + n) % PatternFactory.PatternCount;
                if (!_thumbDirty[p] || !PatternFactory.IsWallMapReady(p)) continue;

                PatternFactory.PaintThumbnail(p, _edit.primary, _edit.secondary, _edit.tertiary, _thumbs[p]);
                _thumbDirty[p] = false;
                budget--;
                _thumbCursor = (p + 1) % PatternFactory.PatternCount;
            }
        }

        private void RefreshStatus()
        {
            if (_station == null || _decor == null) return;

            bool first = !_decor.HasPainted;
            bool changed = first || !_edit.SameAs(_original);
            double cost = _station.CurrentPrice;
            var wallet = GameRoot.Money;
            bool affordable = wallet != null && wallet.CanAfford(cost);

            string what = first ? "FIRST PAINT JOB" : "REPAINT";

            if (!changed)
            {
                _status.text = "Change something to repaint";
                _status.color = Quiet;
            }
            else if (!affordable)
            {
                _status.text = $"{what} {MoneyFormat.Short(cost)}  -  NOT ENOUGH MONEY";
                _status.color = Bad;
            }
            else
            {
                _status.text = $"{what}  {MoneyFormat.Short(cost)}";
                _status.color = Color.white;
            }

            bool ok = changed && affordable;
            _confirm.interactable = ok;
            _confirmImage.color = ok ? Accent : Disabled;
            _confirmLabel.text = changed ? "CONFIRM " + MoneyFormat.Short(cost) : "NO CHANGE";
            _confirmLabel.color = ok ? Ink : new Color(1f, 1f, 1f, 0.55f);
        }

        // ================================================================ small helpers

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Positions a rect by its top-left corner, measured down from the top of its parent.</summary>
        internal static RectTransform Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        internal static Text Label(RectTransform parent, string content, int size, float x, float y,
            float w, float h, Color colour)
        {
            var text = UIFactory.CreateText("Text", parent, content, size);
            Place(text.rectTransform, x, y, w, h);
            text.color = colour;
            return text;
        }

        // ================================================================ the colour picker

        /// <summary>
        /// One full colour picker: a saturation / value square, a hue strip beside it, and a
        /// swatch of the result. Both controls are drawn from code and read the finger's
        /// position directly, so they work the same under a mouse and a thumb.
        ///
        /// Held as hue, saturation and value rather than RGB, because that is what makes the
        /// square and the strip behave: dragging the hue strip must not lose the saturation.
        /// </summary>
        private class ColourPicker
        {
            public event Action Changed;

            private float _h, _s, _v;
            private RectTransform _container;
            private RectTransform _svRect, _hueRect, _svCursor, _hueCursor;
            private Image _swatch, _svCursorFill, _hueCursorFill;
            private Texture2D _svTexture, _hueTexture;
            private const int SvSize = 56;

            public Color Value => Color.HSVToRGB(_h, _s, _v);

            public static ColourPicker Create(RectTransform parent, string title, string hint, float x, float y)
            {
                var picker = new ColourPicker();
                picker.Build(parent, title, hint, x, y);
                return picker;
            }

            public void SetVisible(bool visible) => _container.gameObject.SetActive(visible);

            public void Set(Color colour)
            {
                Color.RGBToHSV(colour, out _h, out _s, out _v);
                RedrawSquare();
                Refresh();
            }

            private void Build(RectTransform parent, string title, string hint, float x, float y)
            {
                _container = UIFactory.CreateRect("Picker" + title, parent);
                Place(_container, x, y, 940f, 320f);

                var name = Label(_container, title, 40, 0f, 6f, 280f, 52f, Color.white);
                name.fontStyle = FontStyle.Bold;
                name.alignment = TextAnchor.MiddleLeft;

                var sub = Label(_container, hint, 26, 0f, 58f, 280f, 70f, Quiet);
                sub.alignment = TextAnchor.UpperLeft;
                sub.horizontalOverflow = HorizontalWrapMode.Wrap;

                var swatchFrame = UIFactory.CreatePanel("SwatchFrame", _container, Color.white);
                Place(swatchFrame.rectTransform, 0f, 150f, 250f, 140f);
                _swatch = UIFactory.CreatePanel("Swatch", swatchFrame.rectTransform, Color.red);
                Place(_swatch.rectTransform, 6f, 6f, 238f, 128f);

                // The saturation / value square.
                _svTexture = new Texture2D(SvSize, SvSize, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                _svRect = UIFactory.CreateRect("Square", _container);
                Place(_svRect, 290f, 10f, 450f, 300f);
                var svImage = _svRect.gameObject.AddComponent<RawImage>();
                svImage.texture = _svTexture;
                svImage.raycastTarget = true;
                var svArea = _svRect.gameObject.AddComponent<DragArea>();
                svArea.Moved += n => { _s = n.x; _v = n.y; Refresh(); Changed?.Invoke(); };

                _svCursor = Cursor(_svRect, 46f, out _svCursorFill, circle: true);

                // The hue strip, vertical.
                _hueTexture = new Texture2D(2, 96, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                var pixels = new Color32[2 * 96];
                for (int j = 0; j < 96; j++)
                {
                    Color c = Color.HSVToRGB(j / 95f, 1f, 1f);
                    pixels[j * 2] = pixels[j * 2 + 1] = c;
                }
                _hueTexture.SetPixels32(pixels);
                _hueTexture.Apply(false, false);

                _hueRect = UIFactory.CreateRect("Hue", _container);
                Place(_hueRect, 780f, 10f, 110f, 300f);
                var hueImage = _hueRect.gameObject.AddComponent<RawImage>();
                hueImage.texture = _hueTexture;
                hueImage.raycastTarget = true;
                var hueArea = _hueRect.gameObject.AddComponent<DragArea>();
                hueArea.Moved += n => { _h = Mathf.Clamp(n.y, 0f, 0.9999f); RedrawSquare(); Refresh(); Changed?.Invoke(); };

                _hueCursor = Cursor(_hueRect, 0f, out _hueCursorFill, circle: false);
            }

            /// <summary>A marker: a white ring round the colour it is sitting on.</summary>
            private static RectTransform Cursor(RectTransform parent, float size, out Image fill, bool circle)
            {
                var outer = UIFactory.CreateRect("Cursor", parent);
                outer.anchorMin = outer.anchorMax = new Vector2(0f, 0f);
                outer.pivot = new Vector2(0.5f, 0.5f);

                Image ring;
                if (circle)
                {
                    outer.sizeDelta = new Vector2(size, size);
                    ring = outer.gameObject.AddComponent<Image>();
                    ring.sprite = UIFactory.Circle;
                    ring.color = Color.white;
                    var shadow = UIFactory.CreateRect("Edge", outer);
                    shadow.anchorMin = Vector2.zero; shadow.anchorMax = Vector2.one;
                    shadow.offsetMin = new Vector2(3f, 3f); shadow.offsetMax = new Vector2(-3f, -3f);
                    var edge = shadow.gameObject.AddComponent<Image>();
                    edge.sprite = UIFactory.Circle;
                    edge.color = new Color(0f, 0f, 0f, 0.55f);
                    edge.raycastTarget = false;
                    var inner = UIFactory.CreateRect("Fill", outer);
                    inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
                    inner.offsetMin = new Vector2(6f, 6f); inner.offsetMax = new Vector2(-6f, -6f);
                    fill = inner.gameObject.AddComponent<Image>();
                    fill.sprite = UIFactory.Circle;
                }
                else
                {
                    outer.anchorMin = new Vector2(0f, 0f);
                    outer.anchorMax = new Vector2(1f, 0f);
                    outer.sizeDelta = new Vector2(16f, 30f);
                    ring = outer.gameObject.AddComponent<Image>();
                    ring.sprite = UIFactory.RoundedBox;
                    ring.type = Image.Type.Sliced;
                    ring.color = Color.white;
                    var inner = UIFactory.CreateRect("Fill", outer);
                    inner.anchorMin = Vector2.zero; inner.anchorMax = Vector2.one;
                    inner.offsetMin = new Vector2(5f, 5f); inner.offsetMax = new Vector2(-5f, -5f);
                    fill = inner.gameObject.AddComponent<Image>();
                    fill.sprite = UIFactory.RoundedBox;
                    fill.type = Image.Type.Sliced;
                }

                ring.raycastTarget = false;
                fill.raycastTarget = false;
                return outer;
            }

            private void RedrawSquare()
            {
                var pixels = new Color32[SvSize * SvSize];
                for (int y = 0; y < SvSize; y++)
                {
                    float v = y / (SvSize - 1f);
                    for (int x = 0; x < SvSize; x++)
                        pixels[y * SvSize + x] = Color.HSVToRGB(_h, x / (SvSize - 1f), v);
                }
                _svTexture.SetPixels32(pixels);
                _svTexture.Apply(false, false);
            }

            private void Refresh()
            {
                Color c = Value;
                _swatch.color = c;
                _svCursorFill.color = c;
                _hueCursorFill.color = Color.HSVToRGB(_h, 1f, 1f);

                var sv = _svRect.rect;
                _svCursor.anchoredPosition = new Vector2(sv.width * _s, sv.height * _v);

                var hue = _hueRect.rect;
                _hueCursor.anchoredPosition = new Vector2(0f, hue.height * _h);
            }
        }

        /// <summary>Reports where in its rect (0-1 each way, y up) it is pressed or dragged.</summary>
        private class DragArea : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            public event Action<Vector2> Moved;

            public void OnPointerDown(PointerEventData e) => Report(e);
            public void OnDrag(PointerEventData e) => Report(e);

            private void Report(PointerEventData e)
            {
                var rect = (RectTransform)transform;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        rect, e.position, e.pressEventCamera, out Vector2 local)) return;

                Rect r = rect.rect;
                Moved?.Invoke(new Vector2(
                    Mathf.Clamp01((local.x - r.xMin) / r.width),
                    Mathf.Clamp01((local.y - r.yMin) / r.height)));
            }
        }
    }
}
