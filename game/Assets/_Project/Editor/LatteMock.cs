using System;
using System.Collections.Generic;
using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The latte-art minigame's four screens as still pictures, for Filip to judge before any of it is built (PROPOSAL, phase 1;
    /// nothing is wired): LOOK (the order: heart, leaf or smiley), POUR (hold to pour the milk, fill the meter to the green), DRAW
    /// (trace the pattern with the milk) and RESULT (stars, price, tip). Each is drawn twice, in the two arrangements the shared
    /// minigame frame (<see cref="Tycoon.UI.MinigameOverlay"/>) takes: the portrait card 1080 x 1920 and the landscape card
    /// 2080 x 1260, laid out exactly as <see cref="HaircutMock"/> does (same board, plaques, tabs, banner, pill, text sizes),
    /// built from the bouquet's drawing helpers (<see cref="BouquetMock"/>). A script then composes the 390 x 844 and 844 x 390
    /// phone sheets. Only a PREMIUM coffee opens it: a plain coffee has no minigame.
    /// Run: <c>-executeMethod Tycoon.EditorTools.LatteMock.Export</c> (batch mode, no -nographics: the lettering is rasterised by the GPU).
    /// Writes latte-{p|l}-{1..4}.png into the preview folder's latte/ subfolder.
    /// </summary>
    public static class LatteMock
    {
        private static string OutDir => CafeIconSheet.OutDir + "latte/";

        private static Color Ink => BouquetMock.Ink;
        private static Color White => Color.white;
        private static Color Cream => BouquetMock.Cream;
        private static Color GoldText => BouquetMock.GoldText;

        private enum Art { Heart, Leaf, Smiley }
        private static readonly string[] Names = { "HEART", "LEAF", "SMILEY" };
        private static readonly Art Target = Art.Heart;
        private const int Price = 70, Tip = 30;

        private const float BodyX = 100f, BodyY = 660f, BodyW = 880f, BodyH = 1125f;

        private static readonly Color Foam = new Color(1f, 0.97f, 0.88f);
        private static readonly Color FoamEdge = new Color(0.93f, 0.80f, 0.60f);

        [MenuItem("Tycoon/Export Latte Mock")]
        public static void Export()
        {
            float keepU = HudSheet.U;
            HudSheet.U = 1f;
            try
            {
                Directory.CreateDirectory(OutDir);
                for (int step = 1; step <= 4; step++)
                {
                    Screen(false, step);
                    Screen(true, step);
                }
                Debug.Log("LATTEMOCK_OK");
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
            Debug.Log("[LatteMock] -> " + OutDir + file);
        }

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
                // The header column on the left, the body on the right (the numbers of HaircutMock / BouquetMinigame.ApplyOrientation).
                Shift(0, 253);
                HudSheet.Plate(50, 150, 980, 520, 64, HudArt.Looks.Board, false);
                Header(step);
                Shift(1030, -580);
                HudSheet.Plate(50, 610, 980, 1200, 64, HudArt.Looks.Board, false);
                Body(step);
            }
            Save($"latte-{(land ? "l" : "p")}-{step}.png");
        }

        // ------------------------------------------------------------------ small drawing kit (px, y down)

        private static float EdgeD(float ax, float ay, float bx, float by, float px, float py, float cx, float cy)
        {
            float len = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            float d = ((bx - ax) * (py - ay) - (by - ay) * (px - ax)) / len;
            float dc = ((bx - ax) * (cy - ay) - (by - ay) * (cx - ax)) / len;
            return dc >= 0f ? d : -d;
        }

        /// <summary>A heart of "radius" r centred at (cx, cy): two round lobes and a point, as a signed distance.</summary>
        private static float HeartSdf(float x, float y, float cx, float cy, float r)
        {
            float u = (x - cx) / r, v = (y - cy) / r;
            float a = 0.31f - Mathf.Sqrt((u + 0.27f) * (u + 0.27f) + (v + 0.13f) * (v + 0.13f));
            float b = 0.31f - Mathf.Sqrt((u - 0.27f) * (u - 0.27f) + (v + 0.13f) * (v + 0.13f));
            float t = Mathf.Min(EdgeD(-0.56f, 0.00f, 0.56f, 0.00f, u, v, 0f, 0.3f),
                Mathf.Min(EdgeD(0.56f, 0.00f, 0f, 0.66f, u, v, 0f, 0.3f), EdgeD(0f, 0.66f, -0.56f, 0.00f, u, v, 0f, 0.3f)));
            return Mathf.Max(Mathf.Max(a, b), t) * r;
        }

        private static void Heart(float cx, float cy, float r, Color top, Color bottom, float outline = 0f) =>
            BouquetMock.Shape((x, y) => HeartSdf(x, y, cx, cy, r), cx - r * 0.7f, cy - r * 0.5f, cx + r * 0.7f, cy + r * 0.7f, top, bottom, outline, 1f);

        /// <summary>The cup seen from above: gold saucer, white cup with its handle, crema on the coffee. Returns nothing; (cx, cy) is its middle.</summary>
        private static void CupTop(float cx, float cy, float r)
        {
            BouquetMock.Ellipse(cx + r * 0.08f, cy + r * 0.16f, r * 1.42f, r * 1.38f, new Color(0f, 0f, 0f, 0.28f));
            BouquetMock.Disc(cx, cy, r * 1.30f, new Color(1f, 0.92f, 0.60f), new Color(0.80f, 0.55f, 0.16f), 5f);
            BouquetMock.Disc(cx, cy, r * 1.14f, new Color(1f, 0.88f, 0.50f), new Color(0.86f, 0.62f, 0.22f), 0f);
            // The handle, on the right.
            BouquetMock.Shape((x, y) => Mathf.Min(r * 0.34f - Mathf.Sqrt((x - cx - r * 1.12f) * (x - cx - r * 1.12f) + (y - cy) * (y - cy)),
                    -(r * 0.18f - Mathf.Sqrt((x - cx - r * 1.12f) * (x - cx - r * 1.12f) + (y - cy) * (y - cy)))),
                cx + r * 0.7f, cy - r * 0.4f, cx + r * 1.5f, cy + r * 0.4f, new Color(1f, 1f, 0.98f), new Color(0.80f, 0.84f, 0.92f), 4f, 1f);
            BouquetMock.Disc(cx, cy, r, new Color(1f, 1f, 0.99f), new Color(0.80f, 0.84f, 0.93f), 5f);
            BouquetMock.Disc(cx, cy, r * 0.90f, new Color(0.62f, 0.38f, 0.20f), new Color(0.40f, 0.21f, 0.09f), 0f);
            BouquetMock.Disc(cx, cy, r * 0.78f, new Color(0.80f, 0.54f, 0.30f), new Color(0.58f, 0.32f, 0.14f), 0f);   // the crema
            BouquetMock.Line(cx - r * 0.62f, cy - r * 0.50f, cx - r * 0.42f, cy - r * 0.70f, r * 0.03f, new Color(1f, 1f, 1f, 0.35f));
        }

        private static void Pattern(Art a, float cx, float cy, float r, float alpha = 1f)
        {
            var col = new Color(Foam.r, Foam.g, Foam.b, alpha);
            var edge = new Color(FoamEdge.r, FoamEdge.g, FoamEdge.b, alpha);
            switch (a)
            {
                case Art.Heart:
                    Heart(cx, cy - r * 0.05f, r * 0.78f, col, edge);
                    break;
                case Art.Leaf:
                {
                    BouquetMock.Line(cx, cy - r * 0.62f, cx, cy + r * 0.60f, r * 0.045f, col);
                    for (int i = 0; i < 5; i++)
                    {
                        float y = cy + r * 0.50f - i * r * 0.26f, k = 0.26f + 0.12f * Mathf.Sin((i + 0.5f) / 5f * Mathf.PI);
                        BouquetMock.Line(cx, y, cx - r * k * 1.55f, y - r * 0.20f, r * 0.075f, col);
                        BouquetMock.Line(cx, y, cx + r * k * 1.55f, y - r * 0.20f, r * 0.075f, col);
                    }
                    break;
                }
                default:
                {
                    BouquetMock.Disc(cx, cy, r * 0.62f, col, edge, 0f, alpha);
                    var brown = new Color(0.40f, 0.21f, 0.09f, alpha);
                    BouquetMock.Disc(cx - r * 0.22f, cy - r * 0.12f, r * 0.075f, brown, brown, 0f, alpha);
                    BouquetMock.Disc(cx + r * 0.22f, cy - r * 0.12f, r * 0.075f, brown, brown, 0f, alpha);
                    var prev = Vector2.zero;
                    for (int i = 0; i <= 14; i++)
                    {
                        float ang = Mathf.Lerp(28f, 152f, i / 14f) * Mathf.Deg2Rad;
                        var p = new Vector2(cx + Mathf.Cos(ang) * r * 0.36f, cy + Mathf.Sin(ang) * r * 0.36f - r * 0.02f);
                        if (i > 0) BouquetMock.Line(prev.x, prev.y, p.x, p.y, r * 0.045f, brown);
                        prev = p;
                    }
                    break;
                }
            }
        }

        private static void Dashed(Vector2[] pts, Color core, float width)
        {
            const float dash = 36f, gap = 20f;
            float left = dash; bool on = true;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                float len = Vector2.Distance(a, b), t = 0f;
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
            }
        }

        private static void Solid(Vector2[] pts, Color core, float width)
        {
            for (int i = 0; i < pts.Length - 1; i++) BouquetMock.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, width + 3.5f, Ink);
            for (int i = 0; i < pts.Length - 1; i++) BouquetMock.Line(pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, width, core);
        }

        /// <summary>The path to trace for a heart (the classic parametric curve), starting at the top notch and going round.</summary>
        private static Vector2[] HeartPath(float cx, float cy, float r, float from, float to)
        {
            var l = new List<Vector2>();
            int n = 72;
            for (int i = 0; i <= n; i++)
            {
                float t = Mathf.Lerp(from, to, i / (float)n) * Mathf.PI * 2f;
                float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                l.Add(new Vector2(cx + x / 17f * r, cy - (y + 2.5f) / 17f * r));
            }
            return l.ToArray();
        }

        private static void Sparkles(float cx, float cy, float spread)
        {
            var spark = new Color(1f, 0.96f, 0.62f);
            BouquetMock.PutCentred(BouquetArt.Sparkle(64, spark), cx - spread, cy - spread * 0.35f);
            BouquetMock.PutCentred(BouquetArt.Sparkle(44, spark), cx + spread * 0.95f, cy - spread * 0.6f);
            BouquetMock.PutCentred(BouquetArt.Sparkle(52, spark), cx + spread * 0.85f, cy + spread * 0.55f);
            BouquetMock.PutCentred(BouquetArt.Sparkle(36, spark), cx - spread * 0.95f, cy + spread * 0.6f);
        }

        // ------------------------------------------------------------------ header: title, order, steps

        private static void Header(int step)
        {
            HudSheet.Plate(290, 84, 500, 130, 44, HudArt.Looks.Gold, false);
            BouquetMock.Text("LATTE ART", 540, 149, 60, White, 0, 0.08f);

            HudSheet.Plate(100, 250, 880, 275, 40, HudArt.Looks.Plaque, false);
            HudSheet.Plate(130, 230, 240, 60, 24, HudArt.Looks.Gold, false);
            BouquetMock.Text("ORDER", 250, 259, 30, White, 0, 0.08f);

            // The customer's wish: the pattern in a round slot, its name, the base price and what the tip is for.
            BouquetMock.PutCentred(BouquetArt.Slot(210, BouquetArt.SlotState.Filled), 236, 405, 210);
            BouquetMock.Disc(236, 402, 78, new Color(0.80f, 0.54f, 0.30f), new Color(0.58f, 0.32f, 0.14f), 3f);
            Pattern(Target, 236, 402, 78);
            BouquetMock.Text(Names[(int)Target], 400, 372, 62, White, -1, 0.04f);
            BouquetMock.Text("BASE", 400, 450, 30, Cream, -1, 0.06f);
            HudSheet.Blit(HudArt.CoinTexture(70, HudSheet.U), 515, 413);
            BouquetMock.Text("$" + Price, 596, 450, 44, White, -1, 0.03f);
            for (int i = 0; i < 3; i++) BouquetMock.PutCentred(BouquetArt.Star(80, false), 780 + i * 72, 372, 58);
            BouquetMock.Text("TIP: HOW CLOSE", 846, 450, 20, Cream, 0, 0.03f);

            string[] names = { "LOOK", "POUR", "DRAW" };
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
                case 2: DrawPour(); break;
                case 3: DrawDraw(); break;
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

        private static void Thumb(float left, float top)
        {
            HudSheet.Plate(left, top, 200, 236, 30, HudArt.Looks.Plaque, false);
            BouquetMock.Disc(left + 100, top + 130, 82, new Color(1f, 1f, 0.99f), new Color(0.80f, 0.84f, 0.93f), 3f);
            BouquetMock.Disc(left + 100, top + 130, 70, new Color(0.80f, 0.54f, 0.30f), new Color(0.58f, 0.32f, 0.14f), 0f);
            Pattern(Target, left + 100, top + 130, 70);
            HudSheet.Plate(left + 24, top - 18, 152, 40, 16, HudArt.Looks.Gold, false);
            BouquetMock.Text("TARGET", left + 100, top + 2, 20, White, 0, 0.08f);
        }

        // ---- 1. LOOK -----------------------------------------------------------------------------------

        private static void DrawLook()
        {
            Banner("COPY THIS LATTE ART");
            HudSheet.Plate(180, 810, 720, 700, 44, HudArt.Looks.Plaque, false);
            HudSheet.Plate(210, 790, 250, 60, 24, HudArt.Looks.Gold, false);
            BouquetMock.Text("TARGET", 335, 819, 28, White, 0, 0.08f);
            BouquetMock.PutCentred(BouquetArt.Rays(640, 16, new Color(1f, 0.90f, 0.45f, 0.22f)), 540f, 1150f);
            CupTop(500, 1125, 208);
            Pattern(Target, 500, 1125, 208);
            Sparkles(520, 1120, 290);

            HudSheet.Plate(330, 1535, 420, 100, 38, HudArt.Looks.Gold, false);
            BouquetMock.Text(Names[(int)Target], 540, 1585, 56, White, 0, 0.06f);
            BouquetMock.Text("TODAY: HEART, LEAF OR SMILEY", 540, 1668, 28, Cream, 0, 0.06f);
            StepPill(1);
            // Three small cups: the patterns that can be ordered, the current one lit.
            for (int i = 0; i < 3; i++)
            {
                float x = 250 + i * 145;
                bool now = i == (int)Target;
                BouquetMock.Disc(x, 1457, 54, now ? new Color(1f, 0.90f, 0.40f) : new Color(1f, 1f, 1f, 0.35f), now ? new Color(1f, 0.70f, 0.14f) : new Color(1f, 1f, 1f, 0.35f), 0f);
                BouquetMock.Disc(x, 1457, 46, new Color(0.80f, 0.54f, 0.30f), new Color(0.58f, 0.32f, 0.14f), 2.5f);
                Pattern((Art)i, x, 1457, 46);
            }
            var hand = BouquetArt.Hand(210, 16f, out var tip);
            BouquetMock.PutCentred(BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.85f)), 800f, 1230f);
            BouquetMock.Put(hand, 800f - tip.x, 1234f - (hand.height - tip.y));
            BouquetMock.Text("TAP TO START", 735, 1470, 28, GoldText, 0, 0.06f);
        }

        // ---- 2. POUR -----------------------------------------------------------------------------------

        private static void Pitcher(float cx, float cy, float tiltDeg)
        {
            var rot = Quaternion.Euler(0f, 0f, tiltDeg);
            Vector2 P(float x, float y) { var v = rot * new Vector3(x, y, 0f); return new Vector2(cx + v.x, cy + v.y); }
            var a = P(0f, -70f); var b = P(0f, 90f);
            var steel1 = new Color(0.96f, 0.97f, 1f); var steel2 = new Color(0.60f, 0.66f, 0.76f);
            // Handle (right), body, spout (left), a milk band.
            var h = P(105f, 5f);
            BouquetMock.Shape((x, y) => Mathf.Min(54f - Mathf.Sqrt((x - h.x) * (x - h.x) + (y - h.y) * (y - h.y)), -(30f - Mathf.Sqrt((x - h.x) * (x - h.x) + (y - h.y) * (y - h.y)))),
                h.x - 56, h.y - 56, h.x + 56, h.y + 56, steel1, steel2, 4f, 1f);
            var s0 = P(-48f, -30f); var s1 = P(-110f, -92f);
            BouquetMock.Shape((x, y) => BouquetMock.Capsule(x, y, s0.x, s0.y, s1.x, s1.y, 26f), Mathf.Min(s0.x, s1.x) - 30, Mathf.Min(s0.y, s1.y) - 30,
                Mathf.Max(s0.x, s1.x) + 30, Mathf.Max(s0.y, s1.y) + 30, steel1, steel2, 4f, 1f);
            BouquetMock.Shape((x, y) => BouquetMock.Capsule(x, y, a.x, a.y, b.x, b.y, 74f), cx - 160, cy - 200, cx + 160, cy + 200, steel1, steel2, 4f, 1f);
            var m0 = P(0f, 30f);
            BouquetMock.Disc(m0.x, m0.y, 22f, new Color(0.45f, 0.70f, 0.98f), new Color(0.20f, 0.42f, 0.86f), 3f);
        }

        private static void DrawPour()
        {
            Banner("HOLD TO POUR THE MILK");
            const float cx = 410f, cy = 1250f, r = 236f;
            BouquetMock.PutCentred(BouquetArt.Rays(560, 14, new Color(1f, 0.90f, 0.45f, 0.12f)), cx, cy);
            CupTop(cx, cy, r);
            // The milk pool under the stream, spreading.
            BouquetMock.Disc(cx + 14, cy + 14, 150, new Color(1f, 0.97f, 0.88f, 0.4f), new Color(0.95f, 0.84f, 0.66f, 0.4f), 0f, 0.4f);
            BouquetMock.Disc(cx + 14, cy + 14, 110, new Color(1f, 0.99f, 0.93f), new Color(0.95f, 0.84f, 0.66f), 0f, 0.96f);
            // The stream from the spout.
            BouquetMock.Line(712, 1050, cx + 20, cy + 10, 15f, new Color(1f, 1f, 0.97f));
            BouquetMock.Line(712, 1050, cx + 20, cy + 10, 7f, new Color(1f, 1f, 1f));
            Pitcher(822, 985, 36f);
            // The finger holds the screen: a ring and a pill.
            var hand = BouquetArt.Hand(230, 14f, out var tip);
            BouquetMock.PutCentred(BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.9f)), 800f, 1250f);
            BouquetMock.Put(hand, 800f - tip.x, 1254f - (hand.height - tip.y));
            BouquetMock.Pill("HOLD", 800f, 1440f, 190f);

            // The fill meter: the green stretch is the right amount of milk; the gold marker is where it is now.
            HudSheet.Plate(150, 1540, 780, 90, 40, HudArt.Looks.Plaque, false);
            HudSheet.Plate(190, 1568, 700, 36, 18, HudArt.Looks.Tab, false);
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, 190f + 700f * 0.70f, 1586f, 700f * 0.12f, 18f, 8f), 190f + 700f * 0.58f, 1568f, 190f + 700f * 0.82f, 1604f,
                new Color(0.50f, 0.95f, 0.45f), new Color(0.22f, 0.70f, 0.22f), 0f, 1f);
            float fill = 700f * 0.52f;
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, 190f + fill * 0.5f + 5f, 1586f, fill * 0.5f - 2f, 12f, 12f), 192f, 1574f, 190f + fill + 10f, 1598f,
                new Color(1f, 0.99f, 0.93f), new Color(0.92f, 0.82f, 0.64f), 0f, 0.95f);
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, 190f + fill, 1586f, 7f, 34f, 6f), 180f + fill, 1552f, 200f + fill, 1620f,
                new Color(1f, 0.92f, 0.45f), new Color(1f, 0.70f, 0.12f), 3f, 1f);
            BouquetMock.Text("MILK", 150, 1664, 22, Cream, -1, 0.08f);
            BouquetMock.Text("FILL TO THE GREEN", 600, 1664, 24, GoldText, 0, 0.08f);
            StepPill(2);
        }

        // ---- 3. DRAW -----------------------------------------------------------------------------------

        private static void DrawDraw()
        {
            Banner("TRACE THE PATTERN");
            const float cx = 500f, cy = 1190f, r = 282f;
            CupTop(cx, cy, r);
            // The milk base: a creamy ring the pattern is drawn onto.
            BouquetMock.Disc(cx, cy, r * 0.70f, new Color(1f, 0.97f, 0.88f, 0.55f), new Color(0.95f, 0.84f, 0.66f, 0.55f), 0f, 0.55f);
            var guide = HeartPath(cx, cy - 6f, r * 0.80f, 0f, 1f);
            var done = new Vector2[32];
            var rest = new Vector2[guide.Length - 31];
            Array.Copy(guide, 0, done, 0, 32);
            Array.Copy(guide, 31, rest, 0, rest.Length);
            Solid(done, Foam, 11f);
            Dashed(rest, new Color(1f, 0.90f, 0.42f), 5.5f);

            var at = guide[31];
            BouquetMock.Line(guide[18].x, guide[18].y, at.x, at.y, 14f, new Color(1f, 1f, 1f, 0.5f));
            BouquetMock.PutCentred(BouquetArt.Sparkle(52, new Color(1f, 0.95f, 0.6f)), at.x - 34f, at.y - 40f);
            BouquetMock.PutCentred(BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.9f)), at.x, at.y);
            var hand = BouquetArt.Hand(230, 14f, out var tip);
            BouquetMock.Put(hand, at.x - tip.x, at.y + 2f - (hand.height - tip.y));
            for (int i = 0; i < 3; i++) BouquetMock.Chevron(at.x + 60f + i * 34f, at.y + 40f, 24f, new Color(1f, 1f, 1f, 0.95f - i * 0.22f));
            BouquetMock.Pill("STAY ON THE LINE", 740f, 1560f, 340f);

            HudSheet.Plate(190, 1626, 700, 56, 28, HudArt.Looks.Tab, false);
            float fill = 700f * 0.45f - 16f;
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, 198f + fill * 0.5f + 8f, 1654f, fill * 0.5f, 18f, 18f), 198f, 1634f, 198f + fill + 16f, 1674f,
                new Color(0.65f, 0.95f, 0.45f), new Color(0.30f, 0.72f, 0.22f), 0f, 1f);
            BouquetMock.Text("ON LINE", 232, 1654, 24, White, -1, 0.08f);
            Thumb(766f, 800f);
            StepPill(3);
        }

        // ---- 4. RESULT ---------------------------------------------------------------------------------

        private static void DrawResult()
        {
            Banner("PERFECT LATTE!");
            BouquetMock.PutCentred(BouquetArt.Rays(820, 18, new Color(1f, 0.90f, 0.45f, 0.75f)), 540f, 1040f);
            CupTop(510, 1000, 198);
            Pattern(Target, 510, 1000, 198);
            Sparkles(530, 1000, 300);

            BouquetMock.PutCentred(BouquetArt.Star(150, true), 375f, 1318f, 128);
            BouquetMock.PutCentred(BouquetArt.Star(180, true), 540f, 1294f, 160);
            BouquetMock.PutCentred(BouquetArt.Star(150, true), 705f, 1318f, 128);
            BouquetMock.Text("PERFECT!", 540f, 1432f, 78, new Color(1f, 0.97f, 0.62f), 0, 0.06f, new Color(1f, 0.70f, 0.14f), 1.3f);

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
    }
}
