using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Paints every picture of the latte-art minigame once, here in the editor, into Resources/Latte as PNG textures
    /// plus an index (each picture's anchor), exactly as <see cref="BouquetBake"/> does for the bouquet: the art is a
    /// software painter (<see cref="LatteArt"/>), far too slow to run on a phone while the player waits, so the
    /// minigame only loads the textures. The WebGL import settings (ASTC) are the bouquet's, applied by the same code.
    ///
    /// The scene build calls <see cref="EnsureBaked"/>, which only re-bakes when <see cref="LatteArt.ArtVersion"/>
    /// changes (bump it after changing LatteArt, the hair icons or the minigame's art list).
    /// <c>Tycoon > Bake Latte Art</c> forces it.
    /// </summary>
    public static class LatteBake
    {
        private const string Folder = "Assets/_Project/Resources/" + LatteArt.ArtFolder;
        private static string IndexPath => Folder + "/index.json";

        public static void EnsureBaked()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(IndexPath);
            if (text != null)
            {
                var index = JsonUtility.FromJson<BouquetMinigame.BakedIndex>(text.text);
                if (index != null && index.version == LatteArt.ArtVersion)
                {
                    BouquetBake.ApplyImportSettings(Folder, index.keys, key => true);
                    return;
                }
            }
            Bake();
        }

        [MenuItem("Tycoon/Bake Latte Art")]
        public static void Bake()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            string full = Path.GetFullPath(Folder);
            if (Directory.Exists(full))
                foreach (var f in Directory.GetFiles(full)) File.Delete(f);
            Directory.CreateDirectory(full);

            var index = new BouquetMinigame.BakedIndex { version = LatteArt.ArtVersion };
            long bytes = 0;
            LatteArt.BakeSink = (key, tex, anchor) =>
            {
                var png = tex.EncodeToPNG();
                bytes += png.Length;
                File.WriteAllBytes(Path.Combine(full, key + ".png"), png);
                index.keys.Add(key);
                index.rects.Add(new Vector4(0f, 0f, tex.width, tex.height));
                index.anchors.Add(anchor);
                Object.DestroyImmediate(tex);
            };
            try { LatteArt.BakeAll(); }
            finally { LatteArt.BakeSink = null; }

            File.WriteAllText(Path.GetFullPath(IndexPath), JsonUtility.ToJson(index));
            AssetDatabase.Refresh();
            BouquetBake.ApplyImportSettings(Folder, index.keys, key => true);

            Debug.Log($"[LatteBake] {index.keys.Count} pictures, {bytes / 1024} KB of PNG, in {clock.ElapsedMilliseconds / 1000f:0.0} s");
        }
    }
}
