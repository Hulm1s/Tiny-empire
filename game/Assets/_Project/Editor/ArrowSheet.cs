using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Exports the guide arrow to a PNG so it can be judged in context: the three task colours
    /// at full size and at the ~90 px it is really shown at (pointing down and pointing
    /// sideways), on the game's grass and on the dark loading background.
    /// </summary>
    public static class ArrowSheet
    {
        private const string OutputPath = "../arrow-sheet.png";

        private static readonly Color Green = new Color(0.30f, 0.78f, 0.34f);
        private static readonly Color Red = new Color(0.90f, 0.26f, 0.22f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.16f);

        [MenuItem("Tycoon/Export Arrow Sheet")]
        public static void Export()
        {
            var colours = new[] { Green, Red, Gold };
            var sources = new Texture2D[colours.Length];
            for (int i = 0; i < colours.Length; i++)
                sources[i] = IconFactory.GuideArrow(colours[i]).texture;

            const int big = 256, small = 90, pad = 24;
            // Per background band: full-size row, then a row of 90 px arrows (down x3, sideways x3).
            int bandHeight = pad + big + pad + small + pad;
            int width = pad + 6 * (small + pad) + pad;
            width = Mathf.Max(width, pad + 3 * (big + pad));
            int height = bandHeight * 2;

            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var backgrounds = new[] { new Color(0.42f, 0.66f, 0.30f), new Color(0.13f, 0.17f, 0.20f) };

            for (int band = 0; band < 2; band++)
            {
                // Band 0 is drawn at the top of the image; texture rows run bottom to top.
                int y0 = height - (band + 1) * bandHeight;
                for (int y = 0; y < bandHeight; y++)
                    for (int x = 0; x < width; x++)
                        sheet.SetPixel(x, y0 + y, backgrounds[band]);

                int bigY = y0 + bandHeight - pad - big;
                for (int i = 0; i < 3; i++)
                    Blit(sheet, sources[i], pad + i * (big + pad) + big / 2f, bigY + big / 2f, big, 0f);

                int smallY = y0 + pad;
                for (int i = 0; i < 3; i++)
                {
                    Blit(sheet, sources[i], pad + i * (small + pad) + small / 2f, smallY + small / 2f, small, 0f);
                    // Pointing right: the down arrow turned a quarter turn anticlockwise.
                    Blit(sheet, sources[i], pad + (i + 3) * (small + pad) + small / 2f, smallY + small / 2f, small, 90f);
                }
            }

            sheet.Apply();
            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            Debug.Log($"[ArrowSheet] -> {path}");
            Debug.Log("ARROW_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Draws the source scaled to <paramref name="size"/> px and rotated anticlockwise by
        /// <paramref name="angle"/>, 4x4 supersampled so a 90 px copy is smooth.
        /// </summary>
        private static void Blit(Texture2D sheet, Texture2D src, float cx, float cy, int size, float angle)
        {
            float rad = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            int half = Mathf.CeilToInt(size * 0.75f);
            const int ss = 4;

            for (int dy = -half; dy <= half; dy++)
            {
                for (int dx = -half; dx <= half; dx++)
                {
                    float r = 0, g = 0, b = 0, a = 0;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float px = dx + (sx + 0.5f) / ss - 0.5f;
                            float py = dy + (sy + 0.5f) / ss - 0.5f;
                            // Undo the rotation, then map the sheet size onto the source.
                            float lx = px * cos + py * sin;
                            float ly = -px * sin + py * cos;
                            float u = lx / size + 0.5f, v = ly / size + 0.5f;
                            if (u < 0f || u > 1f || v < 0f || v > 1f) continue;
                            var c = src.GetPixelBilinear(u, v);
                            r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                        }
                    }
                    if (a <= 0f) continue;

                    float cover = a / (ss * ss);
                    var over = new Color(r / a, g / a, b / a);
                    int x = Mathf.RoundToInt(cx) + dx, y = Mathf.RoundToInt(cy) + dy;
                    if (x < 0 || y < 0 || x >= sheet.width || y >= sheet.height) continue;
                    sheet.SetPixel(x, y, Color.Lerp(sheet.GetPixel(x, y), over, cover));
                }
            }
        }
    }
}
