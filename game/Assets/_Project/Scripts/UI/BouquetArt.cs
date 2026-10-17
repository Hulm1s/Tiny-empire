using UnityEngine;
using Painter = Tycoon.UI.IconFactory.Painter;
using Sd = Tycoon.UI.IconFactory.Sd;

namespace Tycoon.UI
{
    /// <summary>
    /// The 2D art of the florist's bouquet minigame, drawn in code with the same painter and the
    /// same finish as the product icons (<see cref="IconFactory"/>): a thick warm-dark outline, an
    /// "inflated" glossy body lit from the upper left, flat details clipped inside it.
    ///
    /// The four flowers are the icons' own blooms (<c>IconFactory.RoseBloom</c> and friends), only
    /// bigger and on longer stems, so the rose the player trims is the rose on the shop shelf.
    ///
    /// Everything here returns a plain Texture2D, y up like any texture; turn one into a sprite
    /// with <see cref="ToSprite"/>. Nothing is cached - callers build what they need once.
    /// Sizes are in output pixels. This is art only: no minigame logic lives here.
    ///
    /// <c>Tycoon > Export Bouquet Mock</c> (Editor/BouquetMock.cs) lays it all out as the five
    /// screens of the minigame for review.
    /// </summary>
    public static class BouquetArt
    {
        public enum Flower { Rose, Tulip, Sunflower, Hydrangea }

        /// <summary>How far the ribbon has got round the stems.</summary>
        public enum Ribbon { None, Wrapping, Bow }

        /// <summary>How far the paper has got round the bouquet.</summary>
        public enum Wrap { None, HalfFolded, Full }

        /// <summary>Supersampling for everything here (averaged down to the output size).</summary>
        private const int SS = 3;

        private static readonly Color Ink = new Color(0.17f, 0.09f, 0.05f);
        private static readonly Color RibbonLit = new Color(0.72f, 0.54f, 1f);
        private static readonly Color RibbonShade = new Color(0.38f, 0.18f, 0.74f);
        private static readonly Color PaperLit = new Color(1f, 0.93f, 0.78f);
        private static readonly Color PaperShade = new Color(0.84f, 0.62f, 0.38f);
        private static readonly Color PinkLit = new Color(1f, 0.70f, 0.80f);
        private static readonly Color PinkShade = new Color(0.84f, 0.28f, 0.50f);
        private static readonly Color CutLit = new Color(0.93f, 1f, 0.70f);
        private static readonly Color CutShade = new Color(0.66f, 0.84f, 0.38f);
        private static readonly Color GloveLit = Color.white;
        private static readonly Color GloveShade = new Color(0.66f, 0.73f, 0.86f);

        public static string Id(Flower f)
        {
            switch (f)
            {
                case Flower.Rose: return "rose";
                case Flower.Tulip: return "tulip";
                case Flower.Sunflower: return "sunflower";
                default: return "hydrangea";
            }
        }

        public static Sprite ToSprite(Texture2D tex) =>
            Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

        // ------------------------------------------------------------------ products

        /// <summary>
        /// A florist product's icon (a flower, or the bouquet) drawn big. The texture is a whole
        /// multiple of the icon's 96 px at or above <paramref name="px"/>; draw it at the size wanted.
        /// </summary>
        public static Texture2D Product(string id, int px)
        {
            int keep = Mathf.Max(1, Mathf.CeilToInt(px / 96f));
            var p = new Painter(96, keep * SS);
            IconFactory.DrawFlorist(p, id);
            return p.ToTexture("Bouquet_" + id, keep);
        }

        // ------------------------------------------------------------------ flowers

        /// <summary>Half the size of a bloom at scale 1, with its outline: its window.</summary>
        private static float BloomReach(Flower f)
        {
            switch (f)
            {
                case Flower.Rose: return 34f;
                case Flower.Tulip: return 36f;
                case Flower.Sunflower: return 40f;
                default: return 36f;
            }
        }

        /// <summary>Paints one bloom at (cx, cy), scale k, confined to its own window (fast).</summary>
        private static void Bloom(Painter p, Flower f, float cx, float cy, float k)
        {
            float r = BloomReach(f) * k;
            p.SetWindow(cx - r, cy - r, cx + r, cy + r);
            switch (f)
            {
                case Flower.Rose: IconFactory.RoseBloom(p, cx, cy, k); break;
                case Flower.Tulip: IconFactory.TulipBloom(p, cx, cy, k); break;
                case Flower.Sunflower: IconFactory.SunflowerBloom(p, cx, cy, k); break;
                default: IconFactory.HydrangeaBloom(p, cx, cy, k); break;
            }
            p.ResetWindow();
        }

        /// <summary>A glossy stem along a polyline; one outline round the whole of it.</summary>
        private static void Stem(Painter p, Vector2[] pts, float r, float k, float flatBottom = float.NaN)
        {
            Painter.Shape shape = (x, y) =>
            {
                float d = float.MinValue;
                for (int i = 0; i < pts.Length - 1; i++)
                    d = Mathf.Max(d, Sd.Capsule(x, y, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, r));
                // A trimmed stem ends square across, not rounded.
                if (!float.IsNaN(flatBottom)) d = Mathf.Min(d, y - flatBottom);
                return d;
            };
            Window(p, pts, r + 3f * k + 2f);
            p.Blob(shape, IconFactory.StemLit, IconFactory.StemShade, outline: 2.2f * k, inflate: r * 0.9f, spec: 0.25f);
            // A pale streak down the lit side.
            Painter.Shape streak = (x, y) =>
            {
                float d = float.MinValue;
                for (int i = 0; i < pts.Length - 1; i++)
                    d = Mathf.Max(d, Sd.Capsule(x, y, pts[i].x - r * 0.38f, pts[i].y, pts[i + 1].x - r * 0.38f, pts[i + 1].y, r * 0.22f));
                return d;
            };
            p.Fill(streak, new Color(1f, 1f, 1f, 0.35f), shape, r * 0.2f);
            p.ResetWindow();
        }

        private static void Window(Painter p, Vector2[] pts, float pad)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var q in pts)
            {
                x0 = Mathf.Min(x0, q.x); y0 = Mathf.Min(y0, q.y);
                x1 = Mathf.Max(x1, q.x); y1 = Mathf.Max(y1, q.y);
            }
            p.SetWindow(x0 - pad, y0 - pad, x1 + pad, y1 + pad);
        }

        private static void Leaf(Painter p, float bx, float by, float angle, float len, float wid, float k)
        {
            float r = len + wid;
            p.SetWindow(bx - r, by - r, bx + r, by + r);
            IconFactory.IconLeaf(p, bx, by, angle, len, wid, k);
            p.ResetWindow();
        }

        /// <summary>The fresh pale face of a cut stem, seen slightly from above.</summary>
        private static void CutFace(Painter p, float cx, float cy, float r, float k, float angle = 0f)
        {
            p.SetWindow(cx - r * 1.6f, cy - r * 1.6f, cx + r * 1.6f, cy + r * 1.6f);
            Painter.Shape face = (x, y) => Sd.Ellipse(x, y, cx, cy, r, r * 0.42f, angle);
            p.Blob(face, CutLit, CutShade, outline: 1.8f * k, inflate: r * 0.4f, spec: 0.5f);
            p.Fill((x, y) => Sd.Ellipse(x, y, cx, cy, r * 0.42f, r * 0.17f, angle), new Color(0.55f, 0.75f, 0.28f, 0.7f), face, 0.6f * k);
            p.ResetWindow();
        }

        /// <summary>
        /// One long flower for the trimming step: its bloom on top of a stem
        /// <paramref name="stemPx"/> long with two leaves. Untrimmed, the stem's last stretch is dry
        /// and brown; trimmed, it ends square with a fresh pale cut. <paramref name="k"/> is the
        /// bloom's scale against its icon (2 is about 130 px across). <paramref name="head"/> is the
        /// bloom's centre in the texture (y up).
        /// </summary>
        public static Texture2D StemFlower(Flower f, float k, int stemPx, bool trimmed, out Vector2 head)
        {
            int w = Mathf.CeilToInt(BloomReach(f) * 2.3f * k);
            int h = stemPx + Mathf.CeilToInt(BloomReach(f) * k) + 16;
            var p = new Painter(w, h, SS);
            float cx = w * 0.5f, bottom = 12f;
            var hd = new Vector2(cx, h - BloomReach(f) * k - 4f);
            head = hd;
            float r = 4.2f * k;

            var stem = new[] { new Vector2(cx, bottom), new Vector2(cx, head.y) };
            Stem(p, stem, r, k, trimmed ? bottom : float.NaN);
            if (!trimmed)
            {
                // The old, dried end that wants cutting off: brown fading up into green.
                float dry = Mathf.Min(170f, stemPx * 0.26f);
                p.SetWindow(cx - r * 2f, 0f, cx + r * 2f, bottom + dry + 4f);
                Painter.Shape s = (x, y) => Sd.Capsule(x, y, cx, bottom, cx, hd.y, r);
                for (int i = 0; i < 8; i++)
                {
                    float top = bottom + dry * (1f - i / 8f);
                    p.Fill((x, y) => Mathf.Min(s(x, y), top - y), new Color(0.46f, 0.30f, 0.10f, 0.22f), null, 0.6f);
                }
                // Dry streaks and a split at the very end.
                p.Fill((x, y) => Sd.Capsule(x, y, cx + r * 0.25f, bottom + 2f, cx - r * 0.05f, bottom + dry * 0.6f, 0.55f * k),
                    new Color(0.28f, 0.16f, 0.05f, 0.7f), s, 0.4f);
                p.Fill((x, y) => Sd.Capsule(x, y, cx - r * 0.45f, bottom + 2f, cx - r * 0.35f, bottom + dry * 0.3f, 0.45f * k),
                    new Color(0.28f, 0.16f, 0.05f, 0.6f), s, 0.4f);
                p.ResetWindow();
            }
            else CutFace(p, cx, bottom, r, k);

            // Foliage as on the icon: a pair of leaves under the bloom, one more down the stem.
            float under = head.y - BloomReach(f) * k * 0.80f;
            Leaf(p, cx + 1f, Mathf.Max(bottom + 40f, head.y - stemPx * 0.45f), -48f, 38f * k, 12f * k, k);
            Leaf(p, cx - 1f, under - 4f * k, 66f, 30f * k, 10f * k, k);
            Leaf(p, cx + 1f, under, -64f, 28f * k, 9.5f * k, k);
            Bloom(p, f, head.x, head.y, k);
            return p.ToTexture("BouquetStem_" + Id(f));
        }

        /// <summary>
        /// A trimmed-off stem end lying at <paramref name="angle"/> degrees (0 = level): fresh cut
        /// at one end, dry and brown at the other. Square texture.
        /// </summary>
        public static Texture2D Offcut(float k, float length, float angle)
        {
            int n = Mathf.CeilToInt(length + 14f * k);
            var p = new Painter(n, n, SS);
            float c = n * 0.5f, r = 4.2f * k, a = angle * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var p0 = new Vector2(c, c) - dir * (length * 0.5f);
            var p1 = new Vector2(c, c) + dir * (length * 0.5f);
            Painter.Shape s = (x, y) => Mathf.Min(Sd.Capsule(x, y, p0.x, p0.y, p1.x, p1.y, r),
                // square across the cut end
                (x - p0.x) * dir.x + (y - p0.y) * dir.y);
            p.Blob(s, IconFactory.StemLit, IconFactory.StemShade, outline: 2.2f * k, inflate: r * 0.9f, spec: 0.25f);
            for (int i = 0; i < 5; i++)
            {
                float from = length * (0.45f + i * 0.11f);
                p.Fill((x, y) => Mathf.Min(s(x, y), (x - p0.x) * dir.x + (y - p0.y) * dir.y - from),
                    new Color(0.50f, 0.34f, 0.14f, 0.2f), null, 0.6f);
            }
            CutFace(p, p0.x, p0.y, r, k, angle + 90f);
            return p.ToTexture("BouquetOffcut");
        }

        // ------------------------------------------------------------------ the bunch

        /// <summary>
        /// Where the bunch is drawn. All bunch geometry is authored in a 600 x 900 frame (y up,
        /// x centred on 300); <see cref="Map"/> puts a point of it on the texture.
        /// </summary>
        public struct BunchLayout
        {
            public int width, height;
            /// <summary>Texture pixels per frame unit.</summary>
            public float scale;
            /// <summary>Texture x of the frame's centre line, and texture y of the frame's 0.</summary>
            public float centreX, baseY;

            public Vector2 Map(float bx, float by) => new Vector2(centreX + (bx - 300f) * scale, baseY + by * scale);

            /// <summary>The tie point (where the stems cross and the ribbon goes), in the texture.</summary>
            public Vector2 Tie => Map(300f, TieY);

            public static BunchLayout Fit(int width, int height, float scale, float baseY = 0f) =>
                new BunchLayout { width = width, height = height, scale = scale, centreX = width * 0.5f, baseY = baseY };
        }

        private const float TieY = 430f;

        // The six heads, back to front: (x, y) in the frame. Packed so neighbours just touch;
        // the greenery behind fills what is left.
        private static readonly Vector2[] HeadAt =
        {
            new Vector2(300f, 790f), new Vector2(196f, 742f), new Vector2(404f, 742f),
            new Vector2(132f, 628f), new Vector2(468f, 628f), new Vector2(300f, 662f),
        };

        /// <summary>The usual order: three roses, two tulips, one hydrangea in front.</summary>
        public static readonly Flower[] SampleOrder =
            { Flower.Rose, Flower.Rose, Flower.Rose, Flower.Tulip, Flower.Tulip, Flower.Hydrangea };

        /// <summary>
        /// The gathered bouquet: up to six blooms (back to front, see <see cref="SampleOrder"/>)
        /// over stems that cross at the tie point, with the ribbon and the paper as far along as
        /// asked. <paramref name="tailEnd"/> (texture px) is where the loose ribbon end is pulled
        /// to while it is still <see cref="Ribbon.Wrapping"/>.
        ///
        /// Drawn as its parts in order (<see cref="DrawPart"/>, <see cref="DrawStem"/>, <see cref="DrawBloom"/>);
        /// the same parts can be drawn one per texture (<see cref="BunchPart"/>, <see cref="BunchStem"/>,
        /// <see cref="BunchBloom"/>) and stacked - the painter blends source-over, so the stack is the same picture.
        /// </summary>
        /// <param name="supersample">Samples per pixel along each axis (default 3, the approved finish).</param>
        public static Texture2D Bunch(BunchLayout L, Flower[] flowers, Ribbon ribbon, Wrap wrap, Vector2 tailEnd = default,
            int supersample = SS)
        {
            var p = new Painter(L.width, L.height, Mathf.Clamp(supersample, 1, SS));
            int n = Mathf.Min(6, flowers.Length);
            bool wrapped = wrap != Wrap.None;

            if (wrapped) DrawPart(p, L, Part.PaperBack, tailEnd);
            DrawPart(p, L, Part.Leaves, tailEnd);
            for (int i = 0; i < n; i++) DrawStem(p, L, n, i, wrapped);
            for (int i = 0; i < n; i++) DrawBloom(p, L, flowers[i], i);
            if (wrap == Wrap.Full) DrawPart(p, L, Part.PaperFull, tailEnd);
            else if (wrap == Wrap.HalfFolded) DrawPart(p, L, Part.PaperHalf, tailEnd);
            if (ribbon == Ribbon.Wrapping) DrawPart(p, L, Part.RibbonWrapping, tailEnd);
            else if (ribbon == Ribbon.Bow) DrawPart(p, L, Part.RibbonBow, tailEnd);
            return p.ToTexture("BouquetBunch");
        }

        /// <summary>The parts of the bunch that do not depend on which flowers are in it.</summary>
        public enum Part { PaperBack, Leaves, PaperFull, PaperHalf, RibbonWrapping, RibbonBow }

        /// <summary>One flower-independent part of the bunch, alone on a transparent canvas the size of the layout.</summary>
        public static Texture2D BunchPart(BunchLayout L, Part part, Vector2 tailEnd = default)
        {
            var p = new Painter(L.width, L.height, SS);
            DrawPart(p, L, part, tailEnd);
            return p.ToTexture("BouquetPart_" + part);
        }

        /// <summary>Stem <paramref name="i"/> of a bunch of <paramref name="n"/>, alone (cut square at the foot unless wrapped).</summary>
        public static Texture2D BunchStem(BunchLayout L, int n, int i, bool wrapped)
        {
            var p = new Painter(L.width, L.height, SS);
            DrawStem(p, L, n, i, wrapped);
            return p.ToTexture("BouquetStemPart");
        }

        /// <summary>A bloom at head <paramref name="i"/>, alone.</summary>
        public static Texture2D BunchBloom(BunchLayout L, Flower f, int i)
        {
            var p = new Painter(L.width, L.height, SS);
            DrawBloom(p, L, f, i);
            return p.ToTexture("BouquetBloomPart");
        }

        /// <summary>Where head <paramref name="i"/> sits on the layout's texture (y up).</summary>
        public static Vector2 HeadPosition(BunchLayout L, int i) => L.Map(HeadAt[i].x, HeadAt[i].y);

        private static void DrawPart(Painter p, BunchLayout L, Part part, Vector2 tailEnd)
        {
            float s = L.scale;
            Vector2 M(float x, float y) => L.Map(x, y);
            var tie = M(300f, TieY);
            switch (part)
            {
                case Part.PaperBack:
                {
                    // Paper behind: a sheet rising behind the blooms to two points.
                    var back = new[] { M(300f, 40f), M(30f, 660f), M(176f, 616f), M(300f, 700f), M(424f, 616f), M(570f, 660f) };
                    Window(p, back, 8f * s);
                    Painter.Shape sh = (x, y) => IconFactory.PolygonField(back, x, y) + 4f * s;
                    p.Blob(sh, PaperLit, PaperShade, outline: 3.4f * s, inflate: 60f * s, spec: 0.25f);
                    // The sheet's inside face, a shade darker towards the bottom of the cone.
                    p.Fill((x, y) => Mathf.Min(sh(x, y), M(0f, 520f).y - y), new Color(0.70f, 0.46f, 0.24f, 0.22f), sh, 60f * s);
                    p.ResetWindow();
                    break;
                }
                case Part.Leaves:
                {
                    // Greenery fanning out behind the heads.
                    float lk = s * 1.5f;
                    Leaf(p, M(286f, 520f).x, M(0f, 520f).y, 64f, 236f * s, 48f * s, lk);
                    Leaf(p, M(314f, 520f).x, M(0f, 520f).y, -64f, 236f * s, 48f * s, lk);
                    Leaf(p, M(292f, 560f).x, M(0f, 560f).y, 34f, 262f * s, 46f * s, lk);
                    Leaf(p, M(308f, 560f).x, M(0f, 560f).y, -34f, 262f * s, 46f * s, lk);
                    Leaf(p, M(296f, 600f).x, M(0f, 600f).y, 12f, 250f * s, 40f * s, lk);
                    Leaf(p, M(304f, 600f).x, M(0f, 600f).y, -12f, 250f * s, 40f * s, lk);
                    break;
                }
                case Part.PaperFull:
                    // Paper in front: the cone, its top edge dipping in the middle where the two sides
                    // meet, with a pink cuff turned over along it.
                    Sheet(p, s, new[] { M(300f, 30f), M(96f, 420f), M(300f, 372f), M(504f, 420f) }, PaperLit, PaperShade, true);
                    break;
                case Part.PaperHalf:
                    // The right side still lies open, pink inside up, beyond its fold line.
                    Sheet(p, s, new[] { M(300f, 30f), M(504f, 420f), M(780f, 352f), M(620f, 70f) }, PinkLit, PinkShade, false);
                    // The left side, folded over the stems.
                    Sheet(p, s, new[] { M(300f, 30f), M(96f, 420f), M(318f, 376f) }, PaperLit, PaperShade, true);
                    break;
                case Part.RibbonWrapping:
                    Tail(p, tie + new Vector2(36f * s, -6f * s), tailEnd, s);
                    Band(p, tie, s, -8f);
                    break;
                case Part.RibbonBow:
                    Band(p, tie, s, -6f);
                    Bow(p, tie + new Vector2(0f, 4f * s), s);
                    break;
            }
        }

        /// <summary>Stem i of n: from its head to the tie point, then down in the bundle.</summary>
        private static void DrawStem(Painter p, BunchLayout L, int n, int i, bool wrapped)
        {
            float s = L.scale;
            Vector2 M(float x, float y) => L.Map(x, y);
            var tie = M(300f, TieY);
            float sr = 6.6f * s;
            float lowY = wrapped ? 120f : 70f;
            float bx = 300f + (i - (n - 1) * 0.5f) * 11f;
            var pts = new[] { M(HeadAt[i].x, HeadAt[i].y - 10f), tie + new Vector2((bx - 300f) * s * 0.6f, 0f), M(bx, lowY) };
            Stem(p, pts, sr, s * 1.3f, M(0f, lowY).y);
            if (!wrapped) CutFace(p, pts[2].x, pts[2].y, sr, s * 1.3f);
        }

        private static void DrawBloom(Painter p, BunchLayout L, Flower f, int i)
        {
            var hp = L.Map(HeadAt[i].x, HeadAt[i].y);
            Bloom(p, f, hp.x, hp.y, 2.0f * L.scale);
        }

        /// <summary>One sheet of wrapping paper; <paramref name="cuff"/> turns a pink band over its top edge.</summary>
        private static void Sheet(Painter p, float s, Vector2[] poly, Color lit, Color shade, bool cuff)
        {
            Window(p, poly, 10f * s);
            Painter.Shape sh = (x, y) => IconFactory.PolygonField(poly, x, y) + 4f * s;
            p.Blob(sh, lit, shade, outline: 3.4f * s, inflate: 44f * s, spec: 0.3f);
            if (cuff)
            {
                // The band runs along the edges from the second point round to the last.
                var band = new System.Collections.Generic.List<Vector2>();
                for (int i = 1; i < poly.Length; i++) band.Add(poly[i]);
                var edge = band.ToArray();
                float w = 30f * s;
                Painter.Shape strip = (x, y) =>
                {
                    float d = float.MinValue;
                    for (int i = 0; i < edge.Length - 1; i++)
                        d = Mathf.Max(d, Sd.Capsule(x, y, edge[i].x, edge[i].y, edge[i + 1].x, edge[i + 1].y, w));
                    return Mathf.Min(d, sh(x, y) - 1.5f * s);
                };
                p.Blob(strip, PinkLit, PinkShade, outline: 2.4f * s, inflate: 14f * s, spec: 0.4f, clip: sh);
            }
            // A gloss streak down the left of the sheet.
            var a = poly[0];
            var b = poly[1];
            var g0 = Vector2.Lerp(a, b, 0.22f) + new Vector2(24f * s, 0f);
            var g1 = Vector2.Lerp(a, b, 0.72f) + new Vector2(24f * s, 0f);
            p.Fill((x, y) => Sd.Capsule(x, y, g0.x, g0.y, g1.x, g1.y, 6f * s), new Color(1f, 1f, 1f, 0.45f), sh, 4f * s);
            p.ResetWindow();
        }

        private static void Band(Painter p, Vector2 c, float s, float angle)
        {
            p.SetWindow(c.x - 70f * s, c.y - 40f * s, c.x + 70f * s, c.y + 40f * s);
            Painter.Shape band = (x, y) => Sd.Box(x, y, c.x, c.y, 42f * s, 15f * s, 7f * s, angle);
            p.Blob(band, RibbonLit, RibbonShade, outline: 2.8f * s, inflate: 10f * s, spec: 0.5f);
            // Satin sheen, and the darker edge where it turns round the back.
            p.Fill((x, y) => Sd.Box(x, y, c.x - 4f * s, c.y + 5f * s, 30f * s, 2.4f * s, 2f * s, angle), new Color(1f, 1f, 1f, 0.55f), band, 1.5f * s);
            p.Fill((x, y) => Mathf.Min(band(x, y), Mathf.Abs(x - c.x) - 32f * s), new Color(0.24f, 0.08f, 0.50f, 0.35f), band, 4f * s);
            p.ResetWindow();
        }

        /// <summary>The loose ribbon end: a satin strip that twists as it curves out to (to).</summary>
        private static void Tail(Painter p, Vector2 from, Vector2 to, float s)
        {
            const int segs = 24;
            var pts = new Vector2[segs + 1];
            var mid = new Vector2(Mathf.Lerp(from.x, to.x, 0.5f), Mathf.Min(from.y, to.y) - 40f * s);
            var rad = new float[segs + 1];
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                pts[i] = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * mid + t * t * to;
                // Wide where it faces us, narrow where it turns edge-on.
                rad[i] = s * (5f + 8f * Mathf.Abs(Mathf.Cos(t * Mathf.PI * 1.25f + 0.3f)));
            }
            Painter.Shape sh = (x, y) =>
            {
                float d = float.MinValue;
                for (int i = 0; i < segs; i++)
                    d = Mathf.Max(d, Sd.Capsule(x, y, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, (rad[i] + rad[i + 1]) * 0.5f));
                return d;
            };
            Window(p, pts, 16f * s);
            p.Blob(sh, RibbonLit, RibbonShade, outline: 2.6f * s, inflate: 9f * s, spec: 0.55f);
            p.ResetWindow();
        }

        private static void Bow(Painter p, Vector2 c, float s)
        {
            p.SetWindow(c.x - 130f * s, c.y - 130f * s, c.x + 130f * s, c.y + 90f * s);
            // Tails first, then the two loops, then the knot over them.
            foreach (float side in new[] { -1f, 1f })
            {
                var a = c + new Vector2(side * 6f * s, -6f * s);
                var b = c + new Vector2(side * 40f * s, -78f * s);
                var e = c + new Vector2(side * 30f * s, -104f * s);
                var f = c + new Vector2(side * 58f * s, -96f * s);
                // A strip widening to a notched (swallow-tail) end.
                Painter.Shape tail = (x, y) => Mathf.Max(Sd.Capsule(x, y, a.x, a.y, b.x, b.y, 11f * s),
                    Mathf.Max(Sd.Capsule(x, y, b.x, b.y, e.x, e.y, 9f * s), Sd.Capsule(x, y, b.x, b.y, f.x, f.y, 9f * s)));
                p.Blob(tail, RibbonLit, RibbonShade, outline: 2.6f * s, inflate: 9f * s, spec: 0.45f);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float lx = c.x + side * 52f * s, ly = c.y + 14f * s;
                Painter.Shape loop = (x, y) => Sd.Egg(x, y, lx, ly, 28f * s, 48f * s, 0.25f, side * -64f);
                p.Blob(loop, RibbonLit, RibbonShade, outline: 3f * s, inflate: 24f * s, spec: 0.55f);
                // The loop's open middle and a sheen along its top.
                p.Fill((x, y) => Sd.Ellipse(x, y, c.x + side * 48f * s, c.y + 12f * s, 20f * s, 7.5f * s, side * 26f),
                    new Color(0.24f, 0.08f, 0.50f, 0.8f), loop, 1.5f * s);
                p.Fill((x, y) => Sd.Ellipse(x, y, c.x + side * 60f * s, c.y + 34f * s, 18f * s, 4f * s, side * 22f),
                    new Color(1f, 1f, 1f, 0.5f), loop, 2f * s);
            }
            Painter.Shape knot = (x, y) => Sd.Box(x, y, c.x, c.y + 6f * s, 18f * s, 21f * s, 9f * s);
            p.Blob(knot, RibbonLit, RibbonShade, outline: 3f * s, inflate: 13f * s, spec: 0.6f);
            p.ResetWindow();
        }

        // ------------------------------------------------------------------ tools and hints

        /// <summary>Scissors pointing right, blades a little open. Square texture.</summary>
        public static Texture2D Scissors(int px)
        {
            var p = new Painter(px, px, SS);
            float u = px / 200f;
            Vector2 P(float x, float y) => new Vector2(x * u, y * u);
            var pivot = P(100f, 100f);
            var steelLit = new Color(0.97f, 0.98f, 1f);
            var steelShade = new Color(0.50f, 0.56f, 0.68f);
            var handleLit = new Color(1f, 0.52f, 0.42f);
            var handleShade = new Color(0.72f, 0.10f, 0.14f);

            foreach (float side in new[] { 1f, -1f })
            {
                // Blade: a long tapered wedge from the pivot out to the right.
                var tip = P(190f, 100f + side * 26f);
                var root = pivot;
                Painter.Shape blade = (x, y) =>
                {
                    float ex = tip.x - root.x, ey = tip.y - root.y;
                    float t = Mathf.Clamp01(((x - root.x) * ex + (y - root.y) * ey) / (ex * ex + ey * ey));
                    float w = Mathf.Lerp(19f, 4f, t) * u;
                    return Sd.Capsule(x, y, root.x, root.y, tip.x, tip.y, w);
                };
                p.Blob(blade, steelLit, steelShade, outline: 3f * u, inflate: 8f * u, spec: 0.8f);
                p.Fill((x, y) => Sd.Capsule(x, y, P(112f, 100f + side * 4f).x, P(0, 100f + side * 4f).y, tip.x - 10f * u, tip.y - side * 2f * u, 1.6f * u),
                    new Color(1f, 1f, 1f, 0.9f), blade, 0.6f * u);
            }
            foreach (float side in new[] { 1f, -1f })
            {
                // Handle: an arm from the pivot to a round finger loop on the left.
                var loopC = P(42f, 100f - side * 40f);
                Painter.Shape arm = (x, y) => Sd.Capsule(x, y, pivot.x, pivot.y, loopC.x + 18f * u, loopC.y + side * 8f * u, 10f * u);
                Painter.Shape ring = (x, y) => Mathf.Max(arm(x, y), Mathf.Min(Sd.Circle(x, y, loopC.x, loopC.y, 34f * u), -Sd.Circle(x, y, loopC.x, loopC.y, 17f * u)));
                p.Blob(ring, handleLit, handleShade, outline: 3.2f * u, inflate: 9f * u, spec: 0.5f);
            }
            Painter.Shape screw = (x, y) => Sd.Circle(x, y, pivot.x, pivot.y, 10f * u);
            p.Blob(screw, new Color(1f, 0.92f, 0.55f), new Color(0.80f, 0.52f, 0.12f), outline: 2.6f * u, inflate: 8f * u, spec: 0.7f);
            return p.ToTexture("BouquetScissors");
        }

        /// <summary>
        /// The tutorial hand: a white glove pointing with its index finger, tilted
        /// <paramref name="angle"/> degrees (0 = straight up, positive leans left).
        /// <paramref name="tip"/> is the fingertip in the texture (y up). Square texture.
        /// </summary>
        public static Texture2D Hand(int px, float angle, out Vector2 tip)
        {
            var p = new Painter(px, px, SS);
            float u = px / 260f;
            tip = new Vector2(110f * u, 236f * u);
            var t = tip;
            float sn = Mathf.Sin(angle * Mathf.Deg2Rad), cs = Mathf.Cos(angle * Mathf.Deg2Rad);
            // Hand space: fingertip at the origin, finger up, units of u.
            void L(float x, float y, out float lx, out float ly)
            {
                float ox = (x - t.x) / u, oy = (y - t.y) / u;
                lx = ox * cs + oy * sn;
                ly = -ox * sn + oy * cs;
            }
            float Glove(float lx, float ly)
            {
                float finger = Sd.Capsule(lx, ly, 0f, -14f, 2f, -88f, 15f);
                float palm = Sd.Box(lx, ly, 26f, -122f, 40f, 40f, 20f);
                float k1 = Sd.Circle(lx, ly, 30f, -88f, 15f);
                float k2 = Sd.Circle(lx, ly, 52f, -94f, 14f);
                float k3 = Sd.Circle(lx, ly, 66f, -110f, 13f);
                float thumb = Sd.Capsule(lx, ly, -6f, -132f, -30f, -100f, 14f);
                float body = Sd.Smooth(Mathf.Max(palm, Mathf.Max(k1, Mathf.Max(k2, k3))), finger, 6f);
                return Mathf.Max(body, thumb) * u;
            }
            Painter.Shape glove = (x, y) => { L(x, y, out float lx, out float ly); return Glove(lx, ly); };
            Painter.Shape cuff = (x, y) => { L(x, y, out float lx, out float ly); return Sd.Box(lx, ly, 28f, -172f, 44f, 15f, 8f, 0f) * u; };

            p.Blob(cuff, new Color(0.55f, 0.80f, 1f), new Color(0.16f, 0.38f, 0.74f), outline: 3.4f * u, inflate: 10f * u, spec: 0.5f);
            p.Blob(glove, GloveLit, GloveShade, outline: 3.6f * u, inflate: 26f * u, spec: 0.35f);
            // Creases between the curled fingers, and the nail of the pointing one.
            var crease = new Color(0.45f, 0.50f, 0.64f, 0.75f);
            p.Fill((x, y) => { L(x, y, out float lx, out float ly); return Sd.Capsule(lx, ly, 41f, -100f, 39f, -116f, 1.6f) * u; }, crease, glove, 0.8f * u);
            p.Fill((x, y) => { L(x, y, out float lx, out float ly); return Sd.Capsule(lx, ly, 60f, -106f, 56f, -122f, 1.6f) * u; }, crease, glove, 0.8f * u);
            p.Fill((x, y) => { L(x, y, out float lx, out float ly); return Sd.Capsule(lx, ly, 14f, -84f, 18f, -108f, 1.6f) * u; }, crease, glove, 0.8f * u);
            p.Fill((x, y) => { L(x, y, out float lx, out float ly); return Sd.Capsule(lx, ly, -7f, -22f, -6f, -60f, 3.2f) * u; }, new Color(1f, 1f, 1f, 0.9f), glove, 1f * u);
            p.GroundShadow(4f * u, 4f * u, 0.30f);
            return p.ToTexture("BouquetHand");
        }

        /// <summary>
        /// A gesture arrow along a quadratic curve from <paramref name="from"/> through
        /// <paramref name="bend"/> to <paramref name="to"/> (texture px, y up): one white, glossy,
        /// outlined shape - a shaft (solid, or in dashes) that swells from its tail, and a chunky
        /// head whose point is at <paramref name="to"/>.
        /// </summary>
        public static Texture2D Arrow(int w, int h, Vector2 from, Vector2 bend, Vector2 to, float width, bool dashed = false)
        {
            const int segs = 48;
            var pts = new Vector2[segs + 1];
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                pts[i] = (1 - t) * (1 - t) * from + 2 * (1 - t) * t * bend + t * t * to;
            }
            return ArrowAlong(w, h, pts, width, dashed, "BouquetArrow");
        }

        /// <summary>A round arrow for "draw a circle": an arc of <paramref name="sweep"/> degrees (anticlockwise if positive).</summary>
        public static Texture2D CircleArrow(int px, float radius, float startDeg, float sweep, float width)
        {
            float c = px * 0.5f;
            const int segs = 72;
            var pts = new Vector2[segs + 1];
            for (int i = 0; i <= segs; i++) pts[i] = Polar(c, radius, startDeg + sweep * i / segs);
            return ArrowAlong(px, px, pts, width, false, "BouquetCircleArrow");
        }

        private static Texture2D ArrowAlong(int w, int h, Vector2[] pts, float width, bool dashed, string name)
        {
            var p = new Painter(w, h, SS);
            int segs = pts.Length - 1;
            var to = pts[segs];
            var dir = (to - pts[segs - 2]).normalized;
            var nrm = new Vector2(-dir.y, dir.x);
            float headLen = width * 1.9f, headWide = width * 1.25f;
            var baseC = to - dir * headLen;

            // The shaft runs until it is under the head.
            int end = segs;
            while (end > 1 && Vector2.Dot(pts[end] - baseC, dir) > -width * 0.3f) end--;
            float r = width * 0.5f;
            // Dashes by distance along the curve, gaps wider than the shaft so they read as gaps.
            var on = new bool[segs];
            float run = 0f, period = width * 2.6f;
            for (int i = 0; i < segs; i++)
            {
                float mid = run + Vector2.Distance(pts[i], pts[i + 1]) * 0.5f;
                on[i] = i < end && (!dashed || Mathf.Repeat(mid, period) < width * 1.1f || i >= end - 2);
                run += Vector2.Distance(pts[i], pts[i + 1]);
            }
            var rad = new float[segs + 1];
            for (int i = 0; i <= segs; i++) rad[i] = r * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(i / (end * 0.7f + 1f)));

            var head = new[] { to, baseC + nrm * headWide, baseC - nrm * headWide };
            float round = width * 0.16f;
            var inner = new[] { to - dir * round * 1.6f, baseC + nrm * (headWide - round * 1.6f) + dir * round, baseC - nrm * (headWide - round * 1.6f) + dir * round };
            Painter.Shape shape = (x, y) =>
            {
                float d = IconFactory.PolygonField(inner, x, y) + round;
                for (int i = 0; i < segs; i++)
                    if (on[i]) d = Mathf.Max(d, Sd.Capsule(x, y, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y, rad[i + 1]));
                return d;
            };
            float ow = Mathf.Max(3f, width * 0.2f);
            p.Blob(shape, Color.white, new Color(0.78f, 0.83f, 0.93f), outline: ow, inflate: width * 0.45f, spec: 0.3f);
            p.GroundShadow(width * 0.12f, width * 0.12f, 0.25f);
            return p.ToTexture(name);
        }

        /// <summary>
        /// A swipe trail: a soft streak from <paramref name="from"/> to <paramref name="to"/>
        /// (texture px, y up), thin and faint at its start, full and bright at its end.
        /// </summary>
        public static Texture2D Trail(int w, int h, Vector2 from, Vector2 to, float width, Color colour)
        {
            var p = new Painter(w, h, SS);
            const int segs = 24;
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs, t1 = (i + 1) / (float)segs;
                var a = Vector2.Lerp(from, to, t0);
                var b = Vector2.Lerp(from, to, t1);
                float rr = width * 0.5f * Mathf.Lerp(0.25f, 1f, t1);
                var c = new Color(colour.r, colour.g, colour.b, colour.a * Mathf.Lerp(0.05f, 0.30f, t1));
                p.Fill((x, y) => Sd.Capsule(x, y, a.x, a.y, b.x, b.y, rr), c, null, width * 0.15f);
            }
            return p.ToTexture("BouquetTrail");
        }

        private static Vector2 Polar(float c, float r, float deg) =>
            new Vector2(c + r * Mathf.Cos(deg * Mathf.Deg2Rad), c + r * Mathf.Sin(deg * Mathf.Deg2Rad));

        /// <summary>A soft glowing ring where the finger presses.</summary>
        public static Texture2D PressRing(int px, Color colour)
        {
            var p = new Painter(px, px, SS);
            float c = px * 0.5f;
            for (int i = 0; i < 3; i++)
            {
                float rr = c * (0.45f + i * 0.22f);
                var col = new Color(colour.r, colour.g, colour.b, colour.a * (0.9f - i * 0.28f));
                p.Fill((x, y) => (px * 0.035f) - Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - rr), col, null, 1.5f);
            }
            p.Fill((x, y) => Sd.Circle(x, y, c, c, c * 0.30f), new Color(colour.r, colour.g, colour.b, colour.a * 0.55f), null, c * 0.12f);
            return p.ToTexture("BouquetPress");
        }

        // ------------------------------------------------------------------ badges and slots

        /// <summary>A green disc with a white tick: done.</summary>
        public static Texture2D CheckBadge(int px)
        {
            var p = new Painter(px, px, SS);
            float c = px * 0.5f, u = px / 100f;
            Painter.Shape disc = (x, y) => Sd.Circle(x, y, c, c + 2f * u, 40f * u);
            p.Blob(disc, new Color(0.62f, 0.95f, 0.46f), new Color(0.16f, 0.56f, 0.22f), outline: 5f * u, inflate: 30f * u, spec: 0.5f);
            Painter.Shape tick = (x, y) => Mathf.Max(Sd.Capsule(x, y, c - 18f * u, c + 2f * u, c - 5f * u, c - 12f * u, 6.5f * u),
                                                     Sd.Capsule(x, y, c - 5f * u, c - 12f * u, c + 19f * u, c + 16f * u, 6.5f * u));
            p.Fill((x, y) => tick(x, y) + 3.5f * u, new Color(0.10f, 0.32f, 0.12f, 0.9f));
            p.Fill(tick, Color.white);
            return p.ToTexture("BouquetCheck");
        }

        public enum SlotState { Empty, Target, Filled }

        /// <summary>
        /// A place in the arrangement for one flower: a cream dish when filled, a sunken hole with
        /// a dashed rim when empty, and the same with a glowing gold rim when it is where the
        /// flower being dragged should go. Square texture; the dish is about 0.8 of it across.
        /// </summary>
        public static Texture2D Slot(int px, SlotState state)
        {
            var p = new Painter(px, px, SS);
            float c = px * 0.5f, u = px / 200f;
            if (state == SlotState.Filled)
            {
                Painter.Shape dish = (x, y) => Sd.Circle(x, y, c, c, 80f * u);
                p.Blob(dish, new Color(1f, 0.98f, 0.90f), new Color(0.86f, 0.72f, 0.52f), outline: 6f * u, inflate: 22f * u, spec: 0.25f);
                p.Fill((x, y) => 1.6f * u - Mathf.Abs(Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) - 68f * u),
                    new Color(0.78f, 0.62f, 0.40f, 0.45f), dish, 1f * u);
            }
            else
            {
                bool target = state == SlotState.Target;
                if (target)
                    p.Fill((x, y) => Sd.Circle(x, y, c, c, 96f * u), new Color(1f, 0.88f, 0.35f, 0.30f), null, 14f * u);
                Painter.Shape hole = (x, y) => Sd.Circle(x, y, c, c, 80f * u);
                p.Fill(hole, new Color(0.07f, 0.13f, 0.08f, 0.55f), null, 1f * u);
                // Inner shadow along the top edge, a lit lip along the bottom.
                p.Fill((x, y) => Mathf.Min(hole(x, y), -Sd.Circle(x, y, c, c - 12f * u, 80f * u)), new Color(0f, 0f, 0f, 0.35f), hole, 6f * u);
                p.Fill((x, y) => Mathf.Min(hole(x, y), -Sd.Circle(x, y, c, c + 6f * u, 80f * u)), new Color(1f, 1f, 1f, 0.18f), hole, 3f * u);

                int dashes = 22;
                float rr = 86f * u, w = (target ? 5.5f : 4f) * u;
                var col = target ? new Color(1f, 0.88f, 0.32f) : new Color(1f, 1f, 1f, 0.55f);
                for (int i = 0; i < dashes; i++)
                {
                    float a0 = (i + 0.2f) / dashes * Mathf.PI * 2f, a1 = (i + 0.72f) / dashes * Mathf.PI * 2f;
                    var q0 = new Vector2(c + rr * Mathf.Cos(a0), c + rr * Mathf.Sin(a0));
                    var q1 = new Vector2(c + rr * Mathf.Cos(a1), c + rr * Mathf.Sin(a1));
                    if (target) p.Fill((x, y) => Sd.Capsule(x, y, q0.x, q0.y, q1.x, q1.y, w + 2.5f * u), new Color(Ink.r, Ink.g, Ink.b, 0.8f));
                    p.Fill((x, y) => Sd.Capsule(x, y, q0.x, q0.y, q1.x, q1.y, w), col);
                }
            }
            return p.ToTexture("BouquetSlot");
        }

        /// <summary>A five-point star, gold when earned, a dark socket when not.</summary>
        public static Texture2D Star(int px, bool lit)
        {
            var p = new Painter(px, px, SS);
            float c = px * 0.5f, R = px * 0.43f;
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float a = (90f + i * 36f) * Mathf.Deg2Rad;
                float rr = i % 2 == 0 ? R : R * 0.50f;
                pts[i] = new Vector2(c + rr * Mathf.Cos(a), c - px * 0.02f + rr * Mathf.Sin(a));
            }
            Painter.Shape star = (x, y) => IconFactory.PolygonField(pts, x, y) + px * 0.03f;
            if (lit)
            {
                p.Blob(star, new Color(1f, 0.95f, 0.50f), new Color(0.96f, 0.56f, 0.06f), outline: px * 0.05f, inflate: px * 0.16f, spec: 0.7f);
                p.Fill((x, y) => Sd.Capsule(x, y, c - R * 0.36f, c + R * 0.10f, c - R * 0.16f, c + R * 0.42f, px * 0.028f), new Color(1f, 1f, 1f, 0.85f), star, 1.5f);
            }
            else
                p.Blob(star, new Color(0.44f, 0.34f, 0.26f), new Color(0.20f, 0.14f, 0.10f), outline: px * 0.05f, inflate: px * 0.16f, spec: 0.1f);
            return p.ToTexture("BouquetStar");
        }

        /// <summary>A four-point sparkle.</summary>
        public static Texture2D Sparkle(int px, Color colour)
        {
            var p = new Painter(px, px, SS);
            float c = px * 0.5f;
            Painter.Shape sh = (x, y) =>
            {
                float dx = Mathf.Abs(x - c) / c, dy = Mathf.Abs(y - c) / c;
                // An astroid: sharp points on the axes, pinched waist.
                float v = Mathf.Pow(dx, 0.5f) + Mathf.Pow(dy, 0.5f);
                return (1f - v) * c * 0.35f;
            };
            p.Fill(sh, colour, null, 1.2f);
            p.Fill((x, y) => Sd.Circle(x, y, c, c, c * 0.18f), Color.white, null, c * 0.1f);
            return p.ToTexture("BouquetSparkle");
        }

        /// <summary>Soft golden rays behind a reward.</summary>
        public static Texture2D Rays(int px, int count, Color colour)
        {
            var p = new Painter(px, px, 1);
            float c = px * 0.5f;
            p.Raster((x, y) =>
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / c;
                if (r > 1f) return Color.clear;
                float a = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f) * count;
                float stripe = Mathf.Clamp01((Mathf.Abs(Mathf.Repeat(a, 1f) - 0.5f) - 0.22f) * 10f);
                float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
                float glow = Mathf.Clamp01(1f - r * 1.3f);
                return new Color(colour.r, colour.g, colour.b, colour.a * (stripe * fade * 0.6f + glow * glow * 0.55f));
            });
            return p.ToTexture("BouquetRays");
        }
    }
}
