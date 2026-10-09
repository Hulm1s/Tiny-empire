using System.Collections.Generic;
using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Exports the restyled HUD to a PNG so it can be judged before it is wired in: the money
    /// plaque with its coin, the pause medallion (normal and pressed), the task card with its
    /// tabs and badge, and the framed pause menu with its buttons - each drawn with the real
    /// <see cref="HudArt"/> sprites on the game's grass and on the dark loading background,
    /// laid out at the size they will have on a 375 x 812 phone.
    ///
    /// The lettering here is a schematic stroke font, because a headless run has no GPU to
    /// rasterise the game's font. It stands in for the white, dark-edged text the HUD will use;
    /// judge the frames, coin, medallion and colours, not the letter shapes.
    ///
    /// Run: Tycoon > Export HUD Sheet, or
    /// <c>-executeMethod Tycoon.EditorTools.HudSheet.Export</c>. Writes ../hud-sheet.png.
    /// </summary>
    public static class HudSheet
    {
        private const string OutputPath = "../hud-sheet.png";

        /// <summary>Pixels per canvas unit: the same as the runtime sprites are drawn at.</summary>
        private const float U = HudArt.RuntimeScale;

        private static readonly Color Grass = new Color(0.42f, 0.66f, 0.30f);
        private static readonly Color Night = new Color(0.13f, 0.17f, 0.20f);

        private static readonly Color Green = new Color(0.30f, 0.78f, 0.34f);
        private static readonly Color Red = new Color(0.90f, 0.26f, 0.22f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.16f);

        private static Color[] _buf;
        private static int _w, _h;

        // Origin of the mock being drawn, in sheet pixels from the top-left.
        private static float _ox, _oy;

        [MenuItem("Tycoon/Export HUD Sheet")]
        public static void Export()
        {
            const int gap = 20;
            int mockW = Mathf.RoundToInt(1008 * U);
            int hudH = Mathf.RoundToInt(800 * U);
            int menuH = Mathf.RoundToInt(1253 * U);
            int detailH = 330;

            _w = gap + mockW + gap + mockW + gap;
            _h = gap + hudH + gap + menuH + gap + detailH + gap;
            _buf = new Color[_w * _h];
            for (int i = 0; i < _buf.Length; i++) _buf[i] = new Color(0.07f, 0.07f, 0.08f);

            for (int col = 0; col < 2; col++)
            {
                var bg = col == 0 ? Grass : Night;
                float x = gap + col * (mockW + gap);

                Rect(x, gap, mockW, hudH, bg);
                _ox = x; _oy = gap;
                DrawHud();

                float y2 = gap + hudH + gap;
                Rect(x, y2, mockW, menuH, bg);
                _ox = x; _oy = y2;
                DrawMenu(pressedResume: col == 0, armedDelete: col == 1);
            }

            DrawDetails(gap + hudH + gap + menuH + gap, detailH);

            var sheet = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            sheet.SetPixels(_buf);
            sheet.Apply();
            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            Debug.Log($"[HudSheet] -> {path}");
            Debug.Log("HUDSHEET_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ---- the mocks --------------------------------------------------------------------

        private static void DrawHud()
        {
            // Money plaque, top centre, 420 x 116.
            Plate(294, 28, 420, 116, 44, HudArt.Looks.Plaque, false);
            // Smaller than the plaque is tall, so the coin and its outline sit fully inside it.
            Blit(HudArt.CoinTexture(92, U), 294 + 62 - 46, 28 + 58 - 43.5f);
            Text("$1.2K", 294 + 285, 28 + 58, 46, Color.white, 0);

            // Pause medallion, top right, 110 across.
            Blit(HudArt.MedallionTexture(131, U, false), 1008 - 28 - 55 - 65.5f, 83 - 63);

            // Task card, top left under the money readout.
            const float cx = 28, cy = 160;
            Plate(cx, cy, 600, 202, 34, HudArt.Looks.Plaque, false);

            Plate(cx + 14, cy + 14, 300, 54, 24, HudArt.Looks.Tab, false);
            Text("PROBLEMS", cx + 14 + 135, cy + 14 + 27, 24, Red, 0, 0.14f);
            // The count badge, pinned to the tab's top-right corner (disc centre 31 / 28.5 units into its texture).
            float bx = cx + 14 + 300 - 22, by = cy + 14 + 6;
            Blit(HudArt.DiscTexture(62, HudArt.Looks.Badge, U, false, "b", null), bx - 31, by - 28.5f);
            Text("2", bx, by, 20, Color.white, 0);

            Plate(cx + 14 + 310, cy + 14, 190, 54, 24, HudArt.Looks.Gold, false);
            Text("GOALS", cx + 14 + 310 + 95, cy + 14 + 27, 26, Color.white, 0, 0.14f);

            Dot(cx + 14 + 23, cy + 82 + 28, 26, Gold, 1f);
            Text("Buy Chicken - $120", cx + 14 + 50, cy + 82 + 28, 24, Color.white, -1);
            Dot(cx + 14 + 23, cy + 82 + 28 + 56, 26, Gold, 0.5f);
            Text("Hire Farmer - $250", cx + 14 + 50, cy + 82 + 28 + 56, 24, new Color(1, 1, 1, 0.5f), -1);

            // The guide arrow beside it, to show the family resemblance.
            Blit(IconFactoryTexture(Gold), 620, 470, 150);
            Blit(IconFactoryTexture(Red), 790, 470, 150);
            Blit(IconFactoryTexture(Green), 450, 470, 150);
        }

        private static void DrawMenu(bool pressedResume, bool armedDelete)
        {
            Rect(0, 0, 1008, 1253, new Color(0.02f, 0.04f, 0.06f, 0.70f), true);

            Plate(114, 150, 780, 920, 56, HudArt.Looks.Board, false);
            Plate(244, 80, 520, 124, 42, HudArt.Looks.Gold, false);
            Text("PAUSED", 504, 80 + 62, 62, Color.white, 0, 0.14f);

            Plate(194, 330, 620, 130, 46, HudArt.Looks.Gold, pressedResume);
            Text("RESUME", 504, 330 + 65 + (pressedResume ? 5 : 0), 48, Color.white, 0, 0.16f);

            Plate(194, 500, 620, 130, 46, HudArt.Looks.Blue, false);
            Text("SOUND: ON", 504, 500 + 65, 48, Color.white, 0, 0.16f);

            Plate(194, 670, 620, 130, 46, HudArt.Looks.Red, false);
            Text(armedDelete ? "TAP TO CONFIRM" : "DELETE SAVE", 504, 670 + 65, 48, Color.white, 0, 0.16f);

            Text("Progress saves automatically on this device.", 504, 900, 21, new Color(1, 1, 1, 0.6f), 0);
        }

        private static void DrawDetails(float top, int height)
        {
            Rect(20, top, _w / 2f - 30, height, Grass);
            Rect(_w / 2f + 10, top, _w / 2f - 30, height, Night);
            _ox = 0; _oy = 0;

            const float big = 1.5f;
            float y = top + 20;
            for (int half = 0; half < 2; half++)
            {
                float x0 = 20 + half * (_w / 2f - 10);
                BlitPx(HudArt.MedallionTexture(131, big, false), x0 + 20, y, 1f);
                BlitPx(HudArt.MedallionTexture(131, big, true), x0 + 20 + 230, y, 1f);
                BlitPx(HudArt.CoinTexture(116, big), x0 + 20 + 440, y, 1f);
                BlitPx(HudArt.DiscTexture(62, HudArt.Looks.Badge, big, false, "b", null), x0 + 20 + 650, y + 60, 1f);
            }
        }

        private static Texture2D IconFactoryTexture(Color c) => IconFactory.GuideArrow(c).texture;

        // ---- placing things (mock units -> sheet pixels) ------------------------------------

        /// <summary>A framed plate whose VISIBLE rectangle starts at (x, y), in mock units.</summary>
        private static void Plate(float x, float y, float w, float h, float radius, HudArt.Look look, bool pressed)
        {
            var tex = HudArt.PanelTexture(w + 2 * HudArt.Margin, h + 2 * HudArt.Margin + 6f, radius, look, U, pressed);
            Blit(tex, x - HudArt.Margin, y - HudArt.Margin);
        }

        private static void Blit(Texture2D tex, float xUnits, float yUnits) =>
            BlitPx(tex, _ox + xUnits * U, _oy + yUnits * U, 1f);

        /// <summary>Draws a texture scaled to <paramref name="sizeUnits"/> across (arrows).</summary>
        private static void Blit(Texture2D tex, float xUnits, float yUnits, float sizeUnits)
        {
            float px = sizeUnits * U;
            BlitPx(tex, _ox + xUnits * U, _oy + yUnits * U, px / tex.width);
        }

        private static void BlitPx(Texture2D tex, float left, float top, float scale)
        {
            var src = tex.GetPixels();
            int tw = tex.width, th = tex.height;
            int outW = Mathf.CeilToInt(tw * scale), outH = Mathf.CeilToInt(th * scale);

            for (int oy = 0; oy < outH; oy++)
            {
                for (int ox = 0; ox < outW; ox++)
                {
                    int sx = Mathf.Min(tw - 1, Mathf.FloorToInt(ox / scale));
                    int sy = Mathf.Min(th - 1, Mathf.FloorToInt((outH - 1 - oy) / scale));
                    Color c;
                    if (scale < 0.999f)
                    {
                        // Box average over the source texels this pixel covers.
                        int span = Mathf.Max(1, Mathf.CeilToInt(1f / scale));
                        float r = 0, g = 0, b = 0, a = 0;
                        int count = 0;
                        for (int j = 0; j < span; j++)
                        {
                            for (int i = 0; i < span; i++)
                            {
                                int qx = Mathf.Min(tw - 1, sx + i), qy = Mathf.Min(th - 1, sy + j);
                                var s = src[qy * tw + qx];
                                r += s.r * s.a; g += s.g * s.a; b += s.b * s.a; a += s.a; count++;
                            }
                        }
                        if (a <= 0f) continue;
                        c = new Color(r / a, g / a, b / a, a / count);
                    }
                    else c = src[sy * tw + sx];

                    int x = Mathf.RoundToInt(left) + ox, y = Mathf.RoundToInt(top) + oy;
                    Plot(x, y, c, 1f);
                }
            }
        }

        private static void Plot(int x, int y, Color src, float cover)
        {
            if (x < 0 || y < 0 || x >= _w || y >= _h) return;
            int i = (_h - 1 - y) * _w + x;
            float sa = src.a * cover;
            if (sa <= 0f) return;
            var dst = _buf[i];
            float oa = sa + dst.a * (1f - sa);
            _buf[i] = new Color(
                (src.r * sa + dst.r * dst.a * (1f - sa)) / oa,
                (src.g * sa + dst.g * dst.a * (1f - sa)) / oa,
                (src.b * sa + dst.b * dst.a * (1f - sa)) / oa, oa);
        }

        private static void Rect(float x, float y, float w, float h, Color c, bool inMock = false)
        {
            float ox = inMock ? _ox : 0f, oy = inMock ? _oy : 0f;
            float sw = inMock ? w * U : w, sh = inMock ? h * U : h;
            int x0 = Mathf.RoundToInt(ox + (inMock ? x * U : x)), y0 = Mathf.RoundToInt(oy + (inMock ? y * U : y));
            for (int yy = 0; yy < sh; yy++)
                for (int xx = 0; xx < sw; xx++)
                    Plot(x0 + xx, y0 + yy, c, 1f);
        }

        private static void Dot(float cx, float cy, float diameter, Color c, float alpha)
        {
            float r = diameter * 0.5f * U;
            float px = _ox + cx * U, py = _oy + cy * U;
            for (int y = Mathf.FloorToInt(py - r - 2); y <= Mathf.CeilToInt(py + r + 2); y++)
            {
                for (int x = Mathf.FloorToInt(px - r - 2); x <= Mathf.CeilToInt(px + r + 2); x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - px) * (x + 0.5f - px) + (y + 0.5f - py) * (y + 0.5f - py));
                    Plot(x, y, new Color(0.15f, 0.08f, 0.04f, alpha), Mathf.Clamp01(r + 2f * U - d + 0.5f));
                    Plot(x, y, new Color(c.r, c.g, c.b, alpha), Mathf.Clamp01(r - d + 0.5f));
                }
            }
        }

        // ---- schematic lettering ------------------------------------------------------------
        // Each glyph is a set of polylines on a 4 x 6 grid (y up). Strokes are drawn thick with
        // round joins and a dark edge, which is close enough to the real lettering's weight.

        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            ['A'] = "0,0 2,6 4,0|0.8,2 3.2,2",
            ['B'] = "0,0 0,6 3,6 3.8,5 3.8,4 3,3 0,3|3,3 4,2 4,1 3,0 0,0",
            ['C'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1",
            ['D'] = "0,0 0,6 2.5,6 4,4.5 4,1.5 2.5,0 0,0",
            ['E'] = "4,6 0,6 0,0 4,0|0,3 3,3",
            ['F'] = "4,6 0,6 0,0|0,3 3,3",
            ['G'] = "4,5 3,6 1,6 0,5 0,1 1,0 3,0 4,1 4,3 2.2,3",
            ['H'] = "0,0 0,6|4,0 4,6|0,3 4,3",
            ['I'] = "2,0 2,6|1,6 3,6|1,0 3,0",
            ['J'] = "4,6 4,1 3,0 1,0 0,1",
            ['K'] = "0,0 0,6|4,6 0,3 4,0",
            ['L'] = "0,6 0,0 4,0",
            ['M'] = "0,0 0,6 2,3 4,6 4,0",
            ['N'] = "0,0 0,6 4,0 4,6",
            ['O'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0",
            ['P'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3",
            ['Q'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0|2.5,2 4,0",
            ['R'] = "0,0 0,6 3,6 4,5 4,4 3,3 0,3|2,3 4,0",
            ['S'] = "4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1",
            ['T'] = "0,6 4,6|2,6 2,0",
            ['U'] = "0,6 0,1 1,0 3,0 4,1 4,6",
            ['V'] = "0,6 2,0 4,6",
            ['W'] = "0,6 1,0 2,3 3,0 4,6",
            ['X'] = "0,0 4,6|0,6 4,0",
            ['Y'] = "0,6 2,3 4,6|2,3 2,0",
            ['Z'] = "0,6 4,6 0,0 4,0",
            ['0'] = "1,0 0,1 0,5 1,6 3,6 4,5 4,1 3,0 1,0",
            ['1'] = "1,5 2,6 2,0|1,0 3,0",
            ['2'] = "0,5 1,6 3,6 4,5 4,4 0,0 4,0",
            ['3'] = "0,5 1,6 3,6 4,5 4,4 3,3 1.5,3|3,3 4,2 4,1 3,0 1,0 0,1",
            ['4'] = "3,0 3,6 0,2 4,2",
            ['5'] = "4,6 0,6 0,3 3,3 4,2 4,1 3,0 0,0",
            ['6'] = "4,6 1,6 0,5 0,1 1,0 3,0 4,1 4,2 3,3 0,3",
            ['7'] = "0,6 4,6 1.5,0",
            ['8'] = "1,3 0,4 0,5 1,6 3,6 4,5 4,4 3,3 1,3 0,2 0,1 1,0 3,0 4,1 4,2 3,3",
            ['9'] = "0,0 3,0 4,1 4,5 3,6 1,6 0,5 0,4 1,3 4,3",
            ['$'] = "4,5 3,6 1,6 0,5 0,4 1,3 3,3 4,2 4,1 3,0 1,0 0,1|2,-0.8 2,6.8",
            ['.'] = "2,0 2,0.05",
            ['-'] = "0.5,3 3.5,3",
            [':'] = "2,4 2,4.05|2,1 2,1.05",
            ['!'] = "2,6 2,2|2,0 2,0.05",
        };

        /// <summary>
        /// Draws lettering with its vertical centre at (x, y) in mock units. align: 0 centred,
        /// -1 starts at x. Lower-case letters are drawn as smaller capitals.
        /// </summary>
        private static void Text(string s, float x, float y, float capUnits, Color fill, int align, float track = 0f)
        {
            float cap = capUnits * U;
            float stroke = cap * 0.20f;
            float edge = cap * 0.09f + 1.2f;

            // Lay the glyphs out first so the string can be centred.
            var segs = new List<Vector4>();
            float cursor = 0f;
            foreach (char raw in s)
            {
                bool small = char.IsLower(raw);
                char ch = char.ToUpperInvariant(raw);
                float scale = (small ? 0.80f : 1f) * cap / 6f;

                if (ch == ' ') { cursor += 3f * scale; continue; }
                if (!Glyphs.TryGetValue(ch, out var def)) { cursor += 5f * scale; continue; }

                foreach (var line in def.Split('|'))
                {
                    var pts = line.Split(' ');
                    Vector2 prev = default;
                    for (int i = 0; i < pts.Length; i++)
                    {
                        var xy = pts[i].Split(',');
                        var p = new Vector2(cursor + float.Parse(xy[0], System.Globalization.CultureInfo.InvariantCulture) * scale, float.Parse(xy[1], System.Globalization.CultureInfo.InvariantCulture) * scale);
                        if (i > 0) segs.Add(new Vector4(prev.x, prev.y, p.x, p.y));
                        prev = p;
                    }
                }
                cursor += 5.4f * scale + track * cap;
            }
            float width = cursor - 1.4f * cap / 6f - track * cap;

            float left = _ox + x * U - (align == 0 ? width * 0.5f : 0f);
            float baseline = _oy + y * U + cap * 0.5f;   // bottom of the capitals, y down

            int x0 = Mathf.FloorToInt(left - stroke - edge - 2), x1 = Mathf.CeilToInt(left + width + stroke + edge + 2);
            int y0 = Mathf.FloorToInt(baseline - cap - stroke - edge - 2), y1 = Mathf.CeilToInt(baseline + stroke + edge + 2);

            for (int py = y0; py <= y1; py++)
            {
                for (int px = x0; px <= x1; px++)
                {
                    // Glyph space: x right, y up from the baseline.
                    float gx = px + 0.5f - left, gy = baseline - (py + 0.5f);
                    float best = float.MaxValue;
                    for (int k = 0; k < segs.Count; k++)
                    {
                        var sgm = segs[k];
                        float d = SegmentDistance(gx, gy, sgm.x, sgm.y, sgm.z, sgm.w);
                        if (d < best) best = d;
                    }

                    float outer = Mathf.Clamp01(stroke * 0.5f + edge - best + 0.5f);
                    if (outer <= 0f) continue;
                    Plot(px, py, new Color(0.10f, 0.078f, 0.063f, fill.a), outer);
                    Plot(px, py, fill, Mathf.Clamp01(stroke * 0.5f - best + 0.5f));
                }
            }
        }

        private static float SegmentDistance(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float len2 = dx * dx + dy * dy;
            float t = len2 <= 0f ? 0f : Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / len2);
            float cx = ax + dx * t - px, cy = ay + dy * t - py;
            return Mathf.Sqrt(cx * cx + cy * cy);
        }
    }
}
