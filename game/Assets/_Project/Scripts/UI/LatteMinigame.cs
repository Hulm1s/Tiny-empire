using System.Collections.Generic;
using Tycoon.Audio;
using Tycoon.Core;
using Tycoon.Stations;
using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// The latte-art minigame: opened by the player at the premium machine (<see cref="LatteStation"/>) for ONE premium coffee.
    /// LOOK at the pattern the order shows (heart, leaf or smiley), POUR (hold to pour the milk; let go with the level in the
    /// green), DRAW (trace the pattern with the milk), RESULT (stars, price, tip). The stars decide the tip. Built on the
    /// shared <see cref="MinigameOverlay"/> frame exactly as <see cref="HaircutMinigame"/> is: the same board, plaques, tabs,
    /// banner, pill and text sizes, the portrait card 1080 x 1920 and the landscape card 2080 x 1260, the same Close button
    /// (nothing is paid and the cup is not made if it is closed). The art is <see cref="LatteArt"/>.
    ///
    /// The world is frozen while it is open. A hired barista never sees it: <see cref="LatteStation"/> makes the cup by timer.
    /// </summary>
    public class LatteMinigame : MinigameOverlay
    {
        public static LatteMinigame Instance { get; private set; }

        private static readonly Color White = Color.white;
        private static readonly Color Cream = new Color(1f, 0.93f, 0.70f);
        private static readonly Color GoldText = new Color(1f, 0.86f, 0.36f);
        private static readonly Color Foam = new Color(1f, 0.97f, 0.88f);

        // The body: everything under the header belongs to the current step (the bouquet's numbers).
        private const float InX0 = 109f, InY0 = 669f, InX1 = 971f, InY1 = 1776f;

        private enum Step { Look, Pour, Draw, Result }

        // ---- the order ----------------------------------------------------------------
        private LatteStation _station;
        private double _quote;
        private LatteArt.Kind _kind;

        // ---- state --------------------------------------------------------------------
        private Step _step;
        private float _clock, _stepDelay, _anim, _resultLeft, _lookLeft;
        private int _stars;
        private bool _paid, _preview, _pressing;
        private float _level, _pourScore, _drawScore;

        // ---- built once ---------------------------------------------------------------
        private RectTransform _head, _body;
        private GameObject _boardPortrait, _boardTop, _boardBottom;
        private Image _orderPattern;
        private Text _orderName, _orderPrice;
        private Image[] _stepArt, _stepBadge, _stepDisc;
        private Text[] _stepNumber, _stepName, _stepChevron;
        private Text _banner, _stepPill;
        private RectTransform _stepPillRect;
        private RectTransform[] _layers;
        private MinigamePad _pad;

        // LOOK
        private Image _lookPattern, _tapHand, _tapRing;
        private Text _tapText;

        // POUR
        private Image _pool, _meterFill, _marker;
        private RectTransform _stream;
        private Image _streamImage;
        private Text _pourPill;

        // DRAW
        private RectTransform _strokeRoot, _guideRoot;
        private Image _drawHand, _drawRing;
        private Text _drawPill;
        private Image _drawMeter;
        private readonly List<Vector2> _guide = new List<Vector2>();
        private bool[] _covered;
        private Vector2 _last;
        private float _devSum;
        private int _devCount;

        // RESULT
        private Image _rays, _resultPattern;
        private Image[] _starImages;
        private Text _rating, _priceText, _tipText, _totalText;

        // tuning
        private const float PourRate = 0.34f;         // levels per second held
        private const float GreenFrom = 0.58f, GreenTo = 0.82f;
        private const float Tolerance = 72f;          // design units from the line a stroke still counts
        private const float DrawCx = 500f, DrawCy = 1190f, DrawR = 282f;

        public static LatteMinigame Create(RectTransform safeArea, GameObject joystick)
        {
            var game = safeArea.gameObject.AddComponent<LatteMinigame>();
            game.Attach(safeArea, joystick);
            Instance = game;
            return game;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================ open / close

        /// <summary>Opens for a premium coffee. The pattern is chosen at random (the order's picture).</summary>
        public void Open(LatteStation station, double quote)
        {
            if (IsOpen || station == null) return;
            EnsureBuilt();
            _station = station;
            _preview = false;
            Begin((LatteArt.Kind)Random.Range(0, 3), quote);
        }

        /// <summary>Shows the minigame with no station (screenshots and tests): the given pattern, from the given step.</summary>
        public void OpenPreview(LatteArt.Kind kind, double quote, int step)
        {
            EnsureBuilt();
            _station = null;
            _preview = true;
            Begin(kind, quote);
            if (step > 0) GoPreview((Step)Mathf.Clamp(step, 0, 3));
        }

        private void GoPreview(Step step)
        {
            _level = 0.70f;
            _pourScore = 0.9f;
            _drawScore = 0.85f;
            Go(step);
            if (step == Step.Draw) FakeStroke(0.45f);
        }

        private void Begin(LatteArt.Kind kind, double quote)
        {
            _kind = kind;
            _quote = quote;
            _clock = 0f;
            _paid = false;
            _level = 0f;
            _pourScore = 0f;
            _drawScore = 0f;
            _devSum = 0f;
            _devCount = 0;

            var pattern = LatteArt.Pattern(kind);
            _orderPattern.sprite = pattern;
            _lookPattern.sprite = pattern;
            _resultPattern.sprite = pattern;
            _orderName.text = LatteArt.Names[(int)kind];
            _orderPrice.text = MoneyFormat.Short(quote);
            BuildGuide();
            Show();
            Go(Step.Look);
        }

        private void Close()
        {
            if (!_paid && _station != null) _station.Abandon();
            _station = null;
            _pressing = false;
            Hide();
        }

        // ================================================================ building

        /// <summary>Puts a cup picture so the CUP (not the picture) is centred at (cx, cy) with this radius.</summary>
        private static void PlaceCup(Image image, float cx, float cy, float radius)
        {
            float size = radius / LatteArt.CupR;
            Place(image.rectTransform, cx + (0.5f - LatteArt.CupCx) * size, cy, size, size);
        }

        private Image CupWithPattern(RectTransform layer, float cx, float cy, float radius, bool pattern, out Image patternImage)
        {
            var cup = Picture("Cup", layer, LatteArt.Cup(), 0, 0, 10, 10);
            PlaceCup(cup, cx, cy, radius);
            patternImage = null;
            if (pattern)
            {
                patternImage = Picture("Pattern", layer, null, 0, 0, 10, 10);
                PlaceCup(patternImage, cx, cy, radius);
            }
            return cup;
        }

        protected override void BuildContent(RectTransform card)
        {
            _boardPortrait = Plate("Board", card, 50, 150, 980, 1660, 64, HudArt.Looks.Board, out _).gameObject;
            var head = Layer("Header", card);
            var body = Layer("Body", card);
            _head = head;
            _body = body;
            _boardTop = Plate("BoardTop", head, 50, 150, 980, 520, 64, HudArt.Looks.Board, out _).gameObject;
            _boardBottom = Plate("BoardBottom", body, 50, 610, 980, 1200, 64, HudArt.Looks.Board, out _).gameObject;
            _boardTop.SetActive(false);
            _boardBottom.SetActive(false);
            Plate("Title", head, 290, 84, 500, 130, 44, HudArt.Looks.Gold, out _);
            Label("TitleText", head, "LATTE ART", 66, 540, 149, 480, 0, TextAnchor.MiddleCenter, 0.08f);

            // The way out, should the player change their mind: nothing is paid and no cup is made.
            var close = UIFactory.CreatePlateButton("Close", head, new Vector2(96f, 96f), 34f, HudArt.Looks.Red, "X", 52, out _);
            Place(close.GetComponent<RectTransform>(), 975f, 200f, 96f, 96f);
            close.onClick.AddListener(Close);
            var closeHit = UIFactory.CreateRect("Hit", (RectTransform)close.transform);
            closeHit.anchorMin = closeHit.anchorMax = new Vector2(0.5f, 0.5f);
            closeHit.pivot = new Vector2(0.5f, 0.5f);
            closeHit.anchoredPosition = Vector2.zero;
            closeHit.sizeDelta = new Vector2(160f, 160f);
            var closeHitImage = closeHit.gameObject.AddComponent<Image>();
            closeHitImage.color = new Color(0f, 0f, 0f, 0f);
            closeHitImage.raycastTarget = true;

            // ---- ORDER: the customer's wish ------------------------------------------------
            Plate("Order", head, 100, 250, 880, 275, 40, HudArt.Looks.Plaque, out _);
            Plate("OrderTag", head, 130, 226, 270, 68, 26, HudArt.Looks.Gold, out _);
            Label("OrderText", head, "ORDER", 38, 265, 259, 250, 0, TextAnchor.MiddleCenter, 0.08f);
            Picture("OrderSlot", head, LatteArt.Slot(), 236, 408, 210, 210);
            var disc = Picture("OrderDisc", head, UIFactory.Circle, 236, 405, 156, 156);
            disc.color = new Color(0.69f, 0.43f, 0.22f);
            _orderPattern = Picture("OrderPattern", head, null, 0, 0, 10, 10);
            PlaceCup(_orderPattern, 236f, 405f, 78f);
            _orderName = Label("OrderName", head, "HEART", 80, 600, 372, 400, 0, TextAnchor.MiddleLeft, 0.04f);
            Picture("OrderCoin", head, HudArt.Coin(64f), 428, 458, 64, 64);
            _orderPrice = Label("OrderPrice", head, "$70", 58, 590, 458, 230, 0, TextAnchor.MiddleLeft, 0.03f);
            var starOff = LatteArt.Star(false);
            for (int i = 0; i < 3; i++) Picture("OrderStar" + i, head, starOff, 752 + i * 78, 392, 68, 68);
            Label("TipText", head, "TIP", 36, 830, 456, 200, 0, TextAnchor.MiddleCenter, 0.08f).color = Cream;

            // ---- the three steps ------------------------------------------------------------
            string[] names = { "LOOK", "POUR", "DRAW" };
            _stepArt = new Image[3];
            _stepBadge = new Image[3];
            _stepDisc = new Image[3];
            _stepNumber = new Text[3];
            _stepName = new Text[3];
            _stepChevron = new Text[3];
            var check = LatteArt.Check();
            for (int i = 0; i < 3; i++)
            {
                float x = 100 + i * 300;
                Plate("Step" + i, head, x, 548, 280, 90, 34, HudArt.Looks.Tab, out _stepArt[i]);
                _stepBadge[i] = Picture("Done", head, check, x + 40, 592, 52, 52);
                _stepDisc[i] = Picture("Disc", head, UIFactory.Circle, x + 40, 593, 42, 42);
                _stepNumber[i] = UIFactory.CreateText("Number", head, (i + 1).ToString(), 26);
                _stepNumber[i].fontStyle = FontStyle.Bold;
                Place(_stepNumber[i].rectTransform, x + 40, 593, 42, 42);
                _stepName[i] = Label("Name", head, names[i], 38, x + 168, 593, 170, 0, TextAnchor.MiddleCenter, 0.08f);
                if (i < 2) _stepChevron[i] = Label("Chevron", head, ">", 34, x + 292, 591, 30);
            }

            // ---- the body ----------------------------------------------------------------------
            var mat = HudArt.Looks.Green;
            mat.bodyTop = new Color(0.50f, 0.72f, 0.48f);
            mat.bodyBottom = new Color(0.30f, 0.54f, 0.34f);
            mat.gloss = 0.08f;
            Plate("Body", body, 100, 660, 880, 1125, 44, mat, out _);

            _layers = new RectTransform[4];
            for (int i = 0; i < 4; i++) _layers[i] = Layer("Step" + i + "Layer", body);
            BuildLook(_layers[0]);
            BuildPour(_layers[1]);
            BuildDraw(_layers[2]);
            BuildResult(_layers[3]);

            Plate("Banner", body, 132, 688, 816, 84, 32, HudArt.Looks.Plaque, out _);
            _banner = Label("BannerText", body, "", 42, 540, 730, 790, 0, TextAnchor.MiddleCenter, 0.05f);
            _stepPillRect = Plate("StepPill", body, 400, 1700, 280, 66, 30, HudArt.Looks.Tab, out _);
            _stepPill = Label("StepPillText", body, "STEP 1 / 3", 32, 540, 1733, 270, 0, TextAnchor.MiddleCenter, 0.08f);
            _stepPill.color = GoldText;

            _pad = Pad(body, InX0, InY0, InX1 - InX0, InY1 - InY0);
            _pad.Down += OnDown;
            _pad.Drag += OnDrag;
            _pad.Up += OnUp;
        }

        private const float LandW = 2080f, LandH = 1260f;

        protected override Vector2? LandscapeDesign => new Vector2(LandW, LandH);

        protected override void ApplyOrientation(bool landscape)
        {
            _boardPortrait.SetActive(!landscape);
            _boardTop.SetActive(landscape);
            _boardBottom.SetActive(landscape);
            _head.anchoredPosition = landscape ? new Vector2(-510f, -583f) : Vector2.zero;
            _body.anchoredPosition = landscape ? new Vector2(510f, 250f) : Vector2.zero;
        }

        private void ShowSteps(int current)
        {
            for (int i = 0; i < 3; i++)
            {
                bool done = i < current, now = i == current;
                var look = done ? HudArt.Looks.Green : now ? HudArt.Looks.Gold : HudArt.Looks.Tab;
                UIFactory.SetPlate(_stepArt[i], new Vector2(280f, 90f), 34f, look);
                float a = done || now ? 1f : 0.5f;
                _stepBadge[i].gameObject.SetActive(done);
                _stepDisc[i].gameObject.SetActive(!done);
                _stepNumber[i].gameObject.SetActive(!done);
                _stepDisc[i].color = new Color(1f, 0.97f, 0.88f, a);
                _stepNumber[i].color = new Color(0.40f, 0.22f, 0.06f, a);
                _stepName[i].color = new Color(1f, 1f, 1f, a);
                if (_stepChevron[i] != null) _stepChevron[i].color = new Color(1f, 0.86f, 0.4f, done ? 1f : 0.45f);
            }
        }

        private void Go(Step step)
        {
            _step = step;
            _stepDelay = 0f;
            _pressing = false;
            for (int i = 0; i < _layers.Length; i++) _layers[i].gameObject.SetActive(i == (int)step);
            ShowSteps(Mathf.Min((int)step, 2) + (step == Step.Result ? 1 : 0));

            bool result = step == Step.Result;
            _stepPillRect.gameObject.SetActive(!result);
            _stepPill.gameObject.SetActive(!result);
            if (!result) _stepPill.text = $"STEP {(int)step + 1} / 3";

            switch (step)
            {
                case Step.Look:
                    _banner.text = "COPY THIS LATTE ART";
                    _lookLeft = 14f;
                    break;
                case Step.Pour:
                    _banner.text = "HOLD TO POUR THE MILK";
                    BeginPour();
                    break;
                case Step.Draw:
                    _banner.text = "TRACE THE PATTERN";
                    BeginDraw();
                    break;
                case Step.Result:
                    _banner.text = "PERFECT LATTE!";
                    ShowResult();
                    break;
            }
        }

        // ================================================================ 1. LOOK

        private void BuildLook(RectTransform layer)
        {
            Plate("Plaque", layer, 180, 810, 720, 700, 44, HudArt.Looks.Plaque, out _);
            Plate("Tag", layer, 210, 782, 260, 68, 26, HudArt.Looks.Gold, out _);
            Label("TagText", layer, "TARGET", 34, 340, 816, 250, 0, TextAnchor.MiddleCenter, 0.08f);
            CupWithPattern(layer, 500f, 1125f, 208f, true, out _lookPattern);

            var thumbs = new[] { LatteArt.Kind.Heart, LatteArt.Kind.Leaf, LatteArt.Kind.Smiley };
            for (int i = 0; i < 3; i++)
            {
                float x = 250 + i * 145;
                Picture("ThumbRing", layer, UIFactory.Circle, x, 1457, 108, 108).color = new Color(1f, 1f, 1f, 0.35f);
                Picture("ThumbDisc", layer, UIFactory.Circle, x, 1457, 92, 92).color = new Color(0.69f, 0.43f, 0.22f);
                var p = Picture("Thumb", layer, LatteArt.Pattern(thumbs[i]), 0, 0, 10, 10);
                PlaceCup(p, x, 1457f, 46f);
            }

            _tapRing = Picture("TapRing", layer, LatteArt.Ring(), 800f, 1230f, 110, 110);
            _tapHand = Picture("TapHand", layer, HandSprite(16f, out var tip), 800f - tip.x + 115f, 1234f - (230f - tip.y) + 115f, 230, 230);
            _tapText = Label("TapText", layer, "TAP TO START", 32, 735f, 1470f, 330, 0, TextAnchor.MiddleCenter, 0.06f);
            _tapText.color = GoldText;
            Label("Hint", layer, "HEART, LEAF OR SMILEY", 28, 540f, 1668f, 800, 0, TextAnchor.MiddleCenter, 0.06f).color = Cream;
        }

        private static Sprite HandSprite(float angle, out Vector2 tip) => LatteArt.Hand(angle, out tip);

        // ================================================================ 2. POUR

        private void BuildPour(RectTransform layer)
        {
            const float cx = 410f, cy = 1250f, r = 236f;
            Picture("Rays", layer, LatteArt.RaysSmall(), cx, cy, 600, 600);
            CupWithPattern(layer, cx, cy, r, false, out _);
            _pool = Picture("Pool", layer, UIFactory.Circle, cx + 14f, cy + 14f, 10, 10);
            _pool.color = new Color(1f, 0.99f, 0.93f, 0.97f);

            var streamRect = UIFactory.CreateRect("Stream", layer);
            _stream = streamRect;
            _streamImage = streamRect.gameObject.AddComponent<Image>();
            _streamImage.color = new Color(1f, 1f, 0.97f);
            _streamImage.raycastTarget = false;

            var jug = Picture("Jug", layer, LatteArt.Jug(), 800f, 960f, 420, 420);
            jug.rectTransform.SetAsLastSibling();

            Plate("MeterBack", layer, 150, 1540, 780, 90, 40, HudArt.Looks.Plaque, out _);
            Plate("MeterTrack", layer, 190, 1568, 700, 36, 18, HudArt.Looks.Tab, out _);
            var green = UIFactory.CreatePanel("Green", layer, new Color(0.40f, 0.88f, 0.38f));
            Place(green.rectTransform, 190f + 700f * (GreenFrom + GreenTo) * 0.5f, 1586f, 700f * (GreenTo - GreenFrom), 30f);
            green.raycastTarget = false;
            _meterFill = UIFactory.CreatePanel("Fill", layer, new Color(1f, 0.98f, 0.88f));
            _meterFill.raycastTarget = false;
            _marker = UIFactory.CreatePanel("Marker", layer, new Color(1f, 0.82f, 0.25f));
            _marker.raycastTarget = false;
            Label("MeterL", layer, "MILK", 26, 190, 1664, 160, 0, TextAnchor.MiddleLeft, 0.08f).color = Cream;
            Label("MeterH", layer, "FILL TO THE GREEN", 28, 600, 1664, 480, 0, TextAnchor.MiddleCenter, 0.08f).color = GoldText;

            var ring = Picture("HoldRing", layer, LatteArt.Ring(), 800f, 1250f, 110, 110);
            ring.rectTransform.SetSiblingIndex(ring.rectTransform.GetSiblingIndex());
            Plate("HoldPlate", layer, 705, 1414, 190, 52, 26, HudArt.Looks.Tab, out _);
            _pourPill = Label("HoldText", layer, "HOLD", 30, 800f, 1440f, 190, 0, TextAnchor.MiddleCenter, 0.08f);
            _pourPill.color = GoldText;
        }

        private void BeginPour()
        {
            _level = 0f;
            RefreshPour();
        }

        private void RefreshPour()
        {
            float w = Mathf.Max(0.001f, 700f * Mathf.Min(1f, _level));
            Place(_meterFill.rectTransform, 190f + w * 0.5f, 1586f, w, 24f);
            Place(_marker.rectTransform, 190f + 700f * Mathf.Min(1f, _level), 1586f, 14f, 64f);
            float poolSize = Mathf.Lerp(20f, 330f, Mathf.Clamp01(_level));
            Place(_pool.rectTransform, 424f, 1264f, poolSize, poolSize);
            _stream.gameObject.SetActive(_pressing);
        }

        private void FinishPour()
        {
            if (_level < 0.12f) { _level = 0f; RefreshPour(); return; }

            // 1 in the middle of the green, falling to 0 a quarter of the meter away.
            float mid = (GreenFrom + GreenTo) * 0.5f;
            _pourScore = Mathf.Clamp01(1f - Mathf.Max(0f, Mathf.Abs(_level - mid) - (GreenTo - GreenFrom) * 0.5f) / 0.25f);
            _pressing = false;
            RefreshPour();
            _stepDelay = 0.7f;
        }

        // ================================================================ 3. DRAW

        private void BuildDraw(RectTransform layer)
        {
            CupWithPattern(layer, DrawCx, DrawCy, DrawR, false, out _);
            var milk = Picture("MilkBase", layer, UIFactory.Circle, DrawCx, DrawCy, DrawR * 1.4f, DrawR * 1.4f);
            milk.color = new Color(1f, 0.97f, 0.88f, 0.5f);
            _guideRoot = Layer("Guide", layer);
            _strokeRoot = Layer("Strokes", layer);

            // The target, small, in the corner (like the haircut's).
            Plate("Thumb", layer, 766, 800, 200, 236, 30, HudArt.Looks.Plaque, out _);
            Picture("ThumbDisc", layer, UIFactory.Circle, 866f, 930f, 140, 140).color = new Color(0.69f, 0.43f, 0.22f);
            var t = Picture("ThumbPattern", layer, null, 0, 0, 10, 10);
            _drawThumb = t;
            PlaceCup(t, 866f, 930f, 70f);
            Plate("ThumbTag", layer, 790, 782, 152, 40, 16, HudArt.Looks.Gold, out _);
            Label("ThumbTagText", layer, "TARGET", 20, 866f, 802f, 150, 0, TextAnchor.MiddleCenter, 0.08f);

            Plate("MeterBack", layer, 190, 1626, 700, 56, 28, HudArt.Looks.Tab, out _);
            _drawMeter = UIFactory.CreatePanel("Fill", layer, new Color(0.55f, 0.90f, 0.38f));
            _drawMeter.raycastTarget = false;
            Label("MeterText", layer, "ON LINE", 24, 280, 1654, 200, 0, TextAnchor.MiddleLeft, 0.08f);
            Plate("PillPlate", layer, 570, 1534, 340, 52, 26, HudArt.Looks.Tab, out _);
            _drawPill = Label("Pill", layer, "STAY ON THE LINE", 24, 740f, 1560f, 340, 0, TextAnchor.MiddleCenter, 0.08f);
            _drawPill.color = Cream;

            _drawRing = Picture("DrawRing", layer, LatteArt.Ring(), 0, 0, 110, 110);
            _drawHand = Picture("DrawHand", layer, HandSprite(14f, out _handTip), 0, 0, 230, 230);
        }

        private Image _drawThumb;
        private Vector2 _handTip;

        /// <summary>The path to trace, in design units, for the order's pattern: a polyline the finger follows.</summary>
        private void BuildGuide()
        {
            _guide.Clear();
            float r = DrawR;
            switch (_kind)
            {
                case LatteArt.Kind.Heart:
                    for (int i = 0; i <= 64; i++)
                    {
                        float t = i / 64f * Mathf.PI * 2f;
                        float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                        float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                        _guide.Add(new Vector2(DrawCx + x / 17f * r * 0.80f, DrawCy - 6f - (y + 2.5f) / 17f * r * 0.80f));
                    }
                    break;

                case LatteArt.Kind.Leaf:
                {
                    // Up the spine, then each leaf out and back, bottom to top.
                    _guide.Add(new Vector2(DrawCx, DrawCy + r * 0.60f));
                    _guide.Add(new Vector2(DrawCx, DrawCy - r * 0.62f));
                    for (int i = 0; i < 5; i++)
                    {
                        float yy = DrawCy - r * 0.50f + i * r * 0.26f, k = 0.26f + 0.12f * Mathf.Sin((4 - i + 0.5f) / 5f * Mathf.PI);
                        yy = DrawCy + r * 0.50f - (4 - i) * r * 0.26f;
                        _guide.Add(new Vector2(DrawCx - r * k * 1.55f, yy - r * 0.20f));
                        _guide.Add(new Vector2(DrawCx, yy));
                        _guide.Add(new Vector2(DrawCx + r * k * 1.55f, yy - r * 0.20f));
                        _guide.Add(new Vector2(DrawCx, yy));
                    }
                    break;
                }

                default:
                    for (int i = 0; i <= 40; i++)
                    {
                        float t = i / 40f * Mathf.PI * 2f;
                        _guide.Add(new Vector2(DrawCx + Mathf.Cos(t) * r * 0.62f, DrawCy + Mathf.Sin(t) * r * 0.62f));
                    }
                    for (int i = 0; i <= 14; i++)
                    {
                        float a = Mathf.Lerp(28f, 152f, i / 14f) * Mathf.Deg2Rad;
                        _guide.Add(new Vector2(DrawCx + Mathf.Cos(a) * r * 0.36f, DrawCy + Mathf.Sin(a) * r * 0.36f - r * 0.02f));
                    }
                    break;
            }
            _covered = new bool[_guide.Count];
        }

        private void BeginDraw()
        {
            _drawThumb.sprite = LatteArt.Pattern(_kind);
            for (int i = _strokeRoot.childCount - 1; i >= 0; i--) Kill(_strokeRoot.GetChild(i).gameObject);
            for (int i = _guideRoot.childCount - 1; i >= 0; i--) Kill(_guideRoot.GetChild(i).gameObject);
            for (int i = 0; i < _covered.Length; i++) _covered[i] = false;
            _devSum = 0f;
            _devCount = 0;
            _clock = 0f;

            // The dashed guide.
            const float dash = 30f;
            for (int i = 0; i < _guide.Count - 1; i++)
            {
                var a = _guide[i]; var b = _guide[i + 1];
                float len = Vector2.Distance(a, b);
                for (float s = 0f; s < len; s += dash * 1.6f)
                {
                    var p0 = Vector2.Lerp(a, b, s / len);
                    var p1 = Vector2.Lerp(a, b, Mathf.Min(1f, (s + dash) / len));
                    Stroke(_guideRoot, p0, p1, 11f, new Color(0.15f, 0.08f, 0.04f));
                    Stroke(_guideRoot, p0, p1, 6f, new Color(1f, 0.90f, 0.42f));
                }
            }
            _drawHand.gameObject.SetActive(true);
            _drawRing.gameObject.SetActive(true);
            PlaceDrawHand(_guide[0]);
            RefreshDraw();
        }

        private void PlaceDrawHand(Vector2 at)
        {
            Place(_drawRing.rectTransform, at.x, at.y, 110, 110);
            Place(_drawHand.rectTransform, at.x - _handTip.x + 115f, at.y + 2f - (230f - _handTip.y) + 115f, 230, 230);
        }

        /// <summary>Foam along a stroke, a dot every few units.</summary>
        private void Foamed(Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 7f));
            for (int i = 0; i <= steps; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)steps);
                var dot = UIFactory.CreateRect("Foam", _strokeRoot);
                var image = dot.gameObject.AddComponent<Image>();
                image.sprite = UIFactory.Circle;
                image.color = Foam;
                image.raycastTarget = false;
                Place(dot, p.x, p.y, 24f, 24f);
            }
            Mark(b);
        }

        /// <summary>Marks the guide points the stroke is near, and sums how far off the line it is.</summary>
        private void Mark(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < _guide.Count; i++)
            {
                float d = Vector2.Distance(p, _guide[i]);
                if (d < best) best = d;
                if (d <= Tolerance) _covered[i] = true;
            }
            _devSum += Mathf.Min(best, 160f);
            _devCount++;
        }

        private float Coverage()
        {
            // Dense guides: also count the stretches between the points.
            int n = 0;
            for (int i = 0; i < _covered.Length; i++) if (_covered[i]) n++;
            return _covered.Length == 0 ? 0f : (float)n / _covered.Length;
        }

        private float Accuracy() => _devCount == 0 ? 0f : Mathf.Clamp01(1f - _devSum / _devCount / 110f);

        private void RefreshDraw()
        {
            float w = Mathf.Max(0.01f, (700f - 16f) * Coverage());
            Place(_drawMeter.rectTransform, 198f + w * 0.5f, 1654f, w, 36f);
        }

        private void FakeStroke(float share)
        {
            int count = Mathf.RoundToInt(_guide.Count * share);
            for (int i = 1; i < count; i++) Foamed(_guide[i - 1], _guide[i]);
            if (count > 0) PlaceDrawHand(_guide[Mathf.Clamp(count - 1, 0, _guide.Count - 1)]);
            RefreshDraw();
        }

        private void FinishDraw()
        {
            _drawScore = Coverage() * (0.55f + 0.45f * Accuracy());
            _pressing = false;
            _stepDelay = 0.6f;
        }

        // ================================================================ 4. RESULT

        private void BuildResult(RectTransform layer)
        {
            _rays = Picture("Rays", layer, LatteArt.RaysBig(), 540f, 1000f, 820, 820);
            CupWithPattern(layer, 510f, 1000f, 198f, true, out _resultPattern);
            Picture("Spark1", layer, LatteArt.Spark(64), 300f, 880f, 64, 64);
            Picture("Spark2", layer, LatteArt.Spark(44), 790f, 820f, 44, 44);
            Picture("Spark3", layer, LatteArt.Spark(52), 812f, 1130f, 52, 52);
            Picture("Spark4", layer, LatteArt.Spark(36), 262f, 1160f, 36, 36);

            _starImages = new[]
            {
                Picture("Star1", layer, null, 375f, 1318f, 128, 128),
                Picture("Star2", layer, null, 540f, 1294f, 160, 160),
                Picture("Star3", layer, null, 705f, 1318f, 128, 128),
            };
            _rating = Label("Rating", layer, "PERFECT!", 86, 540f, 1428f, 800, 0, TextAnchor.MiddleCenter, 0.06f);
            _rating.color = new Color(1f, 0.97f, 0.62f);

            Plate("Bill", layer, 150, 1494, 780, 256, 40, HudArt.Looks.Plaque, out _);
            Label("PriceLabel", layer, "PRICE", 40, 330, 1546, 260, 0, TextAnchor.MiddleLeft, 0.06f).color = Cream;
            _priceText = Label("Price", layer, "$0", 46, 770, 1546, 260, 0, TextAnchor.MiddleRight, 0.03f);
            Label("TipLabel", layer, "TIP", 40, 330, 1608, 260, 0, TextAnchor.MiddleLeft, 0.06f).color = Cream;
            Picture("Coin", layer, HudArt.Coin(56f), 410f, 1609f, 56, 56);
            _tipText = Label("Tip", layer, "+$0", 46, 770, 1608, 260, 0, TextAnchor.MiddleRight, 0.03f);
            var rule = UIFactory.CreatePanel("Rule", layer, new Color(1f, 0.86f, 0.4f, 0.55f));
            Place(rule.rectTransform, 540f, 1650f, 680f, 3f);
            Label("TotalLabel", layer, "TOTAL", 46, 330, 1703, 260, 0, TextAnchor.MiddleLeft, 0.06f).color = GoldText;
            _totalText = Label("Total", layer, "$0", 62, 760, 1703, 300, 0, TextAnchor.MiddleRight, 0.03f);
            _totalText.color = new Color(1f, 0.95f, 0.55f);
        }

        private void ShowResult()
        {
            float score = _pourScore * 0.45f + _drawScore * 0.55f;
            _stars = score >= 0.72f ? 3 : score >= 0.42f ? 2 : 1;

            double tip = 0d;
            _paid = _station != null && _station.Finish(_stars, out tip);
            if (!_paid) tip = _preview ? _quote * (_stars >= 3 ? 0.75d : _stars == 2 ? 0.45d : 0.2d) : 0d;

            var star = LatteArt.Star(true);
            var starOff = LatteArt.Star(false);
            for (int i = 0; i < 3; i++)
            {
                _starImages[i].sprite = i < _stars ? star : starOff;
                _starImages[i].rectTransform.localScale = Vector3.zero;
            }
            _rating.text = _stars >= 3 ? "PERFECT!" : _stars == 2 ? "GREAT!" : "GOOD";
            _priceText.text = MoneyFormat.Short(_quote);
            _tipText.text = "+" + MoneyFormat.Short(tip);
            _totalText.text = MoneyFormat.Short(_quote + tip);
            _resultLeft = 3.6f;
            _anim = 0f;
            SoundFx.Play(Sfx.Unlock);
        }

        // ================================================================ input

        private void OnDown(Vector2 p)
        {
            _pressing = true;
            _last = p;
            switch (_step)
            {
                case Step.Look:
                    if (_lookLeft < 13.5f) Go(Step.Pour);
                    break;
                case Step.Pour:
                    if (_stepDelay <= 0f) _pressing = true;
                    break;
                case Step.Draw:
                    PlaceDrawHand(p);
                    break;
                case Step.Result:
                    if (_resultLeft < 3.0f) Close();
                    break;
            }
        }

        private void OnDrag(Vector2 p)
        {
            if (!_pressing) return;
            if (_step == Step.Draw && _stepDelay <= 0f)
            {
                Foamed(_last, p);
                PlaceDrawHand(p);
                RefreshDraw();
                if (Coverage() >= 0.88f) FinishDraw();
            }
            _last = p;
        }

        private void OnUp(Vector2 p)
        {
            if (_step == Step.Pour && _pressing && _stepDelay <= 0f) FinishPour();
            _pressing = false;
        }

        // ================================================================ every frame

        protected override void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.1f);
            _anim += dt;
            if (_step != Step.Result) _clock += dt;

            float pulse = 1f + 0.05f * Mathf.Sin(_anim * 6f);
            if (_step == Step.Look)
            {
                _lookLeft -= dt;
                _tapHand.rectTransform.localScale = Vector3.one * pulse;
                _tapText.color = new Color(GoldText.r, GoldText.g, GoldText.b, 0.65f + 0.35f * Mathf.Sin(_anim * 4f));
                if (_lookLeft <= 0f) Go(Step.Pour);
            }

            if (_step == Step.Pour && _stepDelay <= 0f)
            {
                if (_pressing)
                {
                    _level += PourRate * dt;
                    // The stream, from the spout to the pool.
                    PlaceStream();
                    if (_level >= 1f) { _level = 1f; FinishPour(); }
                }
                RefreshPour();
                _pourPill.color = new Color(GoldText.r, GoldText.g, GoldText.b, _pressing ? 1f : 0.6f + 0.4f * Mathf.Sin(_anim * 4f));
            }

            if (_step == Step.Draw && _stepDelay <= 0f && _clock > 28f) FinishDraw();

            if (_stepDelay > 0f)
            {
                _stepDelay -= dt;
                if (_stepDelay <= 0f && _step != Step.Result) Go(_step + 1);
            }

            if (_step == Step.Result)
            {
                _rays.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -_anim * 12f);
                for (int i = 0; i < 3; i++)
                {
                    float t = Mathf.Clamp01((_anim - 0.15f - i * 0.18f) / 0.25f);
                    float pop = t < 1f ? Mathf.Sin(t * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f) * 1.15f : 1f;
                    _starImages[i].rectTransform.localScale = Vector3.one * Mathf.Max(0f, pop);
                }
                _resultLeft -= dt;
                if (_resultLeft <= 0f) Close();
            }
        }

        private void PlaceStream()
        {
            // From the jug's spout (lower left of it) to the middle of the pool.
            var a = new Vector2(700f, 1000f); var b = new Vector2(424f, 1264f);
            var mid = (a + b) * 0.5f;
            float len = Vector2.Distance(a, b);
            Place(_stream, mid.x, mid.y, len, 14f);
            _stream.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-(b.y - a.y), b.x - a.x) * Mathf.Rad2Deg);
        }

        // ---- small helpers -------------------------------------------------------------------

        private static void Kill(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        private static Image Stroke(RectTransform parent, Vector2 a, Vector2 b, float width, Color color)
        {
            var panel = UIFactory.CreatePanel("Stroke", parent, color);
            var mid = (a + b) * 0.5f;
            float len = Vector2.Distance(a, b) + width * 0.6f;
            Place(panel.rectTransform, mid.x, mid.y, len, width);
            float angle = Mathf.Atan2(-(b.y - a.y), b.x - a.x) * Mathf.Rad2Deg;
            panel.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            panel.raycastTarget = false;
            return panel;
        }

    }
}
