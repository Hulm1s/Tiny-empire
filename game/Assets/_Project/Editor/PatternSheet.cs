using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Dumps every wall pattern, the floors and the cube preview into one PNG, the way
    /// <see cref="IconSheet"/> does for icons.
    ///
    /// The patterns are generated in code and only ever seen on a wall a few dozen pixels tall,
    /// which cannot tell a clean seam from a broken one. Each pattern is drawn here as a block of
    /// wall 3.2 m square - two repeats of the tile each way - which is exactly where a seam, a
    /// pattern that does not wrap, or a scale that is wrong shows up as a visible line. Every cell
    /// is the same size in metres, so patterns can be compared for scale as well.
    /// </summary>
    public static class PatternSheet
    {
        private const string OutputPath = "../pattern-sheet.png";

        // Three palettes, so a pattern is seen in more than one set of colours.
        private static readonly Color[][] Palettes =
        {
            new[] { new Color(0.96f, 0.92f, 0.82f), new Color(0.62f, 0.78f, 0.86f), new Color(0.82f, 0.28f, 0.28f) },
            new[] { new Color(0.78f, 0.36f, 0.30f), new Color(0.95f, 0.82f, 0.45f), new Color(0.97f, 0.95f, 0.9f) },
            new[] { new Color(0.28f, 0.45f, 0.42f), new Color(0.88f, 0.9f, 0.84f), new Color(0.22f, 0.24f, 0.3f) },
        };

        [MenuItem("Tycoon/Export Pattern Sheet")]
        public static void Export()
        {
            const int cell = 384, pad = 12, columns = 5;
            int patterns = PatternFactory.PatternCount;
            int paletteRows = 2;     // the patterns are drawn in the first two palettes
            int rows = paletteRows * Mathf.CeilToInt(patterns / (float)columns) + 1 /*floors*/ + 1 /*cubes*/;

            int width = columns * (cell + pad) + pad;
            int height = rows * (cell + pad) + pad;
            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var flat = new Color32[width * height];
            for (int i = 0; i < flat.Length; i++) flat[i] = new Color32(70, 74, 80, 255);
            sheet.SetPixels32(flat);

            int row = 0;
            for (int palette = 0; palette < paletteRows; palette++)
            {
                var colours = Palettes[palette];
                var wall = new Color32[PatternFactory.Size * PatternFactory.Size];

                for (int p = 0; p < patterns; p++)
                {
                    PatternFactory.RecolorWall(p, colours[0], colours[1], colours[2], wall);
                    int column = p % columns;
                    int r = row + p / columns;
                    DrawWall(sheet, wall, PatternFactory.IsBanded(p), pad + column * (cell + pad),
                        height - pad - (r + 1) * cell - r * pad, cell);
                }
                row += Mathf.CeilToInt(patterns / (float)columns);
            }

            // Floors: tiles, light oak, walnut, and a solid.
            var floorPixels = new Color32[PatternFactory.Size * PatternFactory.Size];
            Color[] floorColours =
            {
                new Color(0.84f, 0.82f, 0.77f), PatternFactory.WoodColor(0f), PatternFactory.WoodColor(0.5f),
                PatternFactory.WoodColor(1f), new Color(0.45f, 0.55f, 0.6f),
            };
            int[] kinds =
            {
                PatternFactory.FloorTiles, PatternFactory.FloorWood, PatternFactory.FloorWood,
                PatternFactory.FloorWood, PatternFactory.FloorSolid,
            };
            for (int i = 0; i < 5; i++)
            {
                int x0 = pad + i * (cell + pad);
                int y0 = height - pad - (row + 1) * cell - row * pad;

                if (kinds[i] == PatternFactory.FloorSolid)
                {
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                            sheet.SetPixel(x0 + x, y0 + y, floorColours[i]);
                    continue;
                }

                var tex = PatternFactory.NewTexture("sheetfloor");
                PatternFactory.PaintFloor(kinds[i], floorColours[i], tex);
                floorPixels = tex.GetPixels32();
                Object.DestroyImmediate(tex);
                DrawWall(sheet, floorPixels, false, x0, y0, cell);
            }
            row++;

            // The cube, in the first palette, for every pattern.
            int cubeCell = (cell + pad) / 2 - pad;
            for (int p = 0; p < patterns; p++)
            {
                var colours = Palettes[0];
                var wall = new Color32[PatternFactory.Size * PatternFactory.Size];
                PatternFactory.RecolorWall(p, colours[0], colours[1], colours[2], wall);

                var cube = new Texture2D(160, 160, TextureFormat.RGBA32, false);
                PatternFactory.PaintCube(wall, PatternFactory.IsBanded(p), cube);
                var pixels = cube.GetPixels32();

                int slot = p;
                int x0 = pad + slot * (cubeCell + pad);
                int y0 = height - pad - (row + 1) * cell - row * pad;
                for (int y = 0; y < 160; y++)
                {
                    for (int x = 0; x < 160; x++)
                    {
                        Color32 over = pixels[y * 160 + x];
                        if (over.a == 0) continue;
                        int tx = x0 + x, ty = y0 + cell - 170 + y;
                        if (tx >= width) continue;
                        Color under = sheet.GetPixel(tx, ty);
                        sheet.SetPixel(tx, ty, Color.Lerp(under, new Color32(over.r, over.g, over.b, 255), over.a / 255f));
                    }
                }
                Object.DestroyImmediate(cube);
            }

            sheet.Apply();
            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            Debug.Log($"[PatternSheet] {patterns} patterns, 5 floors -> {path}");
            Debug.Log("PATTERNS_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>A 3.2 m square of wall: the colour map tiled twice across and (for tiles) twice up.</summary>
        private static void DrawWall(Texture2D sheet, Color32[] pixels, bool banded, int x0, int y0, int cell)
        {
            int size = PatternFactory.Size;
            for (int y = 0; y < cell; y++)
            {
                // Metres up the wall, 0 to 3.2.
                float metres = y / (float)cell * 3.2f;
                float v = banded ? metres / PatternFactory.BandedHeight : metres / PatternFactory.TileMeters;
                int sy = banded ? Mathf.Clamp((int)(v * size), 0, size - 1)
                                : ((int)(v * size)) % size;

                for (int x = 0; x < cell; x++)
                {
                    float u = x / (float)cell * 2f;
                    int sx = ((int)(u * size)) % size;
                    sheet.SetPixel(x0 + x, y0 + y, pixels[sy * size + sx]);
                }
            }
        }
    }
}
