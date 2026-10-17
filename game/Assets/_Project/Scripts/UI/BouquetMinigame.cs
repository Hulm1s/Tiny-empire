using System.Collections;
using System.Collections.Generic;
using Tycoon.Audio;
using Tycoon.Config;
using Tycoon.Core;
using Tycoon.Customers;
using Tycoon.Stations;
using UnityEngine;
using UnityEngine.UI;
using Flower = Tycoon.UI.BouquetArt.Flower;

namespace Tycoon.UI
{
    /// <summary>
    /// The florist's bouquet minigame: what the PLAYER does at the counter instead of handing flowers
    /// over one at a time. Opened by <see cref="BouquetCounter"/> when the player has stood in the
    /// square with a customer waiting and every flower of the bouquet in stock.
    ///
    /// Laid out exactly as the approved mock-ups (Editor/BouquetMock.cs, bouquet-step*.png): the same
    /// header on every step - title, the customer's ORDER (the finished bouquet and each flower x N),
    /// the four step tabs CUT > FILL > TIE > WRAP - and under it the current step's own area:
    ///   1 CUT   swipe across the dashed line to trim every stem
    ///   2 FILL  drag each flower from YOUR FLOWERS into a place that wants it (a tap places it too)
    ///   3 TIE   circle the stems twice to tie the ribbon
    ///   4 WRAP  swipe the paper across, right to left
    ///   result  stars, PRICE + TIP = TOTAL; a tap (or three seconds) closes it
    /// Stars come from the time taken against a par for the bouquet's size, less one for repeated
    /// wrong drops; the tip is the counter's share for that many stars. All art is BouquetArt / HudArt,
    /// drawn once and kept (the flowers, slots and hints are shared between bouquets; the gathered
    /// bunch is drawn per bouquet, a frame at a time while the player cuts and arranges).
    /// </summary>
    public class BouquetMinigame : MinigameOverlay
    {
        public static BouquetMinigame Instance { get; private set; }

        private static readonly Color White = Color.white;
        private static readonly Color Cream = new Color(1f, 0.93f, 0.70f);
        private static readonly Color GoldText = new Color(1f, 0.86f, 0.36f);
        private static readonly Color Spark = new Color(1f, 0.96f, 0.62f);

        // The body: everything under the header belongs to the current step (mock-up numbers).
        private const float InX0 = 109f, InY0 = 669f, InX1 = 971f, InY1 = 1776f;

        // CUT
        private const float CutY = 1452f, HeadY = 885f, EndY = 1668f;

        // TIE
        private const float TieScale = 1.04f, TieBottom = 1700f, TieRadius = 178f;

        // WRAP
        private const float WrapScale = 1.0f, WrapBottom = 1702f, WrapCentre = 460f;

        private enum Step { Cut, Fill, Tie, Wrap, Result }

        // ---- the order --------------------------------------------------------------
        private BouquetCounter _counter;
        private double _quote;
        private readonly List<Flower> _heads = new List<Flower>();          // one per flower, back to front
        private readonly List<ItemDefinition> _kinds = new List<ItemDefinition>();
        private readonly List<int> _kindCounts = new List<int>();
        private Flower[] _headArray;

        // ---- state --------------------------------------------------------------------
        private Step _step;
        private float _clock;            // seconds since open, for the stars
        private int _mistakes;
        private float _stepDelay;        // a beat between a step done and the next shown
        private float _anim;             // free-running, for hints and the result
        private float _resultLeft;
        private int _stars;
        private bool _paid;

        // ---- built once ---------------------------------------------------------------
        private RectTransform[] _lineTabs;
        private Image[] _lineIcons;
        private Text[] _lineCounts;
        private Image[] _stepArt;
        private Image[] _stepBadge;
        private Image[] _stepDisc;
        private Text[] _stepNumber;
        private Text[] _stepName;
        private Text[] _stepChevron;
        private Text _banner;
        private Text _stepPill;
        private RectTransform _stepPillRect;
        private RectTransform[] _layers;
        private MinigamePad _pad;
        private RectTransform _head, _body;
        private GameObject _boardPortrait, _boardTop, _boardBottom;

        // CUT
        private RectTransform _cutStems;
        private readonly List<Image> _stems = new List<Image>();
        private readonly List<float> _stemX = new List<float>();
        private readonly List<bool> _trimmed = new List<bool>();
        private readonly List<Image> _badges = new List<Image>();
        private readonly List<Offcut> _offcuts = new List<Offcut>();
        private Image _scissors;
        private RectTransform _cutHint;
        private Vector2 _last;
        private bool _pressing;

        private class Offcut { public Image Image; public Vector2 At; public float Vy; public float Spin; public float Rest; }

        // FILL
        private RectTransform _fillLayer;
        private readonly List<Image> _slots = new List<Image>();
        private readonly List<Image> _slotIcons = new List<Image>();
        private readonly List<Image> _slotChecks = new List<Image>();
        private readonly List<Vector2> _slotAt = new List<Vector2>();
        private readonly List<int> _slotKind = new List<int>();
        private readonly List<bool> _slotFilled = new List<bool>();
        private Image[] _trayIcons;
        private Image[] _traySlots;
        private Text[] _trayCounts;
        private Vector2[] _trayAt;
        private int[] _trayLeft;
        private Image _dragged;
        private int _dragKind = -1;
        private Vector2 _dragStart;
        private float _shake;
        private int _shakeSlot = -1;

        // TIE
        private Image _tieArrow;
        private Image _tieHand;
        private Text _loopText;
        private Vector2 _tieAt;
        private float _tieAngle;
        private float _tieTurned;
        private bool _tieTracking;
        private int _loops;

        // WRAP
        private Image _wrapArrow;
        private Image _wrapHand;
        private float _wrapFrom = -1f;

        // RESULT
        private Image _rays;
        private Image[] _starImages;
        private Text _rating;
        private Text _priceText, _tipText, _totalText;
        private RectTransform _resultLayer;

        // ---- art: drawn once ----------------------------------------------------------
        private static readonly Dictionary<string, Sprite> Art = new Dictionary<string, Sprite>();

        // The gathered bouquet in four places (header, TIE, WRAP, result), stacked from baked layers.
        private BunchView _orderView, _tieView, _wrapView, _resultView;

        public static BouquetMinigame Create(RectTransform safeArea, GameObject joystick)
        {
            var game = safeArea.gameObject.AddComponent<BouquetMinigame>();
            game.Attach(safeArea, joystick);
            Instance = game;
            return game;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================ the art
        //
        // Every picture here is BouquetArt, a software painter. Painting the big ones on a phone costs
        // seconds (measured: 3-6 s for one bouquet picture in the editor), so ALL of it is drawn once in
        // the editor (Editor/BouquetBake.cs, run by the scene build) into Resources/Bouquet as textures,
        // with each one's anchor (a bloom's centre, a fingertip) and, for the bouquet's layers, the
        // rectangle it was cropped to. At runtime nothing is painted: the textures are loaded, and the
        // gathered bouquet is stacked from its layers (BunchView). If a picture has not been baked it is
        // painted on the spot, so the game still works - slowly - and the editor warns once.

        /// <summary>Bumped whenever a picture's drawing changes, so the scene build re-bakes.</summary>
        public const string ArtVersion = "1";
        public const string ArtFolder = "Bouquet";

        /// <summary>The bouquet's layers are baked at the TIE step's scale, in a frame wide enough for the open paper.</summary>
        public const float BakeScale = 1.04f;

        public static BouquetArt.BunchLayout BakeLayout => new BouquetArt.BunchLayout
        {
            width = Mathf.CeilToInt(820f * BakeScale),
            height = Mathf.CeilToInt(880f * BakeScale),
            scale = BakeScale,
            centreX = 320f * BakeScale,
            baseY = 0f,
        };

        /// <summary>While baking: every picture drawn goes here (key, texture, anchor, crop it).</summary>
        public static System.Action<string, Texture2D, Vector2, bool> BakeSink;

        [System.Serializable]
        public class BakedIndex
        {
            public string version;
            public List<string> keys = new List<string>();
            public List<Vector4> rects = new List<Vector4>();     // x, y, w, h in the drawn canvas (y up)
            public List<Vector2> anchors = new List<Vector2>();
        }

        private static BakedIndex _index;
        private static readonly Dictionary<string, Vector4> Rects = new Dictionary<string, Vector4>();
        private static bool _warned;

        private static void LoadIndex()
        {
            if (_index != null) return;
            var text = Resources.Load<TextAsset>(ArtFolder + "/index");
            _index = text != null ? JsonUtility.FromJson<BakedIndex>(text.text) : new BakedIndex();
            if (_index.version != ArtVersion) _index = new BakedIndex();
            for (int i = 0; i < _index.keys.Count; i++)
            {
                Rects[_index.keys[i]] = _index.rects[i];
                if (_index.anchors[i] != Vector2.zero) Anchors[_index.keys[i]] = _index.anchors[i];
            }
        }

        private static Sprite Cached(string key, System.Func<Texture2D> draw, bool crop = false)
        {
            if (BakeSink != null)
            {
                var drawn = draw();
                BakeSink(key, drawn, Anchors.TryGetValue(key, out var a) ? a : Vector2.zero, crop);
                return null;
            }

            if (Art.TryGetValue(key, out var hit) && hit != null) return hit;

            LoadIndex();
            Texture2D tex = Rects.ContainsKey(key) ? Resources.Load<Texture2D>(ArtFolder + "/" + key) : null;
            if (tex == null)
            {
                if (!_warned) { _warned = true; Debug.LogWarning($"[Bouquet] art '{key}' is not baked; painting it now (slow)."); }
                tex = draw();
                tex.hideFlags = HideFlags.DontSave;
                Rects[key] = new Vector4(0f, 0f, tex.width, tex.height);
            }
            var sprite = BouquetArt.ToSprite(tex);
            Art[key] = sprite;
            return sprite;
        }

        /// <summary>Paints every picture the minigame uses, into <see cref="BakeSink"/> (Editor/BouquetBake.cs).</summary>
        public static void BakeAll()
        {
            foreach (var id in new[] { "rose", "tulip", "sunflower", "hydrangea" })
                foreach (int px in new[] { 130, 192 }) Cached($"product_{id}_{px}", () => BouquetArt.Product(id, px));
            SlotArt(150, BouquetArt.SlotState.Filled);
            SlotArt(200, BouquetArt.SlotState.Filled);
            foreach (BouquetArt.SlotState st in System.Enum.GetValues(typeof(BouquetArt.SlotState))) SlotArt(220, st);
            CheckArt(56); CheckArt(64);
            ScissorsArt(); CutArrowArt(); RingArt(); CircleArrowArt(); WrapArrowArt(); RaysArt();
            foreach (int px in new[] { 64, 44, 52, 36 }) SparkArt(px);
            StarArt(true); StarArt(false);
            foreach (float angle in new[] { 16f, -8f, -12f }) HandSprite(angle, out _);
            foreach (Flower f in System.Enum.GetValues(typeof(Flower)))
                foreach (float k in new[] { 2.0f, 1.6f })
                {
                    StemSprite(f, k, false, out _);
                    StemSprite(f, k, true, out _);
                }
            foreach (float k in new[] { 2.0f, 1.6f }) OffcutArt(k);
            foreach (BouquetArt.Part part in System.Enum.GetValues(typeof(BouquetArt.Part))) PartArt(part);
            for (int n = 1; n <= 6; n++)
                for (int i = 0; i < n; i++)
                {
                    StemPartArt(n, i, false);
                    StemPartArt(n, i, true);
                }
            foreach (Flower f in System.Enum.GetValues(typeof(Flower))) BloomArt(f);
        }

        private static Sprite SlotArt(int px, BouquetArt.SlotState st) =>
            Cached($"slot{px}_{st}", () => BouquetArt.Slot(px, st));
        private static Sprite CheckArt(int px) => Cached("check" + px, () => BouquetArt.CheckBadge(px));
        private static Sprite ScissorsArt() => Cached("scissors150", () => BouquetArt.Scissors(150));
        private static Sprite CutArrowArt() => Cached("cutArrow",
            () => BouquetArt.Arrow(260, 90, new Vector2(16f, 45f), new Vector2(130f, 45f), new Vector2(246f, 45f), 24f));
        private static Sprite RingArt() => Cached("ring110", () => BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.9f)));
        private static int CirclePx => Mathf.CeilToInt((TieRadius + 50f) * 2f);
        private static Sprite CircleArrowArt() => Cached("circleArrow", () => BouquetArt.CircleArrow(CirclePx, TieRadius, -12f, 296f, 30f));
        private static Sprite SparkArt(int px) => Cached("spark" + px, () => BouquetArt.Sparkle(px, Spark));
        private static Sprite RaysArt() => Cached("rays", () => BouquetArt.Rays(820, 18, new Color(1f, 0.90f, 0.45f, 0.75f)));
        private static Sprite StarArt(bool lit) => Cached(lit ? "starOn" : "starOff", () => BouquetArt.Star(180, lit));
        private static int K10(float k) => Mathf.RoundToInt(k * 10f);
        private static Sprite OffcutArt(float k) => Cached($"offcut_{K10(k)}", () => BouquetArt.Offcut(k, EndY - CutY - 6f, 0f));

        /// <summary>The WRAP step's gesture arrow: from the open flap's edge, over and across the stems.</summary>
        private static Sprite WrapArrowArt()
        {
            var L = WrapLayout();
            Vector2 S(float bx, float by) { var t = L.Map(bx, by); return new Vector2(InX0 + t.x, WrapBottom - t.y); }
            var from = S(700f, 250f);
            Vector2 A(float sx, float sy) => new Vector2(sx - 280f, 1580f - sy);
            var fromA = A(from.x - 40f, from.y - 50f);
            return Cached("wrapArrow", () => BouquetArt.Arrow(720, 420, fromA, A(640f, 1215f), A(395f, 1415f), 30f));
        }

        // The bouquet's layers (cropped when baked).
        private static Vector2 TailEnd
        {
            get
            {
                var L = BakeLayout;
                float ang = -28f * Mathf.Deg2Rad;
                var tie = L.Tie;
                return new Vector2(tie.x + TieRadius * Mathf.Cos(ang), tie.y + TieRadius * Mathf.Sin(ang));
            }
        }
        private static Sprite PartArt(BouquetArt.Part part) =>
            Cached("bpart_" + part, () => BouquetArt.BunchPart(BakeLayout, part, TailEnd), true);
        private static Sprite StemPartArt(int n, int i, bool wrapped) =>
            Cached($"bstem_{n}_{i}_{(wrapped ? 1 : 0)}", () => BouquetArt.BunchStem(BakeLayout, n, i, wrapped), true);
        private static Sprite BloomArt(Flower f) =>
            Cached("bbloom_" + f, () => BouquetArt.BunchBloom(BakeLayout, f, 0), true);

        /// <summary>Where a picture sits in the canvas it was drawn on (x, y, w, h; y up).</summary>
        private static Vector4 RectOf(string key, Sprite sprite) =>
            Rects.TryGetValue(key, out var r) ? r : new Vector4(0f, 0f, sprite.rect.width, sprite.rect.height);

        /// <summary>
        /// The gathered bouquet, stacked from baked layers: paper behind, leaves, stems, blooms, paper in
        /// front, ribbon - the same order BouquetArt.Bunch paints them. A container the size of the bake
        /// canvas, scaled and placed so the frame's centre line and foot land where wanted.
        /// </summary>
        private class BunchView
        {
            public readonly RectTransform Root;
            private readonly Image _back, _leaves, _front, _ribbon;
            private readonly Image[] _stems = new Image[6], _blooms = new Image[6];

            public BunchView(RectTransform parent, string name)
            {
                var L = BakeLayout;
                Root = UIFactory.CreateRect(name, parent);
                Root.anchorMin = Root.anchorMax = new Vector2(0f, 1f);
                Root.pivot = Vector2.zero;
                Root.sizeDelta = new Vector2(L.width, L.height);
                _back = Layer("Back");
                _leaves = Layer("Leaves");
                for (int i = 0; i < 6; i++) _stems[i] = Layer("Stem" + i);
                for (int i = 0; i < 6; i++) _blooms[i] = Layer("Bloom" + i);
                _front = Layer("Front");
                _ribbon = Layer("Ribbon");
            }

            private Image Layer(string name)
            {
                var rect = UIFactory.CreateRect(name, Root);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                var image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                rect.gameObject.SetActive(false);
                return image;
            }

            /// <summary>Puts the frame's centre line at design x <paramref name="cx"/>, its foot at design y <paramref name="bottom"/>.</summary>
            public void Place(float cx, float bottom, float scale)
            {
                float f = scale / BakeScale;
                Root.localScale = new Vector3(f, f, 1f);
                Root.anchoredPosition = new Vector2(cx - BakeLayout.centreX * f, -bottom);
            }

            private static void Set(Image image, Sprite sprite, string key, Vector2 offset)
            {
                bool on = sprite != null;
                image.gameObject.SetActive(on);
                if (!on) return;
                image.sprite = sprite;
                var r = RectOf(key, sprite);
                var rect = image.rectTransform;
                rect.anchoredPosition = new Vector2(r.x + offset.x, r.y + offset.y);
                rect.sizeDelta = new Vector2(r.z, r.w);
            }

            public void Show(Flower[] heads, BouquetArt.Wrap wrap, BouquetArt.Ribbon ribbon)
            {
                var L = BakeLayout;
                bool wrapped = wrap != BouquetArt.Wrap.None;
                int n = Mathf.Min(6, heads.Length);
                Set(_back, wrapped ? PartArt(BouquetArt.Part.PaperBack) : null, "bpart_" + BouquetArt.Part.PaperBack, Vector2.zero);
                Set(_leaves, PartArt(BouquetArt.Part.Leaves), "bpart_" + BouquetArt.Part.Leaves, Vector2.zero);
                var head0 = BouquetArt.HeadPosition(L, 0);
                for (int i = 0; i < 6; i++)
                {
                    Set(_stems[i], i < n ? StemPartArt(n, i, wrapped) : null, $"bstem_{n}_{i}_{(wrapped ? 1 : 0)}", Vector2.zero);
                    Set(_blooms[i], i < n ? BloomArt(heads[i]) : null, "bbloom_" + (i < n ? heads[i] : Flower.Rose),
                        BouquetArt.HeadPosition(L, i) - head0);
                }
                var front = wrap == BouquetArt.Wrap.Full ? BouquetArt.Part.PaperFull : BouquetArt.Part.PaperHalf;
                Set(_front, wrapped ? PartArt(front) : null, "bpart_" + front, Vector2.zero);
                var rib = ribbon == BouquetArt.Ribbon.Bow ? BouquetArt.Part.RibbonBow : BouquetArt.Part.RibbonWrapping;
                Set(_ribbon, ribbon != BouquetArt.Ribbon.None ? PartArt(rib) : null, "bpart_" + rib, Vector2.zero);
            }
        }

        private static Flower FlowerOf(ItemDefinition item)
        {
            switch (item != null ? item.id : "")
            {
                case "rose": return Flower.Rose;
                case "tulip": return Flower.Tulip;
                case "sunflower": return Flower.Sunflower;
                default: return Flower.Hydrangea;
            }
        }

        private static Sprite ProductSprite(ItemDefinition item, int px) =>
            Cached($"product_{item.id}_{px}", () => BouquetArt.Product(item.id, px));

        // ================================================================ open / close

        public void Open(BouquetCounter counter, CustomerAgent customer, double quote)
        {
            if (IsOpen || counter == null || customer == null || !customer.IsOrder) return;

            var clock = System.Diagnostics.Stopwatch.StartNew();
            EnsureBuilt();
            long built = clock.ElapsedMilliseconds;
            _counter = counter;
            _quote = quote;

            _kinds.Clear();
            _kindCounts.Clear();
            _heads.Clear();
            for (int i = 0; i < customer.OrderLines; i++)
            {
                int n = customer.OrderRemaining(i);
                if (n <= 0) continue;
                _kinds.Add(customer.OrderItem(i));
                _kindCounts.Add(n);
                for (int k = 0; k < n && _heads.Count < 6; k++) _heads.Add(FlowerOf(customer.OrderItem(i)));
            }
            _headArray = _heads.ToArray();

            _clock = 0f;
            _mistakes = 0;
            _paid = false;

            FillOrder();
            BuildCut();
            BuildFill();
            Show();
            Go(Step.Cut);
            if (Debug.isDebugBuild) Debug.Log($"[Bouquet] opened in {clock.ElapsedMilliseconds} ms (building the screen {built} ms)");
        }

        private void Close()
        {
            if (!_paid && _counter != null) _counter.Abandon();
            _counter = null;
            CancelDrag();
            Hide();
        }

        // ================================================================ building

        protected override void BuildContent(RectTransform card)
        {
            // Two groups, each laid out in the same 1080 x 1920 design coordinates as the whole card: the
            // header (title, order, step tabs) and the body (the step's play area). Portrait stacks them
            // as one card; landscape sets them side by side (ApplyOrientation).
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
            Label("TitleText", head, "BOUQUET", 60, 540, 149, 480, 0, TextAnchor.MiddleCenter, 0.08f);

            // The way out, should the player change their mind: nothing is taken from the stock.
            var close = UIFactory.CreatePlateButton("Close", head, new Vector2(84f, 84f), 30f, HudArt.Looks.Red,
                "X", 44, out _);
            Place(close.GetComponent<RectTransform>(), 975f, 200f, 84f, 84f);
            close.onClick.AddListener(Close);
            // The plate is 84 units - about 30 pt on a phone, too small for a thumb. An invisible,
            // bigger target sits on it (160 units = 58 pt in portrait, 47 pt on a phone held sideways); it clears the pad
            // and every other target, and the click bubbles up to the button.
            var closeHit = UIFactory.CreateRect("Hit", (RectTransform)close.transform);
            closeHit.anchorMin = closeHit.anchorMax = new Vector2(0.5f, 0.5f);
            closeHit.pivot = new Vector2(0.5f, 0.5f);
            closeHit.anchoredPosition = Vector2.zero;
            closeHit.sizeDelta = new Vector2(160f, 160f);
            var closeHitImage = closeHit.gameObject.AddComponent<Image>();
            closeHitImage.color = new Color(0f, 0f, 0f, 0f);
            closeHitImage.raycastTarget = true;

            // ---- ORDER --------------------------------------------------------------
            Plate("Order", head, 100, 250, 880, 275, 40, HudArt.Looks.Plaque, out _);
            Plate("OrderTag", head, 130, 230, 240, 60, 24, HudArt.Looks.Gold, out _);
            Label("OrderText", head, "ORDER", 30, 250, 259, 220, 0, TextAnchor.MiddleCenter, 0.08f);
            // The finished bouquet, small: 0.27 of the frame, centred on (236, 400).
            _orderView = new BunchView(head, "OrderBunch");
            _orderView.Place(236f, 400f + 440f * 0.27f, 0.27f);

            _lineTabs = new RectTransform[3];
            _lineIcons = new Image[3];
            _lineCounts = new Text[3];
            var slot = SlotArt(150, BouquetArt.SlotState.Filled);
            for (int i = 0; i < 3; i++)
            {
                float x = 380 + i * 200;
                _lineTabs[i] = Plate("Line" + i, head, x, 310, 180, 195, 34, HudArt.Looks.Tab, out _);
                Picture("Slot", _lineTabs[i], slot, 90, 80, 150, 150);
                _lineIcons[i] = Picture("Icon", _lineTabs[i], null, 90, 74, 116, 116);
                _lineCounts[i] = Label("Count", _lineTabs[i], "x1", 36, 90, 164, 160, 0, TextAnchor.MiddleCenter, 0.02f);
            }

            // ---- the four steps ----------------------------------------------------------
            string[] names = { "CUT", "FILL", "TIE", "WRAP" };
            _stepArt = new Image[4];
            _stepBadge = new Image[4];
            _stepDisc = new Image[4];
            _stepNumber = new Text[4];
            _stepName = new Text[4];
            _stepChevron = new Text[4];
            for (int i = 0; i < 4; i++)
            {
                float x = 100 + i * 230;
                Plate("Step" + i, head, x, 548, 190, 90, 34, HudArt.Looks.Tab, out _stepArt[i]);
                _stepBadge[i] = Picture("Done", head, CheckArt(56), x + 33, 592, 48, 48);
                _stepDisc[i] = Picture("Disc", head, UIFactory.Circle, x + 33, 593, 36, 36);
                _stepNumber[i] = UIFactory.CreateText("Number", head, (i + 1).ToString(), 22);
                _stepNumber[i].fontStyle = FontStyle.Bold;
                Place(_stepNumber[i].rectTransform, x + 33, 593, 36, 36);
                _stepName[i] = Label("Name", head, names[i], 26, x + 120, 593, 130, 0, TextAnchor.MiddleCenter, 0.06f);
                if (i < 3) _stepChevron[i] = Label("Chevron", head, ">", 30, x + 205, 591, 30);
            }

            // ---- the body ------------------------------------------------------------------
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

            _layers = new RectTransform[5];
            for (int i = 0; i < 5; i++) _layers[i] = Layer("Step" + i + "Layer", body);
            _cutStems = Layer("Stems", _layers[0]);
            _fillLayer = _layers[1];
            BuildTie(_layers[2]);
            BuildWrap(_layers[3]);
            BuildResult(_layers[4]);

            // The banner and the pill sit over every step.
            Plate("Banner", body, 132, 688, 816, 80, 32, HudArt.Looks.Plaque, out _);
            _banner = Label("BannerText", body, "", 34, 540, 727, 790, 0, TextAnchor.MiddleCenter, 0.05f);
            _stepPillRect = Plate("StepPill", body, 420, 1706, 240, 54, 27, HudArt.Looks.Tab, out _);
            _stepPill = Label("StepPillText", body, "STEP 1 / 4", 26, 540, 1732, 230, 0, TextAnchor.MiddleCenter, 0.08f);
            _stepPill.color = GoldText;

            _pad = Pad(body, InX0, InY0, InX1 - InX0, InY1 - InY0);
            _pad.Down += OnDown;
            _pad.Drag += OnDrag;
            _pad.Up += OnUp;
        }

        // ---- landscape ------------------------------------------------------------------------
        // Side by side: the header column on the left (design x 50..1030), the body on the right, both
        // 980 wide with a 40 gap; the header's frame is 586 tall (84..670), the body's 1200 (610..1810), in a card 2080 x 1260 so the frames never touch the screen edge.
        private const float LandW = 2080f, LandH = 1260f;

        protected override Vector2? LandscapeDesign => new Vector2(LandW, LandH);

        protected override void ApplyOrientation(bool landscape)
        {
            _boardPortrait.SetActive(!landscape);
            _boardTop.SetActive(landscape);
            _boardBottom.SetActive(landscape);
            // Design y runs down; local y runs up. The header's centre (y 377) and the body's (y 1210)
            // are brought to the middle of the card.
            _head.anchoredPosition = landscape ? new Vector2(-510f, -583f) : Vector2.zero;
            _body.anchoredPosition = landscape ? new Vector2(510f, 250f) : Vector2.zero;
        }

        /// <summary>The cutting mat's faint grid: a line every 56 units, a stronger one every fourth (one 224-unit tile).</summary>
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

        private void FillOrder()
        {
            for (int i = 0; i < 3; i++)
            {
                bool on = i < _kinds.Count;
                _lineTabs[i].gameObject.SetActive(on);
                if (!on) continue;
                _lineIcons[i].sprite = ProductSprite(_kinds[i], 130);
                _lineCounts[i].text = "x" + _kindCounts[i];
            }

            _orderView.Show(_headArray, BouquetArt.Wrap.Full, BouquetArt.Ribbon.Bow);
        }

        private void ShowSteps(int current)
        {
            for (int i = 0; i < 4; i++)
            {
                bool done = i < current, now = i == current;
                var look = done ? HudArt.Looks.Green : now ? HudArt.Looks.Gold : HudArt.Looks.Tab;
                UIFactory.SetPlate(_stepArt[i], new Vector2(190f, 90f), 34f, look);
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
            ShowSteps((int)step);

            bool result = step == Step.Result;
            _stepPillRect.gameObject.SetActive(!result);
            _stepPill.gameObject.SetActive(!result);
            if (!result) _stepPill.text = $"STEP {(int)step + 1} / 4";

            switch (step)
            {
                case Step.Cut: _banner.text = "SWIPE TO TRIM THE STEMS"; break;
                case Step.Fill: _banner.text = "DRAG THE FLOWERS INTO PLACE"; break;
                case Step.Tie:
                    _banner.text = "CIRCLE THE STEMS TO TIE THEM";
                    _loops = 0;
                    _tieTurned = 0f;
                    _tieTracking = false;
                    _tieView.Show(_headArray, BouquetArt.Wrap.None, BouquetArt.Ribbon.None);
                    _loopText.text = "LOOP 1 / 2";
                    break;
                case Step.Wrap:
                    _banner.text = "SWIPE THE PAPER ACROSS";
                    _wrapFrom = -1f;
                    _wrapView.Show(_headArray, BouquetArt.Wrap.HalfFolded, BouquetArt.Ribbon.Bow);
                    break;
                case Step.Result:
                    _banner.text = "ORDER COMPLETE!";
                    ShowResult();
                    break;
            }
            _banner.fontSize = _banner.text.Length > 26 ? 30 : 34;
        }

        // ---------------------------------------------------------------- the bunch, per bouquet

        private static BouquetArt.BunchLayout TieLayout()
        {
            int w = (int)(InX1 - InX0), h = Mathf.CeilToInt(880f * TieScale);
            return new BouquetArt.BunchLayout { width = w, height = h, scale = TieScale, centreX = 540f - InX0, baseY = 0f };
        }

        private static BouquetArt.BunchLayout WrapLayout()
        {
            int w = (int)(InX1 - InX0), h = Mathf.CeilToInt(880f * WrapScale);
            return new BouquetArt.BunchLayout { width = w, height = h, scale = WrapScale, centreX = WrapCentre - InX0, baseY = 0f };
        }

        // ================================================================ 1. CUT

        private void BuildCut()
        {
            foreach (Transform child in _cutStems) Destroy(child.gameObject);
            _stems.Clear();
            _stemX.Clear();
            _trimmed.Clear();
            _badges.Clear();
            _offcuts.Clear();

            var layer = _cutStems;

            // The part that comes off: a red-tinted strip under the line.
            var strip = UIFactory.CreatePanel("Offcuts", layer, new Color(0.62f, 0.14f, 0.10f, 0.16f));
            Place(strip.rectTransform, (InX0 + InX1) * 0.5f, (CutY + 8f + 1694f) * 0.5f, InX1 - InX0 - 16f, 1694f - CutY - 8f);

            int n = _heads.Count;
            float k = n <= 4 ? 2.0f : 1.6f;

            // The widest bloom sets how close to the panel's edge a stem may stand: the whole flower, head
            // and leaves, stays inside the play area with a margin, and the row is centred on it.
            float half = 0f;
            foreach (var hf in _heads)
            {
                var sp = StemSprite(hf, k, false, out var hh);
                half = Mathf.Max(half, hh.x, sp.rect.width - hh.x);
            }
            float reach = (InX1 - InX0) * 0.5f - 24f - half;       // centre of a stem, at most, from the middle
            float span = Mathf.Min(700f, reach * 2f);
            for (int i = 0; i < n; i++)
            {
                // Up to four stand as in the mock-up, 195 apart and centred; more spread edge to edge (centred too).
                float x = n <= 4 ? 540f + (i - (n - 1) * 0.5f) * 195f : 540f - span * 0.5f + span * i / (n - 1);
                var f = _heads[i];
                var untrimmed = StemSprite(f, k, false, out var head);
                var image = Put("Stem" + i, layer, untrimmed, x - head.x, HeadY - (untrimmed.rect.height - head.y));
                _stems.Add(image);
                _stemX.Add(x);
                _trimmed.Add(false);

                var badge = Picture("Check" + i, layer, CheckArt(64), x + 44f, CutY - 44f, 58, 58);
                badge.gameObject.SetActive(false);
                _badges.Add(badge);
            }

            // The line: a bold dashed guide right across, scissors at the start.
            for (float x = 248f; x < 952f; x += 46f)
            {
                float e = Mathf.Min(x + 28f, 952f);
                var back = UIFactory.CreatePanel("Dash", layer, new Color(0.15f, 0.08f, 0.04f));
                Place(back.rectTransform, (x + e) * 0.5f, CutY, e - x + 9f, 13f);
                var dash = UIFactory.CreatePanel("DashLit", layer, new Color(1f, 0.90f, 0.42f));
                Place(dash.rectTransform, (x + e) * 0.5f, CutY, e - x + 5f, 7f);
            }
            _scissors = Picture("Scissors", layer, Cached("scissors150", () => BouquetArt.Scissors(150)), 196f, CutY - 2f, 150, 150);

            // The hint: a finger on the line and the way to swipe, until the first stem is cut.
            _cutHint = UIFactory.CreateRect("Hint", layer);
            UIFactory.Stretch(_cutHint);
            var arrow = CutArrowArt();
            Put("Arrow", _cutHint, arrow, 600f, CutY - 110f);
            Picture("Ring", _cutHint, Cached("ring110", () => BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.9f))), 540f, CutY, 110, 110);
            var hand = HandSprite(16f, out var tip);
            Put("Hand", _cutHint, hand, 540f - tip.x, CutY + 2f - (hand.rect.height - tip.y));
        }

        /// <summary>A long flower for trimming; <paramref name="head"/> is the bloom's centre in it (y up).</summary>
        private static Sprite StemSprite(Flower f, float k, bool trimmed, out Vector2 head)
        {
            int stemPx = Mathf.RoundToInt((trimmed ? CutY : EndY) - HeadY);
            string key = $"stem_{f}_{K10(k)}_{trimmed}";
            var sprite = Cached(key, () =>
            {
                var tex = BouquetArt.StemFlower(f, k, stemPx, trimmed, out var h);
                Anchors[key] = h;
                return tex;
            });
            head = Anchors[key];
            return sprite;
        }

        /// <summary>The pointing glove; <paramref name="tip"/> is the fingertip in it (y up).</summary>
        private static Sprite HandSprite(float angle, out Vector2 tip)
        {
            string key = "hand_" + angle;
            var sprite = Cached(key, () =>
            {
                var tex = BouquetArt.Hand(230, angle, out var t);
                Anchors[key] = t;
                return tex;
            });
            tip = Anchors[key];
            return sprite;
        }

        /// <summary>The anchor point of each drawn sprite that has one (a bloom's centre, a fingertip).</summary>
        private static readonly Dictionary<string, Vector2> Anchors = new Dictionary<string, Vector2>();

        private void CutAt(Vector2 a, Vector2 b)
        {
            // Every stem the swipe crosses near the line is cut.
            for (int i = 0; i < _stems.Count; i++)
            {
                if (_trimmed[i]) continue;
                float x = _stemX[i];
                if (x < Mathf.Min(a.x, b.x) || x > Mathf.Max(a.x, b.x)) continue;
                float t = Mathf.Approximately(a.x, b.x) ? 0f : (x - a.x) / (b.x - a.x);
                float y = Mathf.Lerp(a.y, b.y, t);
                if (Mathf.Abs(y - CutY) > 75f) continue;
                Trim(i);
            }
        }

        private void Trim(int i)
        {
            _trimmed[i] = true;
            float k = _heads.Count <= 4 ? 2.0f : 1.6f;
            var sprite = StemSprite(_heads[i], k, true, out var head);
            var rect = _stems[i].rectTransform;
            _stems[i].sprite = sprite;
            float x = _stemX[i];
            Place(rect, x - head.x + sprite.rect.width * 0.5f, HeadY - (sprite.rect.height - head.y) + sprite.rect.height * 0.5f,
                sprite.rect.width, sprite.rect.height);
            _badges[i].gameObject.SetActive(true);
            _cutHint.gameObject.SetActive(false);

            // The dry end falls into the strip.
            float length = EndY - CutY - 6f;
            var off = OffcutArt(k);
            var image = Picture("Offcut", _cutStems, off, x, CutY + length * 0.5f, off.rect.width, off.rect.height);
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            _offcuts.Add(new Offcut { Image = image, At = new Vector2(x, CutY + length * 0.5f), Vy = 0f,
                Spin = (i % 2 == 0 ? 1f : -1f) * 260f, Rest = 1630f + (i % 3) * 14f });

            SoundFx.Play(Sfx.Pop);
            if (!_trimmed.Contains(false)) _stepDelay = 0.45f;
        }

        // ================================================================ 2. FILL

        private static readonly Vector2[] SixPlaces =
        {
            new Vector2(430, 870), new Vector2(650, 870), new Vector2(320, 1055),
            new Vector2(540, 1055), new Vector2(760, 1055), new Vector2(540, 1240),
        };

        private void BuildFill()
        {
            foreach (Transform child in _fillLayer) Destroy(child.gameObject);
            _slots.Clear(); _slotIcons.Clear(); _slotChecks.Clear(); _slotAt.Clear(); _slotKind.Clear(); _slotFilled.Clear();

            var layer = _fillLayer;
            int n = _heads.Count;
            var places = Places(n);
            var empty = SlotArt(220, BouquetArt.SlotState.Empty);
            for (int i = 0; i < n; i++)
            {
                int kind = _kinds.FindIndex(it => FlowerOf(it) == _heads[i]);
                var at = places[i];
                _slots.Add(Picture("Place" + i, layer, empty, at.x, at.y, 214, 214));
                var icon = Picture("Ghost" + i, layer, ProductSprite(_kinds[kind], 192), at.x, at.y - 4f, 150, 150);
                icon.color = new Color(0.45f, 0.45f, 0.45f, 0.30f);
                _slotIcons.Add(icon);
                var check = Picture("Check" + i, layer, CheckArt(64), at.x + 62f, at.y - 62f, 56, 56);
                check.gameObject.SetActive(false);
                _slotChecks.Add(check);
                _slotAt.Add(at);
                _slotKind.Add(kind);
                _slotFilled.Add(false);
            }

            Plate("Tray", layer, 130, 1362, 820, 318, 40, HudArt.Looks.Tab, out _);
            Plate("TrayTag", layer, 156, 1340, 262, 46, 23, HudArt.Looks.Gold, out _);
            Label("TrayText", layer, "YOUR FLOWERS", 24, 287, 1363, 250, 0, TextAnchor.MiddleCenter, 0.08f);

            int kinds = _kinds.Count;
            _trayIcons = new Image[kinds];
            _traySlots = new Image[kinds];
            _trayCounts = new Text[kinds];
            _trayAt = new Vector2[kinds];
            _trayLeft = new int[kinds];
            var filled = SlotArt(200, BouquetArt.SlotState.Filled);
            for (int i = 0; i < kinds; i++)
            {
                float x = kinds == 1 ? 540f : kinds == 2 ? 415f + i * 250f : 290f + i * 250f;
                _trayAt[i] = new Vector2(x, 1512f);
                _trayLeft[i] = _kindCounts[i];
                _traySlots[i] = Picture("TraySlot" + i, layer, filled, x, 1512, 186, 186);
                _trayIcons[i] = Picture("TrayIcon" + i, layer, ProductSprite(_kinds[i], 192), x, 1507, 140, 140);
                _trayCounts[i] = Label("TrayCount" + i, layer, "x" + _kindCounts[i], 32, x, 1638, 120, 0, TextAnchor.MiddleCenter, 0.02f);
            }
        }

        /// <summary>Where the places go for a bouquet of n: the mock-up's two, three and one rows, as full as needed.</summary>
        private static List<Vector2> Places(int n)
        {
            var list = new List<Vector2>();
            int[] rows = n <= 3 ? new[] { n } : n == 4 ? new[] { 2, 2 } : n == 5 ? new[] { 2, 3 } : new[] { 2, 3, 1 };
            float[] ys = rows.Length == 1 ? new[] { 1055f } : rows.Length == 2 ? new[] { 960f, 1150f } : new[] { 870f, 1055f, 1240f };
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r]; c++)
                    list.Add(new Vector2(540f + (c - (rows[r] - 1) * 0.5f) * 220f, ys[r]));
            return list;
        }

        private void RefreshTray()
        {
            for (int i = 0; i < _trayIcons.Length; i++)
            {
                bool any = _trayLeft[i] > 0;
                _trayIcons[i].color = any ? White : new Color(1f, 1f, 1f, 0.35f);
                _traySlots[i].color = new Color(1f, 1f, 1f, any ? 1f : 0.45f);
                _trayCounts[i].text = "x" + _trayLeft[i];
                _trayCounts[i].color = new Color(1f, 1f, 1f, any ? 1f : 0.45f);
            }
        }

        private void HighlightPlaces(int kind)
        {
            var empty = SlotArt(220, BouquetArt.SlotState.Empty);
            var target = SlotArt(220, BouquetArt.SlotState.Target);
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slotFilled[i]) continue;
                _slots[i].sprite = kind >= 0 && _slotKind[i] == kind ? target : empty;
            }
        }

        private int TrayAt(Vector2 p)
        {
            for (int i = 0; i < _trayAt.Length; i++)
                if ((p - _trayAt[i]).sqrMagnitude < 100f * 100f) return i;
            return -1;
        }

        private void BeginDrag(int kind, Vector2 at)
        {
            _dragKind = kind;
            _dragStart = at;
            _dragged = Picture("Held", _fillLayer, ProductSprite(_kinds[kind], 192), at.x, at.y - 60f, 190, 190);
            HighlightPlaces(kind);
        }

        private void CancelDrag()
        {
            if (_dragged != null) Destroy(_dragged.gameObject);
            _dragged = null;
            _dragKind = -1;
            if (_slots.Count > 0) HighlightPlaces(-1);
        }

        private void Drop(Vector2 at)
        {
            int kind = _dragKind;
            bool tap = (at - _dragStart).sqrMagnitude < 30f * 30f;
            CancelDrag();
            if (kind < 0) return;

            int best = -1;
            float bestD = float.MaxValue;
            int wrong = -1;
            for (int i = 0; i < _slotAt.Count; i++)
            {
                if (_slotFilled[i]) continue;
                float d = (_slotAt[i] - (at + new Vector2(0f, -60f))).sqrMagnitude;
                bool near = d < 125f * 125f;
                if (_slotKind[i] != kind) { if (near) wrong = i; continue; }
                // A tap fills the first free place that wants this flower; a drag, the one it lands on.
                if (tap) { if (best < 0) best = i; }
                else if (near && d < bestD) { best = i; bestD = d; }
            }

            if (best >= 0)
            {
                FillPlace(best);
                return;
            }

            if (wrong >= 0)
            {
                // Wrong place: it goes back to the tray, and the place shakes.
                _mistakes++;
                _shake = 0.35f;
                _shakeSlot = wrong;
                SoundFx.Play(Sfx.Grumble);
            }
        }

        private void FillPlace(int slot)
        {
            int kind = _slotKind[slot];
            _slotFilled[slot] = true;
            _trayLeft[kind]--;
            _slots[slot].sprite = SlotArt(220, BouquetArt.SlotState.Filled);
            _slotIcons[slot].color = White;
            Place(_slotIcons[slot].rectTransform, _slotAt[slot].x, _slotAt[slot].y - 4f, 158, 158);
            _slotChecks[slot].gameObject.SetActive(true);
            RefreshTray();
            SoundFx.Play(Sfx.Pop);
            if (!_slotFilled.Contains(false)) _stepDelay = 0.45f;
        }

        // ================================================================ 3. TIE

        private void BuildTie(RectTransform layer)
        {
            var L = TieLayout();
            var tieTex = L.Tie;
            _tieAt = new Vector2(InX0 + tieTex.x, TieBottom - tieTex.y);
            int h = Mathf.CeilToInt(880f * TieScale);

            var glow = Picture("Glow", layer, UIFactory.Circle, _tieAt.x, _tieAt.y, (TieRadius + 34f) * 2f, (TieRadius + 34f) * 2f);
            glow.color = new Color(1f, 0.95f, 0.75f, 0.10f);
            _tieView = new BunchView(layer, "Bunch");
            _tieView.Place(540f, TieBottom, TieScale);

            int px = CirclePx;
            _tieArrow = Picture("Circle", layer, CircleArrowArt(),
                _tieAt.x, _tieAt.y, px, px);

            float ang = -28f * Mathf.Deg2Rad;
            var tipS = new Vector2(_tieAt.x + TieRadius * Mathf.Cos(ang), _tieAt.y - TieRadius * Mathf.Sin(ang));
            var hand = HandSprite(-8f, out var tip);
            _tieHand = Put("Hand", layer, hand, tipS.x - tip.x, tipS.y - (hand.rect.height - tip.y));

            Plate("LoopPill", layer, 806f - 105f, 1628f - 31f, 210f, 62f, 31f, HudArt.Looks.Tab, out _);
            _loopText = Label("LoopText", layer, "LOOP 1 / 2", 28, 806f, 1628f, 200, 0, TextAnchor.MiddleCenter, 0.06f);
            _loopText.color = Cream;
        }

        private void TieDrag(Vector2 p)
        {
            Vector2 d = p - _tieAt;
            float r = d.magnitude;
            if (r < 50f || r > 460f) { _tieTracking = false; return; }

            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            if (_tieTracking)
            {
                float step = Mathf.DeltaAngle(_tieAngle, angle);
                // A direction once chosen is kept: going back unwinds.
                _tieTurned += step;
            }
            _tieAngle = angle;
            _tieTracking = true;

            int loops = Mathf.FloorToInt(Mathf.Abs(_tieTurned) / 330f);   // a little forgiving on a full circle
            if (loops <= _loops) return;
            _loops = Mathf.Min(2, loops);
            _tieArrow.gameObject.SetActive(false);
            _tieHand.gameObject.SetActive(false);
            SoundFx.Play(Sfx.Pop);
            if (_loops == 1)
            {
                _tieView.Show(_headArray, BouquetArt.Wrap.None, BouquetArt.Ribbon.Wrapping);
                _loopText.text = "LOOP 2 / 2";
            }
            else
            {
                _tieView.Show(_headArray, BouquetArt.Wrap.None, BouquetArt.Ribbon.Bow);
                _loopText.text = "TIED!";
                _stepDelay = 0.5f;
            }
        }

        // ================================================================ 4. WRAP

        private void BuildWrap(RectTransform layer)
        {
            var L = WrapLayout();
            int h = Mathf.CeilToInt(880f * WrapScale);
            _wrapView = new BunchView(layer, "Bunch");
            _wrapView.Place(WrapCentre, WrapBottom, WrapScale);

            Vector2 S(float bx, float by) { var t = L.Map(bx, by); return new Vector2(InX0 + t.x, WrapBottom - t.y); }
            // The open flap's fold line, dashed.
            var f0 = S(300f, 30f);
            var f1 = S(504f, 420f);
            float len = Vector2.Distance(f0, f1);
            float tilt = Mathf.Atan2(-(f1.y - f0.y), f1.x - f0.x) * Mathf.Rad2Deg;
            for (float dd = 18f; dd < len - 10f; dd += 34f)
            {
                var a = Vector2.Lerp(f0, f1, (dd + 9f) / len);
                var back = UIFactory.CreatePanel("Fold", layer, new Color(0.15f, 0.08f, 0.04f));
                Place(back.rectTransform, a.x, a.y, 22f, 7f);
                back.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                var lit = UIFactory.CreatePanel("FoldLit", layer, White);
                Place(lit.rectTransform, a.x, a.y, 18f, 4f);
                lit.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                back.gameObject.name = "FoldLine";
            }

            var from = S(700f, 250f);
            _wrapArrow = Put("Arrow", layer, WrapArrowArt(), 280f, 1160f);
            var hand = HandSprite(-12f, out var tip);
            _wrapHand = Put("Hand", layer, hand, from.x - tip.x, from.y - (hand.rect.height - tip.y));
        }

        private void WrapDrag(Vector2 p)
        {
            if (_wrapFrom < 0f) return;
            if (_wrapFrom - p.x < 260f) return;
            _wrapFrom = -1f;
            _wrapView.Show(_headArray, BouquetArt.Wrap.Full, BouquetArt.Ribbon.Bow);
            _wrapArrow.gameObject.SetActive(false);
            _wrapHand.gameObject.SetActive(false);
            SoundFx.Play(Sfx.Pop);
            _stepDelay = 0.5f;
        }

        // ================================================================ RESULT

        private void BuildResult(RectTransform layer)
        {
            _resultLayer = layer;
            _rays = Picture("Rays", layer, null, 540f, 1010f, 820, 820);
            _resultView = new BunchView(layer, "Bunch");
            _resultView.Place(540f, 1020f + 440f * 0.6f, 0.6f);
            Picture("Spark1", layer, SparkArt(64), 300f, 860f, 64, 64);
            Picture("Spark2", layer, SparkArt(44), 790f, 820f, 44, 44);
            Picture("Spark3", layer, SparkArt(52), 812f, 1130f, 52, 52);
            Picture("Spark4", layer, SparkArt(36), 262f, 1160f, 36, 36);

            _starImages = new[]
            {
                Picture("Star1", layer, null, 375f, 1318f, 128, 128),
                Picture("Star2", layer, null, 540f, 1294f, 160, 160),
                Picture("Star3", layer, null, 705f, 1318f, 128, 128),
            };
            _rating = Label("Rating", layer, "GREAT!", 78, 540f, 1432f, 800, 0, TextAnchor.MiddleCenter, 0.06f);
            _rating.color = new Color(1f, 0.97f, 0.62f);

            Plate("Bill", layer, 170, 1498, 740, 250, 40, HudArt.Looks.Plaque, out _);
            Label("PriceLabel", layer, "PRICE", 32, 330, 1548, 220, 0, TextAnchor.MiddleLeft, 0.06f).color = Cream;
            _priceText = Label("Price", layer, "$0", 38, 750, 1548, 220, 0, TextAnchor.MiddleRight, 0.03f);
            Label("TipLabel", layer, "TIP", 32, 330, 1608, 220, 0, TextAnchor.MiddleLeft, 0.06f).color = Cream;
            Picture("Coin", layer, HudArt.Coin(56f), 335f, 1609f, 56, 56);
            _tipText = Label("Tip", layer, "+$0", 38, 750, 1608, 220, 0, TextAnchor.MiddleRight, 0.03f);
            var rule = UIFactory.CreatePanel("Rule", layer, new Color(1f, 0.86f, 0.4f, 0.55f));
            Place(rule.rectTransform, 540f, 1648f, 652f, 3f);
            Label("TotalLabel", layer, "TOTAL", 38, 330, 1700, 220, 0, TextAnchor.MiddleLeft, 0.06f).color = GoldText;
            _totalText = Label("Total", layer, "$0", 54, 750, 1700, 260, 0, TextAnchor.MiddleRight, 0.03f);
            _totalText.color = new Color(1f, 0.95f, 0.55f);
        }

        private void ShowResult()
        {
            // Par: a few seconds plus a little per flower. Quick and clean is three stars.
            float par = 6f + 1.2f * _heads.Count;
            _stars = _clock <= par ? 3 : _clock <= par * 1.8f ? 2 : 1;
            if (_mistakes >= 2) _stars = Mathf.Max(1, _stars - 1);

            double price = _quote, tip = 0d;
            _paid = _counter != null && _counter.Finish(_stars, out price, out tip);
            if (!_paid) { price = _quote; tip = 0d; }

            _rays.sprite = RaysArt();
            _resultView.Show(_headArray, BouquetArt.Wrap.Full, BouquetArt.Ribbon.Bow);
            for (int i = 0; i < 3; i++)
            {
                bool lit = i < _stars;
                _starImages[i].sprite = StarArt(lit);
                _starImages[i].rectTransform.localScale = Vector3.zero;
            }
            _rating.text = _stars >= 3 ? "PERFECT!" : _stars == 2 ? "GREAT!" : "GOOD";
            _priceText.text = MoneyFormat.Short(price);
            _tipText.text = "+" + MoneyFormat.Short(tip);
            _totalText.text = MoneyFormat.Short(price + tip);
            _resultLeft = 3.2f;
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
                case Step.Fill:
                    if (_stepDelay > 0f) return;
                    int kind = TrayAt(p);
                    if (kind >= 0 && _trayLeft[kind] > 0) BeginDrag(kind, p);
                    break;
                case Step.Tie:
                    _tieTracking = false;
                    TieDrag(p);
                    break;
                case Step.Wrap:
                    // The swipe starts on the open flap, right of the stems.
                    _wrapFrom = p.x > 560f ? p.x : -1f;
                    break;
                case Step.Result:
                    if (_resultLeft < 2.6f) Close();
                    break;
            }
        }

        private void OnDrag(Vector2 p)
        {
            if (!_pressing) return;
            switch (_step)
            {
                case Step.Cut:
                    if (_stepDelay <= 0f) CutAt(_last, p);
                    Move(_scissors.rectTransform, Mathf.Clamp(p.x, 150f, 940f), CutY - 2f);
                    break;
                case Step.Fill:
                    if (_dragged != null) Move(_dragged.rectTransform, p.x, p.y - 60f);
                    break;
                case Step.Tie:
                    if (_stepDelay <= 0f && _loops < 2) TieDrag(p);
                    break;
                case Step.Wrap:
                    if (_stepDelay <= 0f) WrapDrag(p);
                    break;
            }
            _last = p;
        }

        private void OnUp(Vector2 p)
        {
            if (_step == Step.Cut && _stepDelay <= 0f) CutAt(_last, p);
            if (_step == Step.Fill && _dragKind >= 0) Drop(p);
            _pressing = false;
            _tieTracking = false;
            _wrapFrom = -1f;
        }

        // ================================================================ every frame

        protected override void Tick(float dt)
        {
            dt = Mathf.Min(dt, 0.1f);
            _anim += dt;
            if (_step != Step.Result) _clock += dt;

            // The falling offcuts.
            for (int i = 0; i < _offcuts.Count; i++)
            {
                var o = _offcuts[i];
                if (o.Image == null || o.At.y >= o.Rest) continue;
                o.Vy += 2600f * dt;
                o.At.y = Mathf.Min(o.Rest, o.At.y + o.Vy * dt);
                Move(o.Image.rectTransform, o.At.x, o.At.y);
                float z = o.Image.rectTransform.localEulerAngles.z + o.Spin * dt;
                if (o.At.y >= o.Rest) z = 90f + (o.Spin > 0f ? 14f : -9f);
                o.Image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, z);
            }

            // The hints bob, so they read as "do this" rather than as part of the picture.
            float bob = Mathf.Sin(_anim * 5f) * 8f;
            if (_step == Step.Cut && _cutHint.gameObject.activeSelf) _cutHint.anchoredPosition = new Vector2(bob * 2f, 0f);
            if (_step == Step.Tie && _tieHand.gameObject.activeSelf) _tieHand.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(_anim * 6f));
            if (_step == Step.Wrap && _wrapHand.gameObject.activeSelf) _wrapHand.rectTransform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(_anim * 6f));

            if (_shake > 0f && _shakeSlot >= 0)
            {
                _shake -= dt;
                float off = _shake > 0f ? Mathf.Sin(_anim * 60f) * 10f : 0f;
                Move(_slots[_shakeSlot].rectTransform, _slotAt[_shakeSlot].x + off, _slotAt[_shakeSlot].y);
            }

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
