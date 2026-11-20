using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// The pictures of the latte-art minigame (<see cref="LatteMinigame"/>), painted once from signed distance shapes into
    /// small transparent textures: the cup seen from above, the three foam patterns (heart, leaf, smiley), the milk jug
    /// tipped to pour. Same look as the bouquet and haircut art (dark outline, gradient body, soft highlight). Every picture is
    /// square and has the cup's middle at <see cref="CupCx"/> of its width, so the minigame can place any of them by
    /// the cup rather than by the picture. Cached as sprites; the first call paints it.
    /// </summary>
    public static class LatteArt
    {
        public enum Kind { Heart, Leaf, Smiley }

        public static readonly string[] Names = { "HEART", "LEAF", "SMILEY" };

        /// <summary>Where the cup's middle is, as a share of the picture's width, and its radius as a share of the width.</summary>
        public const float CupCx = 0.45f, CupR = 0.31f;

        private static readonly Color Ink = new Color(0.15f, 0.08f, 0.04f);
        private static readonly Color Foam = new Color(1f, 0.97f, 0.88f);
        private static readonly Color FoamEdge = new Color(0.93f, 0.80f, 0.60f);

        /// <summary>Bump after changing any picture below or the list in <see cref="BakeAll"/>: the scene build re-bakes (<c>LatteBake</c>).</summary>
        public const string ArtVersion = "1";
        public const string ArtFolder = "Latte";

        /// <summary>Set by the editor's bake: each picture is handed to it (key, texture, anchor) instead of being loaded.</summary>
        public static System.Action<string, Texture2D, Vector2> BakeSink;

        private static readonly System.Collections.Generic.Dictionary<string, Vector2> Anchors = new System.Collections.Generic.Dictionary<string, Vector2>();
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> Art = new System.Collections.Generic.Dictionary<string, Sprite>();
        private static BouquetMinigame.BakedIndex _index;
        private static readonly System.Collections.Generic.HashSet<string> Baked = new System.Collections.Generic.HashSet<string>();
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

        /// <summary>The baked picture (ASTC under WebGL, loaded in a blink); painted now, slowly, only if the bake is missing.</summary>
        private static Sprite Cached(string key, System.Func<Texture2D> draw)
        {
            if (BakeSink != null)
            {
                BakeSink(key, draw(), Anchors.TryGetValue(key, out var a) ? a : Vector2.zero);
                return null;
            }

            if (Art.TryGetValue(key, out var hit) && hit != null) return hit;

            LoadIndex();
            Texture2D tex = Baked.Contains(key) ? Resources.Load<Texture2D>(ArtFolder + "/" + key) : null;
            if (tex == null)
            {
                if (!_warned) { _warned = true; Debug.LogWarning($"[Latte] art '{key}' is not baked; painting it now (slow)."); }
                tex = draw();
                tex.hideFlags = HideFlags.DontSave;
            }
            var sprite = BouquetArt.ToSprite(tex);
            Art[key] = sprite;
            return sprite;
        }

        public static Sprite Cup() => Cached("cup", () => Paint(640, CupPixel));
        public static Sprite Pattern(Kind kind) => Cached("pat_" + kind, () => Paint(640, (x, y, n) => PatternPixel(kind, x, y, n)));
        public static Sprite Jug() => Cached("jug", () => Paint(420, JugPixel));

        // The shared bouquet pieces the minigame uses, baked with the rest so nothing is painted on the phone.
        public static Sprite Slot() => Cached("slot210", () => BouquetArt.Slot(210, BouquetArt.SlotState.Filled));
        public static Sprite Star(bool lit) => Cached(lit ? "starOn" : "starOff", () => BouquetArt.Star(180, lit));
        public static Sprite Check() => Cached("check64", () => BouquetArt.CheckBadge(64));
        public static Sprite Ring() => Cached("ring110", () => BouquetArt.PressRing(110, new Color(1f, 1f, 1f, 0.9f)));
        public static Sprite RaysSmall() => Cached("rays600", () => BouquetArt.Rays(600, 14, new Color(1f, 0.90f, 0.45f, 0.12f)));
        public static Sprite RaysBig() => Cached("rays820", () => BouquetArt.Rays(820, 18, new Color(1f, 0.90f, 0.45f, 0.75f)));
        public static Sprite Spark(int px) => Cached("spark" + px, () => BouquetArt.Sparkle(px, new Color(1f, 0.96f, 0.62f)));

        public static Sprite Hand(float angle, out Vector2 tip)
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

        /// <summary>Paints every picture of the minigame into <see cref="BakeSink"/> (the editor's <c>LatteBake</c>).</summary>
        public static void BakeAll()
        {
            Cup(); Jug();
            foreach (Kind k in System.Enum.GetValues(typeof(Kind))) Pattern(k);
            Slot(); Star(true); Star(false); Check(); Ring(); RaysSmall(); RaysBig();
            foreach (int px in new[] { 64, 44, 52, 36 }) Spark(px);
            Hand(16f, out _); Hand(14f, out _);
        }

        // ------------------------------------------------------------------ painting

        private delegate Color Pixel(float x, float y, int n);

        /// <summary>Paints an n x n texture; (x, y) are pixels from the top-left, so shapes read like the mock-ups.</summary>
        private static Texture2D Paint(int n, Pixel pixel)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    px[(n - 1 - j) * n + i] = pixel(i + 0.5f, j + 0.5f, n);
            tex.SetPixels(px);
            tex.Apply(false, false);
            return tex;
        }

        private static float Circle(float x, float y, float cx, float cy, float r) =>
            r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

        private static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, ex = bx - ax, ey = by - ay;
            float h = Mathf.Clamp01((px * ex + py * ey) / Mathf.Max(1e-5f, ex * ex + ey * ey));
            float dx = px - ex * h, dy = py - ey * h;
            return r - Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Ring(float x, float y, float cx, float cy, float outer, float inner) =>
            Mathf.Min(Circle(x, y, cx, cy, outer), -Circle(x, y, cx, cy, inner));

        private static float EdgeD(float ax, float ay, float bx, float by, float px, float py, float cx, float cy)
        {
            float len = Mathf.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            float d = ((bx - ax) * (py - ay) - (by - ay) * (px - ax)) / len;
            float dc = ((bx - ax) * (cy - ay) - (by - ay) * (cx - ax)) / len;
            return dc >= 0f ? d : -d;
        }

        /// <summary>A heart of "radius" r centred at (cx, cy): two round lobes and a point.</summary>
        public static float HeartSdf(float x, float y, float cx, float cy, float r)
        {
            float u = (x - cx) / r, v = (y - cy) / r;
            float a = 0.31f - Mathf.Sqrt((u + 0.27f) * (u + 0.27f) + (v + 0.13f) * (v + 0.13f));
            float b = 0.31f - Mathf.Sqrt((u - 0.27f) * (u - 0.27f) + (v + 0.13f) * (v + 0.13f));
            float t = Mathf.Min(EdgeD(-0.56f, 0f, 0.56f, 0f, u, v, 0f, 0.3f),
                Mathf.Min(EdgeD(0.56f, 0f, 0f, 0.66f, u, v, 0f, 0.3f), EdgeD(0f, 0.66f, -0.56f, 0f, u, v, 0f, 0.3f)));
            return Mathf.Max(Mathf.Max(a, b), t) * r;
        }

        private static Color Over(Color dst, Color src, float cover)
        {
            float a = Mathf.Clamp01(cover) * src.a;
            if (a <= 0f) return dst;
            float oa = a + dst.a * (1f - a);
            return new Color((src.r * a + dst.r * dst.a * (1f - a)) / oa, (src.g * a + dst.g * dst.a * (1f - a)) / oa,
                (src.b * a + dst.b * dst.a * (1f - a)) / oa, oa);
        }

        /// <summary>A dark outline of this width round a shape whose distance is <paramref name="d"/>, then its fill.</summary>
        private static Color Shape(Color dst, float d, Color top, Color bottom, float t, float outline)
        {
            if (outline > 0f) dst = Over(dst, Ink, d + outline + 0.5f);
            return Over(dst, Color.Lerp(bottom, top, t), d + 0.5f);
        }

        // ------------------------------------------------------------------ the cup from above

        private static Color CupPixel(float x, float y, int n)
        {
            float cx = n * CupCx, cy = n * 0.5f, r = n * CupR;
            var c = new Color(0f, 0f, 0f, 0f);
            float t = Mathf.Clamp01(1f - (y - (cy - r * 1.3f)) / (r * 2.6f));

            // The shadow, the saucer (gold), the handle, the cup (white), the coffee.
            c = Over(c, new Color(0f, 0f, 0f, 0.28f), Circle(x, y, cx + r * 0.08f, cy + r * 0.16f, r * 1.36f) * 0.04f + 0.5f);
            c = Shape(c, Circle(x, y, cx, cy, r * 1.30f), new Color(1f, 0.92f, 0.60f), new Color(0.80f, 0.55f, 0.16f), t, 5f);
            c = Over(c, new Color(0.93f, 0.66f, 0.24f, 0.6f), Circle(x, y, cx, cy, r * 1.14f) + 0.5f);
            c = Shape(c, Ring(x, y, cx + r * 1.12f, cy, r * 0.34f, r * 0.18f), new Color(1f, 1f, 0.98f), new Color(0.80f, 0.84f, 0.92f), t, 4f);
            c = Shape(c, Circle(x, y, cx, cy, r), new Color(1f, 1f, 0.99f), new Color(0.80f, 0.84f, 0.93f), t, 5f);
            c = Over(c, new Color(0.40f, 0.21f, 0.09f), Circle(x, y, cx, cy, r * 0.90f) + 0.5f);
            c = Over(c, Color.Lerp(new Color(0.58f, 0.32f, 0.14f), new Color(0.80f, 0.54f, 0.30f), t), Circle(x, y, cx, cy, r * 0.78f) + 0.5f);
            c = Over(c, new Color(1f, 1f, 1f, 0.35f), Capsule(x, y, cx - r * 0.62f, cy - r * 0.50f, cx - r * 0.42f, cy - r * 0.70f, r * 0.03f) + 0.5f);
            return c;
        }

        // ------------------------------------------------------------------ the patterns, in milk foam on the coffee

        private static Color PatternPixel(Kind kind, float x, float y, int n)
        {
            float cx = n * CupCx, cy = n * 0.5f, r = n * CupR;
            var c = new Color(0f, 0f, 0f, 0f);
            float t = Mathf.Clamp01(1f - (y - (cy - r)) / (r * 2f));
            switch (kind)
            {
                case Kind.Heart:
                    return Over(c, Color.Lerp(FoamEdge, Foam, t), HeartSdf(x, y, cx, cy - r * 0.05f, r * 0.78f) + 0.5f);

                case Kind.Leaf:
                {
                    float d = Capsule(x, y, cx, cy - r * 0.62f, cx, cy + r * 0.60f, r * 0.045f);
                    for (int i = 0; i < 5; i++)
                    {
                        float yy = cy + r * 0.50f - i * r * 0.26f, k = 0.26f + 0.12f * Mathf.Sin((i + 0.5f) / 5f * Mathf.PI);
                        d = Mathf.Max(d, Capsule(x, y, cx, yy, cx - r * k * 1.55f, yy - r * 0.20f, r * 0.075f));
                        d = Mathf.Max(d, Capsule(x, y, cx, yy, cx + r * k * 1.55f, yy - r * 0.20f, r * 0.075f));
                    }
                    return Over(c, Color.Lerp(FoamEdge, Foam, t), d + 0.5f);
                }

                default:
                {
                    float d = Circle(x, y, cx, cy, r * 0.62f);
                    c = Over(c, Color.Lerp(FoamEdge, Foam, t), d + 0.5f);
                    var brown = new Color(0.40f, 0.21f, 0.09f);
                    float f = Mathf.Max(Circle(x, y, cx - r * 0.22f, cy - r * 0.12f, r * 0.075f), Circle(x, y, cx + r * 0.22f, cy - r * 0.12f, r * 0.075f));
                    for (int i = 0; i < 14; i++)
                    {
                        float a0 = Mathf.Lerp(28f, 152f, i / 14f) * Mathf.Deg2Rad, a1 = Mathf.Lerp(28f, 152f, (i + 1) / 14f) * Mathf.Deg2Rad;
                        f = Mathf.Max(f, Capsule(x, y, cx + Mathf.Cos(a0) * r * 0.36f, cy + Mathf.Sin(a0) * r * 0.36f - r * 0.02f,
                            cx + Mathf.Cos(a1) * r * 0.36f, cy + Mathf.Sin(a1) * r * 0.36f - r * 0.02f, r * 0.045f));
                    }
                    return Over(c, brown, f + 0.5f);
                }
            }
        }

        // ------------------------------------------------------------------ the jug

        /// <summary>The milk jug tipped to pour to the LEFT (the spout is the lower-left end): the picture's middle is the jug's.</summary>
        private static Color JugPixel(float x, float y, int n)
        {
            float mid = n * 0.5f, s = n / 420f;
            float lx = (x - mid) / s, ly = (y - mid) / s;
            // Rotate the point back by the tilt (-50 degrees), so the shapes are drawn upright.
            const float a = 50f * Mathf.Deg2Rad;
            float ux = lx * Mathf.Cos(a) - ly * Mathf.Sin(a), uy = lx * Mathf.Sin(a) + ly * Mathf.Cos(a);
            var c = new Color(0f, 0f, 0f, 0f);
            var top = new Color(0.96f, 0.97f, 1f); var bottom = new Color(0.60f, 0.66f, 0.76f);
            float t = Mathf.Clamp01(0.5f - ly / 360f);

            c = Shape(c, Ring(ux, uy, 105f, 5f, 54f, 30f) * s, top, bottom, t, 4f * s);                       // the handle
            c = Shape(c, Capsule(ux, uy, -48f, -40f, -92f, -96f, 26f) * s, top, bottom, t, 4f * s);           // the spout
            c = Shape(c, Capsule(ux, uy, 0f, -70f, 0f, 90f, 74f) * s, top, bottom, t, 4f * s);                // the body
            c = Over(c, new Color(1f, 1f, 1f, 0.7f), Capsule(ux, uy, -38f, -20f, -38f, 50f, 6f) * s + 0.5f);   // a glint
            c = Shape(c, Circle(ux, uy, 0f, 30f, 22f) * s, new Color(0.45f, 0.70f, 0.98f), new Color(0.20f, 0.42f, 0.86f), 0.5f, 3f * s);
            return c;
        }
    }
}
