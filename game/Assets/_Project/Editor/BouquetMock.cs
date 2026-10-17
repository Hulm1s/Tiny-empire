using System;
using System.Collections.Generic;
using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;
using Flower = Tycoon.UI.BouquetArt.Flower;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The bouquet minigame's five screens as still pictures, phone portrait 1080 x 1920, for Filip
    /// to judge before any of it is built: CUT, FILL, TIE, WRAP and the RESULT. Every screen keeps
    /// the same header (title, the customer's order, the four step tabs); everything under it is
    /// the current step's alone.
    ///
    /// The whole minigame is 2D. The frames and plaques are the HUD's own (<see cref="HudArt"/>);
    /// the flowers, ribbon, paper, scissors, hand and gesture arrows are <see cref="BouquetArt"/>,
    /// which the real minigame can use as it is. Lettering is the game's own font (the legacy
    /// runtime font, bold, dark edge - what <c>UIFactory.CreateLabel</c> makes), rasterised by the
    /// GPU, so this needs a run WITHOUT -nographics.
    ///
    /// Nothing here is logic. Writes ../bouquet-step1-cut.png, -step2-arrange, -step3-tie,
    /// -step4-wrap, ../bouquet-result.png and ../bouquet-flowers-2d.png (the four flowers next to
    /// their product icons).
    ///
    /// Run: Tycoon > Export Bouquet Mock, or
    /// <c>-executeMethod Tycoon.EditorTools.BouquetMock.Export</c> (batch mode, no -nographics).
    /// </summary>
    public static class BouquetMock
    {
        private const int Cw = 1080, Ch = 1920;

        private static readonly Color Ink = new Color(0.15f, 0.08f, 0.04f);
        private static readonly Color White = Color.white;
        private static readonly Color Cream = new Color(1f, 0.93f, 0.70f);
        private static readonly Color GoldText = new Color(1f, 0.86f, 0.36f);

        // The body: everything under the header belongs to the current step.
        private const float BodyX = 100f, BodyY = 660f, BodyW = 880f, BodyH = 1125f;
        private const float InX0 = 109f, InY0 = 669f, InX1 = 971f, InY1 = 1776f, InR = 35f;

        [MenuItem("Tycoon/Export Bouquet Mock")]
        public static void Export()
        {
            float keepU = HudSheet.U;
            HudSheet.U = 1f;
            try
            {
                Screen("bouquet-step1-cut.png", 1, DrawCut);
                Screen("bouquet-step2-arrange.png", 2, DrawFill);
                Screen("bouquet-step3-tie.png", 3, DrawTie);
                Screen("bouquet-step4-wrap.png", 4, DrawWrap);
                Screen("bouquet-result.png", 5, DrawResult);
                FlowerSheet("bouquet-flowers-2d.png");
                Debug.Log("BOUQUETMOCK_OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
            finally { HudSheet.U = keepU; }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ the screen

        private static void Begin(int w, int h)
        {
            HudSheet._w = w; HudSheet._h = h;
            HudSheet._ox = 0f; HudSheet._oy = 0f;
            HudSheet._buf = new Color[w * h];
        }

        private static void Save(string file)
        {
            string path = Path.GetFullPath("../" + file);
            var sheet = new Texture2D(HudSheet._w, HudSheet._h, TextureFormat.RGBA32, false);
            sheet.SetPixels(HudSheet._buf);
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log($"[BouquetMock] -> {path}");
        }

        /// <summary>One screen: the shared header, then <paramref name="body"/>. Step 5 is the result.</summary>
        private static void Screen(string file, int step, Action body)
        {
            Begin(Cw, Ch);
            // The farm behind, dimmed: grass with a few soft shapes, then a dark veil.
            HudSheet.Rect(0, 0, Cw, Ch, HudSheet.Grass);
            for (int i = 0; i < 9; i++)
                HudSheet.Rect(40f + (i % 3) * 360f, 80f + (i / 3) * 640f, 300, 120, new Color(1f, 1f, 1f, 0.07f));
            HudSheet.Rect(0, 0, Cw, Ch, new Color(0.02f, 0.04f, 0.06f, 0.70f));

            HudSheet.Plate(50, 150, 980, 1660, 64, HudArt.Looks.Board, false);
            HudSheet.Plate(290, 84, 500, 130, 44, HudArt.Looks.Gold, false);
            Text("BOUQUET", 540, 149, 60, White, 0, 0.08f);

            DrawOrder();
            DrawSteps(step);

            HudSheet.Plate(BodyX, BodyY, BodyW, BodyH, 44, Mat(), false);
            CuttingMat();

            body();
            Save(file);
        }

        private static HudArt.Look Mat()
        {
            var l = HudArt.Looks.Green;
            l.bodyTop = new Color(0.50f, 0.72f, 0.48f);
            l.bodyBottom = new Color(0.30f, 0.54f, 0.34f);
            l.gloss = 0.08f;
            return l;
        }

        /// <summary>A florist's cutting mat: a faint grid, a stronger line every fourth.</summary>
        private static void CuttingMat()
        {
            for (int y = (int)InY0; y < (int)InY1; y++)
                for (int x = (int)InX0; x < (int)InX1; x++)
                {
                    float d = RBox(x + 0.5f, y + 0.5f, (InX0 + InX1) * 0.5f, (InY0 + InY1) * 0.5f, (InX1 - InX0) * 0.5f, (InY1 - InY0) * 0.5f, InR);
                    if (d < 3f) continue;
                    int gx = (x - 120) % 56, gy = (y - 690) % 56;
                    bool major = ((x - 120) / 56) % 4 == 0 && gx < 2 || ((y - 690) / 56) % 4 == 0 && gy < 2;
                    if (gx < 2 || gy < 2) HudSheet.Plot(x, y, new Color(1f, 1f, 1f, major ? 0.10f : 0.05f), 1f);
                }
        }

        private static void DrawOrder()
        {
            HudSheet.Plate(100, 250, 880, 275, 40, HudArt.Looks.Plaque, false);
            HudSheet.Plate(130, 230, 240, 60, 24, HudArt.Looks.Gold, false);
            Text("ORDER", 250, 259, 30, White, 0, 0.08f);

            // The bouquet the customer wants, as it will look.
            Ellipse(236, 506, 80, 13, new Color(0f, 0f, 0f, 0.28f));
            PutBunch(0.27f, BouquetArt.Ribbon.Bow, BouquetArt.Wrap.Full, 236f, 400f);

            string[] ids = { "rose", "tulip", "hydrangea" };
            string[] count = { "x3", "x2", "x1" };
            for (int i = 0; i < 3; i++)
            {
                float x = 380 + i * 200;
                HudSheet.Plate(x, 310, 180, 195, 34, HudArt.Looks.Tab, false);
                PutCentred(BouquetArt.Slot(150, BouquetArt.SlotState.Filled), x + 90, 390, 150);
                PutCentred(BouquetArt.Product(ids[i], 130), x + 90, 384, 116);
                Text(count[i], x + 90, 474, 36, White, 0, 0.02f);
            }
        }

        private static void DrawSteps(int step)
        {
            string[] names = { "CUT", "FILL", "TIE", "WRAP" };
            for (int i = 0; i < 4; i++)
            {
                float x = 100 + i * 230;
                bool done = i + 1 < step, now = i + 1 == step;
                var look = done ? HudArt.Looks.Green : now ? HudArt.Looks.Gold : HudArt.Looks.Tab;
                HudSheet.Plate(x, 548, 190, 90, 34, look, false);
                float a = done || now ? 1f : 0.5f;
                if (done) PutCentred(BouquetArt.CheckBadge(56), x + 33, 592, 48);
                else
                {
                    Disc(x + 33, 593, 17, new Color(1f, 0.98f, 0.92f, a), new Color(0.90f, 0.84f, 0.72f, a), 3.5f, a);
                    Text((i + 1).ToString(), x + 33, 593, 19, new Color(0.40f, 0.22f, 0.06f, a), 0, 0f, null, 0f);
                }
                Text(names[i], x + 120, 593, 26, new Color(1f, 1f, 1f, a), 0, 0.06f);
                if (i < 3) Chevron(x + 205, 593, 11, new Color(1f, 0.86f, 0.4f, done ? 1f : 0.45f));
            }
        }

        /// <summary>The instruction across the top of the body.</summary>
        private static void Banner(string s)
        {
            HudSheet.Plate(132, 688, 816, 80, 32, HudArt.Looks.Plaque, false);
            Text(s, 540, 727, s.Length > 26 ? 30 : 34, White, 0, 0.05f);
        }

        /// <summary>"STEP n / 4" along the foot of the body.</summary>
        private static void StepPill(int n)
        {
            HudSheet.Plate(420, 1706, 240, 54, 27, HudArt.Looks.Tab, false);
            Text($"STEP {n} / 4", 540, 1732, 26, GoldText, 0, 0.08f);
        }

        // ------------------------------------------------------------------ 1. CUT

        private static void DrawCut()
        {
            Banner("SWIPE TO TRIM THE STEMS");
            const float cutY = 1452f, headY = 885f, endY = 1668f, k = 2.0f;
            float[] xs = { 245f, 440f, 635f, 828f };
            Flower[] kinds = { Flower.Rose, Flower.Tulip, Flower.Rose, Flower.Hydrangea };

            // The part that comes off: a red-tinted, hatched strip under the line.
            for (int y = (int)cutY + 8; y < 1694; y++)
                for (int x = (int)InX0; x < (int)InX1; x++)
                {
                    float d = RBox(x + 0.5f, y + 0.5f, (InX0 + InX1) * 0.5f, (InY0 + InY1) * 0.5f, (InX1 - InX0) * 0.5f, (InY1 - InY0) * 0.5f, InR);
                    if (d < 3f) continue;
                    float fade = Mathf.Clamp01((y - cutY - 8f) / 30f) * Mathf.Clamp01((1694f - y) / 30f);
                    bool hatch = ((x + y) % 34) < 11;
                    HudSheet.Plot(x, y, new Color(0.62f, 0.14f, 0.10f, (hatch ? 0.22f : 0.13f) * fade), 1f);
                }

            // Offcuts of the two stems already trimmed, fallen into the strip.
            PutCentred(BouquetArt.Offcut(k, endY - cutY - 6f, 14f), 262f, 1622f);
            PutCentred(BouquetArt.Offcut(k, endY - cutY - 6f, -9f), 470f, 1640f);

            for (int i = 0; i < 4; i++)
            {
                bool trimmed = i < 2;
                int stemPx = Mathf.RoundToInt((trimmed ? cutY : endY) - headY);
                var tex = BouquetArt.StemFlower(kinds[i], k, stemPx, trimmed, out var head);
                Put(tex, xs[i] - head.x, headY - (tex.height - head.y));
            }

            // The line itself: scissors at the start, then a bold dashed guide right across.
            for (float x = 248f; x < 952f; x += 46f)
            {
                float e = Mathf.Min(x + 28f, 952f);
                Line(x, cutY, e, cutY, 9f, Ink);
                Line(x, cutY, e, cutY, 5.5f, new Color(1f, 0.90f, 0.42f));
            }
            PutCentred(BouquetArt.Scissors(150), 196f, cutY - 2f);

            foreach (int i in new[] { 0, 1 })
                PutCentred(BouquetArt.CheckBadge(64), xs[i] + 44f, cutY - 44f, 58);
            PutCentred(BouquetArt.Sparkle(54, new Color(1f, 0.95f, 0.6f)), xs[1] + 40f, cutY + 34f);
            PutCentred(BouquetArt.Sparkle(36, new Color(1f, 0.95f, 0.6f)), xs[1] - 30f, cutY - 26f);

            // The gesture: a finger mid-swipe on the line, its trail behind, the way ahead above.
            const float tipX = 540f;
            var trail = BouquetArt.Trail(320, 60, new Vector2(10f, 30f), new Vector2(305f, 30f), 46f, White);
            Put(trail, tipX - 305f, cutY - 30f);
            var arrow = BouquetArt.Arrow(260, 90, new Vector2(16f, 45f), new Vector2(130f, 45f), new Vector2(246f, 45f), 24f);
            Put(arrow, 600f, cutY - 110f);
            PutCentred(BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.9f)), tipX, cutY);
            var hand = BouquetArt.Hand(240, 16f, out var tip);
            Put(hand, tipX - tip.x, cutY + 2f - (hand.height - tip.y));

            StepPill(1);
        }

        // ------------------------------------------------------------------ 2. FILL

        private static void DrawFill()
        {
            Banner("DRAG THE FLOWERS INTO PLACE");
            Vector2[] at =
            {
                new Vector2(430, 870), new Vector2(650, 870), new Vector2(320, 1055),
                new Vector2(540, 1055), new Vector2(760, 1055), new Vector2(540, 1240),
            };
            string[] id = { "rose", "rose", "rose", "tulip", "tulip", "hydrangea" };
            var state = new[]
            {
                BouquetArt.SlotState.Filled, BouquetArt.SlotState.Filled, BouquetArt.SlotState.Filled,
                BouquetArt.SlotState.Filled, BouquetArt.SlotState.Target, BouquetArt.SlotState.Empty,
            };
            for (int i = 0; i < 6; i++)
            {
                PutCentred(BouquetArt.Slot(220, state[i]), at[i].x, at[i].y, 214);
                var icon = BouquetArt.Product(id[i], 192);
                if (state[i] == BouquetArt.SlotState.Filled)
                {
                    PutCentred(icon, at[i].x, at[i].y - 4f, 158);
                    PutCentred(BouquetArt.CheckBadge(64), at[i].x + 62f, at[i].y - 62f, 56);
                }
                else PutCentred(icon, at[i].x, at[i].y - 4f, 150, 0.30f, 0.55f);
            }

            HudSheet.Plate(130, 1362, 820, 318, 40, HudArt.Looks.Tab, false);
            HudSheet.Plate(156, 1340, 262, 46, 23, HudArt.Looks.Gold, false);
            Text("YOUR FLOWERS", 287, 1363, 21, White, 0, 0.08f);
            string[] tray = { "rose", "tulip", "hydrangea" };
            string[] left = { "x0", "x1", "x1" };
            for (int i = 0; i < 3; i++)
            {
                float x = 290 + i * 250;
                // The rose is used up; the tulip is the one in the player's hand.
                float a = i == 0 ? 0.45f : i == 1 ? 0.6f : 1f;
                PutCentred(BouquetArt.Slot(200, BouquetArt.SlotState.Filled), x, 1512, 186, a);
                PutCentred(BouquetArt.Product(tray[i], 192), x, 1507, 140, i == 2 ? 1f : 0.35f, i == 2 ? 0f : 0.45f);
                Text(left[i], x, 1638, 32, new Color(1f, 1f, 1f, i == 0 ? 0.45f : 1f), 0, 0.02f);
            }

            // The gesture: the tulip lifted out of the tray, and its way to the lit slot.
            Vector2 A(float sx, float sy) => new Vector2(sx - 560f, 1440f - sy);   // arrow canvas 400 x 360 at (560, 1080)
            var arrow = BouquetArt.Arrow(400, 360, A(668f, 1338f), A(880f, 1320f), A(806f, 1166f), 24f, true);
            Put(arrow, 560f, 1080f);
            Ellipse(600, 1488, 72, 15, new Color(0f, 0f, 0f, 0.30f));
            PutCentred(BouquetArt.Product("tulip", 192), 590f, 1388f, 190);
            var hand = BouquetArt.Hand(230, 14f, out var tip);
            Put(hand, 602f - tip.x, 1404f - (hand.height - tip.y));

            StepPill(2);
        }

        // ------------------------------------------------------------------ 3. TIE

        private static void DrawTie()
        {
            Banner("CIRCLE THE STEMS TO TIE THEM");
            const float s = 1.04f, bottom = 1700f;
            int w = (int)(InX1 - InX0), h = Mathf.CeilToInt(880f * s);
            var L = new BouquetArt.BunchLayout { width = w, height = h, scale = s, centreX = 540f - InX0, baseY = 0f };
            var tieTex = L.Tie;
            var tie = new Vector2(InX0 + tieTex.x, bottom - tieTex.y);

            const float rad = 178f;
            // Where the finger (and the ribbon's loose end) is: low on the right of the circle.
            float ang = -28f * Mathf.Deg2Rad;
            var tipS = new Vector2(tie.x + rad * Mathf.Cos(ang), tie.y - rad * Mathf.Sin(ang));
            var tail = new Vector2(tipS.x - InX0, bottom - tipS.y);

            // A pale disc behind the knot, so the action reads.
            Ellipse(tie.x, tie.y, rad + 34f, rad + 34f, new Color(1f, 0.95f, 0.75f, 0.10f));
            var bunch = BouquetArt.Bunch(L, BouquetArt.SampleOrder, BouquetArt.Ribbon.Wrapping, BouquetArt.Wrap.None, tail);
            Put(bunch, InX0, bottom - h);

            int px = Mathf.CeilToInt((rad + 50f) * 2f);
            PutCentred(BouquetArt.CircleArrow(px, rad, -12f, 296f, 30f), tie.x, tie.y);
            PutCentred(BouquetArt.PressRing(100, new Color(1f, 1f, 1f, 0.9f)), tipS.x, tipS.y);
            var hand = BouquetArt.Hand(230, -8f, out var tip);
            Put(hand, tipS.x - tip.x, tipS.y - (hand.height - tip.y));
            Pill("LOOP 1 / 2", 806f, 1628f, 210f);

            StepPill(3);
        }

        // ------------------------------------------------------------------ 4. WRAP

        private static void DrawWrap()
        {
            Banner("SWIPE THE PAPER ACROSS");
            const float s = 1.0f, bottom = 1702f, centre = 460f;
            int w = (int)(InX1 - InX0), h = Mathf.CeilToInt(880f * s);
            var L = new BouquetArt.BunchLayout { width = w, height = h, scale = s, centreX = centre - InX0, baseY = 0f };
            var bunch = BouquetArt.Bunch(L, BouquetArt.SampleOrder, BouquetArt.Ribbon.Bow, BouquetArt.Wrap.HalfFolded);
            Put(bunch, InX0, bottom - h);

            Vector2 S(float bx, float by) { var t = L.Map(bx, by); return new Vector2(InX0 + t.x, bottom - t.y); }
            // Fold line of the open flap.
            var f0 = S(300f, 30f);
            var f1 = S(504f, 420f);
            float len = Vector2.Distance(f0, f1);
            for (float d = 18f; d < len - 10f; d += 34f)
            {
                var a = Vector2.Lerp(f0, f1, d / len);
                var b = Vector2.Lerp(f0, f1, Mathf.Min(1f, (d + 18f) / len));
                Line(a.x, a.y, b.x, b.y, 7f, Ink);
                Line(a.x, a.y, b.x, b.y, 4f, White);
            }

            // The gesture: from the open flap's edge, over and across the stems.
            var from = S(700f, 250f);
            // Arrow canvas: 720 x 420 with its top-left at (280, 1160); its points are y up.
            Vector2 A(float sx, float sy) => new Vector2(sx - 280f, 1580f - sy);
            var arrow = BouquetArt.Arrow(720, 420, A(from.x - 40f, from.y - 50f), A(640f, 1215f), A(395f, 1415f), 30f);
            Put(arrow, 280f, 1160f);
            PutCentred(BouquetArt.PressRing(100, new Color(1f, 1f, 1f, 0.9f)), from.x, from.y);
            var hand = BouquetArt.Hand(230, -12f, out var tip);
            Put(hand, from.x - tip.x, from.y - (hand.height - tip.y));

            StepPill(4);
        }

        // ------------------------------------------------------------------ RESULT

        private static void DrawResult()
        {
            Banner("ORDER COMPLETE!");
            PutCentred(BouquetArt.Rays(820, 18, new Color(1f, 0.90f, 0.45f, 0.75f)), 540f, 1010f);
            PutBunch(0.60f, BouquetArt.Ribbon.Bow, BouquetArt.Wrap.Full, 540f, 1020f);
            var spark = new Color(1f, 0.96f, 0.62f);
            PutCentred(BouquetArt.Sparkle(64, spark), 300f, 860f);
            PutCentred(BouquetArt.Sparkle(44, spark), 790f, 820f);
            PutCentred(BouquetArt.Sparkle(52, spark), 812f, 1130f);
            PutCentred(BouquetArt.Sparkle(36, spark), 262f, 1160f);

            PutCentred(BouquetArt.Star(150, true), 375f, 1318f, 128);
            PutCentred(BouquetArt.Star(180, true), 540f, 1294f, 160);
            PutCentred(BouquetArt.Star(150, false), 705f, 1318f, 128);
            Text("GREAT!", 540f, 1432f, 78, new Color(1f, 0.97f, 0.62f), 0, 0.06f, new Color(1f, 0.70f, 0.14f), 1.3f);

            HudSheet.Plate(170, 1498, 740, 250, 40, HudArt.Looks.Plaque, false);
            Text("PRICE", 220, 1548, 32, Cream, -1, 0.06f);
            Text("$40", 860, 1548, 38, White, 1, 0.03f);
            Text("TIP", 220, 1608, 32, Cream, -1, 0.06f);
            HudSheet.Blit(HudArt.CoinTexture(70, HudSheet.U), 300f, 1574f);
            Text("+$18", 860, 1608, 38, White, 1, 0.03f);
            Line(214, 1648, 866, 1648, 2.5f, new Color(1f, 0.86f, 0.4f, 0.55f));
            Text("TOTAL", 220, 1700, 38, GoldText, -1, 0.06f);
            Text("$58", 860, 1700, 54, new Color(1f, 0.95f, 0.55f), 1, 0.03f, new Color(1f, 0.72f, 0.16f));
        }

        // ------------------------------------------------------------------ the four flowers

        private static void FlowerSheet(string file)
        {
            const int w = 1080, h = 900;
            Begin(w, h);
            HudSheet.Rect(0, 0, w, h, new Color(0.13f, 0.17f, 0.20f));
            HudSheet.Plate(30, 30, 1020, 840, 48, HudArt.Looks.Board, false);
            Text("2D FLOWERS AND THEIR ICONS", 540, 90, 34, Cream, 0, 0.06f);
            Flower[] all = { Flower.Rose, Flower.Tulip, Flower.Sunflower, Flower.Hydrangea };
            string[] names = { "ROSE", "TULIP", "SUNFLOWER", "HYDRANGEA" };
            for (int i = 0; i < 4; i++)
            {
                float x = 160 + i * 253;
                HudSheet.Plate(x - 110, 140, 220, 700, 36, Mat(), false);
                PutCentred(BouquetArt.Product(BouquetArt.Id(all[i]), 192), x, 240, 150);
                Text("ICON", x, 330, 20, new Color(1f, 1f, 1f, 0.7f), 0, 0.1f);
                var tex = BouquetArt.StemFlower(all[i], 1.9f, 330, true, out var head);
                Put(tex, x - head.x, 450f - (tex.height - head.y));
                Text(names[i], x, 812, names[i].Length > 6 ? 19 : 24, White, 0, 0.04f);
            }
            Save(file);
        }

        // ------------------------------------------------------------------ placing art

        private static void PutBunch(float scale, BouquetArt.Ribbon ribbon, BouquetArt.Wrap wrap, float cx, float cy)
        {
            var L = BouquetArt.BunchLayout.Fit(Mathf.CeilToInt(600f * scale), Mathf.CeilToInt(880f * scale), scale);
            var tex = BouquetArt.Bunch(L, BouquetArt.SampleOrder, ribbon, wrap);
            // (cx, cy) is the middle of the bouquet's own height (frame y 440).
            Put(tex, cx - L.centreX, cy - (tex.height - 440f * scale));
        }

        /// <summary>Draws a y-up texture with its top-left at (left, top), 1:1.</summary>
        private static void Put(Texture2D tex, float left, float top, float alpha = 1f)
        {
            var src = tex.GetPixels();
            int tw = tex.width, th = tex.height;
            int l = Mathf.RoundToInt(left), t = Mathf.RoundToInt(top);
            for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    var c = src[(th - 1 - y) * tw + x];
                    if (c.a <= 0f) continue;
                    HudSheet.Plot(l + x, t + y, c, alpha);
                }
        }

        /// <summary>Draws a texture centred at (cx, cy), <paramref name="size"/> px across (its own size if 0).</summary>
        private static void PutCentred(Texture2D tex, float cx, float cy, float size = 0f, float alpha = 1f, float dark = 0f)
        {
            if (size <= 0f || Mathf.Abs(size - tex.width) < 0.5f)
            {
                if (dark <= 0f) { Put(tex, cx - tex.width * 0.5f, cy - tex.height * 0.5f, alpha); return; }
                size = tex.width;
            }
            float scale = size / tex.width;
            int ow = Mathf.CeilToInt(tex.width * scale), oh = Mathf.CeilToInt(tex.height * scale);
            int x0 = Mathf.RoundToInt(cx - ow * 0.5f), y0 = Mathf.RoundToInt(cy - oh * 0.5f);
            int taps = Mathf.Clamp(Mathf.CeilToInt(2f / scale), 2, 6);
            var shadeTo = new Color(0.10f, 0.16f, 0.12f);
            for (int j = 0; j < oh; j++)
                for (int i = 0; i < ow; i++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int ty = 0; ty < taps; ty++)
                        for (int tx = 0; tx < taps; tx++)
                        {
                            float u = (i + (tx + 0.5f) / taps) / ow, v = 1f - (j + (ty + 0.5f) / taps) / oh;
                            var c = tex.GetPixelBilinear(u, v);
                            r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                        }
                    if (a <= 0f) continue;
                    var col = new Color(r / a, g / a, b / a, 1f);
                    if (dark > 0f) col = Color.Lerp(col, shadeTo, dark);
                    HudSheet.Plot(x0 + i, y0 + j, col, a / (taps * taps) * alpha);
                }
        }

        private static void Pill(string s, float cx, float cy, float w)
        {
            HudSheet.Plate(cx - w * 0.5f, cy - 26f, w, 52f, 26f, HudArt.Looks.Tab, false);
            Text(s, cx, cy, 24, Cream, 0, 0.08f);
        }

        // ------------------------------------------------------------------ shapes (px, y down)

        private delegate float Sdf(float x, float y);

        private static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, ex = bx - ax, ey = by - ay;
            float h = Mathf.Clamp01((px * ex + py * ey) / Mathf.Max(1e-5f, ex * ex + ey * ey));
            float dx = px - ex * h, dy = py - ey * h;
            return r - Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float RBox(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x - cx) - (hw - r), qy = Mathf.Abs(y - cy) - (hh - r);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return r - (outside + Mathf.Min(Mathf.Max(qx, qy), 0f));
        }

        private static void Shape(Sdf d, float x0, float y0, float x1, float y1, Color top, Color bottom, float outline, float alpha)
        {
            for (int py = Mathf.FloorToInt(y0 - outline - 2); py <= Mathf.CeilToInt(y1 + outline + 2); py++)
                for (int px = Mathf.FloorToInt(x0 - outline - 2); px <= Mathf.CeilToInt(x1 + outline + 2); px++)
                {
                    float dd = d(px + 0.5f, py + 0.5f);
                    if (outline > 0f)
                    {
                        float oc = Mathf.Clamp01(dd + outline + 0.5f);
                        if (oc > 0f) HudSheet.Plot(px, py, new Color(Ink.r, Ink.g, Ink.b, alpha), oc);
                    }
                    float c = Mathf.Clamp01(dd + 0.5f);
                    if (c <= 0f) continue;
                    float t = Mathf.Clamp01(Mathf.InverseLerp(y1, y0, py + 0.5f));
                    var col = Color.Lerp(bottom, top, t);
                    HudSheet.Plot(px, py, new Color(col.r, col.g, col.b, col.a * alpha), c);
                }
        }

        private static void Disc(float cx, float cy, float r, Color top, Color bottom, float outline, float alpha = 1f) =>
            Shape((x, y) => r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)), cx - r, cy - r, cx + r, cy + r, top, bottom, outline, alpha);

        private static void Line(float ax, float ay, float bx, float by, float r, Color c) =>
            Shape((x, y) => Capsule(x, y, ax, ay, bx, by, r), Mathf.Min(ax, bx) - r, Mathf.Min(ay, by) - r,
                Mathf.Max(ax, bx) + r, Mathf.Max(ay, by) + r, c, c, 0f, 1f);

        private static void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            for (int y = Mathf.FloorToInt(cy - ry - 2); y <= Mathf.CeilToInt(cy + ry + 2); y++)
                for (int x = Mathf.FloorToInt(cx - rx - 2); x <= Mathf.CeilToInt(cx + rx + 2); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // Soft edge: these are shadows and glows.
                    float a = Mathf.Clamp01((1f - d) * 4f);
                    if (a > 0f) HudSheet.Plot(x, y, c, a);
                }
        }

        private static void Chevron(float cx, float cy, float s, Color c)
        {
            Line(cx - s * 0.3f, cy - s * 0.6f, cx + s * 0.3f, cy, s * 0.2f, c);
            Line(cx + s * 0.3f, cy, cx - s * 0.3f, cy + s * 0.6f, s * 0.2f, c);
        }

        // ------------------------------------------------------------------ lettering
        // The game's own font, bold, white with a hard dark edge and a soft drop - what the HUD
        // labels look like. Glyphs come from the dynamic font's atlas, read back through the GPU.

        private static Font _font;
        private static float _capRatio;

        private static Font GameFont()
        {
            if (_font != null) return _font;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _font.RequestCharactersInTexture("H", 100, FontStyle.Bold);
            _font.GetCharacterInfo('H', out var ci, 100, FontStyle.Bold);
            _capRatio = Mathf.Max(0.5f, ci.maxY / 100f);
            return _font;
        }

        private static Color[] ReadAtlas(out int w, out int h, out bool useAlpha)
        {
            var src = _font.material.mainTexture;
            w = src.width; h = src.height;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels();
            UnityEngine.Object.DestroyImmediate(tex);
            useAlpha = false;
            foreach (var c in px)
                if (c.a < 0.98f) { useAlpha = true; break; }
            return px;
        }

        /// <summary>
        /// Lettering with its capitals centred on y. align 0 centred, -1 starts at x, 1 ends at x.
        /// <paramref name="bottom"/> gives a vertical gradient; <paramref name="edgeScale"/> 0 drops the edge.
        /// </summary>
        private static void Text(string s, float x, float y, float cap, Color fill, int align, float track,
            Color? bottom = null, float edgeScale = 1f)
        {
            var font = GameFont();
            int size = Mathf.RoundToInt(cap / _capRatio);
            font.RequestCharactersInTexture(s, size, FontStyle.Bold);
            var atlas = ReadAtlas(out int aw, out int ah, out bool useAlpha);

            float Sample(Vector2 uv)
            {
                float fx = uv.x * aw - 0.5f, fy = uv.y * ah - 0.5f;
                int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                float tx = fx - x0, ty = fy - y0;
                float At(int ix, int iy)
                {
                    if (ix < 0 || iy < 0 || ix >= aw || iy >= ah) return 0f;
                    var c = atlas[iy * aw + ix];
                    return useAlpha ? c.a : c.r;
                }
                return Mathf.Lerp(Mathf.Lerp(At(x0, y0), At(x0 + 1, y0), tx), Mathf.Lerp(At(x0, y0 + 1), At(x0 + 1, y0 + 1), tx), ty);
            }

            // Lay out.
            var infos = new List<CharacterInfo>();
            var pens = new List<float>();
            float pen = 0f, ascent = 0f, descent = 0f;
            foreach (char ch in s)
            {
                font.GetCharacterInfo(ch, out var ci, size, FontStyle.Bold);
                infos.Add(ci);
                pens.Add(pen);
                pen += ci.advance + track * cap;
                ascent = Mathf.Max(ascent, ci.maxY);
                descent = Mathf.Min(descent, ci.minY);
            }
            float width = pen - track * cap;

            float edge = edgeScale <= 0f ? 0f : Mathf.Clamp(cap * 0.12f, 3f, 11f) * edgeScale;
            float drop = edge > 0f ? Mathf.Max(2f, edge * 0.55f) : 0f;
            int pad = Mathf.CeilToInt(edge + drop + 3f);
            int mw = Mathf.CeilToInt(width) + pad * 2, mh = Mathf.CeilToInt(ascent - descent) + pad * 2;
            var mask = new float[mw * mh];
            float baseRow = pad + ascent;   // mask row of the baseline (rows go down)

            for (int g = 0; g < infos.Count; g++)
            {
                var ci = infos[g];
                int gw = ci.maxX - ci.minX, gh = ci.maxY - ci.minY;
                if (gw <= 0 || gh <= 0) continue;
                for (int j = 0; j < gh; j++)
                    for (int i = 0; i < gw; i++)
                    {
                        float fx = (i + 0.5f) / gw, fy = (j + 0.5f) / gh;   // fy from the glyph's bottom
                        var uv = ci.uvBottomLeft + (ci.uvBottomRight - ci.uvBottomLeft) * fx + (ci.uvTopLeft - ci.uvBottomLeft) * fy;
                        float cov = Sample(uv);
                        if (cov <= 0f) continue;
                        int mx = pad + Mathf.RoundToInt(pens[g]) + ci.minX + i;
                        int my = Mathf.RoundToInt(baseRow - ci.minY) - 1 - j;
                        if (mx < 0 || my < 0 || mx >= mw || my >= mh) continue;
                        mask[my * mw + mx] = Mathf.Max(mask[my * mw + mx], cov);
                    }
            }

            // The edge: the letters grown by a disc of radius `edge`.
            var outline = new float[mw * mh];
            if (edge > 0f)
            {
                int er = Mathf.CeilToInt(edge + 1f);
                var offs = new List<Vector3>();
                for (int dy = -er; dy <= er; dy++)
                    for (int dx = -er; dx <= er; dx++)
                    {
                        float w = Mathf.Clamp01(edge + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                        if (w > 0f) offs.Add(new Vector3(dx, dy, w));
                    }
                for (int my = 0; my < mh; my++)
                    for (int mx = 0; mx < mw; mx++)
                    {
                        float best = 0f;
                        foreach (var o in offs)
                        {
                            int sx = mx + (int)o.x, sy = my + (int)o.y;
                            if (sx < 0 || sy < 0 || sx >= mw || sy >= mh) continue;
                            float v = mask[sy * mw + sx] * o.z;
                            if (v > best) { best = v; if (best >= 1f) break; }
                        }
                        outline[my * mw + mx] = best;
                    }
            }

            float left = align == 0 ? x - width * 0.5f : align < 0 ? x : x - width;
            int ox = Mathf.RoundToInt(left) - pad;
            int oy = Mathf.RoundToInt(y + cap * 0.5f - baseRow);
            var edgeCol = new Color(0.10f, 0.078f, 0.063f, fill.a);
            int dropPx = Mathf.RoundToInt(drop);
            for (int my = 0; my < mh; my++)
                for (int mx = 0; mx < mw; mx++)
                {
                    if (dropPx > 0)
                    {
                        int sy = my - dropPx;
                        float sh = sy >= 0 ? outline[sy * mw + mx] : 0f;
                        if (sh > 0f) HudSheet.Plot(ox + mx, oy + my, new Color(0f, 0f, 0f, 0.35f * fill.a), sh);
                    }
                    float e = outline[my * mw + mx];
                    if (e > 0f) HudSheet.Plot(ox + mx, oy + my, edgeCol, e);
                    float m = mask[my * mw + mx];
                    if (m <= 0f) continue;
                    var col = fill;
                    if (bottom.HasValue)
                    {
                        float t = Mathf.Clamp01((my - (baseRow - cap)) / cap);
                        col = Color.Lerp(fill, bottom.Value, t);
                        col.a = fill.a;
                    }
                    HudSheet.Plot(ox + mx, oy + my, col, m);
                }
        }
    }
}
