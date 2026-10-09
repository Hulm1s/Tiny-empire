using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// The HUD's frames, plaques, coin, medallion and badge, drawn in code in the same style as
    /// the guide arrow and the logo: a thick dark-brown outline, a gold rim lit from above, a
    /// fine seam, a glossy body and a soft drop shadow. Sprites only - no Materials, no assets.
    ///
    /// Every size is given in HUD canvas units and drawn at <see cref="RuntimeScale"/> pixels
    /// per unit, so the thickness of an outline stays the same whatever size the panel is
    /// (which a stretched 9-slice would not guarantee). Each sprite carries a margin of about
    /// 10 units for its shadow, so lay the Image out a little larger than the plate you want.
    ///
    /// Built from the colours below only; <c>Tycoon > Export HUD Sheet</c> renders them all.
    /// </summary>
    public static class HudArt
    {
        /// <summary>Pixels per canvas unit at runtime. 1080 units across a phone is ~375-430 pt.</summary>
        public const float RuntimeScale = 0.75f;

        /// <summary>Shadow margin around every sprite, in units.</summary>
        public const float Margin = 10f;

        public struct Look
        {
            public Color bodyTop, bodyBottom, rimTop, rimBottom;
            public float gloss;     // strength of the white wash over the upper half
            public float outline;   // dark outline thickness, units
            public float rim;       // gold rim thickness, units
        }

        public static readonly Color Outline = new Color(0.15f, 0.08f, 0.04f);
        private static readonly Color Seam = new Color(0.36f, 0.19f, 0.04f);
        private static readonly Color GoldTop = new Color(1f, 0.9f, 0.5f);
        private static readonly Color GoldBottom = new Color(0.8f, 0.5f, 0.1f);

        private static Look Make(Color top, Color bottom, float gloss, float outline = 9f, float rim = 9f) =>
            new Look { bodyTop = top, bodyBottom = bottom, rimTop = GoldTop, rimBottom = GoldBottom,
                       gloss = gloss, outline = outline, rim = rim };

        /// <summary>The ready-made looks. Bodies differ; the dark outline and gold rim never do.</summary>
        public static class Looks
        {
            /// <summary>Dark panel for readouts and cards (the money plaque, the task card).</summary>
            public static readonly Look Plaque = Make(
                new Color(0.22f, 0.155f, 0.10f, 0.92f), new Color(0.11f, 0.075f, 0.05f, 0.92f), 0.09f);

            /// <summary>The pause board: darker and taller, a little thicker rim.</summary>
            public static readonly Look Board = Make(
                new Color(0.25f, 0.175f, 0.115f, 0.96f), new Color(0.12f, 0.08f, 0.05f, 0.96f), 0.07f, 11f, 11f);

            public static readonly Look Gold = Make(
                new Color(1f, 0.86f, 0.36f), new Color(0.94f, 0.60f, 0.10f), 0.40f);

            public static readonly Look Blue = Make(
                new Color(0.45f, 0.74f, 0.95f), new Color(0.17f, 0.40f, 0.68f), 0.40f);

            public static readonly Look Red = Make(
                new Color(0.97f, 0.42f, 0.33f), new Color(0.66f, 0.13f, 0.11f), 0.40f);

            public static readonly Look Green = Make(
                new Color(0.55f, 0.88f, 0.42f), new Color(0.20f, 0.55f, 0.20f), 0.40f);

            /// <summary>A quiet tab: dark body, so the coloured one beside it reads as selected.</summary>
            public static readonly Look Tab = Make(
                new Color(0.27f, 0.19f, 0.12f, 0.95f), new Color(0.14f, 0.095f, 0.06f, 0.95f), 0.10f, 7f, 6f);

            public static readonly Look CoinGold = Make(
                new Color(1f, 0.92f, 0.50f), new Color(0.96f, 0.66f, 0.12f), 0.5f, 7f, 8f);

            public static readonly Look Medallion = Make(
                new Color(0.30f, 0.21f, 0.13f), new Color(0.11f, 0.075f, 0.05f), 0.20f, 10f, 11f);

            /// <summary>The same medallion with a slimmer frame, for the small collapse button.</summary>
            public static readonly Look MedallionSmall = Make(
                new Color(0.30f, 0.21f, 0.13f), new Color(0.11f, 0.075f, 0.05f), 0.20f, 5f, 6f);

            public static readonly Look Badge = new Look
            {
                bodyTop = new Color(1f, 0.45f, 0.36f), bodyBottom = new Color(0.74f, 0.14f, 0.12f),
                rimTop = Color.white, rimBottom = new Color(0.82f, 0.84f, 0.9f),
                gloss = 0.35f, outline = 4f, rim = 3.5f
            };
        }

        // ---- sprites ----------------------------------------------------------------------

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>A framed rectangle, <paramref name="width"/> x <paramref name="height"/> units.</summary>
        public static Sprite Panel(float width, float height, float radius, Look look, bool pressed = false,
            string key = null)
        {
            string k = $"panel_{key}{ColorUtility.ToHtmlStringRGBA(look.bodyTop)}{ColorUtility.ToHtmlStringRGBA(look.bodyBottom)}_{width}x{height}_{radius}_{pressed}";
            if (Cache.TryGetValue(k, out var hit) && hit != null) return hit;
            var sprite = ToSprite(PanelTexture(width, height, radius, look, RuntimeScale, pressed, k));
            Cache[k] = sprite;
            return sprite;
        }

        public static Sprite Coin(float size)
        {
            string k = "coin_" + size;
            if (Cache.TryGetValue(k, out var hit) && hit != null) return hit;
            var sprite = ToSprite(CoinTexture(size, RuntimeScale, k));
            Cache[k] = sprite;
            return sprite;
        }

        /// <summary>What is stamped on a medallion.</summary>
        public enum Glyph { Pause, Minus, Plus }

        public static Sprite Medallion(float size, bool pressed, Glyph glyph = Glyph.Pause)
        {
            string k = $"medal_{size}_{pressed}_{glyph}";
            if (Cache.TryGetValue(k, out var hit) && hit != null) return hit;
            var sprite = ToSprite(MedallionTexture(size, RuntimeScale, pressed, k, glyph));
            Cache[k] = sprite;
            return sprite;
        }

        public static Sprite Badge(float size)
        {
            string k = "badge_" + size;
            if (Cache.TryGetValue(k, out var hit) && hit != null) return hit;
            var sprite = ToSprite(DiscTexture(size, Looks.Badge, RuntimeScale, false, k, null));
            Cache[k] = sprite;
            return sprite;
        }

        private static Sprite ToSprite(Texture2D tex) =>
            Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        // ---- textures (also used by the sheet, at its own scale) ----------------------------

        public static Texture2D PanelTexture(float widthU, float heightU, float radiusU, Look look,
            float u, bool pressed, string name = "HudPanel")
        {
            int w = Mathf.CeilToInt(widthU * u), h = Mathf.CeilToInt(heightU * u);
            float margin = Margin * u, dropMax = 6f * u;
            float drop = pressed ? 1.5f * u : dropMax;
            float hx = w * 0.5f - margin;
            float hy = (h - 2f * margin - dropMax) * 0.5f;
            float cx = w * 0.5f, cy = margin + drop + hy;
            float r = Mathf.Min(radiusU * u, Mathf.Min(hx, hy));

            var px = Render(w, h, (x, y) => RoundRect(x, y, hx, hy, r), cx, cy, cy - hy, cy + hy, look, u, pressed);
            return ToTexture(w, h, px, name);
        }

        public static Texture2D DiscTexture(float sizeU, Look look, float u, bool pressed, string name,
            Action<Color[], int, float, float, float> decorate)
        {
            int n = Mathf.CeilToInt(sizeU * u);
            float margin = 8f * u, dropMax = 5f * u;
            float drop = pressed ? 1.2f * u : dropMax;
            float radius = (n - 2f * margin - dropMax) * 0.5f;
            float cx = n * 0.5f, cy = margin + drop + radius;

            var px = Render(n, n, (x, y) => radius - Mathf.Sqrt(x * x + y * y), cx, cy,
                cy - radius, cy + radius, look, u, pressed);
            decorate?.Invoke(px, n, cx, cy, radius);
            return ToTexture(n, n, px, name);
        }

        public static Texture2D CoinTexture(float sizeU, float u, string name = "HudCoin")
        {
            return DiscTexture(sizeU, Looks.CoinGold, u, false, name, (px, n, cx, cy, r) =>
            {
                float ringR = r * 0.60f;
                float ringW = Mathf.Max(1.5f, r * 0.07f);
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);

                        // Stamped inner ring: a dark groove with a pale lip under it.
                        float groove = Cov(ringW * 0.5f - Mathf.Abs(dist - ringR));
                        Over(ref px[y * n + x], new Color(0.62f, 0.38f, 0.05f, 0.85f), groove);
                        float lip = Cov(ringW * 0.5f - Mathf.Abs(dist - (ringR - ringW * 1.1f)));
                        Over(ref px[y * n + x], new Color(1f, 0.97f, 0.7f, 0.6f), lip * (dy > 0 ? 0.3f : 1f));

                        // A raised centre, a shade lighter than the face.
                        float centre = Cov(ringR - ringW - dist);
                        Over(ref px[y * n + x], new Color(1f, 0.98f, 0.75f, 0.22f), centre);

                        // The dollar sign, stamped: one S and the bar through it.
                        float k = r * 0.105f;
                        float gx = dx / k + 2f, gy = dy / k + 3f;
                        float best = float.MaxValue;
                        for (int i = 0; i < Dollar.Length - 1; i += 1)
                        {
                            if (float.IsNaN(Dollar[i].x) || float.IsNaN(Dollar[i + 1].x)) continue;
                            best = Mathf.Min(best, SegDist(gx, gy, Dollar[i], Dollar[i + 1]));
                        }
                        float sign = Cov(((r * 0.085f) / k - best) * k);
                        Over(ref px[y * n + x], new Color(0.62f, 0.38f, 0.05f, 0.92f), sign * centre);
                    }
                }
                Glint(px, n, cx - r * 0.42f, cy + r * 0.48f, r * 0.20f);
            });
        }

        public static Texture2D MedallionTexture(float sizeU, float u, bool pressed, string name = "HudMedallion",
            Glyph glyph = Glyph.Pause)
        {
            // Small medallions get the thin frame, or the outline and rim would eat the disc.
            var look = sizeU < 90f ? Looks.MedallionSmall : Looks.Medallion;
            return DiscTexture(sizeU, look, u, pressed, name, (px, n, cx, cy, r) =>
            {
                // The glyph is a few chunky bars, white with a dark edge, like the lettering.
                // Each box: centre x, centre y, half width, half height, as fractions of the radius.
                Vector4[] boxes;
                switch (glyph)
                {
                    case Glyph.Minus:
                        boxes = new[] { new Vector4(0f, 0f, 0.46f, 0.13f) };
                        break;
                    case Glyph.Plus:
                        boxes = new[] { new Vector4(0f, 0f, 0.46f, 0.13f), new Vector4(0f, 0f, 0.13f, 0.46f) };
                        break;
                    default:
                        boxes = new[] { new Vector4(-0.20f, 0f, 0.15f, 0.42f), new Vector4(0.20f, 0f, 0.15f, 0.42f) };
                        break;
                }

                float edge = Mathf.Max(2f, r * 0.07f);
                Color ink = pressed ? new Color(0.88f, 0.86f, 0.80f) : Color.white;
                for (int y = 0; y < n; y++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                        float d = float.MinValue;
                        foreach (var b in boxes)
                        {
                            float hw = b.z * r, hh = b.w * r;
                            float rr = Mathf.Min(hw, hh) * 0.6f;
                            d = Mathf.Max(d, RoundRect(dx - b.x * r, dy - b.y * r, hw, hh, rr));
                        }
                        Over(ref px[y * n + x], Outline, Cov(d + edge));
                        Over(ref px[y * n + x], ink, Cov(d));
                    }
                }
            });
        }

        private static readonly Vector2[] Dollar =
        {
            new Vector2(4f, 5f), new Vector2(3f, 6f), new Vector2(1f, 6f), new Vector2(0f, 5f), new Vector2(0f, 4f),
            new Vector2(1f, 3f), new Vector2(3f, 3f), new Vector2(4f, 2f), new Vector2(4f, 1f), new Vector2(3f, 0f),
            new Vector2(1f, 0f), new Vector2(0f, 1f),
            new Vector2(float.NaN, 0f),
            new Vector2(2f, -0.9f), new Vector2(2f, 6.9f),
        };

        private static float SegDist(float px, float py, Vector2 a, Vector2 b)
        {
            float dx = b.x - a.x, dy = b.y - a.y;
            float len2 = dx * dx + dy * dy;
            float t = len2 <= 0f ? 0f : Mathf.Clamp01(((px - a.x) * dx + (py - a.y) * dy) / len2);
            float qx = a.x + dx * t - px, qy = a.y + dy * t - py;
            return Mathf.Sqrt(qx * qx + qy * qy);
        }

        // ---- the renderer -----------------------------------------------------------------

        private static float RoundRect(float x, float y, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x) - (hx - r);
            float qy = Mathf.Abs(y) - (hy - r);
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            return r - (outside + Mathf.Min(Mathf.Max(qx, qy), 0f));
        }

        private static float Cov(float d) => Mathf.Clamp01(d + 0.5f);

        private static void Over(ref Color dst, Color src, float cover)
        {
            float sa = src.a * cover;
            if (sa <= 0f) return;
            float oa = sa + dst.a * (1f - sa);
            dst = new Color(
                (src.r * sa + dst.r * dst.a * (1f - sa)) / oa,
                (src.g * sa + dst.g * dst.a * (1f - sa)) / oa,
                (src.b * sa + dst.b * dst.a * (1f - sa)) / oa,
                oa);
        }

        private static void Glint(Color[] px, int n, float gx, float gy, float rad)
        {
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = x + 0.5f - gx, dy = y + 0.5f - gy;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d / rad);
                    Over(ref px[y * n + x], new Color(1f, 1f, 1f, 0.75f), a * a);
                }
            }
        }

        /// <summary>
        /// One shape, five layers: shadow, outline, gold rim, seam, body, gloss. <paramref name="sdf"/>
        /// is positive inside, measured from the shape's centre (cx, cy).
        /// </summary>
        private static Color[] Render(int w, int h, Func<float, float, float> sdf, float cx, float cy,
            float yBottom, float yTop, Look look, float u, bool pressed)
        {
            var px = new Color[w * h];
            float ow = look.outline * u, rimW = look.rim * u, seamW = 2.2f * u, blur = 5f * u;
            float shadowOff = (pressed ? 2f : 5f) * u;
            float shadowAlpha = pressed ? 0.25f : 0.42f;
            float span = Mathf.Max(1f, yTop - yBottom);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float fx = x + 0.5f - cx, fy = y + 0.5f - cy;
                    float d = sdf(fx, fy);
                    float t = Mathf.Clamp01((y + 0.5f - yBottom) / span);

                    var c = new Color(0f, 0f, 0f, 0f);

                    float sd = sdf(fx, fy + shadowOff);
                    Over(ref c, new Color(0.04f, 0.02f, 0.01f, shadowAlpha),
                        Mathf.Clamp01((sd + blur) / (2f * blur)));

                    Over(ref c, Outline, Cov(d + ow));
                    Over(ref c, Color.Lerp(look.rimBottom, look.rimTop, t), Cov(d));
                    Over(ref c, Seam, Cov(d - rimW + seamW));

                    var body = Color.Lerp(look.bodyBottom, look.bodyTop, t);
                    if (pressed) body = new Color(body.r * 0.86f, body.g * 0.86f, body.b * 0.86f, body.a);
                    float bodyCover = Cov(d - rimW);
                    c = Color.Lerp(c, body, bodyCover);

                    // Gloss: a wash over the upper half, kept off the rim.
                    float wash = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.5f) / 0.5f));
                    Over(ref c, new Color(1f, 1f, 1f, 1f), look.gloss * wash * 0.8f * Cov(d - rimW - 3f * u));

                    px[y * w + x] = c;
                }
            }
            return px;
        }

        private static Texture2D ToTexture(int w, int h, Color[] px, string name)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name };
            tex.SetPixels(px);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }
}
