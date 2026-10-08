using System.IO;
using System.Text;
using Tycoon.UI;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Dumps every icon the game draws into one PNG.
    ///
    /// The icons are generated in code and only ever seen at about forty screen pixels, lying
    /// flat on the ground and turned away from the camera - which is far too small to tell a
    /// good drawing from a broken one. Exporting them at full size is the only way to actually
    /// look at what the drawing code produced, and it costs one menu click.
    /// </summary>
    public static class IconSheet
    {
        private const string OutputPath = "../icon-sheet.png";

        [MenuItem("Tycoon/Export Icon Sheet")]
        public static void Export()
        {
            var actions = (SquareIcon[])System.Enum.GetValues(typeof(SquareIcon));

            var sprites = new System.Collections.Generic.List<Sprite>();
            var names = new System.Collections.Generic.List<string>();

            foreach (var kind in actions)
            {
                var sprite = IconFactory.Action(kind);
                if (sprite == null) continue;
                sprites.Add(sprite);
                names.Add(kind.ToString());
            }

            // The products too: those are drawn by id rather than by enum, so they would
            // otherwise never appear on the sheet.
            foreach (var id in new[] { "egg", "milk", "corn", "hay", "trash", "bread", "apples", "yogurt" })
            {
                var item = ScriptableObject.CreateInstance<Tycoon.Config.ItemDefinition>();
                item.id = id;
                var sprite = IconFactory.For(item);
                Object.DestroyImmediate(item);

                if (sprite == null) continue;
                sprites.Add(sprite);
                names.Add(id);
            }

            if (sprites.Count == 0)
            {
                Debug.LogWarning("[IconSheet] Nothing to export.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            const int cell = 96;
            const int pad = 8;
            int columns = Mathf.Min(6, sprites.Count);
            int rows = Mathf.CeilToInt(sprites.Count / (float)columns);

            int width = columns * (cell + pad) + pad;
            int height = rows * (cell + pad) + pad;

            var sheet = new Texture2D(width, height, TextureFormat.RGBA32, false);

            // A mid grey behind everything: the icons are drawn to sit on the pale wash of an
            // interaction square, and on white or on black they lie about their own contrast.
            var background = new Color32(120, 126, 132, 255);
            var flat = new Color32[width * height];
            for (int i = 0; i < flat.Length; i++) flat[i] = background;
            sheet.SetPixels32(flat);

            for (int i = 0; i < sprites.Count; i++)
            {
                var source = sprites[i].texture;
                int column = i % columns;
                int row = i / columns;

                // Rows run top to bottom on the sheet, but texture rows run bottom to top.
                int originX = pad + column * (cell + pad);
                int originY = height - pad - (row + 1) * cell - row * pad;

                var pixels = source.GetPixels32();
                for (int y = 0; y < source.height && y < cell; y++)
                {
                    for (int x = 0; x < source.width && x < cell; x++)
                    {
                        Color32 over = pixels[y * source.width + x];
                        if (over.a == 0) continue;

                        float a = over.a / 255f;
                        var under = sheet.GetPixel(originX + x, originY + y);
                        sheet.SetPixel(originX + x, originY + y,
                            Color.Lerp(under, new Color32(over.r, over.g, over.b, 255), a));
                    }
                }
            }

            sheet.Apply();

            string path = Path.GetFullPath(OutputPath);
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            var order = new StringBuilder();
            for (int i = 0; i < names.Count; i++)
            {
                if (i > 0) order.Append(i % columns == 0 ? " / " : ", ");
                order.Append(names[i]);
            }

            Debug.Log($"[IconSheet] {sprites.Count} icons -> {path}");
            Debug.Log($"[IconSheet] order: {order}");
            Debug.Log("ICONS_OK");

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
