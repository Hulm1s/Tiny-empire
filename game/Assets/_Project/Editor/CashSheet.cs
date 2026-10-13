using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Exports the physical-cash art to a PNG so it can be judged before it is wired in: one
    /// bundle of bills, the cash pile at each height, the ATM and its square's tile, and the
    /// two-number money plaque (carried cash big, account small with a bank icon) - each drawn
    /// with the real <see cref="CashArt"/> / <see cref="HudArt"/> sprites on the game's grass and
    /// on the dark loading background. It reuses the HUD sheet's canvas and schematic lettering
    /// (see <see cref="HudSheet"/>); judge the shapes and colours, not the letters.
    ///
    /// Run: Tycoon > Export Cash Sheet, or
    /// <c>-executeMethod Tycoon.EditorTools.CashSheet.Export</c>. Writes ../cash-sheet.png.
    /// </summary>
    public static class CashSheet
    {
        private const string OutputPath = "../cash-sheet.png";
        private const float U = HudArt.RuntimeScale;

        [MenuItem("Tycoon/Export Cash Sheet")]
        public static void Export()
        {
            const int gap = 20, panelW = 1000, rowA = 330, rowB = 400, rowC = 380;
            HudSheet._w = gap + panelW + gap + panelW + gap;
            HudSheet._h = gap + rowA + gap + rowB + gap + rowC + gap;
            HudSheet._buf = new Color[HudSheet._w * HudSheet._h];
            for (int i = 0; i < HudSheet._buf.Length; i++) HudSheet._buf[i] = new Color(0.07f, 0.07f, 0.08f);

            for (int col = 0; col < 2; col++)
            {
                var bg = col == 0 ? HudSheet.Grass : HudSheet.Night;
                float x = gap + col * (panelW + gap);
                float yA = gap, yB = gap + rowA + gap, yC = yB + rowB + gap;
                HudSheet._ox = 0; HudSheet._oy = 0;
                HudSheet.Rect(x, yA, panelW, rowA, bg);
                HudSheet.Rect(x, yB, panelW, rowB, bg);
                HudSheet.Rect(x, yC, panelW, rowC, bg);

                // Row A: one bundle and the pile at its four heights.
                HudSheet.BlitPx(CashArt.BundleTexture(1.5f), x + 10, yA + 120, 1f);
                for (int level = 1; level <= CashArt.PileLevels; level++)
                    HudSheet.BlitPx(CashArt.PileTexture(level, 0.9f), x + 190 + (level - 1) * 190, yA + 90, 1f);

                // Row B: the ATM, its square's tile, and the pair as they sit in the shop.
                HudSheet.BlitPx(CashArt.AtmTexture(180, 250, 1.4f), x + 20, yB + 10, 1f);
                HudSheet.BlitPx(CashArt.AtmPadTexture(200, 1.4f), x + 320, yB + 90, 1f);
                HudSheet.BlitPx(CashArt.AtmTexture(180, 250, 0.75f), x + 640, yB + 20, 1f);
                HudSheet.BlitPx(CashArt.AtmPadTexture(200, 0.75f), x + 650, yB + 190, 1f);
                HudSheet.BlitPx(CashArt.PileTexture(3, 0.75f), x + 800, yB + 110, 1f);

                // Row C: the money plaque at the size it has on a 375 x 812 phone.
                HudSheet._ox = x; HudSheet._oy = yC;
                DrawPlaque(10, 20, "$1.2K", "$34.5K");
                DrawPlaque(10, 180, "$0", "$128K");
                DrawPlaque(450, 20, "$87", "$9");
                // The coin that already exists, and the bank disc, for scale.
                HudSheet._ox = 0; HudSheet._oy = 0;
                HudSheet.BlitPx(HudArt.CoinTexture(116, 1.5f), x + 480, yC + 190, 1f);
                HudSheet.BlitPx(CashArt.BankDisc(116, 1.5f, HudArt.Looks.Blue), x + 680, yC + 190, 1f);
            }

            var sheet = new Texture2D(HudSheet._w, HudSheet._h, TextureFormat.RGBA32, false);
            sheet.SetPixels(HudSheet._buf);
            sheet.Apply();
            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            Debug.Log($"[CashSheet] -> {path}");
            Debug.Log("CASHSHEET_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>The plaque with its coin and both numbers, visible top-left at (x, y) mock units.</summary>
        private static void DrawPlaque(float x, float y, string cash, string account)
        {
            HudSheet.Blit(CashArt.PlaqueTexture(U), x - HudArt.Margin, y - HudArt.Margin);
            HudSheet.Blit(HudArt.CoinTexture(92, U), x + CashArt.PlaqueLayout.CoinCentre.x - 46f,
                y + CashArt.PlaqueLayout.CoinCentre.y - 43.5f);
            HudSheet.Text(cash, x + CashArt.PlaqueLayout.CashTextLeft.x, y + CashArt.PlaqueLayout.CashTextLeft.y,
                50, Color.white, -1);
            HudSheet.Text(account, x + CashArt.PlaqueLayout.AccountTextLeft.x, y + CashArt.PlaqueLayout.AccountTextLeft.y,
                26, new Color(0.78f, 0.9f, 1f), -1);
        }
    }
}
