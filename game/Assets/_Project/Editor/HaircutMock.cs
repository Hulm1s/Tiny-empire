using System;
using System.Collections.Generic;
using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;
using Style = Tycoon.UI.HaircutArt.Style;
using Look = Tycoon.UI.HaircutArt.HairLook;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The haircut minigame's four screens as still pictures, for Filip to judge before any of it is built (PROPOSAL,
    /// phase 1): LOOK (the target style), CUT (swipe along the dashed lines), DRY (rub over the hair: comb, then
    /// blow-dry) and RESULT (stars, price, tip). Each is drawn twice, in the two arrangements the shared minigame
    /// frame (<see cref="Tycoon.UI.MinigameOverlay"/>) takes: the portrait card 1080 x 1920 and the landscape card
    /// 2080 x 1260, which sets the header column and the body side by side exactly as the bouquet minigame does. The
    /// same board, plaques, tabs, banner and pill as the bouquet (<see cref="BouquetMock"/>, whose drawing helpers this
    /// uses); the head, comb and dryer are <see cref="HaircutArt"/>, the gestures (scissors, hand, stars) are
    /// <see cref="BouquetArt"/>. Also writes the hairstyle icon sheet (icons at three sizes and in the order bubble).
    ///
    /// Run: <c>-executeMethod Tycoon.EditorTools.HaircutMock.Export</c> (batch mode, no -nographics: the lettering is
    /// rasterised by the GPU). Writes haircut-{p|l}-{1..4}.png and salon-icons.png into the preview folder; a script
    /// then composes the phone-sized sheets.
    /// </summary>
    public static class HaircutMock
    {
        private const string OutDir = "C:/Users/ADMINI~1/AppData/Local/Temp/claude/D--iosGame/b50262dc-8611-42da-8650-85247e0c766c/scratchpad/salon-preview/mock/";

        private static Color Ink => BouquetMock.Ink;
        private static Color White => Color.white;
        private static Color Cream => BouquetMock.Cream;
        private static Color GoldText => BouquetMock.GoldText;

        private static readonly Style Target = Style.Bob;
        private const int Price = 45, Tip = 20;

        // The body, as the bouquet's.
        private const float BodyX = 100f, BodyY = 660f, BodyW = 880f, BodyH = 1125f;

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        private static Texture2D HeadTex(Style s, Look look, int px)
        {
            string key = $"{s}_{look}_{px}";
            if (!Cache.TryGetValue(key, out var t)) Cache[key] = t = HaircutArt.Head(s, px, look);
            return t;
        }

        [MenuItem("Tycoon/Export Haircut Mock")]
        public static void Export()
        {
            float keepU = HudSheet.U;
            HudSheet.U = 1f;
            try
            {
                Directory.CreateDirectory(OutDir);
                IconSheet();
                for (int step = 1; step <= 4; step++)
                {
                    Screen(false, step);
                    Screen(true, step);
                }
                Debug.Log("HAIRCUTMOCK_OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                throw;
            }
            finally { HudSheet.U = keepU; HudSheet._sx = HudSheet._sy = 0; }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void Save(string file)
        {
            HudSheet._sx = HudSheet._sy = 0;
            var sheet = new Texture2D(HudSheet._w, HudSheet._h, TextureFormat.RGBA32, false);
            sheet.SetPixels(HudSheet._buf);
            sheet.Apply();
            File.WriteAllBytes(OutDir + file, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log("[HaircutMock] -> " + OutDir + file);
        }

        // ------------------------------------------------------------------ the screen

        private static void Shift(float dx, float dy) { HudSheet._sx = Mathf.RoundToInt(dx); HudSheet._sy = Mathf.RoundToInt(dy); }

        private static void Screen(bool land, int step)
        {
            int w = land ? 2080 : 1080, h = land ? 1260 : 1920;
            BouquetMock.Begin(w, h);
            Shift(0, 0);
            HudSheet.Rect(0, 0, w, h, HudSheet.Grass);
            for (int i = 0; i < 12; i++)
                HudSheet.Rect(40f + (i % 4) * 500f, 80f + (i / 4) * 400f, 300, 120, new Color(1f, 1f, 1f, 0.07f));
            HudSheet.Rect(0, 0, w, h, new Color(0.02f, 0.04f, 0.06f, 0.70f));

            if (!land)
            {
                HudSheet.Plate(50, 150, 980, 1660, 64, HudArt.Looks.Board, false);
                Header(step);
                Body(step);
            }
            else
            {
                // The header column on the left, the body on the right: the numbers of BouquetMinigame.ApplyOrientation
                // (header 84..670 centred in the card, body 610..1810 centred, 40 apart).
                Shift(0, 253);
                HudSheet.Plate(50, 150, 980, 520, 64, HudArt.Looks.Board, false);
                Header(step);
                Shift(1030, -580);
                HudSheet.Plate(50, 610, 980, 1200, 64, HudArt.Looks.Board, false);
                Body(step);
            }
            Save($"haircut-{(land ? "l" : "p")}-{step}.png");
        }

        // ------------------------------------------------------------------ header: title, order, steps

        private static void Header(int step)
        {
            HudSheet.Plate(290, 84, 500, 130, 44, HudArt.Looks.Gold, false);
            BouquetMock.Text("HAIRCUT", 540, 149, 60, White, 0, 0.08f);

            HudSheet.Plate(100, 250, 880, 275, 40, HudArt.Looks.Plaque, false);
            HudSheet.Plate(130, 230, 240, 60, 24, HudArt.Looks.Gold, false);
            BouquetMock.Text("ORDER", 250, 259, 30, White, 0, 0.08f);

            // The customer's wish: the style in a round slot, its name, the base price and what the tip is for.
            BouquetMock.PutCentred(BouquetArt.Slot(210, BouquetArt.SlotState.Filled), 236, 405, 210);
            BouquetMock.PutCentred(HeadTex(Target, Look.Clean, 192), 236, 402, 160);
            BouquetMock.Text(HaircutArt.Names[(int)Target], 400, 372, 62, White, -1, 0.04f);
            BouquetMock.Text("BASE", 400, 450, 30, Cream, -1, 0.06f);
            HudSheet.Blit(HudArt.CoinTexture(70, HudSheet.U), 515, 413);
            BouquetMock.Text("$" + Price, 596, 450, 44, White, -1, 0.03f);
            for (int i = 0; i < 3; i++) BouquetMock.PutCentred(BouquetArt.Star(80, false), 780 + i * 72, 372, 58);
            BouquetMock.Text("TIP: HOW CLOSE", 846, 450, 20, Cream, 0, 0.03f);

            string[] names = { "LOOK", "CUT", "DRY" };
            for (int i = 0; i < 3; i++)
            {
                float x = 100 + i * 300;
                bool done = i + 1 < step, now = i + 1 == step;
                var look = done ? HudArt.Looks.Green : now ? HudArt.Looks.Gold : HudArt.Looks.Tab;
                HudSheet.Plate(x, 548, 280, 90, 34, look, false);
                float a = done || now ? 1f : 0.5f;
                if (done) BouquetMock.PutCentred(BouquetArt.CheckBadge(56), x + 40, 592, 48);
                else
                {
                    BouquetMock.Disc(x + 40, 593, 17, new Color(1f, 0.98f, 0.92f, a), new Color(0.90f, 0.84f, 0.72f, a), 3.5f, a);
                    BouquetMock.Text((i + 1).ToString(), x + 40, 593, 19, new Color(0.40f, 0.22f, 0.06f, a), 0, 0f, null, 0f);
                }
                BouquetMock.Text(names[i], x + 160, 593, 28, new Color(1f, 1f, 1f, a), 0, 0.08f);
                if (i < 2) BouquetMock.Chevron(x + 292, 593, 11, new Color(1f, 0.86f, 0.4f, done ? 1f : 0.45f));
            }
        }

        // ------------------------------------------------------------------ body

        private static void Body(int step)
        {
            HudSheet.Plate(BodyX, BodyY, BodyW, BodyH, 44, BouquetMock.Mat(), false);
            BouquetMock.CuttingMat();
            switch (step)
            {
                case 1: DrawLook(); break;
                case 2: DrawCut(); break;
                case 3: DrawDry(); break;
                default: DrawResult(); break;
            }
        }

        private static void Banner(string s)
        {
            HudSheet.Plate(132, 688, 816, 80, 32, HudArt.Looks.Plaque, false);
            BouquetMock.Text(s, 540, 727, s.Length > 26 ? 30 : 34, White, 0, 0.05f);
        }

        private static void StepPill(int n)
        {
            HudSheet.Plate(420, 1706, 240, 54, 27, HudArt.Looks.Tab, false);
            BouquetMock.Text($"STEP {n} / 3", 540, 1732, 26, GoldText, 0, 0.08f);
        }

        private static void Sparkles(float cx, float cy, float spread)
        {
            var spark = new Color(1f, 0.96f, 0.62f);
            BouquetMock.PutCentred(BouquetArt.Sparkle(64, spark), cx - spread, cy - spread * 0.35f);
            BouquetMock.PutCentred(BouquetArt.Sparkle(44, spark), cx + spread * 0.95f, cy - spread * 0.6f);
            BouquetMock.PutCentred(BouquetArt.Sparkle(52, spark), cx + spread * 0.85f, cy + spread * 0.55f);
            BouquetMock.PutCentred(BouquetArt.Sparkle(36, spark), cx - spread * 0.95f, cy + spread * 0.6f);
        }

        /// <summary>The target in a small frame in the corner, so the player can compare as they work.</summary>
        private static void Thumb(float left, float top)
        {
            HudSheet.Plate(left, top, 200, 236, 30, HudArt.Looks.Plaque, false);
            BouquetMock.Disc(left + 100, top + 130, 80, new Color(1f, 0.96f, 0.84f), new Color(0.94f, 0.86f, 0.70f), 0f);
            BouquetMock.PutCentred(HeadTex(Target, Look.Clean, 192), left + 100, top + 130, 168);
            HudSheet.Plate(left + 24, top - 18, 152, 40, 16, HudArt.Looks.Gold, false);
            BouquetMock.Text("TARGET", left + 100, top + 2, 20, White, 0, 0.08f);
        }

        // ---- 1. LOOK -----------------------------------------------------------------------------------

        private static void DrawLook()
        {
            Banner("COPY THIS STYLE");
            HudSheet.Plate(180, 810, 720, 700, 44, HudArt.Looks.Plaque, false);
            HudSheet.Plate(210, 790, 250, 60, 24, HudArt.Looks.Gold, false);
            BouquetMock.Text("TARGET", 335, 819, 28, White, 0, 0.08f);
            BouquetMock.PutCentred(BouquetArt.Rays(640, 16, new Color(1f, 0.90f, 0.45f, 0.22f)), 540f, 1150f);
            BouquetMock.Disc(540, 1150, 290, new Color(1f, 0.96f, 0.84f), new Color(0.94f, 0.86f, 0.70f), 0f);
            BouquetMock.PutCentred(HeadTex(Target, Look.Clean, 480), 540, 1145, 600);
            Sparkles(540, 1150, 300);

            HudSheet.Plate(330, 1535, 420, 100, 38, HudArt.Looks.Gold, false);
            BouquetMock.Text(HaircutArt.Names[(int)Target], 540, 1585, 56, White, 0, 0.06f);
            BouquetMock.Text("MEMORISE THE SHAPE", 540, 1668, 28, Cream, 0, 0.08f);
            StepPill(1);
            // A tap anywhere starts the next step (no button): a hand over the picture.
            var hand = BouquetArt.Hand(210, 16f, out var tip);
            BouquetMock.PutCentred(BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.85f)), 830f, 1250f);
            BouquetMock.Put(hand, 830f - tip.x, 1254f - (hand.height - tip.y));
            BouquetMock.Text("TAP TO START", 540, 1478, 28, GoldText, 0, 0.08f);
        }

        // ---- 2. CUT ------------------------------------------------------------------------------------

        private static Vector2 Map(Vector2 g, float cx, float cy, float size)
        {
            float k = size / 96f;
            return new Vector2(cx + (g.x - 48f) * k, cy - (g.y - 48f) * k);
        }

        private static void Dashed(Vector2[] pts, Color core, float width)
        {
            const float dash = 40f, gap = 20f;
            float run = 0f; bool on = true; float left = dash;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                float len = Vector2.Distance(a, b); float t = 0f;
                while (t < len)
                {
                    float step = Mathf.Min(left, len - t);
                    if (on)
                    {
                        var p0 = Vector2.Lerp(a, b, t / len); var p1 = Vector2.Lerp(a, b, (t + step) / len);
                        BouquetMock.Line(p0.x, p0.y, p1.x, p1.y, width + 3.5f, Ink);
                        BouquetMock.Line(p0.x, p0.y, p1.x, p1.y, width, core);
                    }
                    t += step; left -= step;
                    if (left <= 0.01f) { on = !on; left = on ? dash : gap; }
                }
                run += len;
            }
        }

        private static void Solid(Vector2[] pts, Color core, float width)
        {
            for (int i = 0; i < pts.Length - 1; i++)
                BouquetMock.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, width + 3.5f, Ink);
            for (int i = 0; i < pts.Length - 1; i++)
                BouquetMock.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, width, core);
        }

        private static void DrawCut()
        {
            Banner("SWIPE ALONG THE DASHED LINES");
            const float cx = 500f, cy = 1215f, size = 700f;
            BouquetMock.Disc(cx, cy - 20, 300, new Color(1f, 0.96f, 0.84f, 0.5f), new Color(0.94f, 0.86f, 0.70f, 0.5f), 0f);
            BouquetMock.PutCentred(HeadTex(Target, Look.Raw, 480), cx, cy, size);

            var guides = HaircutArt.Guides(Target);
            var done = new Color(0.45f, 0.95f, 0.45f);
            var gold = new Color(1f, 0.90f, 0.42f);
            // Offcuts of the first line, fallen on the floor of the mat.
            BouquetMock.PutCentred(HaircutArt.Clump(Target, 96, 200f), 262f, 1460f);
            BouquetMock.PutCentred(HaircutArt.Clump(Target, 80, 150f), 320f, 1530f);
            BouquetMock.PutCentred(HaircutArt.Clump(Target, 70, 240f), 220f, 1560f);

            var pts0 = Array.ConvertAll(guides[0], g => Map(g, cx, cy, size));
            var pts1 = Array.ConvertAll(guides[1], g => Map(g, cx, cy, size));
            var pts2 = Array.ConvertAll(guides[2], g => Map(g, cx, cy, size));
            Solid(pts0, done, 6f);
            BouquetMock.PutCentred(BouquetArt.CheckBadge(64), pts0[0].x - 52f, (pts0[0].y + pts0[pts0.Length - 1].y) * 0.5f, 60);
            Dashed(pts2, gold, 5.5f);
            Dashed(pts1, gold, 5.5f);

            // The swipe in progress on the second line: scissors at its start, a trail, the finger mid-way.
            var a = pts1[0]; var b = pts1[pts1.Length - 1];
            var fingerAt = Vector2.Lerp(a, b, 0.5f);
            BouquetMock.Line(a.x, a.y, fingerAt.x, fingerAt.y, 15f, new Color(1f, 1f, 1f, 0.55f));
            BouquetMock.PutCentred(BouquetArt.Scissors(150), a.x - 38f, a.y - 4f);
            BouquetMock.PutCentred(BouquetArt.Sparkle(54, new Color(1f, 0.95f, 0.6f)), fingerAt.x - 30f, fingerAt.y + 38f);
            BouquetMock.PutCentred(BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.9f)), fingerAt.x, fingerAt.y);
            var hand = BouquetArt.Hand(230, 14f, out var tip);
            BouquetMock.Put(hand, fingerAt.x - tip.x, fingerAt.y + 2f - (hand.height - tip.y));
            // Chevrons ahead of the finger.
            for (int i = 0; i < 3; i++)
            {
                float x = fingerAt.x + 60f + i * 34f;
                BouquetMock.Chevron(x, fingerAt.y - 34f, 24f, new Color(1f, 1f, 1f, 0.95f - i * 0.22f));
            }
            BouquetMock.Pill("LINE 2 / 3", 806f, 1560f, 230f);
            Thumb(766f, 800f);
            StepPill(2);
        }

        // ---- 3. DRY ------------------------------------------------------------------------------------

        private static void DrawDry()
        {
            Banner("RUB OVER THE HAIR TO SMOOTH IT");
            const float cx = 500f, cy = 1130f, size = 640f;
            BouquetMock.Disc(cx, cy - 10, 280, new Color(1f, 0.96f, 0.84f, 0.5f), new Color(0.94f, 0.86f, 0.70f, 0.5f), 0f);
            BouquetMock.PutCentred(HeadTex(Target, Look.Messy, 480), cx, cy, size);

            // The rub path: a zig-zag over the hair, the stretch already done drawn solid.
            var path = new List<Vector2>();
            const int n = 48;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float x = 26f + t * 44f;
                float y = 68f + 7f * Mathf.Sin(t * Mathf.PI * 7f);
                path.Add(Map(new Vector2(x, y), cx, cy, size));
            }
            var all = path.ToArray();
            var doneSeg = path.GetRange(0, 22).ToArray();
            var restSeg = path.GetRange(21, n - 20).ToArray();
            Solid(doneSeg, new Color(0.45f, 0.95f, 0.45f), 6f);
            Dashed(restSeg, new Color(1f, 0.90f, 0.42f), 5.5f);

            var at = all[21];
            BouquetMock.PutCentred(HaircutArt.Comb(192), all[0].x - 52f, all[0].y - 6f, 150);
            BouquetMock.PutCentred(BouquetArt.Sparkle(52, new Color(1f, 0.95f, 0.6f)), all[10].x, all[10].y - 40f);
            BouquetMock.PutCentred(BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.9f)), at.x, at.y);
            var hand = BouquetArt.Hand(230, 14f, out var tip);
            BouquetMock.Put(hand, at.x - tip.x, at.y + 2f - (hand.height - tip.y));

            // The two tools of this step: comb now, blow-dry next.
            HudSheet.Plate(190, 1500, 330, 104, 38, HudArt.Looks.Gold, false);
            BouquetMock.PutCentred(HaircutArt.Comb(192), 262, 1552, 92);
            BouquetMock.Text("COMB", 420, 1552, 34, White, 0, 0.06f);
            HudSheet.Plate(560, 1500, 330, 104, 38, HudArt.Looks.Tab, false);
            BouquetMock.PutCentred(HaircutArt.Dryer(192), 632, 1552, 92, 0.6f);
            BouquetMock.Text("BLOW-DRY", 796, 1552, 24, new Color(1f, 1f, 1f, 0.55f), 0, 0.05f);

            // Neatness meter.
            HudSheet.Plate(190, 1626, 700, 56, 28, HudArt.Looks.Tab, false);
            float fill = 700f * 0.52f - 16f;
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, 198f + fill * 0.5f + 8f, 1654f, fill * 0.5f, 18f, 18f), 198f, 1634f, 198f + fill + 16f, 1674f,
                new Color(0.65f, 0.95f, 0.45f), new Color(0.30f, 0.72f, 0.22f), 0f, 1f);
            BouquetMock.Text("NEAT", 232, 1654, 24, White, -1, 0.08f);
            Thumb(766f, 800f);
            StepPill(3);
        }

        // ---- 4. RESULT ---------------------------------------------------------------------------------

        private static void DrawResult()
        {
            Banner("LOOKS GREAT!");
            BouquetMock.PutCentred(BouquetArt.Rays(820, 18, new Color(1f, 0.90f, 0.45f, 0.75f)), 540f, 1040f);
            BouquetMock.Disc(540, 1040, 238, new Color(1f, 0.96f, 0.84f), new Color(0.94f, 0.86f, 0.70f), 0f);
            BouquetMock.PutCentred(HeadTex(Target, Look.Shiny, 480), 540, 1036, 480);
            Sparkles(540, 1040, 300);

            BouquetMock.PutCentred(BouquetArt.Star(150, true), 375f, 1318f, 128);
            BouquetMock.PutCentred(BouquetArt.Star(180, true), 540f, 1294f, 160);
            BouquetMock.PutCentred(BouquetArt.Star(150, false), 705f, 1318f, 128);
            BouquetMock.Text("GREAT!", 540f, 1432f, 78, new Color(1f, 0.97f, 0.62f), 0, 0.06f, new Color(1f, 0.70f, 0.14f), 1.3f);

            HudSheet.Plate(170, 1498, 740, 250, 40, HudArt.Looks.Plaque, false);
            BouquetMock.Text("PRICE", 220, 1548, 32, Cream, -1, 0.06f);
            BouquetMock.Text("$" + Price, 860, 1548, 38, White, 1, 0.03f);
            BouquetMock.Text("TIP", 220, 1608, 32, Cream, -1, 0.06f);
            HudSheet.Blit(HudArt.CoinTexture(70, HudSheet.U), 300f, 1574f);
            BouquetMock.Text("+$" + Tip, 860, 1608, 38, White, 1, 0.03f);
            BouquetMock.Line(214, 1648, 866, 1648, 2.5f, new Color(1f, 0.86f, 0.4f, 0.55f));
            BouquetMock.Text("TOTAL", 220, 1700, 38, GoldText, -1, 0.06f);
            BouquetMock.Text("$" + (Price + Tip), 860, 1700, 54, new Color(1f, 0.95f, 0.55f), 1, 0.03f, new Color(1f, 0.72f, 0.16f));
        }

        // ------------------------------------------------------------------ the icon sheet

        private static void Bubble(float cx, float top, Texture2D icon, float fillPatience)
        {
            const float s = 1.6f, w = 200f * s, h = 176f * s, bottomInset = 26f * s;
            float bodyBottom = top + h - bottomInset, cy = (top + bodyBottom) * 0.5f;
            // Tail, then the dark rim and the white panel.
            float tailTop = bodyBottom - 6f;
            BouquetMock.Shape((x, y) => Mathf.Min(24f - Mathf.Abs(x - cx) - (y - tailTop) * 0.9f, tailTop + 30f - y),
                cx - 26f, tailTop, cx + 26f, tailTop + 32f, new Color(1f, 1f, 1f, 0.97f), new Color(1f, 1f, 1f, 0.97f), 4f, 1f);
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, cx, cy, w * 0.5f - 6f, (bodyBottom - top) * 0.5f - 6f, 26f),
                cx - w * 0.5f, top, cx + w * 0.5f, bodyBottom, new Color(1f, 1f, 1f, 0.97f), new Color(1f, 1f, 1f, 0.97f), 6f, 1f);
            BouquetMock.PutCentred(icon, cx, cy - 18f, 150f);
            // The patience bar, as in the game.
            float ty = top + h * (1f - 0.27f) - 0f;
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, cx, ty - 6f, 118f, 10f, 10f), cx - 120f, ty - 18f, cx + 120f, ty + 6f,
                new Color(0.85f, 0.87f, 0.9f), new Color(0.85f, 0.87f, 0.9f), 0f, 1f);
            float fw = 236f * fillPatience;
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, cx - 118f + fw * 0.5f, ty - 6f, fw * 0.5f, 10f, 10f), cx - 118f, ty - 18f, cx - 118f + fw, ty + 6f,
                new Color(0.35f, 0.78f, 0.45f), new Color(0.35f, 0.78f, 0.45f), 0f, 1f);
        }

        private static void IconSheet()
        {
            const int w = 1720, h = 1330;
            BouquetMock.Begin(w, h);
            Shift(0, 0);
            HudSheet.Rect(0, 0, w, h, new Color(0.13f, 0.17f, 0.20f));
            HudSheet.Plate(30, 30, w - 60, h - 60, 48, HudArt.Looks.Board, false);
            BouquetMock.Text("HAIRSTYLE ICONS: THE FOUR STARTING STYLES", w / 2f, 100, 38, Cream, 0, 0.06f);

            string[] note = { "short, brown", "bob, copper", "long layers, blonde", "quiff, black" };
            for (int i = 0; i < 4; i++)
            {
                float x = 230 + i * 420;
                var s = HaircutArt.All[i];
                HudSheet.Plate(x - 190, 150, 380, 1100, 40, BouquetMock.Mat(), false);
                // Big, on a slot.
                BouquetMock.PutCentred(BouquetArt.Slot(300, BouquetArt.SlotState.Filled), x, 330, 300);
                BouquetMock.PutCentred(HaircutArt.Head(s, 288), x, 326, 250);
                BouquetMock.Text(HaircutArt.Names[i], x, 520, 46, White, 0, 0.06f);
                BouquetMock.Text(note[i], x, 566, 22, Cream, 0, 0.04f);

                // At the size the game draws an icon (96) and on a square (about 40).
                BouquetMock.Text("96 PX", x - 80, 640, 18, new Color(1f, 1f, 1f, 0.7f), 0, 0.1f);
                BouquetMock.Text("40 PX", x + 80, 640, 18, new Color(1f, 1f, 1f, 0.7f), 0, 0.1f);
                BouquetMock.Disc(x - 80, 720, 62, new Color(1f, 0.98f, 0.92f), new Color(0.90f, 0.84f, 0.72f), 3f);
                BouquetMock.PutCentred(HaircutArt.Head(s, 96), x - 80, 718, 96);
                BouquetMock.Disc(x + 80, 720, 62, new Color(0.42f, 0.66f, 0.30f), new Color(0.42f, 0.66f, 0.30f), 0f);
                BouquetMock.PutCentred(HaircutArt.Head(s, 96), x + 80, 718, 40);

                // In the order bubble (a single haircut: the icon alone, the patience bar under it).
                BouquetMock.Text("ORDER BUBBLE", x, 835, 18, new Color(1f, 1f, 1f, 0.7f), 0, 0.1f);
                Bubble(x, 860f, HaircutArt.Head(s, 192), i == 0 ? 1f : i == 1 ? 0.7f : i == 2 ? 0.4f : 0.15f);
                // The finished-hair states the minigame uses.
                BouquetMock.Text("RAW  /  MESSY  /  SHINY", x, 1150, 17, new Color(1f, 1f, 1f, 0.7f), 0, 0.1f);
            }
            // The three look states, for the first style, on the bottom strip of each card.
            for (int i = 0; i < 4; i++)
            {
                float x = 230 + i * 420;
                var s = HaircutArt.All[i];
                BouquetMock.PutCentred(HaircutArt.Head(s, 96, Look.Raw), x - 110, 1208, 66);
                BouquetMock.PutCentred(HaircutArt.Head(s, 96, Look.Messy), x, 1208, 66);
                BouquetMock.PutCentred(HaircutArt.Head(s, 96, Look.Shiny), x + 110, 1208, 66);
            }
            Save("salon-icons.png");
        }
    }
}
