using System.Collections.Generic;
using Tycoon.Audio;
using Tycoon.Core;
using Tycoon.Customers;
using Tycoon.Stations;
using UnityEngine;
using UnityEngine.UI;
using Style = Tycoon.UI.HaircutArt.Style;
using Look = Tycoon.UI.HaircutArt.HairLook;

namespace Tycoon.UI
{
    /// <summary>
    /// The salon's haircut minigame: what the PLAYER does at a chair instead of waiting out a ring. Opened by
    /// <see cref="SalonChairStation"/> when the player has stood in the STYLE square with a customer sitting in the
    /// chair. Built on <see cref="MinigameOverlay"/> exactly like the bouquet (portrait card 1080 x 1920 and the
    /// landscape arrangement with the header column beside the body), with the same board, plaques, tabs, banner
    /// and pill, and laid out as the approved mock-ups (Editor/HaircutMock.cs) with the text made bigger:
    ///   1 LOOK  the style the customer asked for, big (tap to go on)
    ///   2 CUT   swipe along each dashed line on the grown-out hair; the lines the swipe stays on are cut and the
    ///           hair falls away. How close the swipes stay to the lines is the accuracy
    ///   3 DRY   rub back and forth over the hair, first with the comb, then the dryer, until it is smooth
    ///   result  stars, PRICE + TIP = TOTAL; a tap (or a few seconds) closes it
    /// Stars come from the accuracy of the cut, less one for taking far too long; the tip is the chair's share for
    /// that many stars. Nothing is paid until the result (<see cref="SalonChairStation.Finish"/>), so closing early
    /// loses nothing. All art is <see cref="HaircutArt"/> and <see cref="BouquetArt"/>, painted once in the editor
    /// (Editor/HaircutBake.cs) into Resources/Haircut: nothing is painted on the phone.
    /// </summary>
    public class HaircutMinigame : MinigameOverlay
    {
        public static HaircutMinigame Instance { get; private set; }

        private static readonly Color White = Color.white;
        private static readonly Color Cream = new Color(1f, 0.93f, 0.70f);
        private static readonly Color GoldText = new Color(1f, 0.86f, 0.36f);
        private static readonly Color Spark = new Color(1f, 0.96f, 0.62f);

        // The body: everything under the header belongs to the current step (the bouquet's numbers).
        private const float InX0 = 109f, InY0 = 669f, InX1 = 971f, InY1 = 1776f;

        private enum Step { Look, Cut, Dry, Result }

        // ---- the order ----------------------------------------------------------------
        private SalonChairStation _station;
        private double _quote;
        private Style _style;

        // ---- state --------------------------------------------------------------------
        private Step _step;
        private float _clock;
        private float _stepDelay;
        private float _anim;
        private float _resultLeft;
        private float _lookLeft;
        private int _stars;
        private bool _paid;
        private bool _preview;

        // ---- built once ---------------------------------------------------------------
        private RectTransform _head, _body;
        private GameObject _boardPortrait, _boardTop, _boardBottom;
        private Image _orderHead;
        private Text _orderName, _orderPrice;
        private Image[] _stepArt, _stepBadge, _stepDisc;
        private Text[] _stepNumber, _stepName, _stepChevron;
        private Text _banner, _stepPill;
        private RectTransform _stepPillRect;
        private RectTransform[] _layers;
        private MinigamePad _pad;

        // LOOK
        private Image _lookHead, _tapHand, _tapRing;
        private Text _lookName, _tapText;

        // CUT
        private RectTransform _cutLayer;
        private Image _cutHead, _scissors, _cutThumb;
        private RectTransform _guideRoot, _cutHint, _clumpRoot;
        private readonly List<Vector2[]> _guides = new List<Vector2[]>();
        private readonly List<float> _guideLength = new List<float>();
        private readonly List<bool[]> _covered = new List<bool[]>();
        private readonly List<float> _devSum = new List<float>();
        private readonly List<int> _devCount = new List<int>();
        private readonly List<bool> _guideDone = new List<bool>();
        private readonly List<List<Image>> _guideDashes = new List<List<Image>>();
        private readonly List<Image> _guideChecks = new List<Image>();
        private readonly List<Clump> _clumps = new List<Clump>();
        private Text _cutPill;
        private Image _hintRing, _hintHand;
        private Vector2 _hintTip;
        private Vector2 _last;
        private bool _pressing;
        private float _accuracy;

        private class Clump { public Image Image; public Vector2 At; public float Vy, Spin, Rest; }

        // DRY
        private RectTransform _dryLayer;
        private Image _dryHead, _comb, _dryer, _dryThumb, _combChip, _dryerChip;
        private Image _meterFill;
        private RectTransform _meterFillRect;
        private Text _dryPill;
        private float _rub;
        private Vector2 _rubLast;
        private readonly List<Image> _rubDashes = new List<Image>();

        // RESULT
        private RectTransform _resultLayer;
        private Image _rays, _resultHead;
        private Image[] _starImages;
        private Text _rating, _priceText, _tipText, _totalText;

        // tuning
        private const float CutTolerance = 80f;      // design units from a line a swipe still counts
        private const float RubNeeded = 2600f;       // design units of rubbing
        private const int Bins = 20;
        private const float CutCx = 480f, CutCy = 1215f, CutSize = 700f;
        private const float DryCx = 500f, DryCy = 1130f, DrySize = 640f;

        public static HaircutMinigame Create(RectTransform safeArea, GameObject joystick)
        {
            var game = safeArea.gameObject.AddComponent<HaircutMinigame>();
            game.Attach(safeArea, joystick);
            Instance = game;
            return game;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================ the art (baked in the editor)

        /// <summary>Bumped whenever a picture's drawing changes, so the scene build re-bakes.</summary>
        public const string ArtVersion = "1";
        public const string ArtFolder = "Haircut";

        /// <summary>While baking: every picture drawn goes here (key, texture, anchor).</summary>
        public static System.Action<string, Texture2D, Vector2> BakeSink;

        /// <summary>The anchor point of each drawn sprite that has one (the glove's fingertip).</summary>
        private static readonly Dictionary<string, Vector2> Anchors = new Dictionary<string, Vector2>();

        private static readonly Dictionary<string, Sprite> Art = new Dictionary<string, Sprite>();
        private static BouquetMinigame.BakedIndex _index;
        private static readonly HashSet<string> Baked = new HashSet<string>();
        private static bool _warned;

        private static void LoadIndex()
        {
            if (_index != null) return;
            var text = Resources.Load<TextAsset>(ArtFolder + "/index");
            _index = text != null ? JsonUtility.FromJson<BouquetMinigame.BakedIndex>(text.text) : new BouquetMinigame.BakedIndex();
            if (_index.version != ArtVersion) _index = new BouquetMinigame.BakedIndex();
            for (int i = 0; i < _index.keys.Count; i++)
            {
                Baked.Add(_index.keys[i]);
                if (_index.anchors[i] != Vector2.zero) Anchors[_index.keys[i]] = _index.anchors[i];
            }
        }

        private static Sprite Cached(string key, System.Func<Texture2D> draw)
        {
            if (BakeSink != null)
            {
                var drawn = draw();
                BakeSink(key, drawn, Anchors.TryGetValue(key, out var a) ? a : Vector2.zero);
                return null;
            }

            if (Art.TryGetValue(key, out var hit) && hit != null) return hit;

            LoadIndex();
            Texture2D tex = Baked.Contains(key) ? Resources.Load<Texture2D>(ArtFolder + "/" + key) : null;
            if (tex == null)
            {
                if (!_warned) { _warned = true; Debug.LogWarning($"[Haircut] art '{key}' is not baked; painting it now (slow)."); }
                tex = draw();
                tex.hideFlags = HideFlags.DontSave;
            }
            var sprite = BouquetArt.ToSprite(tex);
            Art[key] = sprite;
            return sprite;
        }

        /// <summary>Paints every picture the minigame uses, into <see cref="BakeSink"/> (Editor/HaircutBake.cs).</summary>
        public static void BakeAll()
        {
            foreach (var s in HaircutArt.All)
            {
                foreach (Look look in System.Enum.GetValues(typeof(Look))) HeadArt(s, look, 480);
                HeadArt(s, Look.Clean, 192);
                foreach (float angle in ClumpAngles) ClumpArt(s, angle);
            }
            CombArt(); DryerArt();
            ScissorsArt(); RingArt(); CheckArt(); RaysArt();
            foreach (int px in new[] { 64, 44, 52, 36 }) SparkArt(px);
            StarArt(true); StarArt(false);
            foreach (float angle in new[] { 16f }) HandArt(angle, out _);
            SlotArt(210);
        }

        private static readonly float[] ClumpAngles = { 200f, 150f, 240f };

        private static Sprite HeadArt(Style s, Look look, int px) =>
            Cached($"head_{s}_{look}_{px}", () => HaircutArt.Head(s, px, look));
        private static Sprite ClumpArt(Style s, float angle) =>
            Cached($"clump_{s}_{Mathf.RoundToInt(angle)}", () => HaircutArt.Clump(s, 96, angle));
        private static Sprite CombArt() => Cached("comb192", () => HaircutArt.Comb(192));
        private static Sprite DryerArt() => Cached("dryer192", () => HaircutArt.Dryer(192));
        private static Sprite ScissorsArt() => Cached("scissors150", () => BouquetArt.Scissors(150));
        private static Sprite RingArt() => Cached("ring110", () => BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.9f)));
        private static Sprite CheckArt() => Cached("check64", () => BouquetArt.CheckBadge(64));
        private static Sprite RaysArt() => Cached("rays", () => BouquetArt.Rays(820, 18, new Color(1f, 0.90f, 0.45f, 0.75f)));
        private static Sprite SparkArt(int px) => Cached("spark" + px, () => BouquetArt.Sparkle(px, Spark));
        private static Sprite StarArt(bool lit) => Cached(lit ? "starOn" : "starOff", () => BouquetArt.Star(180, lit));
        private static Sprite SlotArt(int px) => Cached("slot" + px, () => BouquetArt.Slot(px, BouquetArt.SlotState.Filled));

        /// <summary>The pointing glove; <paramref name="tip"/> is the fingertip in it (y up).</summary>
        private static Sprite HandArt(float angle, out Vector2 tip)
        {
            string key = "hand_" + angle;
            var sprite = Cached(key, () =>
            {
                var tex = BouquetArt.Hand(230, angle, out var t);
                Anchors[key] = t;
                return tex;
            });
            Anchors.TryGetValue(key, out tip);
            return sprite;
        }

        public static Style StyleOf(Tycoon.Config.ItemDefinition item)
        {
            switch (item != null ? item.id : "")
            {
                case "hair_bob": return Style.Bob;
                case "hair_long": return Style.Long;
                case "hair_quiff": return Style.Quiff;
                default: return Style.Short;
            }
        }

        // ================================================================ open / close

        public void Open(SalonChairStation station, CustomerAgent customer, double quote)
        {
            if (IsOpen || station == null || customer == null) return;
            EnsureBuilt();
            _station = station;
            _preview = false;
            Begin(StyleOf(customer.Wanted), quote);
        }

        /// <summary>For pictures of the real screen (no chair, nothing paid): opens at the given step.</summary>
        public void OpenPreview(Style style, double quote, int step)
        {
            EnsureBuilt();
            _station = null;
            _preview = true;
            Begin(style, quote);
            Go((Step)Mathf.Clamp(step, 0, 3));
        }

        private void Begin(Style style, double quote)
        {
            _style = style;
            _quote = quote;
            _clock = 0f;
            _paid = false;
            _accuracy = 1f;
            _rub = 0f;

            _orderHead.sprite = HeadArt(style, Look.Clean, 192);
            _orderName.text = HaircutArt.Names[(int)style];
            _orderPrice.text = MoneyFormat.Short(quote);
            _lookHead.sprite = HeadArt(style, Look.Clean, 480);
            _lookName.text = HaircutArt.Names[(int)style];
            _cutThumb.sprite = HeadArt(style, Look.Clean, 192);
            _dryThumb.sprite = HeadArt(style, Look.Clean, 192);
            BuildCut();
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
            Label("TitleText", head, "HAIRCUT", 66, 540, 149, 480, 0, TextAnchor.MiddleCenter, 0.08f);

            // The way out, should the player change their mind: nothing is paid.
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
            Picture("OrderSlot", head, SlotArt(210), 236, 408, 210, 210);
            _orderHead = Picture("OrderHead", head, null, 236, 405, 160, 160);
            _orderName = Label("OrderName", head, "BOB", 80, 600, 372, 400, 0, TextAnchor.MiddleLeft, 0.04f);
            Picture("OrderCoin", head, HudArt.Coin(64f), 428, 458, 64, 64);
            _orderPrice = Label("OrderPrice", head, "$45", 58, 590, 458, 230, 0, TextAnchor.MiddleLeft, 0.03f);
            for (int i = 0; i < 3; i++) Picture("OrderStar" + i, head, StarArt(false), 752 + i * 78, 392, 68, 68);
            Label("TipText", head, "TIP", 36, 830, 456, 200, 0, TextAnchor.MiddleCenter, 0.08f).color = Cream;

            // ---- the three steps ------------------------------------------------------------
            string[] names = { "LOOK", "CUT", "DRY" };
            _stepArt = new Image[3];
            _stepBadge = new Image[3];
            _stepDisc = new Image[3];
            _stepNumber = new Text[3];
            _stepName = new Text[3];
            _stepChevron = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                float x = 100 + i * 300;
                Plate("Step" + i, head, x, 548, 280, 90, 34, HudArt.Looks.Tab, out _stepArt[i]);
                _stepBadge[i] = Picture("Done", head, CheckArt(), x + 40, 592, 52, 52);
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
            var grid = UIFactory.CreateRect("Grid", body);
            Place(grid, (InX0 + InX1) * 0.5f, (InY0 + InY1) * 0.5f, InX1 - InX0 - 30f, InY1 - InY0 - 30f);
            var gridImage = grid.gameObject.AddComponent<RawImage>();
            gridImage.texture = GridTexture();
            gridImage.uvRect = new Rect(0f, 0f, (InX1 - InX0 - 30f) / 224f, (InY1 - InY0 - 30f) / 224f);
            gridImage.raycastTarget = false;

            _layers = new RectTransform[4];
            for (int i = 0; i < 4; i++) _layers[i] = Layer("Step" + i + "Layer", body);
            BuildLook(_layers[0]);
            _cutLayer = _layers[1];
            BuildCutLayer(_cutLayer);
            _dryLayer = _layers[2];
            BuildDry(_dryLayer);
            BuildResult(_layers[3]);

            // The banner and the pill sit over every step.
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

        // ---- landscape: the same arrangement as the bouquet (header column left, body right) ----------
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

        private static Texture2D GridTexture()
        {
            const int n = 224;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool major = x < 2 || y < 2;
                    bool minor = x % 56 < 2 || y % 56 < 2;
                    byte a = (byte)(major ? 26 : minor ? 13 : 0);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        // ================================================================ steps and tabs

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
                    _banner.text = "COPY THIS STYLE";
                    _lookLeft = 14f;
                    break;
                case Step.Cut:
                    _banner.text = "SWIPE ALONG THE LINES";
                    break;
                case Step.Dry:
                    _banner.text = "RUB OVER THE HAIR";
                    BeginDry();
                    break;
                case Step.Result:
                    _banner.text = "LOOKS GREAT!";
                    ShowResult();
                    break;
            }
        }

        // ================================================================ 1. LOOK

        private void BuildLook(RectTransform layer)
        {
            Plate("Frame", layer, 180, 810, 720, 690, 44, HudArt.Looks.Plaque, out _);
            Plate("FrameTag", layer, 210, 784, 280, 68, 26, HudArt.Looks.Gold, out _);
            Label("FrameTagText", layer, "TARGET", 38, 350, 818, 260, 0, TextAnchor.MiddleCenter, 0.08f);
            Picture("Rays", layer, RaysArt(), 540f, 1150f, 640, 640).color = new Color(1f, 1f, 1f, 0.30f);
            Picture("Disc", layer, UIFactory.Circle, 540f, 1150f, 580, 580).color = new Color(1f, 0.95f, 0.82f, 1f);
            _lookHead = Picture("Head", layer, null, 540f, 1145f, 600, 600);
            Picture("Spark1", layer, SparkArt(64), 300f, 990f, 64, 64);
            Picture("Spark2", layer, SparkArt(44), 790f, 940f, 44, 44);
            Picture("Spark3", layer, SparkArt(52), 800f, 1290f, 52, 52);

            Plate("NamePlate", layer, 300, 1514, 480, 100, 38, HudArt.Looks.Gold, out _);
            _lookName = Label("Name", layer, "BOB", 62, 540, 1564, 460, 0, TextAnchor.MiddleCenter, 0.06f);
            _tapText = Label("Tap", layer, "TAP TO START", 38, 540, 1652, 700, 0, TextAnchor.MiddleCenter, 0.08f);
            _tapText.color = GoldText;

            // A tap anywhere starts the next step (no button): a hand over the picture.
            _tapRing = Picture("Ring", layer, RingArt(), 830f, 1250f, 110, 110);
            var hand = HandArt(16f, out var tip);
            _tapHand = Put("Hand", layer, hand, 830f - tip.x, 1254f - (hand.rect.height - tip.y));
        }

        // ================================================================ 2. CUT

        private static Vector2 Map(Vector2 g, float cx, float cy, float size)
        {
            float k = size / 96f;
            return new Vector2(cx + (g.x - 48f) * k, cy - (g.y - 48f) * k);
        }

        private void BuildCutLayer(RectTransform layer)
        {
            // The grown-out head (its picture is set per style in BuildCut), the thumbnail of the target.
            Disc(layer, CutCx, CutCy - 20f, 600f, new Color(1f, 0.95f, 0.82f, 0.5f));
            _cutHead = Picture("Head", layer, null, CutCx, CutCy, CutSize, CutSize);
            _guideRoot = UIFactory.CreateRect("Guides", layer);
            UIFactory.Stretch(_guideRoot);
            _clumpRoot = UIFactory.CreateRect("Clumps", layer);
            UIFactory.Stretch(_clumpRoot);

            _cutThumb = BuildThumb(layer, 766f, 800f);
            _scissors = Picture("Scissors", layer, ScissorsArt(), 196f, 1300f, 150, 150);

            _cutHint = UIFactory.CreateRect("Hint", layer);
            UIFactory.Stretch(_cutHint);
            _hintRing = Picture("Ring", _cutHint, RingArt(), 540f, 1300f, 110, 110);
            _hintHand = Picture("Hand", _cutHint, HandArt(16f, out _hintTip), 540f, 1300f, 100, 100);

            Plate("CutPill", layer, 690, 1534, 250, 62, 28, HudArt.Looks.Tab, out _);
            _cutPill = Label("CutPillText", layer, "LINE 1 / 3", 30, 815, 1566, 240, 0, TextAnchor.MiddleCenter, 0.06f);
            _cutPill.color = Cream;
        }

        /// <summary>The target in a small frame in a corner of the body, so the player can compare as they work.</summary>
        private Image BuildThumb(RectTransform layer, float left, float top)
        {
            Plate("Thumb", layer, left, top, 200, 236, 30, HudArt.Looks.Plaque, out _);
            Picture("ThumbDisc", layer, UIFactory.Circle, left + 100, top + 130, 168, 168).color = new Color(1f, 0.96f, 0.84f);
            var head = Picture("ThumbHead", layer, null, left + 100, top + 130, 168, 168);
            Plate("ThumbTag", layer, left + 14, top - 26, 172, 48, 18, HudArt.Looks.Gold, out _);
            Label("ThumbTagText", layer, "TARGET", 26, left + 100, top - 2, 170, 0, TextAnchor.MiddleCenter, 0.08f);
            return head;
        }

        private static Image Disc(RectTransform layer, float cx, float cy, float size, Color color)
        {
            var rect = UIFactory.CreateRect("Disc", layer);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UIFactory.Circle;
            image.color = color;
            image.raycastTarget = false;
            Place(rect, cx, cy, size, size);
            return image;
        }

        private void BuildCut()
        {
            _cutHead.sprite = HeadArt(_style, Look.Raw, 480);
            for (int i = _guideRoot.childCount - 1; i >= 0; i--) Kill(_guideRoot.GetChild(i).gameObject);
            for (int i = _clumpRoot.childCount - 1; i >= 0; i--) Kill(_clumpRoot.GetChild(i).gameObject);
            _clumps.Clear();
            _guides.Clear(); _guideLength.Clear(); _covered.Clear(); _devSum.Clear(); _devCount.Clear();
            _guideDone.Clear(); _guideDashes.Clear(); _guideChecks.Clear();

            var src = HaircutArt.Guides(_style);
            for (int g = 0; g < src.Count; g++)
            {
                var pts = new Vector2[src[g].Length];
                float len = 0f;
                for (int i = 0; i < pts.Length; i++)
                {
                    pts[i] = Map(src[g][i], CutCx, CutCy, CutSize);
                    if (i > 0) len += Vector2.Distance(pts[i - 1], pts[i]);
                }
                _guides.Add(pts);
                _guideLength.Add(len);
                _covered.Add(new bool[Bins]);
                _devSum.Add(0f);
                _devCount.Add(0);
                _guideDone.Add(false);

                // The dashed line: dark under-stroke and a gold stroke, dash by dash.
                var dashes = new List<Image>();
                const float dash = 40f, gap = 20f;
                float run = 0f;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    float segLen = Vector2.Distance(pts[i], pts[i + 1]);
                    float t = 0f;
                    while (t < segLen)
                    {
                        float step = Mathf.Min(dash, segLen - t);
                        var a = Vector2.Lerp(pts[i], pts[i + 1], t / segLen);
                        var b = Vector2.Lerp(pts[i], pts[i + 1], (t + step) / segLen);
                        dashes.Add(Stroke(_guideRoot, a, b, 17f, new Color(0.15f, 0.08f, 0.04f)));
                        dashes.Add(Stroke(_guideRoot, a, b, 10f, new Color(1f, 0.90f, 0.42f)));
                        t += dash + gap;
                    }
                    run += segLen;
                }
                _guideDashes.Add(dashes);

                var mid = pts[pts.Length / 2];
                var check = Picture("Check" + g, _guideRoot, CheckArt(), mid.x - 56f, mid.y, 62, 62);
                check.gameObject.SetActive(false);
                _guideChecks.Add(check);
            }

            _cutHint.gameObject.SetActive(true);
            _cutHint.anchoredPosition = Vector2.zero;
            var first = _guides[0];
            Move(_scissors.rectTransform, first[0].x - 38f, first[0].y - 4f);
            // The finger hint stands on the first line's start (its fingertip on the point).
            Place(_hintRing.rectTransform, first[0].x, first[0].y, 110f, 110f);
            var handSprite = _hintHand.sprite;
            float hw = handSprite.rect.width, hh = handSprite.rect.height;
            Place(_hintHand.rectTransform, first[0].x - _hintTip.x + hw * 0.5f, first[0].y + 2f - (hh - _hintTip.y) + hh * 0.5f, hw, hh);
            _cutPill.text = $"LINE 1 / {_guides.Count}";
        }

        /// <summary>Destroys at the end of the frame while playing; at once outside play mode (the editor's pictures of the screen).</summary>
        private static void Kill(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        /// <summary>A straight rounded stroke between two design points, as a thin panel.</summary>
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

        /// <summary>Where on a guide a point projects: the distance to the line and how far along it (0..1).</summary>
        private static float Project(Vector2[] pts, float totalLength, Vector2 p, out float along)
        {
            float best = float.MaxValue, bestAlong = 0f, run = 0f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var ab = b - a;
                float l2 = ab.sqrMagnitude;
                float t = l2 < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                float d = Vector2.Distance(p, a + ab * t);
                if (d < best) { best = d; bestAlong = (run + Mathf.Sqrt(l2) * t) / Mathf.Max(1e-3f, totalLength); }
                run += Mathf.Sqrt(l2);
            }
            along = bestAlong;
            return best;
        }

        private void CutSwipe(Vector2 a, Vector2 b)
        {
            float len = Vector2.Distance(a, b);
            // Samples closer than the narrowest line segment (a bin of the shortest line is about 6 units), whatever the finger's speed.
            int n = Mathf.Max(1, Mathf.CeilToInt(len / 3f));
            for (int s = 0; s <= n; s++)
            {
                var p = Vector2.Lerp(a, b, s / (float)n);
                // A point on the swipe counts towards every line it is within reach of (two lines meeting at a corner share the
                // corner), but how far it strays - the accuracy - is held against the nearest line only.
                int nearest = -1;
                float nearestD = CutTolerance;
                for (int g = 0; g < _guides.Count; g++)
                {
                    if (_guideDone[g]) continue;
                    float d = Project(_guides[g], _guideLength[g], p, out float along);
                    if (d >= CutTolerance) continue;
                    _covered[g][Mathf.Clamp(Mathf.FloorToInt(along * Bins), 0, Bins - 1)] = true;
                    if (d < nearestD) { nearestD = d; nearest = g; }
                }
                if (nearest < 0) continue;
                _devSum[nearest] += nearestD;
                _devCount[nearest]++;
            }
            for (int g = 0; g < _guides.Count; g++) TryFinishGuide(g);
        }

        private void TryFinishGuide(int g)
        {
            if (_guideDone[g]) return;
            int hit = 0;
            for (int i = 0; i < Bins; i++) if (_covered[g][i]) hit++;
            // Both ends and nearly all the way between: a swipe that stops half-way has not cut the line.
            if (hit < Bins - 2 || !_covered[g][0] || !_covered[g][Bins - 1]) return;

            _guideDone[g] = true;
            float mean = _devCount[g] > 0 ? _devSum[g] / _devCount[g] : CutTolerance;
            _accuracy = 0f;
            int done = 0;
            for (int i = 0; i < _guides.Count; i++)
                if (_guideDone[i])
                {
                    float m = _devCount[i] > 0 ? _devSum[i] / _devCount[i] : CutTolerance;
                    _accuracy += 1f - Mathf.Clamp01(m / CutTolerance);
                    done++;
                }
            _accuracy = done > 0 ? _accuracy / done : 1f;

            // The line turns solid green with a tick, and the hair falls away.
            var pts = _guides[g];
            foreach (var dash in _guideDashes[g]) if (dash != null) Kill(dash.gameObject);
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Stroke(_guideRoot, pts[i], pts[i + 1], 17f, new Color(0.15f, 0.08f, 0.04f));
                Stroke(_guideRoot, pts[i], pts[i + 1], 10f, new Color(0.45f, 0.95f, 0.45f));
            }
            _guideChecks[g].gameObject.SetActive(true);
            _guideChecks[g].transform.SetAsLastSibling();
            _cutHint.gameObject.SetActive(false);

            var mid = pts[pts.Length / 2];
            for (int c = 0; c < 3; c++)
            {
                var sprite = ClumpArt(_style, ClumpAngles[c]);
                var image = Picture("Clump", _clumpRoot, sprite, mid.x + (c - 1) * 34f, mid.y, 96, 96);
                _clumps.Add(new Clump { Image = image, At = new Vector2(mid.x + (c - 1) * 34f, mid.y), Vy = -150f + c * 60f,
                    Spin = (c % 2 == 0 ? 1f : -1f) * 220f, Rest = 1560f + c * 30f });
            }
            SoundFx.Play(Sfx.Pop);

            int remaining = 0;
            for (int i = 0; i < _guides.Count; i++) if (!_guideDone[i]) remaining++;
            _cutPill.text = remaining == 0 ? "CUT!" : $"LINE {_guides.Count - remaining + 1} / {_guides.Count}";
            if (remaining == 0)
            {
                _cutHead.sprite = HeadArt(_style, Look.Messy, 480);
                _stepDelay = 0.6f;
            }
        }

        // ================================================================ 3. DRY

        private void BuildDry(RectTransform layer)
        {
            Disc(layer, DryCx, DryCy - 10f, 560f, new Color(1f, 0.95f, 0.82f, 0.5f));
            _dryHead = Picture("Head", layer, null, DryCx, DryCy, DrySize, DrySize);
            _dryThumb = BuildThumb(layer, 766f, 800f);

            // The rub path: a wave over the hair, dashed.
            const int n = 48;
            var path = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                path[i] = Map(new Vector2(26f + t * 44f, 68f + 7f * Mathf.Sin(t * Mathf.PI * 7f)), DryCx, DryCy, DrySize);
            }
            for (int i = 0; i < n; i += 2)
            {
                _rubDashes.Add(Stroke(layer, path[i], path[Mathf.Min(n, i + 1)], 16f, new Color(0.15f, 0.08f, 0.04f)));
                _rubDashes.Add(Stroke(layer, path[i], path[Mathf.Min(n, i + 1)], 9f, new Color(1f, 0.90f, 0.42f)));
            }

            _comb = Picture("Comb", layer, CombArt(), path[0].x - 52f, path[0].y - 6f, 150, 150);
            _dryer = Picture("Dryer", layer, DryerArt(), path[0].x - 52f, path[0].y - 6f, 150, 150);
            _dryer.gameObject.SetActive(false);

            _combChip = Chip(layer, 190, 1500, CombArt(), "COMB", true);
            _dryerChip = Chip(layer, 560, 1500, DryerArt(), "BLOW-DRY", false);

            // Neatness meter.
            Plate("Meter", layer, 190, 1620, 700, 62, 31, HudArt.Looks.Tab, out _);
            var fill = UIFactory.CreatePanel("MeterFill", layer, new Color(0.45f, 0.82f, 0.30f));
            _meterFill = fill;
            _meterFillRect = fill.rectTransform;
            Place(_meterFillRect, 200f, 1651f, 4f, 40f);
            Label("MeterText", layer, "NEAT", 32, 320, 1651, 160, 0, TextAnchor.MiddleLeft, 0.08f);
        }

        private Image Chip(RectTransform layer, float left, float top, Sprite icon, string text, bool active)
        {
            Image art;
            Plate("Chip" + text, layer, left, top, 330, 104, 38, active ? HudArt.Looks.Gold : HudArt.Looks.Tab, out art);
            Picture("ChipIcon" + text, layer, icon, left + 72, top + 52, 92, 92);
            var label = Label("ChipText" + text, layer, text, text.Length > 5 ? 28 : 36, left + 218, top + 52, 190, 0, TextAnchor.MiddleCenter, 0.05f);
            label.color = White;
            return art;
        }

        private void BeginDry()
        {
            _dryHead.sprite = HeadArt(_style, Look.Messy, 480);
            _rub = 0f;
            _rubLast = Vector2.zero;
            RefreshDry();
            _comb.gameObject.SetActive(true);
            _dryer.gameObject.SetActive(false);
        }

        private void RefreshDry()
        {
            float p = Mathf.Clamp01(_rub / RubNeeded);
            float width = Mathf.Max(4f, 680f * p);
            Place(_meterFillRect, 200f + width * 0.5f - 0f, 1651f, width, 42f);
            bool dryPhase = p >= 0.5f;
            UIFactory.SetPlate(_combChip, new Vector2(330f, 104f), 38f, dryPhase ? HudArt.Looks.Tab : HudArt.Looks.Gold);
            UIFactory.SetPlate(_dryerChip, new Vector2(330f, 104f), 38f, dryPhase ? HudArt.Looks.Gold : HudArt.Looks.Tab);
            _comb.gameObject.SetActive(!dryPhase && _step == Step.Dry);
            _dryer.gameObject.SetActive(dryPhase && _step == Step.Dry);

            // The dashed path goes as the hair is smoothed: solid green over the first part.
            int cover = Mathf.RoundToInt(p * _rubDashes.Count);
            for (int i = 0; i < _rubDashes.Count; i += 2)
            {
                bool smooth = i < cover;
                _rubDashes[i + 1].color = smooth ? new Color(0.45f, 0.95f, 0.45f) : new Color(1f, 0.90f, 0.42f);
            }
        }

        private void RubAt(Vector2 a, Vector2 b)
        {
            // Only rubbing over the hair counts: the head's top and sides.
            var centre = new Vector2(DryCx, DryCy - 140f);
            if ((b - centre).magnitude > 330f) return;
            float step = Vector2.Distance(a, b);
            if (step > 160f) return;      // a jump, not a rub
            _rub += step;
            if (_comb != null && _comb.gameObject.activeSelf) Move(_comb.rectTransform, b.x - 30f, b.y - 20f);
            if (_dryer != null && _dryer.gameObject.activeSelf) Move(_dryer.rectTransform, b.x - 30f, b.y - 20f);

            float p = Mathf.Clamp01(_rub / RubNeeded);
            RefreshDry();
            if (p >= 1f && _stepDelay <= 0f)
            {
                _dryHead.sprite = HeadArt(_style, Look.Clean, 480);
                _comb.gameObject.SetActive(false);
                _dryer.gameObject.SetActive(false);
                SoundFx.Play(Sfx.Pop);
                _stepDelay = 0.6f;
            }
        }

        // ================================================================ RESULT

        private void BuildResult(RectTransform layer)
        {
            _resultLayer = layer;
            _rays = Picture("Rays", layer, RaysArt(), 540f, 1040f, 820, 820);
            Picture("Disc", layer, UIFactory.Circle, 540f, 1040f, 480, 480).color = new Color(1f, 0.95f, 0.82f, 1f);
            _resultHead = Picture("Head", layer, null, 540f, 1036f, 480, 480);
            Picture("Spark1", layer, SparkArt(64), 300f, 880f, 64, 64);
            Picture("Spark2", layer, SparkArt(44), 790f, 840f, 44, 44);
            Picture("Spark3", layer, SparkArt(52), 812f, 1160f, 52, 52);
            Picture("Spark4", layer, SparkArt(36), 262f, 1190f, 36, 36);

            _starImages = new[]
            {
                Picture("Star1", layer, null, 375f, 1318f, 128, 128),
                Picture("Star2", layer, null, 540f, 1294f, 160, 160),
                Picture("Star3", layer, null, 705f, 1318f, 128, 128),
            };
            _rating = Label("Rating", layer, "GREAT!", 86, 540f, 1428f, 800, 0, TextAnchor.MiddleCenter, 0.06f);
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
            // Accuracy of the cut sets the stars; taking far too long costs one.
            float par = 14f + 4f * _guides.Count;
            _stars = _accuracy >= 0.70f ? 3 : _accuracy >= 0.42f ? 2 : 1;
            if (_clock > par * 2.2f) _stars = Mathf.Max(1, _stars - 1);

            double price = _quote, tip = 0d;
            _paid = _station != null && _station.Finish(_stars, out price, out tip);
            if (!_paid) { price = _quote; tip = _preview ? _quote * 0.45d : 0d; }

            _resultHead.sprite = HeadArt(_style, Look.Shiny, 480);
            for (int i = 0; i < 3; i++)
            {
                _starImages[i].sprite = StarArt(i < _stars);
                _starImages[i].rectTransform.localScale = Vector3.zero;
            }
            _rating.text = _stars >= 3 ? "PERFECT!" : _stars == 2 ? "GREAT!" : "GOOD";
            _priceText.text = MoneyFormat.Short(price);
            _tipText.text = "+" + MoneyFormat.Short(tip);
            _totalText.text = MoneyFormat.Short(price + tip);
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
                    if (_lookLeft < 13.5f) { _stepDelay = 0f; Go(Step.Cut); }
                    break;
                case Step.Dry:
                    _rubLast = p;
                    break;
                case Step.Result:
                    if (_resultLeft < 3.0f) Close();
                    break;
            }
        }

        private void OnDrag(Vector2 p)
        {
            if (!_pressing) return;
            switch (_step)
            {
                case Step.Cut:
                    if (_stepDelay <= 0f) CutSwipe(_last, p);
                    Move(_scissors.rectTransform, Mathf.Clamp(p.x, 150f, 940f), Mathf.Clamp(p.y, 800f, 1700f));
                    break;
                case Step.Dry:
                    if (_stepDelay <= 0f) RubAt(_rubLast, p);
                    _rubLast = p;
                    break;
            }
            _last = p;
        }

        private void OnUp(Vector2 p)
        {
            if (_step == Step.Cut && _stepDelay <= 0f) CutSwipe(_last, p);
            _pressing = false;
        }

        // ================================================================ every frame

        protected override void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.1f);
            _anim += dt;
            if (_step != Step.Result) _clock += dt;

            for (int i = 0; i < _clumps.Count; i++)
            {
                var c = _clumps[i];
                if (c.Image == null || c.At.y >= c.Rest) continue;
                c.Vy += 2400f * dt;
                c.At.y = Mathf.Min(c.Rest, c.At.y + c.Vy * dt);
                Move(c.Image.rectTransform, c.At.x, c.At.y);
                c.Image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, (_anim * c.Spin) % 360f);
            }

            float pulse = 1f + 0.05f * Mathf.Sin(_anim * 6f);
            if (_step == Step.Look)
            {
                _lookLeft -= dt;
                _tapHand.rectTransform.localScale = Vector3.one * pulse;
                _tapText.color = new Color(GoldText.r, GoldText.g, GoldText.b, 0.65f + 0.35f * Mathf.Sin(_anim * 4f));
                if (_lookLeft <= 0f) Go(Step.Cut);
            }
            if (_step == Step.Cut && _cutHint.gameObject.activeSelf)
                _cutHint.anchoredPosition = new Vector2(Mathf.Sin(_anim * 5f) * 14f, 0f);

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
    }
}
