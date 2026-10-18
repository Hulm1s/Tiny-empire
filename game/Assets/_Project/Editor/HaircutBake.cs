using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Paints every picture of the haircut minigame once, here in the editor, into Resources/Haircut as PNG textures
    /// plus an index (each picture's anchor), exactly as <see cref="BouquetBake"/> does for the bouquet: the art is a
    /// software painter (<see cref="HaircutArt"/>), far too slow to run on a phone while the player waits, so the
    /// minigame only loads the textures. The WebGL import settings (ASTC) are the bouquet's, applied by the same code.
    ///
    /// The scene build calls <see cref="EnsureBaked"/>, which only re-bakes when <see cref="HaircutMinigame.ArtVersion"/>
    /// changes (bump it after changing HaircutArt, the hair icons or the minigame's art list).
    /// <c>Tycoon > Bake Haircut Art</c> forces it.
    /// </summary>
    public static class HaircutBake
    {
        private const string Folder = "Assets/_Project/Resources/" + HaircutMinigame.ArtFolder;
        private static string IndexPath => Folder + "/index.json";

        public static void EnsureBaked()
        {
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(IndexPath);
            if (text != null)
            {
                var index = JsonUtility.FromJson<BouquetMinigame.BakedIndex>(text.text);
                if (index != null && index.version == HaircutMinigame.ArtVersion)
                {
                    BouquetBake.ApplyImportSettings(Folder, index.keys, key => true);
                    return;
                }
            }
            Bake();
        }

        [MenuItem("Tycoon/Bake Haircut Art")]
        public static void Bake()
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            string full = Path.GetFullPath(Folder);
            if (Directory.Exists(full))
                foreach (var f in Directory.GetFiles(full)) File.Delete(f);
            Directory.CreateDirectory(full);

            var index = new BouquetMinigame.BakedIndex { version = HaircutMinigame.ArtVersion };
            long bytes = 0;
            HaircutMinigame.BakeSink = (key, tex, anchor) =>
            {
                var png = tex.EncodeToPNG();
                bytes += png.Length;
                File.WriteAllBytes(Path.Combine(full, key + ".png"), png);
                index.keys.Add(key);
                index.rects.Add(new Vector4(0f, 0f, tex.width, tex.height));
                index.anchors.Add(anchor);
                Object.DestroyImmediate(tex);
            };
            try { HaircutMinigame.BakeAll(); }
            finally { HaircutMinigame.BakeSink = null; }

            File.WriteAllText(Path.GetFullPath(IndexPath), JsonUtility.ToJson(index));
            AssetDatabase.Refresh();
            BouquetBake.ApplyImportSettings(Folder, index.keys, key => true);

            Debug.Log($"[HaircutBake] {index.keys.Count} pictures, {bytes / 1024} KB of PNG, in {clock.ElapsedMilliseconds / 1000f:0.0} s");
        }
    }
}
