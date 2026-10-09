using UnityEngine;
using UnityEngine.UI;

namespace Tycoon.UI
{
    /// <summary>
    /// Adds tracking (extra space between letters) to a legacy <see cref="Text"/>, which has
    /// no letter-spacing setting of its own. The built-in font runs bold capitals together, so
    /// button labels use this.
    ///
    /// It moves each character's quad sideways by a growing amount, then re-centres or
    /// right-aligns the run to match the Text's alignment. Add it BEFORE any
    /// <see cref="Outline"/> on the same object: the outline copies whatever vertices it is
    /// given, so spacing must already be applied, or the outline stays behind the letters.
    /// Plain text only (no rich-text tags), one line per label.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class LetterSpacing : BaseMeshEffect
    {
        [Tooltip("Extra space after every letter, as a fraction of the font size.")]
        public float tracking = 0.12f;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount < 8) return;

            var text = graphic as Text;
            int letters = vh.currentVertCount / 4;
            float step = tracking * (text != null ? text.fontSize : 30);

            // Where the run sits within its box decides which way the added width is shared out.
            float anchor = 0.5f;
            if (text != null)
            {
                var a = text.alignment;
                if (a == TextAnchor.UpperLeft || a == TextAnchor.MiddleLeft || a == TextAnchor.LowerLeft) anchor = 0f;
                else if (a == TextAnchor.UpperRight || a == TextAnchor.MiddleRight || a == TextAnchor.LowerRight) anchor = 1f;
            }
            float shift = -step * (letters - 1) * anchor;

            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position.x += step * (i / 4) + shift;
                vh.SetUIVertex(v, i);
            }
        }
    }
}
