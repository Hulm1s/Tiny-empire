using System.Collections.Generic;
using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// Draws the shop's wall patterns and floors in code, the way <see cref="IconFactory"/> draws
    /// icons: there is no art in the project, and a runtime-built Texture2D is safe in the URP
    /// web build (a runtime-built MATERIAL is the thing that renders magenta - the textures
    /// made here are only ever handed to an existing material through a MaterialPropertyBlock).
    ///
    /// Each pattern is drawn ONCE into a "map" - four bytes a pixel - and recoloured from it
    /// whenever the player moves a colour slider. Drawing a pattern is the expensive part, a
    /// recolour is a multiply per pixel, which is what keeps a drag on the colour picker smooth
    /// in a single-threaded web build. The map holds, for walls, how much of each of the THREE
    /// colours a pixel is (red = primary, green = secondary, blue = tertiary, always adding up
    /// to one) plus a shade in the fourth byte for bevels and variation; for floors it holds
    /// one brightness byte.
    ///
    /// Every pattern is measured in metres, not in pixels or in UV, because the walls are
    /// different sizes: a pattern is drawn as one square <see cref="TileMeters"/> across and the
    /// wall is tiled with it by <c>length / TileMeters</c>, so a brick is the same size on the
    /// long north wall as on the short partition. Each pattern's own repeat divides the tile
    /// exactly, so the texture wraps without a seam. Edges are anti-aliased by coverage (a
    /// distance against a one-pixel ramp) rather than by sampling, so a thin grout line stays
    /// crisp and does not shimmer.
    /// </summary>
    public static class PatternFactory
    {
        /// <summary>Texture resolution. 256 px over 1.6 m is about 6 mm a pixel.</summary>
        public const int Size = 256;

        /// <summary>The ground one repeat of a wall or floor texture covers.</summary>
        public const float TileMeters = 1.6f;

        /// <summary>
        /// The height a "banded" wall pattern spans in ONE repeat, in metres: the two-band wall
        /// is drawn as a whole wall from floor to top, not as a tile, so it must not repeat
        /// vertically. It is the height of the tallest wall in the shop.
        /// </summary>
        public const float BandedHeight = 3.4f;

        // The floor kinds. Stored in the save, so the numbers must not change.
        public const int FloorTiles = 0;
        public const int FloorWood = 1;
        public const int FloorSolid = 2;

        public static readonly string[] FloorNames = { "TILES", "WOOD", "SOLID" };

        // The wall patterns, in the order the menu lists them. Stored in the save by index, so
        // new ones go on the END.
        public const int Plain = 0;
        public const int Stripes = 1;
        public const int Checker = 2;
        public const int Brick = 3;
        public const int Subway = 4;
        public const int Diamonds = 5;
        public const int Waves = 6;
        public const int Dots = 7;
        public const int Herringbone = 8;
        public const int TwoBand = 9;

        public const int PatternCount = 10;

        public static readonly string[] PatternNames =
        {
            "PLAIN", "STRIPES", "CHECKER", "BRICK", "SUBWAY", "DIAMONDS",
            "WAVES", "DOTS", "HERRING-\nBONE", "TWO-BAND",
        };

        /// <summary>What each colour does in each pattern, shown under the grid.</summary>
        public static readonly string[] PatternHints =
        {
            "One flat colour: Primary.",
            "Primary and Secondary stripes, Tertiary pinstripes between.",
            "Primary and Secondary squares, Tertiary grid lines.",
            "Primary bricks, a few Secondary ones, Tertiary mortar.",
            "Primary tiles, one Secondary row, Tertiary grout.",
            "Primary ground, Secondary diamonds, Tertiary trim and dots.",
            "Primary and Secondary waves, Tertiary lines between.",
            "Secondary and Tertiary dots on a Primary ground.",
            "Primary and Secondary planks, Tertiary joints.",
            "Primary lower wall, Secondary upper wall, Tertiary rail between.",
        };

        /// <summary>True for patterns drawn as a whole wall from the floor up.</summary>
        public static bool IsBanded(int pattern) => pattern == TwoBand;

        // ------------------------------------------------------------------ maps

        private static readonly byte[][] WallMaps = new byte[PatternCount][];
        private static byte[] _tileFloorMap;
        private static byte[] _woodFloorMap;

        public static bool IsWallMapReady(int pattern) =>
            pattern >= 0 && pattern < PatternCount && WallMaps[pattern] != null;

        /// <summary>The weights for a pattern, drawn on first use. Four bytes a pixel.</summary>
        public static byte[] WallMap(int pattern)
        {
            pattern = Mathf.Clamp(pattern, 0, PatternCount - 1);
            if (WallMaps[pattern] != null) return WallMaps[pattern];

            var map = new byte[Size * Size * 4];
            bool banded = IsBanded(pattern);
            float pixelX = TileMeters / Size;
            float pixelY = (banded ? BandedHeight : TileMeters) / Size;
            float aa = Mathf.Max(pixelX, pixelY) * 1.15f;

            for (int j = 0; j < Size; j++)
            {
                float y = (j + 0.5f) * pixelY;
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) * pixelX;
                    Mix m = SampleWall(pattern, x, y, aa);

                    int at = (j * Size + i) * 4;
                    map[at] = ToByte(m.a);
                    map[at + 1] = ToByte(m.b);
                    // Whatever the rounding left over, so the three always sum to 255.
                    map[at + 2] = (byte)Mathf.Clamp(255 - map[at] - map[at + 1], 0, 255);
                    map[at + 3] = (byte)Mathf.Clamp(Mathf.RoundToInt(128f + Mathf.Clamp(m.shade, -1f, 1f) * 127f), 0, 255);
                }
            }

            WallMaps[pattern] = map;
            return map;
        }

        private static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

        private struct Mix
        {
            public float a, b, c;
            public float shade;
        }

        private static Mix Start() => new Mix { a = 1f };

        /// <summary>Lays a colour over what is there with a coverage from 0 to 1.</summary>
        private static void Lay(ref Mix m, int colour, float cover)
        {
            if (cover <= 0f) return;
            float keep = 1f - cover;
            m.a *= keep; m.b *= keep; m.c *= keep;
            if (colour == 0) m.a += cover;
            else if (colour == 1) m.b += cover;
            else m.c += cover;
        }

        /// <summary>1 inside a shape, 0 outside, a one-pixel ramp on the edge. Negative distance is inside.</summary>
        private static float Cov(float signedDistance, float aa) =>
            Mathf.Clamp01(0.5f - signedDistance / aa);

        /// <summary>Distance from the nearest multiple of the period: -half..+half.</summary>
        private static float Periodic(float v, float period) =>
            v - period * Mathf.Floor(v / period + 0.5f);

        private static float Hash(int a, int b, int seed)
        {
            unchecked
            {
                uint h = (uint)(a * 374761393) + (uint)(b * 668265263) + (uint)(seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }

        private static int Mod(int v, int m) => ((v % m) + m) % m;

        /// <summary>
        /// A square wave smoothed across its edges: 0 and 1 alternating every period, 0.5 right
        /// on the edge. <paramref name="edge"/> is the distance to the nearest edge.
        /// </summary>
        private static float Band(float v, float period, float aa, float scale, out float edge)
        {
            float t = v / period;
            float f = t - Mathf.Floor(t);
            int odd = ((int)Mathf.Floor(t)) & 1;
            edge = Mathf.Min(f, 1f - f) * period * scale;
            return Mathf.Lerp(0.5f, odd, Mathf.Clamp01(edge / aa));
        }

        private static Mix SampleWall(int pattern, float x, float y, float aa)
        {
            var m = Start();
            switch (pattern)
            {
                case Plain:
                    break;

                case Stripes:
                {
                    // 0.4 m repeat: 20 cm of each colour, a pinstripe on every border.
                    float centre = Periodic(x - 0.3f, 0.4f);
                    Lay(ref m, 1, Cov(Mathf.Abs(centre) - 0.1f, aa));
                    Lay(ref m, 2, Cov(Mathf.Abs(Periodic(x, 0.2f)) - 0.008f, aa));
                    break;
                }

                case Checker:
                {
                    float ex, ey;
                    float sx = Band(x, 0.4f, aa, 1f, out ex);
                    float sy = Band(y, 0.4f, aa, 1f, out ey);
                    Lay(ref m, 1, sx + sy - 2f * sx * sy);
                    float grid = Mathf.Min(Mathf.Abs(Periodic(x, 0.4f)), Mathf.Abs(Periodic(y, 0.4f)));
                    Lay(ref m, 2, Cov(grid - 0.007f, aa));
                    break;
                }

                case Brick:
                    SampleBrick(ref m, x, y, aa, mortar: 0.018f, bevel: false);
                    break;

                case Subway:
                    SampleBrick(ref m, x, y, aa, mortar: 0.012f, bevel: true);
                    break;

                case Diamonds:
                {
                    float fx = x / 0.4f; fx -= Mathf.Floor(fx);
                    float fy = y / 0.4f; fy -= Mathf.Floor(fy);
                    float a = Mathf.Abs(fx - 0.5f), b = Mathf.Abs(fy - 0.5f);
                    float k = 0.4f / 1.41421f;       // cell units on the diagonal to metres

                    Lay(ref m, 1, Cov((a + b - 0.40f) * k, aa));
                    Lay(ref m, 2, Cov(Mathf.Abs((a + b - 0.455f) * k) - 0.007f, aa));
                    float dx = (fx - 0.5f) * 0.4f, dy = (fy - 0.5f) * 0.4f;
                    Lay(ref m, 2, Cov(Mathf.Sqrt(dx * dx + dy * dy) - 0.026f, aa));

                    // A small diamond where four of the big ones meet.
                    float corner = (0.5f - a) + (0.5f - b);
                    Lay(ref m, 2, Cov((corner - 0.17f) * k, aa));
                    break;
                }

                case Waves:
                {
                    const float amplitude = 0.05f, wavelength = 0.8f, band = 0.4f;
                    float phase = 2f * Mathf.PI * x / wavelength;
                    float yy = y + amplitude * Mathf.Sin(phase);
                    float slope = amplitude * 2f * Mathf.PI / wavelength * Mathf.Cos(phase);
                    float scale = 1f / Mathf.Sqrt(1f + slope * slope);

                    float edge;
                    float v = Band(yy, band, aa, scale, out edge);
                    Lay(ref m, 1, v);
                    Lay(ref m, 2, Cov(edge - 0.007f, aa));
                    break;
                }

                case Dots:
                {
                    // Rows 20 cm apart, a dot every 40 cm, every other row shifted half a step.
                    int row = (int)Mathf.Floor(y / 0.2f);
                    float offset = (row & 1) == 1 ? 0.2f : 0f;
                    float cx = (Mathf.Floor((x - offset) / 0.4f) + 0.5f) * 0.4f + offset;
                    float cy = (row + 0.5f) * 0.2f;
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    Lay(ref m, 1, Cov(d - 0.078f, aa));
                    Lay(ref m, 2, Cov(d - 0.03f, aa));
                    break;
                }

                case Herringbone:
                    SampleHerringbone(ref m, x, y, aa);
                    break;

                case TwoBand:
                    SampleTwoBand(ref m, x, y, aa);
                    break;
            }
            return m;
        }

        /// <summary>Courses of bricks (or glazed tiles) in a running bond, half a brick out on alternate rows.</summary>
        private static void SampleBrick(ref Mix m, float x, float y, float aa, float mortar, bool bevel)
        {
            const float course = 0.2f, length = 0.4f;

            int row = (int)Mathf.Floor(y / course);
            float fy = y - row * course;
            float offset = (row & 1) == 1 ? length * 0.5f : 0f;
            float xo = x + offset;
            int col = (int)Mathf.Floor(xo / length);
            float fx = xo - col * length;

            // Tile counts per repeat are 4 along and 8 up, so the identity wraps exactly.
            float h = Hash(Mod(col, 4), Mod(row, 8), bevel ? 17 : 5);

            if (bevel)
            {
                // The same layout as brick, glazed: one row picked out in the second colour,
                // each tile a hair different in brightness, a lit top edge and a shaded foot.
                if (Mod(row, 8) == 3) Lay(ref m, 1, 1f);
                m.shade += (h - 0.5f) * 0.07f;
                float top = course - fy;
                m.shade += 0.15f * Cov(top - 0.02f, aa);
                m.shade -= 0.12f * Cov(fy - 0.016f, aa);
            }
            else
            {
                if (h < 0.28f) Lay(ref m, 1, 1f);
                m.shade += (h - 0.5f) * 0.14f;
                m.shade += ((fy / course) - 0.5f) * 0.10f;
            }

            float edge = Mathf.Min(Mathf.Min(fx, length - fx), Mathf.Min(fy, course - fy));
            Lay(ref m, 2, Cov(edge - mortar * 0.5f, aa));
            if (edge < mortar * 0.5f + aa) m.shade *= Mathf.Clamp01((edge - mortar * 0.5f) / aa);
        }

        /// <summary>
        /// Planks three times as long as they are wide, laid in L-shaped steps. In a grid of
        /// cells one plank wide, a cell's diagonal (column minus row, modulo six) says whether it
        /// is part of a flat plank (0-2) or an upright one (3-5), and which plank. The repeat is
        /// six cells, which is why the tile is twelve cells across.
        /// </summary>
        private static void SampleHerringbone(ref Mix m, float x, float y, float aa)
        {
            const int cells = 12;
            float c = TileMeters / cells;

            int i = (int)Mathf.Floor(x / c);
            int j = (int)Mathf.Floor(y / c);
            int d = Mod(i - j, 6);

            float lx, ly, lengthX, lengthY;
            int idA, idB;
            bool upright = d >= 3;

            if (!upright)
            {
                int start = i - d;
                idA = Mod(start, cells);
                idB = Mod(j, cells);
                lx = x - start * c;
                ly = y - j * c;
                lengthX = 3f * c; lengthY = c;
            }
            else
            {
                int startRow = j - (5 - d);
                idA = Mod(i, cells);
                idB = Mod(startRow, cells) + 100;
                lx = x - i * c;
                ly = y - startRow * c;
                lengthX = c; lengthY = 3f * c;
            }

            if (upright) Lay(ref m, 1, 1f);
            m.shade += (Hash(idA, idB, 9) - 0.5f) * 0.12f;

            float edge = Mathf.Min(Mathf.Min(lx, lengthX - lx), Mathf.Min(ly, lengthY - ly));
            Lay(ref m, 2, Cov(edge - 0.006f, aa));
        }

        /// <summary>
        /// The whole wall, floor to top: a wainscot of panelled lower wall, a rail, and the upper
        /// wall. Not a tile - it spans <see cref="BandedHeight"/> in one repeat.
        /// </summary>
        private static void SampleTwoBand(ref Mix m, float x, float y, float aa)
        {
            const float railBottom = 1.15f, railTop = 1.27f;

            // Upper wall in the second colour; the rail between is the third.
            Lay(ref m, 1, Cov(railTop - y, aa));
            float railCentre = (railBottom + railTop) * 0.5f;
            Lay(ref m, 2, Cov(Mathf.Abs(y - railCentre) - (railTop - railBottom) * 0.5f, aa));

            // Shaded under the rail and lit along its top, so it reads as a moulding.
            m.shade += 0.18f * Cov(Mathf.Abs(y - (railTop - 0.012f)) - 0.012f, aa);
            m.shade -= 0.14f * Cov(Mathf.Abs(y - (railBottom - 0.025f)) - 0.025f, aa);

            // Raised panels in the lower wall: a thin shaded groove round each, 0.8 m wide.
            if (y < railBottom - 0.04f)
            {
                float px = Mathf.Abs(Periodic(x, 0.8f));
                float py = Mathf.Abs(y - 0.62f);
                float d = Mathf.Max(px - 0.3f, py - 0.4f);
                m.shade -= 0.20f * Cov(Mathf.Abs(d) - 0.006f, aa);
                m.shade += 0.035f * Cov(d, aa);
            }
        }

        // ------------------------------------------------------------------ floors

        /// <summary>One brightness byte a pixel (128 is 1.0), for the tiled or wooden floor.</summary>
        public static byte[] FloorMap(int kind)
        {
            if (kind == FloorWood) return _woodFloorMap ?? (_woodFloorMap = DrawFloor(wood: true));
            return _tileFloorMap ?? (_tileFloorMap = DrawFloor(wood: false));
        }

        private static byte[] DrawFloor(bool wood)
        {
            var map = new byte[Size * Size];
            float px = TileMeters / Size;
            float aa = px * 1.15f;

            for (int j = 0; j < Size; j++)
            {
                float y = (j + 0.5f) * px;
                for (int i = 0; i < Size; i++)
                {
                    float x = (i + 0.5f) * px;
                    float f = wood ? WoodBrightness(x, y, aa) : TileBrightness(x, y, aa);
                    map[j * Size + i] = (byte)Mathf.Clamp(Mathf.RoundToInt(f * 128f), 0, 255);
                }
            }
            return map;
        }

        /// <summary>40 cm tiles, 2 cm of dark grout, each tile a touch different, lit along one edge.</summary>
        private static float TileBrightness(float x, float y, float aa)
        {
            const float tile = 0.4f, grout = 0.02f;
            int ci = (int)Mathf.Floor(x / tile), cj = (int)Mathf.Floor(y / tile);
            float fx = x - ci * tile, fy = y - cj * tile;

            float f = 1f + (Hash(Mod(ci, 4), Mod(cj, 4), 3) - 0.5f) * 0.10f;
            f += 0.06f * Cov((tile - fy) - 0.03f, aa);

            float edge = Mathf.Min(Mathf.Min(fx, tile - fx), Mathf.Min(fy, tile - fy));
            float groutCover = Cov(edge - grout * 0.5f, aa);
            return Mathf.Lerp(f, 0.52f, groutCover);
        }

        /// <summary>
        /// Boards 20 cm wide, each row split into two 80 cm planks at a joint that moves from
        /// row to row, with grain lines that run along the board. The grain only uses whole
        /// numbers of waves across the tile so it wraps.
        /// </summary>
        private static float WoodBrightness(float x, float y, float aa)
        {
            const float board = 0.2f;
            int row = (int)Mathf.Floor(y / board);
            float fy = y - row * board;
            int rowId = Mod(row, 8);

            float joint = Hash(rowId, 0, 41) * TileMeters;
            float along = Mathf.Repeat(x - joint, TileMeters);
            int seg = along < 0.8f ? 0 : 1;
            float segPos = along - seg * 0.8f;
            float plank = Hash(rowId, seg, 7);

            float f = 0.90f + plank * 0.2f;

            // Grain: several fine lines across the board, wavering slowly along it.
            float wander = 0.07f * Mathf.Sin(2f * Mathf.PI * (3f * x / TileMeters) + plank * 6.2832f);
            float lines = Mathf.Sin(2f * Mathf.PI * ((fy / board) * 7f + plank * 5f + wander * 6f));
            float fine = Mathf.Sin(2f * Mathf.PI * ((fy / board) * 19f + plank * 11f - wander * 9f));
            f += lines * 0.030f + fine * 0.018f;

            // Gaps between boards and at the end joints.
            float sideEdge = Mathf.Min(fy, board - fy);
            float endEdge = Mathf.Min(segPos, 0.8f - segPos);
            float gap = Mathf.Max(Cov(sideEdge - 0.004f, aa), Cov(endEdge - 0.003f, aa));
            return Mathf.Lerp(f, 0.45f, gap);
        }

        // ------------------------------------------------------------------ colour and baking

        /// <summary>The wood floor's colours: a pale oak at one end, a dark walnut at the other.</summary>
        public static readonly Color LightOak = new Color(0.80f, 0.62f, 0.40f);
        public static readonly Color Walnut = new Color(0.30f, 0.18f, 0.10f);

        public static Color WoodColor(float t) =>
            Color.Lerp(LightOak, Walnut, Mathf.Clamp01(t));

        /// <summary>A texture ready to be painted into and handed to a MaterialPropertyBlock.</summary>
        public static Texture2D NewTexture(string name, int width = Size, int height = Size)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
                hideFlags = HideFlags.DontSave,
            };
            return tex;
        }

        private static Color32[] _scratch;

        private static Color32[] Scratch(int count)
        {
            if (_scratch == null || _scratch.Length != count) _scratch = new Color32[count];
            return _scratch;
        }

        /// <summary>Colours a wall map with the three colours into a pixel array of Size x Size.</summary>
        public static void RecolorWall(int pattern, Color primary, Color secondary, Color tertiary,
            Color32[] into)
        {
            byte[] map = WallMap(pattern);
            for (int p = 0; p < into.Length; p++)
                into[p] = Shade(map, p * 4, primary, secondary, tertiary);
        }

        /// <summary>Colours a wall map with the three colours and uploads it.</summary>
        public static void PaintWall(int pattern, Color primary, Color secondary, Color tertiary,
            Texture2D target)
        {
            var pixels = Scratch(Size * Size);
            RecolorWall(pattern, primary, secondary, tertiary, pixels);
            target.SetPixels32(pixels);
            target.Apply(true, false);
        }

        private static Color32 Shade(byte[] map, int at, Color a, Color b, Color c)
        {
            float wa = map[at] / 255f, wb = map[at + 1] / 255f, wc = map[at + 2] / 255f;
            float r = a.r * wa + b.r * wb + c.r * wc;
            float g = a.g * wa + b.g * wb + c.g * wc;
            float bl = a.b * wa + b.b * wb + c.b * wc;

            float s = (map[at + 3] - 128) / 127f;
            if (s < 0f)
            {
                float k = 1f + s * 0.6f;
                r *= k; g *= k; bl *= k;
            }
            else if (s > 0f)
            {
                float k = s * 0.55f;
                r += (1f - r) * k; g += (1f - g) * k; bl += (1f - bl) * k;
            }

            return new Color32(ToByte(r), ToByte(g), ToByte(bl), 255);
        }

        /// <summary>Colours a floor map and uploads it. Solid floors have no texture; do not call this.</summary>
        public static void PaintFloor(int kind, Color colour, Texture2D target)
        {
            byte[] map = FloorMap(kind);
            var pixels = Scratch(Size * Size);

            for (int p = 0; p < pixels.Length; p++)
            {
                float f = map[p] / 128f;
                pixels[p] = new Color32(ToByte(colour.r * f), ToByte(colour.g * f), ToByte(colour.b * f), 255);
            }

            target.SetPixels32(pixels);
            target.Apply(true, false);
        }

        /// <summary>
        /// Averages a wall map down to a small swatch, for the pattern buttons. Boxes the map
        /// 4 : 1, which is what makes a thumbnail of a fine pattern look like the pattern and
        /// not like noise.
        /// </summary>
        public static void PaintThumbnail(int pattern, Color primary, Color secondary, Color tertiary,
            Texture2D target)
        {
            byte[] map = WallMap(pattern);
            int w = target.width, h = target.height;
            var pixels = new Color32[w * h];
            int stepX = Size / w, stepY = Size / h;

            // A banded pattern is a whole wall, so its swatch shows the bottom of it to a bit
            // above the rail; a tile shows its whole repeat.
            if (IsBanded(pattern)) stepY = Mathf.Max(1, Mathf.RoundToInt(stepY * (1.6f / BandedHeight)));

            for (int j = 0; j < h; j++)
            {
                for (int i = 0; i < w; i++)
                {
                    float r = 0f, g = 0f, b = 0f;
                    int n = 0;
                    for (int dj = 0; dj < stepY; dj++)
                    {
                        int sj = Mathf.Min(Size - 1, j * stepY + dj);
                        for (int di = 0; di < stepX; di++)
                        {
                            int si = Mathf.Min(Size - 1, i * stepX + di);
                            Color32 c = Shade(map, (sj * Size + si) * 4, primary, secondary, tertiary);
                            r += c.r; g += c.g; b += c.b;
                            n++;
                        }
                    }
                    pixels[j * w + i] = new Color32((byte)(r / n), (byte)(g / n), (byte)(b / n), 255);
                }
            }

            target.SetPixels32(pixels);
            target.Apply(false, false);
        }

        // ------------------------------------------------------------------ the cube

        /// <summary>
        /// Draws a small isometric cube wearing the wall pattern, into an RGBA texture.
        ///
        /// Software-rendered rather than a second camera and a RenderTexture: it needs no
        /// light, no layer, no material and no render pass, so it behaves identically in the
        /// web build and in the editor, and a colour drag costs one small loop. Each pixel is
        /// worked backwards onto whichever of the three visible faces it lies on and the
        /// already-coloured wall pixels are read there with bilinear filtering; four samples a pixel smooth the
        /// silhouette and the edges between faces.
        ///
        /// The faces are lit differently (top brightest, left in the middle, right darkest) and
        /// every edge is darkened a little, which is what makes it read as a solid and not as
        /// three flat swatches.
        /// </summary>
        public static void PaintCube(Color32[] wall, bool banded, Texture2D target)
        {
            int w = target.width, h = target.height;
            var pixels = new Color32[w * h];

            float radius = h * 0.40f;
            float cx = w * 0.5f, cy = h * 0.54f;
            const float root3over2 = 0.8660254f;

            // The two edges of the top face and the downward edge, as screen vectors.
            Vector2 e1 = new Vector2(-root3over2 * radius, 0.5f * radius);
            Vector2 e2 = new Vector2(root3over2 * radius, 0.5f * radius);
            Vector2 down = new Vector2(0f, -radius);

            // A banded wall is shown from its foot to a little above the rail.
            float vSpan = banded ? 1.9f / BandedHeight : 1f;

            const int samples = 2;
            const int total = samples * samples;

            for (int py = 0; py < h; py++)
            {
                for (int px = 0; px < w; px++)
                {
                    float r = 0f, g = 0f, b = 0f;
                    int cube = 0;
                    float shadow = 0f;

                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float x = px + (sx + 0.5f) / samples - cx;
                            float y = py + (sy + 0.5f) / samples - cy;

                            if (!TryCubeFace(x, y, e1, e2, down, out int face, out float u, out float v))
                            {
                                // A soft shadow under the cube, outside it.
                                float sdx = x / (radius * 1.05f);
                                float sdy = (y + radius * 0.98f) / (radius * 0.30f);
                                float sd = Mathf.Sqrt(sdx * sdx + sdy * sdy);
                                if (sd < 1f) shadow += 0.30f * (1f - sd) * (1f - sd);
                                continue;
                            }

                            float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                            float rim = Mathf.Lerp(0.70f, 1f, Mathf.Clamp01(edge / 0.035f));

                            // On the sides v runs down the face, so the wall's own "up" is its
                            // opposite. The top just shows the pattern for the look of it.
                            float tu = u;
                            float tv = face == 0 ? (banded ? 0.5f / BandedHeight : v) : (1f - v) * vSpan;

                            Color32 c = Bilinear(wall, tu, tv, clampV: banded);

                            float light = face == 0 ? 1.10f : face == 1 ? 0.96f : 0.74f;
                            r += Mathf.Min(255f, c.r * light * rim);
                            g += Mathf.Min(255f, c.g * light * rim);
                            b += Mathf.Min(255f, c.b * light * rim);
                            cube++;
                        }
                    }

                    // Cube samples are opaque colour; shadow samples are black with a partial
                    // alpha. Straight (not premultiplied) colour is the cube's, weighted by how
                    // much of the pixel the cube actually owns.
                    float weight = cube + shadow;
                    if (weight <= 0f) { pixels[py * w + px] = new Color32(0, 0, 0, 0); continue; }

                    float alpha = Mathf.Clamp01(weight / total);
                    pixels[py * w + px] = new Color32(
                        (byte)Mathf.Clamp(r / weight, 0f, 255f),
                        (byte)Mathf.Clamp(g / weight, 0f, 255f),
                        (byte)Mathf.Clamp(b / weight, 0f, 255f),
                        (byte)(alpha * 255f));
                }
            }

            target.SetPixels32(pixels);
            target.Apply(false, false);
        }

        /// <summary>Which face of the cube a screen point is on, and where on it (0-1 each way).</summary>
        private static bool TryCubeFace(float x, float y, Vector2 e1, Vector2 e2, Vector2 down,
            out int face, out float u, out float v)
        {
            // Top face: point = a*e1 + b*e2.
            float det = e1.x * e2.y - e1.y * e2.x;
            float a = (x * e2.y - y * e2.x) / det;
            float b = (e1.x * y - e1.y * x) / det;
            if (a >= 0f && a <= 1f && b >= 0f && b <= 1f)
            {
                face = 0; u = a; v = b;
                return true;
            }

            // Left face: point = a*e1 + c*down.
            float detL = e1.x * down.y - e1.y * down.x;
            a = (x * down.y - y * down.x) / detL;
            float c = (e1.x * y - e1.y * x) / detL;
            if (a >= 0f && a <= 1f && c >= 0f && c <= 1f)
            {
                face = 1; u = 1f - a; v = c;
                return true;
            }

            // Right face: point = a*e2 + c*down.
            float detR = e2.x * down.y - e2.y * down.x;
            a = (x * down.y - y * down.x) / detR;
            c = (e2.x * y - e2.y * x) / detR;
            if (a >= 0f && a <= 1f && c >= 0f && c <= 1f)
            {
                face = 2; u = a; v = c;
                return true;
            }

            face = 0; u = v = 0f;
            return false;
        }

        private static Color32 Bilinear(Color32[] wall, float u, float v, bool clampV)
        {
            float fx = u * Size - 0.5f, fy = v * Size - 0.5f;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;

            Color32 Pick(int xi, int yi)
            {
                xi = Mod(xi, Size);
                yi = clampV ? Mathf.Clamp(yi, 0, Size - 1) : Mod(yi, Size);
                return wall[yi * Size + xi];
            }

            Color32 c00 = Pick(x0, y0), c10 = Pick(x0 + 1, y0), c01 = Pick(x0, y0 + 1), c11 = Pick(x0 + 1, y0 + 1);
            float r = Mathf.Lerp(Mathf.Lerp(c00.r, c10.r, tx), Mathf.Lerp(c01.r, c11.r, tx), ty);
            float g = Mathf.Lerp(Mathf.Lerp(c00.g, c10.g, tx), Mathf.Lerp(c01.g, c11.g, tx), ty);
            float b = Mathf.Lerp(Mathf.Lerp(c00.b, c10.b, tx), Mathf.Lerp(c01.b, c11.b, tx), ty);
            return new Color32((byte)r, (byte)g, (byte)b, 255);
        }
    }
}
