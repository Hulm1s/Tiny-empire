using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Paints every picture of the bouquet minigame once, here in the editor, into
    /// Resources/Bouquet as PNG textures plus an index (each picture's anchor, and for the gathered
    /// bouquet's layers the rectangle they were cropped to).
    ///
    /// Why: the art is BouquetArt, a software painter, and painting the big bouquet pictures took 3-6 s
    /// each in the editor (the smoke test's "[Bouquet] drew ..." lines) - far too long to do on a phone
    /// while the player waits. Baked, the minigame paints nothing at runtime: it loads the textures and
    /// stacks the bouquet from its layers (BouquetMinigame.BunchView).
    ///
    /// The scene build calls <see cref="EnsureBaked"/>, which only re-bakes when
    /// <see cref="BouquetMinigame.ArtVersion"/> changes (bump it after changing BouquetArt or the
    /// minigame's art list). <c>Tycoon > Bake Bouquet Art</c> forces it.
    /// </summary>
    public static class BouquetBake
    {
        private const string Folder = "Assets/_Project/Resources/" + BouquetMinigame.ArtFolder;
        private static string IndexPath => Folder + "/index.json";

        public static void EnsureBaked()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(IndexPath);
            if (text != null)
            {
                var index = JsonUtility.FromJson<BouquetMinigame.BakedIndex>(text.text);
                if (index != null && index.version == BouquetMinigame.ArtVersion)
                {
                    // The pictures are current; only the import settings may have changed (they live in
                    // code, not in the .meta files, so a change here must reach the existing textures).
                    ApplyImportSettings(index.keys);
                    return;
                }
            }
            Bake();
        }

        [MenuItem("Tycoon/Bake Bouquet Art")]
        public static void Bake()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            string full = Path.GetFullPath(Folder);
            if (Directory.Exists(full))
                foreach (var f in Directory.GetFiles(full)) File.Delete(f);
            Directory.CreateDirectory(full);

            var index = new BouquetMinigame.BakedIndex { version = BouquetMinigame.ArtVersion };
            long bytes = 0;
            BouquetMinigame.BakeSink = (key, tex, anchor, crop) =>
            {
                var rect = new RectInt(0, 0, tex.width, tex.height);
                if (crop) rect = OpaqueRect(tex);
                var output = tex;
                if (crop)
                {
                    output = new Texture2D(Mathf.Max(1, rect.width), Mathf.Max(1, rect.height), TextureFormat.RGBA32, false);
                    if (rect.width > 0 && rect.height > 0)
                        output.SetPixels(tex.GetPixels(rect.x, rect.y, rect.width, rect.height));
                    output.Apply();
                }
                var png = output.EncodeToPNG();
                bytes += png.Length;
                File.WriteAllBytes(Path.Combine(full, key + ".png"), png);
                index.keys.Add(key);
                index.rects.Add(new Vector4(rect.x, rect.y, rect.width, rect.height));
                index.anchors.Add(anchor);
                if (output != tex) Object.DestroyImmediate(output);
                Object.DestroyImmediate(tex);
            };
            try { BouquetMinigame.BakeAll(); }
            finally { BouquetMinigame.BakeSink = null; }

            File.WriteAllText(Path.GetFullPath(IndexPath), JsonUtility.ToJson(index));
            AssetDatabase.Refresh();

            ApplyImportSettings(index.keys);

            Debug.Log($"[BouquetBake] {index.keys.Count} pictures, {bytes / 1024} KB of PNG, in {clock.ElapsedMilliseconds / 1000f:0.0} s");
        }

        /// <summary>
        /// WebGL texture format of the baked pictures: ASTC 6x6, about 3.6 bits a pixel instead of the 32 of
        /// uncompressed RGBA - an 8x saving, 33 MB of pictures become about 4 MB of texture memory. ASTC is what
        /// phones' GPUs (iPhone, Android) take natively; the project's default WebGL format is DXT5, which
        /// iOS does not support, so a phone would decompress it back to full size. The other platforms (the
        /// editor) keep the default compressed format.
        /// </summary>
        private const TextureImporterFormat WebFormat = TextureImporterFormat.ASTC_6x6;

        /// <summary>Small pictures (badges, scissors, sparkles: 256 px or less) get ASTC 4x4 - 8 bits a pixel, still tiny in memory - because a crisp small outline shows 6x6's softness (PSNR 34-36 dB against 41-47 for the big ones).</summary>
        private const TextureImporterFormat WebFormatSmall = TextureImporterFormat.ASTC_4x4;

        private static TextureImporterFormat FormatFor(string key)
        {
            // The PNG header holds the size: 4 bytes width then 4 bytes height at offset 16.
            using (var f = File.OpenRead(Path.GetFullPath($"{Folder}/{key}.png")))
            {
                var h = new byte[24];
                f.Read(h, 0, 24);
                int w = (h[16] << 24) | (h[17] << 16) | (h[18] << 8) | h[19];
                int ht = (h[20] << 24) | (h[21] << 16) | (h[22] << 8) | h[23];
                return Mathf.Max(w, ht) <= 256 ? WebFormatSmall : WebFormat;
            }
        }

        /// <summary>Largest picture is 879 px; nothing is shown bigger than its own pixels (the card never scales above 1).</summary>
        private const int MaxSize = 1024;

        private static void ApplyImportSettings(System.Collections.Generic.List<string> keys)
        {
            bool changed = false;
            foreach (var key in keys)
            {
                var importer = AssetImporter.GetAtPath($"{Folder}/{key}.png") as TextureImporter;
                if (importer == null) continue;
                if (!NeedsImport(importer, key)) continue;
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.wrapMode = TextureWrapMode.Clamp;
                // The bouquet is also shown small (the ORDER header, the result), so it needs mipmaps.
                importer.mipmapEnabled = key.StartsWith("b");
                importer.isReadable = false;
                importer.maxTextureSize = MaxSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "WebGL",
                    overridden = true,
                    maxTextureSize = MaxSize,
                    format = FormatFor(key),
                    textureCompression = TextureImporterCompression.Compressed,
                    compressionQuality = 50,
                });
                importer.ClearPlatformTextureSettings("Standalone");
                importer.SaveAndReimport();
                changed = true;
            }
            if (changed) Debug.Log("[BouquetBake] import settings applied (WebGL ASTC 6x6, small ones 4x4)");
        }

        private static bool NeedsImport(TextureImporter importer, string key)
        {
            var web = importer.GetPlatformTextureSettings("WebGL");
            return !web.overridden || importer.GetPlatformTextureSettings("Standalone").overridden || web.format != FormatFor(key) || web.maxTextureSize != MaxSize
                || importer.textureCompression != TextureImporterCompression.Compressed
                || importer.mipmapEnabled != key.StartsWith("b");
        }

        /// <summary>The smallest rectangle holding every pixel that is not fully transparent, with a pixel to spare.</summary>
        private static RectInt OpaqueRect(Texture2D tex)
        {
            var px = tex.GetPixels32();
            int w = tex.width, h = tex.height;
            int x0 = w, y0 = h, x1 = -1, y1 = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (px[y * w + x].a == 0) continue;
                    if (x < x0) x0 = x;
                    if (x > x1) x1 = x;
                    if (y < y0) y0 = y;
                    if (y > y1) y1 = y;
                }
            if (x1 < 0) return new RectInt(0, 0, 1, 1);
            x0 = Mathf.Max(0, x0 - 1); y0 = Mathf.Max(0, y0 - 1);
            x1 = Mathf.Min(w - 1, x1 + 1); y1 = Mathf.Min(h - 1, y1 + 1);
            return new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }
    }
}
