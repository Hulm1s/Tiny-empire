using System.Collections.Generic;
using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// The art for physical cash, drawn in code in the same style as <see cref="HudArt"/>, the
    /// guide arrow and the logo: a thick dark-brown outline, a lit top, a soft drop shadow.
    /// Sprites only - no Materials, no assets.
    ///
    /// - <see cref="BundleTexture"/>: one banded bundle of bills (a stack of notes with a paper band).
    /// - <see cref="PileTexture"/>: a pile of bundles at one of <see cref="PileLevels"/> heights,
    ///   the one that sits beside a till and grows as it fills.
    /// - <see cref="AtmTexture"/>: the cash machine standing in the shop.
    /// - <see cref="AtmPadTexture"/>: the flat tile of the ATM's interaction square.
    /// - <see cref="BankDisc"/> and <see cref="PlaqueTexture"/>: the two-number readout.
    ///
    /// All sizes are in units drawn at <c>u</c> pixels per unit. <c>Tycoon > Export Cash Sheet</c>
    /// renders them all.
    /// </summary>
    public static class CashArt
    {
        public const int PileLevels = 4;

        // Bill greens: a pale lit top, the darker edge of the paper stack, and the paper band.
        private static readonly Color BillTopA = new Color(0.62f, 0.90f, 0.46f);
        private static readonly Color BillTopB = new Color(0.34f, 0.72f, 0.30f);
        private static readonly Color BillSideA = new Color(0.26f, 0.58f, 0.24f);
        private static readonly Color BillSideB = new Color(0.17f, 0.42f, 0.18f);
        private static readonly Color BandA = new Color(1f, 0.93f, 0.66f);
        private static readonly Color BandB = new Color(0.90f, 0.70f, 0.28f);

        // ---- one bundle -----------------------------------------------------------------------

        // Bundle size in units: top face half-width / half-height, and the thickness of the stack.
        private const float Hx = 40f, Hy = 24f, Depth = 15f;

        public static Texture2D BundleTexture(float u, string name = "CashBundle")
        {
            int w = Mathf.CeilToInt((2 * Hx + 28f) * u), h = Mathf.CeilToInt((2 * Hy + Depth + 34f) * u);
            var px = new Color[w * h];
            Shadow(px, w, h, w * 0.5f, 16f * u, (Hx + 4f) * u, (Hy + 2f) * u, u, 0.38f);
            DrawBundle(px, w, h, w * 0.5f, (16f + Depth) * u + Hy * u, u);
            return HudArt.ToTexture(w, h, px, name);
        }

        /// <summary>Draws one bundle whose top face is centred on (cx, cy), y up, in pixels.</summary>
        private static void DrawBundle(Color[] px, int w, int h, float cx, float cy, float u)
        {
            float hx = Hx * u, hy = Hy * u, dep = Depth * u, ow = 4.5f * u, r = 9f * u;
            float bandHalf = 9f * u;

            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - dep - hy - ow - 3));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + hy + ow + 3));
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - hx - ow - 3));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + hx + ow + 3));

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float fx = x + 0.5f - cx, fy = y + 0.5f - cy;
                    // The whole block (top face + the stack under it) and the top face alone.
                    float dAll = HudArt.RoundRect(fx, fy + dep * 0.5f, hx, hy + dep * 0.5f, r);
                    float dTop = HudArt.RoundRect(fx, fy, hx, hy, r);

                    ref Color c = ref px[y * w + x];
                    HudArt.Over(ref c, HudArt.Outline, HudArt.Cov(dAll + ow));

                    // The stack's edge: dark green with fine horizontal bill lines.
                    float t = Mathf.Clamp01((fy + dep + hy) / (2f * hy + dep));
                    var side = Color.Lerp(BillSideB, BillSideA, t);
                    float lines = 0.5f + 0.5f * Mathf.Cos(fy / (2.3f * u) * Mathf.PI);
                    side = Color.Lerp(side, BillSideB, 0.45f * lines);
                    HudArt.Over(ref c, side, HudArt.Cov(dAll));

                    // A dark crease where the lit top meets the stack.
                    HudArt.Over(ref c, HudArt.Outline, HudArt.Cov(dTop + 1.6f * u) * 0.85f);

                    // The lit top face, with an inset printed border and a wash from above.
                    float tt = Mathf.Clamp01((fy + hy) / (2f * hy));
                    HudArt.Over(ref c, Color.Lerp(BillTopB, BillTopA, tt), HudArt.Cov(dTop));
                    float border = HudArt.Cov(1.0f * u - Mathf.Abs(dTop - 5.5f * u));
                    HudArt.Over(ref c, new Color(0.88f, 1f, 0.78f, 0.7f), border * HudArt.Cov(dTop - 2f * u));
                    HudArt.Over(ref c, Color.white, 0.22f * Mathf.Clamp01((tt - 0.45f) * 2f) * HudArt.Cov(dTop - 3f * u));

                    // Paper band, full height of the block, with a dark edge and a lit top.
                    float dBand = bandHalf - Mathf.Abs(fx);
                    float inBlock = HudArt.Cov(dAll - 1f * u);
                    HudArt.Over(ref c, HudArt.Outline, HudArt.Cov(dBand + 2.2f * u) * inBlock);
                    HudArt.Over(ref c, Color.Lerp(BandB, BandA, t), HudArt.Cov(dBand) * inBlock);

                    // A "$" stamped on each side of the band, on the top face.
                    float stamp = Mathf.Max(DollarMask(fx - hx * 0.50f, fy, u), DollarMask(fx + hx * 0.50f, fy, u));
                    HudArt.Over(ref c, new Color(0.12f, 0.36f, 0.14f, 0.9f), stamp * HudArt.Cov(dTop));
                }
            }
        }

        /// <summary>The "$" stamped on a bundle, coverage 0..1, centred on (0, 0) in pixels.</summary>
        private static float DollarMask(float fx, float fy, float u)
        {
            float k = 2.7f * u;
            float gx = fx / k + 2f, gy = fy / k + 3f;
            float best = float.MaxValue;
            var d = HudArt.Dollar;
            for (int i = 0; i < d.Length - 1; i++)
            {
                if (float.IsNaN(d[i].x) || float.IsNaN(d[i + 1].x)) continue;
                best = Mathf.Min(best, HudArt.SegDist(gx, gy, d[i], d[i + 1]));
            }
            return HudArt.Cov((0.62f - best) * k);
        }

        private static void Shadow(Color[] px, int w, int h, float cx, float cy, float rx, float ry, float u, float alpha)
        {
            float blur = 5f * u;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = (1f - Mathf.Sqrt(dx * dx + dy * dy)) * Mathf.Min(rx, ry);
                    HudArt.Over(ref px[y * w + x], new Color(0.04f, 0.02f, 0.01f, alpha),
                        Mathf.Clamp01((d + blur) / (2f * blur)));
                }
            }
        }

        // ---- the pile -------------------------------------------------------------------------

        // x offset, layer (0 = floor), y offset on the floor (up = further back), in units.
        private static readonly Vector3[][] Layouts =
        {
            new[] { new Vector3(0, 0, 0) },
            new[] { new Vector3(-30, 0, 0), new Vector3(30, 0, 0), new Vector3(0, 1, 0) },
            new[]
            {
                new Vector3(-44, 0, 10), new Vector3(44, 0, 10), new Vector3(0, 0, -8),
                new Vector3(-24, 1, 6), new Vector3(24, 1, 6), new Vector3(0, 2, 2),
            },
            new[]
            {
                new Vector3(-58, 0, 14), new Vector3(0, 0, 18), new Vector3(58, 0, 14),
                new Vector3(-34, 0, -8), new Vector3(34, 0, -8),
                new Vector3(-34, 1, 10), new Vector3(34, 1, 10), new Vector3(0, 1, -4),
                new Vector3(-18, 2, 6), new Vector3(18, 2, 6), new Vector3(0, 3, 2),
            },
        };

        /// <summary>The pile beside a till at <paramref name="level"/> 1..<see cref="PileLevels"/>.</summary>
        public static Texture2D PileTexture(int level, float u, string name = null)
        {
            level = Mathf.Clamp(level, 1, PileLevels);
            var lay = Layouts[level - 1];

            // A fixed canvas for every level, so the pile grows in place and shares one pivot.
            int w = Mathf.CeilToInt(240f * u), h = Mathf.CeilToInt(190f * u);
            var px = new Color[w * h];
            float baseY = 30f * u;
            Shadow(px, w, h, w * 0.5f, baseY - 2f * u, Mathf.Min(100f, level == 1 ? 50f : 42f + level * 15f) * u,
                (level == 1 ? 24f : 28f + level * 4f) * u, u, 0.40f);

            var order = new List<Vector3>(lay);
            order.Sort((a, b) =>
            {
                int c = a.y.CompareTo(b.y);               // lower layers first
                return c != 0 ? c : b.z.CompareTo(a.z);    // then back to front
            });
            foreach (var b in order)
            {
                float cx = w * 0.5f + b.x * u;
                float cy = baseY + (Hy + Depth + b.y * (Depth + 2f) + b.z) * u;
                DrawBundle(px, w, h, cx, cy, u);
            }
            return HudArt.ToTexture(w, h, px, name ?? "CashPile" + level);
        }

        // ---- textures for the 3D bundle -----------------------------------------------------------

        /// <summary>
        /// The bundle seen from above, for the top face of the 3D bundle mesh: lit green with a
        /// printed border, the gold paper band across the middle and a "$" either side. Opaque.
        /// Aspect is about 1.56 : 1 (the bundle's length : width).
        /// </summary>
        public static Texture2D BundleTopTexture(int width = 256, string name = "CashBundleTop")
        {
            int w = width, h = Mathf.RoundToInt(width / 1.56f);
            var px = new Color[w * h];
            float u = w / 100f;                       // 100 "units" across
            float cx = w * 0.5f, cy = h * 0.5f;
            float bandHalf = 9f * u;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f - cx, fy = y + 0.5f - cy;
                    float tt = Mathf.Clamp01((fy + h * 0.5f) / h);
                    var c = Color.Lerp(BillTopB, BillTopA, tt);
                    float d = HudArt.RoundRect(fx, fy, w * 0.5f, h * 0.5f, 6f * u);
                    float border = HudArt.Cov(0.7f * u - Mathf.Abs(d - 4.2f * u));
                    c = Color.Lerp(c, new Color(0.88f, 1f, 0.78f), border * 0.8f);
                    float stamp = Mathf.Max(DollarMask(fx - w * 0.27f, fy, u * 1.9f), DollarMask(fx + w * 0.27f, fy, u * 1.9f));
                    c = Color.Lerp(c, new Color(0.12f, 0.36f, 0.14f), stamp * 0.9f);
                    float dBand = bandHalf - Mathf.Abs(fx);
                    c = Color.Lerp(c, HudArt.Outline, HudArt.Cov(dBand + 1.6f * u));
                    c = Color.Lerp(c, Color.Lerp(BandB, BandA, tt), HudArt.Cov(dBand));
                    // Dark outer edge so it reads against the stack's side.
                    c = Color.Lerp(c, HudArt.Outline, Mathf.Clamp01(-(d - 1.6f * u) * 0.5f + 0.5f) * 0.0f);
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            }
            return HudArt.ToTexture(w, h, px, name);
        }

        /// <summary>Fine horizontal bill lines in two greens, for the sides of the bundle. Opaque.</summary>
        public static Texture2D BundleSideTexture(string name = "CashBundleSide")
        {
            const int w = 32, h = 64;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float lines = 0.5f + 0.5f * Mathf.Cos(y / 4f * Mathf.PI);
                var c = Color.Lerp(BillSideA, BillSideB, 0.25f + 0.6f * lines);
                for (int x = 0; x < w; x++) px[y * w + x] = c;
            }
            var tex = HudArt.ToTexture(w, h, px, name);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // ---- the bank icon ----------------------------------------------------------------------

        /// <summary>A disc (any <see cref="HudArt.Look"/>) with the bank glyph on it.</summary>
        public static Texture2D BankDisc(float sizeU, float u, HudArt.Look look, string name = "BankIcon")
        {
            return HudArt.DiscTexture(sizeU, look, u, false, name,
                (px, n, cx, cy, r) => DrawBank(px, n, cx, cy, r, Color.white));
        }

        /// <summary>A small classical bank: pediment, columns, steps. Ink with a dark edge, like the glyphs.</summary>
        public static void DrawBank(Color[] px, int n, float cx, float cy, float r, Color ink)
        {
            float edge = Mathf.Max(1.6f, r * 0.075f);
            // Boxes: centre x, centre y, half width, half height, in fractions of r.
            var boxes = new[]
            {
                new Vector4(0f, -0.58f, 0.62f, 0.08f),      // bottom step
                new Vector4(0f, -0.43f, 0.52f, 0.08f),      // upper step
                new Vector4(-0.34f, -0.02f, 0.085f, 0.30f), // columns
                new Vector4(0f, -0.02f, 0.085f, 0.30f),
                new Vector4(0.34f, -0.02f, 0.085f, 0.30f),
                new Vector4(0f, 0.32f, 0.56f, 0.07f),       // entablature
            };
            int h = px.Length / n;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - r)), x1 = Mathf.Min(n - 1, Mathf.CeilToInt(cx + r));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - r)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + r));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / r, dy = (y + 0.5f - cy) / r;
                    float d = float.MinValue;
                    foreach (var b in boxes)
                        d = Mathf.Max(d, HudArt.RoundRect((dx - b.x) * r, (dy - b.y) * r, b.z * r, b.w * r, Mathf.Min(b.z, b.w) * r * 0.45f));
                    // Pediment: a triangle, apex (0, 0.68), base y = 0.40, half width ~0.60.
                    float tri = Mathf.Min((0.68f - dy) * 0.9f - Mathf.Abs(dx) * 0.55f, (dy - 0.40f) * 2f) * r;
                    d = Mathf.Max(d, tri);
                    float hole = (0.075f - Mathf.Sqrt(dx * dx + (dy - 0.50f) * (dy - 0.50f))) * r;

                    HudArt.Over(ref px[y * n + x], HudArt.Outline, HudArt.Cov(d + edge));
                    HudArt.Over(ref px[y * n + x], ink, HudArt.Cov(d));
                    HudArt.Over(ref px[y * n + x], new Color(0.4f, 0.28f, 0.1f), HudArt.Cov(hole) * HudArt.Cov(d));
                }
            }
        }

        // ---- the ATM ----------------------------------------------------------------------------

        private static readonly HudArt.Look AtmBody = new HudArt.Look
        {
            bodyTop = new Color(0.50f, 0.68f, 0.86f), bodyBottom = new Color(0.20f, 0.36f, 0.58f),
            rimTop = new Color(1f, 0.9f, 0.5f), rimBottom = new Color(0.8f, 0.5f, 0.1f),
            gloss = 0.28f, outline = 8f, rim = 7f
        };

        private static readonly HudArt.Look ScreenLook = new HudArt.Look
        {
            bodyTop = new Color(0.09f, 0.26f, 0.22f), bodyBottom = new Color(0.03f, 0.12f, 0.12f),
            rimTop = new Color(0.78f, 0.82f, 0.88f), rimBottom = new Color(0.42f, 0.48f, 0.58f),
            gloss = 0.16f, outline = 3.5f, rim = 3f
        };

        /// <summary>The ATM seen from the front: a sign, the lit screen, keypad, card and cash slots.</summary>
        public static Texture2D AtmTexture(float widthU, float heightU, float u, string name = "Atm")
        {
            int w = Mathf.CeilToInt(widthU * u), h = Mathf.CeilToInt(heightU * u);
            float margin = 12f * u, drop = 6f * u;
            float hx = w * 0.5f - margin, hy = (h - 2f * margin - drop) * 0.5f;
            float cx = w * 0.5f, cy = margin + drop + hy;
            var px = HudArt.Render(w, h, (x, y) => HudArt.RoundRect(x, y, hx, hy, 26f * u),
                cx, cy, cy - hy, cy + hy, AtmBody, u, false);

            float B = cy - hy;                  // body bottom edge in pixels
            float W = hx * 2f, H = hy * 2f;

            // Header sign: gold plate across the top, a bank glyph either side of a coin.
            float sy0 = B + H * 0.865f;
            Plate(px, w, cx, sy0, W * 0.78f, H * 0.17f, 12f * u, HudArt.Looks.Gold, u);
            DrawBank(px, w, cx - W * 0.25f, sy0, H * 0.075f, Color.white);
            DrawBank(px, w, cx + W * 0.25f, sy0, H * 0.075f, Color.white);
            Coin(px, w, cx, sy0, H * 0.068f, u);

            // Screen: dark glass with a green glow, a dollar sign and two soft option bars.
            float sw = W * 0.74f, sh = H * 0.25f, sy = B + H * 0.635f;
            Plate(px, w, cx, sy, sw, sh, 12f * u, ScreenLook, u);
            Glow(px, w, cx, sy, sw * 0.5f, sh * 0.5f, new Color(0.45f, 1f, 0.6f), 0.28f);
            StampDollar(px, w, cx - sw * 0.22f, sy, sh * 0.30f, new Color(0.7f, 1f, 0.75f));
            Bar(px, w, cx + sw * 0.16f, sy + sh * 0.16f, sw * 0.28f, sh * 0.11f, new Color(0.7f, 1f, 0.75f, 0.9f));
            Bar(px, w, cx + sw * 0.16f, sy - sh * 0.14f, sw * 0.28f, sh * 0.11f, new Color(0.7f, 1f, 0.75f, 0.55f));

            // Keypad: 3 x 3 chunky keys, with a green ENTER key beside it.
            float kx0 = cx - W * 0.30f, ky0 = B + H * 0.435f;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    Key(px, w, kx0 + col * W * 0.115f, ky0 - row * H * 0.068f, W * 0.092f, H * 0.058f, u,
                        new Color(0.93f, 0.95f, 0.98f), new Color(0.62f, 0.68f, 0.78f));
            Key(px, w, cx + W * 0.26f, ky0 - H * 0.068f, W * 0.15f, H * 0.19f, u,
                new Color(0.55f, 0.88f, 0.42f), new Color(0.2f, 0.55f, 0.2f));

            // Card slot, and the cash tray with a bill poking out of it.
            Bar(px, w, cx - W * 0.22f, B + H * 0.115f, W * 0.26f, H * 0.032f, new Color(0.08f, 0.06f, 0.05f));
            float tx = cx + W * 0.06f, ty = B + H * 0.115f, tw = W * 0.58f, th = H * 0.085f;
            Bar(px, w, tx, ty, tw, th, new Color(0.08f, 0.06f, 0.05f));
            Bar(px, w, tx, ty + th * 0.15f, tw * 0.80f, th * 0.55f, new Color(0.62f, 0.90f, 0.46f));
            Bar(px, w, tx, ty - th * 0.10f, tw * 0.80f, th * 0.18f, new Color(0.17f, 0.42f, 0.18f));
            return HudArt.ToTexture(w, h, px, name);
        }

        /// <summary>Paints a small framed plate (a HudArt look) centred on (cx, cy), without a shadow.</summary>
        private static void Plate(Color[] px, int w, float cx, float cy, float pw, float ph, float radius,
            HudArt.Look look, float u)
        {
            int h = px.Length / w;
            float ow = look.outline * u, rimW = look.rim * u;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - pw * 0.5f - ow - 2));
            int x1 = Mathf.Min(w - 1, Mathf.CeilToInt(cx + pw * 0.5f + ow + 2));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(cy - ph * 0.5f - ow - 2));
            int y1 = Mathf.Min(h - 1, Mathf.CeilToInt(cy + ph * 0.5f + ow + 2));
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float fx = x + 0.5f - cx, fy = y + 0.5f - cy;
                    float d = HudArt.RoundRect(fx, fy, pw * 0.5f, ph * 0.5f, Mathf.Min(radius, ph * 0.5f));
                    float t = Mathf.Clamp01((fy + ph * 0.5f) / ph);
                    ref Color c = ref px[y * w + x];
                    HudArt.Over(ref c, HudArt.Outline, HudArt.Cov(d + ow));
                    HudArt.Over(ref c, Color.Lerp(look.rimBottom, look.rimTop, t), HudArt.Cov(d));
                    HudArt.Over(ref c, Color.Lerp(look.bodyBottom, look.bodyTop, t), HudArt.Cov(d - rimW));
                    float wash = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.5f) * 2f));
                    HudArt.Over(ref c, Color.white, look.gloss * wash * 0.8f * HudArt.Cov(d - rimW - 2f * u));
                }
            }
        }

        private static void Glow(Color[] px, int w, float cx, float cy, float rx, float ry, Color c, float a)
        {
            int h = px.Length / w;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(cy - ry)); y < Mathf.Min(h, Mathf.CeilToInt(cy + ry)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(cx - rx)); x < Mathf.Min(w, Mathf.CeilToInt(cx + rx)); x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float k = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    HudArt.Over(ref px[y * w + x], c, a * k * k);
                }
        }

        private static void Bar(Color[] px, int w, float cx, float cy, float bw, float bh, Color c)
        {
            int h = px.Length / w;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(cy - bh * 0.5f - 2)); y < Mathf.Min(h, Mathf.CeilToInt(cy + bh * 0.5f + 2)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(cx - bw * 0.5f - 2)); x < Mathf.Min(w, Mathf.CeilToInt(cx + bw * 0.5f + 2)); x++)
                    HudArt.Over(ref px[y * w + x], c,
                        HudArt.Cov(HudArt.RoundRect(x + 0.5f - cx, y + 0.5f - cy, bw * 0.5f, bh * 0.5f, Mathf.Min(bw, bh) * 0.4f)));
        }

        private static void Key(Color[] px, int w, float cx, float cy, float kw, float kh, float u, Color top, Color bottom)
        {
            int h = px.Length / w;
            float ow = 2.6f * u, r = Mathf.Min(kw, kh) * 0.3f;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(cy - kh * 0.5f - ow - 2)); y < Mathf.Min(h, Mathf.CeilToInt(cy + kh * 0.5f + ow + 2)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(cx - kw * 0.5f - ow - 2)); x < Mathf.Min(w, Mathf.CeilToInt(cx + kw * 0.5f + ow + 2)); x++)
                {
                    float fx = x + 0.5f - cx, fy = y + 0.5f - cy;
                    float d = HudArt.RoundRect(fx, fy, kw * 0.5f, kh * 0.5f, r);
                    float t = Mathf.Clamp01((fy + kh * 0.5f) / kh);
                    HudArt.Over(ref px[y * w + x], HudArt.Outline, HudArt.Cov(d + ow));
                    HudArt.Over(ref px[y * w + x], Color.Lerp(bottom, top, t), HudArt.Cov(d));
                    HudArt.Over(ref px[y * w + x], Color.white, 0.35f * Mathf.Clamp01((t - 0.55f) * 2.2f) * HudArt.Cov(d - 1.5f * u));
                }
        }

        private static void Coin(Color[] px, int w, float cx, float cy, float r, float u)
        {
            int h = px.Length / w;
            float ring = r * 0.62f;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(cy - r - 6)); y < Mathf.Min(h, Mathf.CeilToInt(cy + r + 6)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(cx - r - 6)); x < Mathf.Min(w, Mathf.CeilToInt(cx + r + 6)); x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float t = Mathf.Clamp01((dy + r) / (2 * r));
                    HudArt.Over(ref px[y * w + x], HudArt.Outline, HudArt.Cov(r + 3.2f * u - d));
                    HudArt.Over(ref px[y * w + x], Color.Lerp(new Color(0.96f, 0.66f, 0.12f), new Color(1f, 0.92f, 0.5f), t), HudArt.Cov(r - d));
                    HudArt.Over(ref px[y * w + x], new Color(0.62f, 0.38f, 0.05f, 0.85f), HudArt.Cov(1.1f * u - Mathf.Abs(d - ring)));
                }
            StampDollar(px, w, cx, cy, r * 0.62f, new Color(0.55f, 0.32f, 0.04f));
        }

        private static void StampDollar(Color[] px, int w, float cx, float cy, float halfHeight, Color c)
        {
            float k = halfHeight / 3.4f;
            int h = px.Length / w;
            var d = HudArt.Dollar;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(cy - halfHeight * 1.3f)); y < Mathf.Min(h, Mathf.CeilToInt(cy + halfHeight * 1.3f)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(cx - halfHeight)); x < Mathf.Min(w, Mathf.CeilToInt(cx + halfHeight)); x++)
                {
                    float gx = (x + 0.5f - cx) / k + 2f, gy = (y + 0.5f - cy) / k + 3f;
                    float best = float.MaxValue;
                    for (int i = 0; i < d.Length - 1; i++)
                    {
                        if (float.IsNaN(d[i].x) || float.IsNaN(d[i + 1].x)) continue;
                        best = Mathf.Min(best, HudArt.SegDist(gx, gy, d[i], d[i + 1]));
                    }
                    HudArt.Over(ref px[y * w + x], c, HudArt.Cov((0.62f - best) * k));
                }
        }

        // ---- the ATM square's tile --------------------------------------------------------------

        /// <summary>
        /// The flat tile of the ATM's interaction square, seen from above: a blue pad with a gold
        /// rim, chevrons pointing at the machine, and the bank glyph on a disc.
        /// </summary>
        public static Texture2D AtmPadTexture(float sizeU, float u, string name = "AtmPad")
        {
            var look = new HudArt.Look
            {
                bodyTop = new Color(0.42f, 0.74f, 0.98f, 0.86f), bodyBottom = new Color(0.20f, 0.46f, 0.80f, 0.86f),
                rimTop = new Color(1f, 0.9f, 0.5f), rimBottom = new Color(0.8f, 0.5f, 0.1f), gloss = 0.22f, outline = 7f, rim = 7f
            };
            int n = Mathf.CeilToInt(sizeU * u);
            float margin = 8f * u, drop = 5f * u;
            float half = (n - 2f * margin - drop) * 0.5f;
            float cx = n * 0.5f, cy = margin + drop + half;
            var px = HudArt.Render(n, n, (x, y) => HudArt.RoundRect(x, y, half, half, 30f * u), cx, cy, cy - half, cy + half, look, u, false);

            // Chevrons along the top (the machine's side), pointing up.
            for (int i = 0; i < 3; i++)
            {
                float yy = cy + half * (0.40f + i * 0.17f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float fx = x + 0.5f - cx, fy = y + 0.5f - yy;
                        float d = 3.2f * u - Mathf.Abs(Mathf.Abs(fx) * 0.45f + fy);
                        float limit = half * 0.34f - Mathf.Abs(fx);
                        HudArt.Over(ref px[y * n + x], new Color(1f, 1f, 1f, 0.7f - i * 0.18f), HudArt.Cov(d) * HudArt.Cov(limit));
                    }
            }

            Blit(px, n, BankDisc(half * 2f * 0.50f / u, u, HudArt.Looks.Medallion, "d"), cx, cy - half * 0.22f);
            return HudArt.ToTexture(n, n, px, name);
        }

        /// <summary>Composites a texture centred on (cx, cy) and destroys it.</summary>
        private static void Blit(Color[] px, int w, Texture2D src, float cx, float cy)
        {
            int h = px.Length / w;
            var sp = src.GetPixels();
            int ox = Mathf.RoundToInt(cx - src.width * 0.5f), oy = Mathf.RoundToInt(cy - src.height * 0.5f);
            for (int y = 0; y < src.height; y++)
                for (int x = 0; x < src.width; x++)
                {
                    int tx = ox + x, ty = oy + y;
                    if (tx < 0 || ty < 0 || tx >= w || ty >= h) continue;
                    HudArt.Over(ref px[ty * w + tx], sp[y * src.width + x], 1f);
                }
            Object.DestroyImmediate(src);
        }

        // ---- the money plaque -------------------------------------------------------------------

        public const float PlaqueWidth = 420f, PlaqueHeight = 150f;

        private static Sprite _plaque;

        /// <summary>The plaque frame as a runtime sprite, built once.</summary>
        public static Sprite PlaqueSprite()
        {
            if (_plaque != null) return _plaque;
            var tex = PlaqueTexture(HudArt.RuntimeScale);
            _plaque = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _plaque;
        }

        /// <summary>Where the live numbers go, in plaque units from the plaque's top-left.</summary>
        public static class PlaqueLayout
        {
            public static readonly Vector2 CoinCentre = new Vector2(64f, 52f);
            public static readonly Vector2 CashTextLeft = new Vector2(124f, 52f);     // big, left aligned
            public static readonly Vector2 BankCentre = new Vector2(52f, 118f);
            public static readonly Vector2 AccountTextLeft = new Vector2(98f, 118f);  // small, left aligned
        }

        /// <summary>
        /// The two-number money plaque frame: a tall plaque with a coin at the left for the cash in
        /// hand (the big number goes right of it) and, underneath, a recessed account pill with a
        /// bank glyph at its left (the small number goes right of it). The numbers are live text,
        /// so they are not drawn here. The coin is drawn by <see cref="HudArt.CoinTexture"/>.
        /// </summary>
        public static Texture2D PlaqueTexture(float u, string name = "MoneyPlaque")
        {
            float m = HudArt.Margin;
            var tex = HudArt.PanelTexture(PlaqueWidth + 2f * m, PlaqueHeight + 2f * m + 6f, 44f,
                HudArt.Looks.Plaque, u, false, name);
            var px = tex.GetPixels();
            int w = tex.width, h = tex.height;
            Object.DestroyImmediate(tex);

            // Convert plaque units (from the visible top-left) to pixels (y up).
            System.Func<float, float> X = ux => (m + ux) * u;
            System.Func<float, float> Y = uy => h - (m + uy) * u;

            var pill = new HudArt.Look
            {
                bodyTop = new Color(0.05f, 0.035f, 0.025f, 0.95f), bodyBottom = new Color(0.14f, 0.10f, 0.07f, 0.95f),
                rimTop = new Color(0.60f, 0.44f, 0.20f), rimBottom = new Color(0.42f, 0.28f, 0.10f), gloss = 0f, outline = 3f, rim = 2.5f
            };
            float pw = (PlaqueWidth - 52f) * u, ph = 50f * u;
            Plate(px, w, X(PlaqueWidth * 0.5f), Y(118f), pw, ph, 22f * u, pill, u);

            Blit(px, w, BankDisc(62f, u, HudArt.Looks.Blue, "bd"), X(PlaqueLayout.BankCentre.x), Y(PlaqueLayout.BankCentre.y));
            return HudArt.ToTexture(w, h, px, name);
        }
    }
}
