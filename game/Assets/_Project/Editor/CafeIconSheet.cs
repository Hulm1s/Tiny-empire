using System;
using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The café's five icons as one picture for Filip (PROPOSAL, phase 1; nothing is wired): each big, at the two sizes the
    /// game draws an icon (96 px, ~40 px on a square), in the order bubble alone and as the two-icon order (coffee + cake).
    /// Drawn by IconFactory.CafeIcon. Run: -executeMethod Tycoon.EditorTools.CafeIconSheet.Export (no -nographics: the
    /// lettering is rasterised by the GPU). Writes cafe-icons.png into the preview folder.
    /// </summary>
    public static class CafeIconSheet
    {
        internal const string OutDir = "C:/Users/ADMINI~1/AppData/Local/Temp/claude/63332de34978a462/b50262dc-8611-42da-8650-85247e0c766c/scratchpad/cafe-preview/";

        private static readonly string[] Ids = { "coffee", "premium", "cake", "dirty_cup", "crate" };
        private static readonly string[] Names = { "COFFEE", "PREMIUM COFFEE", "CAKE", "DIRTY CUP", "DELIVERY CRATE" };
        private static readonly string[] Notes = { "every coffee needs milk + beans", "latte art, after the upgrade", "from the display case", "carry it to the dishwasher", "milk + eggs, unpacked at the back" };

        [MenuItem("Tycoon/Export Cafe Icons")]
        public static void Export()
        {
            float keepU = HudSheet.U;
            HudSheet.U = 1f;
            try
            {
                Directory.CreateDirectory(OutDir);
                const int w = 2200, h = 1720;
                BouquetMock.Begin(w, h);
                HudSheet._sx = HudSheet._sy = 0;
                HudSheet.Rect(0, 0, w, h, new Color(0.13f, 0.17f, 0.20f));
                HudSheet.Plate(30, 30, w - 60, h - 60, 48, HudArt.Looks.Board, false);
                BouquetMock.Text("CAFE ICONS: COFFEE, PREMIUM COFFEE, CAKE, DIRTY CUP, DELIVERY CRATE", w / 2f, 100, 36, BouquetMock.Cream, 0, 0.06f);

                for (int i = 0; i < Ids.Length; i++)
                {
                    float x = 230 + i * 436;
                    var icon = IconFactory.CafeIcon(Ids[i]).texture;
                    HudSheet.Plate(x - 205, 150, 410, 1040, 40, BouquetMock.Mat(), false);
                    BouquetMock.PutCentred(BouquetArt.Slot(320, BouquetArt.SlotState.Filled), x, 340, 320);
                    BouquetMock.PutCentred(icon, x, 336, 270);
                    BouquetMock.Text(Names[i], x, 540, Names[i].Length > 10 ? 30 : 38, Color.white, 0, 0.05f);
                    BouquetMock.Text(Notes[i], x, 584, 17, BouquetMock.Cream, 0, 0.03f);

                    BouquetMock.Text("96 PX", x - 85, 650, 18, new Color(1f, 1f, 1f, 0.7f), 0, 0.1f);
                    BouquetMock.Text("40 PX ON A SQUARE", x + 85, 650, 14, new Color(1f, 1f, 1f, 0.7f), 0, 0.06f);
                    BouquetMock.Disc(x - 85, 730, 62, new Color(1f, 0.98f, 0.92f), new Color(0.90f, 0.84f, 0.72f), 3f);
                    BouquetMock.PutCentred(icon, x - 85, 728, 96);
                    BouquetMock.Disc(x + 85, 730, 62, new Color(0.62f, 0.60f, 0.58f), new Color(0.62f, 0.60f, 0.58f), 0f);
                    BouquetMock.PutCentred(icon, x + 85, 728, 40);

                    BouquetMock.Text("ORDER BUBBLE", x, 850, 18, new Color(1f, 1f, 1f, 0.7f), 0, 0.1f);
                    if (i < 4) Bubble(x, 870f, new[] { icon }, i == 0 ? 1f : i == 1 ? 0.7f : i == 2 ? 0.4f : 0.15f);
                    else Bubble(x, 870f, new[] { IconFactory.CafeIcon("crate").texture }, 1f);
                }

                // Two-line orders (the bouquet's multi-line bubble): coffee + cake, premium + cake; and the milk / egg of the supply.
                BouquetMock.Text("TWO-ICON ORDERS (a coffee, sometimes with a cake)", 1100, 1246, 28, BouquetMock.Cream, 0, 0.08f);
                Bubble(700, 1285f, new[] { IconFactory.CafeIcon("coffee").texture, IconFactory.CafeIcon("cake").texture }, 0.8f, 0.5f);
                Bubble(1500, 1285f, new[] { IconFactory.CafeIcon("premium").texture, IconFactory.CafeIcon("cake").texture }, 0.45f, 0.5f);
                BouquetMock.Text("SERVED WHEN BOTH ARE DELIVERED, LIKE THE BOUQUET ORDER", 1100, 1640, 24, new Color(1f, 1f, 1f, 0.7f), 0, 0.05f);

                SaveSheet("cafe-icons.png");
                Debug.Log("CAFEICONS_OK");
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

        internal static void SaveSheet(string file)
        {
            HudSheet._sx = HudSheet._sy = 0;
            var sheet = new Texture2D(HudSheet._w, HudSheet._h, TextureFormat.RGBA32, false);
            sheet.SetPixels(HudSheet._buf);
            sheet.Apply();
            File.WriteAllBytes(OutDir + file, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            Debug.Log("[CafeIconSheet] -> " + OutDir + file);
        }

        /// <summary>The game's order bubble: a white panel with the icon(s) and the patience bar (as HaircutMock's, with one or two lines).</summary>
        private static void Bubble(float cx, float top, Texture2D[] icons, float patience, float widen = 0f)
        {
            float s = 1.6f, w = 200f * s * (1f + widen), h = 176f * s, bottomInset = 26f * s;
            float bodyBottom = top + h - bottomInset, cy = (top + bodyBottom) * 0.5f;
            float tailTop = bodyBottom - 6f;
            var white = new Color(1f, 1f, 1f, 0.97f);
            BouquetMock.Shape((x, y) => Mathf.Min(24f - Mathf.Abs(x - cx) - (y - tailTop) * 0.9f, tailTop + 30f - y),
                cx - 26f, tailTop, cx + 26f, tailTop + 32f, white, white, 4f, 1f);
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, cx, cy, w * 0.5f - 6f, (bodyBottom - top) * 0.5f - 6f, 26f),
                cx - w * 0.5f, top, cx + w * 0.5f, bodyBottom, white, white, 6f, 1f);
            if (icons.Length == 1) BouquetMock.PutCentred(icons[0], cx, cy - 18f, 150f);
            else
                for (int i = 0; i < icons.Length; i++)
                {
                    float ix = cx + (i - (icons.Length - 1) * 0.5f) * 220f;
                    BouquetMock.PutCentred(icons[i], ix, cy - 18f, 140f);
                    BouquetMock.Text("x1", ix + 64f, cy + 30f, 24, new Color(0.25f, 0.2f, 0.18f), 0, 0.03f, null, 0f);
                }
            float ty = top + h * (1f - 0.27f);
            float bw = 118f * (1f + widen);
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, cx, ty - 6f, bw, 10f, 10f), cx - bw - 2f, ty - 18f, cx + bw + 2f, ty + 6f,
                new Color(0.85f, 0.87f, 0.9f), new Color(0.85f, 0.87f, 0.9f), 0f, 1f);
            float fw = 2f * bw * patience;
            BouquetMock.Shape((x, y) => BouquetMock.RBox(x, y, cx - bw + fw * 0.5f, ty - 6f, fw * 0.5f, 10f, 10f), cx - bw, ty - 18f, cx - bw + fw, ty + 6f,
                new Color(0.35f, 0.78f, 0.45f), new Color(0.35f, 0.78f, 0.45f), 0f, 1f);
        }
    }
}
