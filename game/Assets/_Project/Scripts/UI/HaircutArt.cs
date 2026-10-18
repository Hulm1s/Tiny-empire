using System.Collections.Generic;
using UnityEngine;
using Painter = Tycoon.UI.IconFactory.Painter;
using Sd = Tycoon.UI.IconFactory.Sd;

namespace Tycoon.UI
{
    /// <summary>
    /// The 2D art of the hairdresser's haircut minigame (PROPOSAL, phase 1 - nothing runs it yet): the head of a
    /// customer with each of the four starting hairstyles in four states (finished, grown out, messy after the
    /// cut, glossy), the comb and the hairdryer, a clump of cut hair, and the dashed lines to swipe along for each
    /// style. It is the same drawing as the hairstyle icons (<see cref="IconFactory.SalonIcon"/>) - the head the
    /// player sees as the target is the head on the customer's order bubble - only on a bigger canvas, so
    /// like <see cref="BouquetArt"/> every call returns a plain y-up Texture2D at the size asked for.
    /// The gesture art (scissors, hand, arrows, stars) is <see cref="BouquetArt"/>'s own and is reused as it is.
    ///
    /// <c>Editor/HaircutMock.cs</c> lays it all out as the screens of the minigame for review.
    /// </summary>
    public static class HaircutArt
    {
        public enum Style { Short, Bob, Long, Quiff }

        /// <summary>Finished style / grown out and shaggy / a little messy after cutting / glossy.</summary>
        public enum HairLook { Clean, Raw, Messy, Shiny }

        public static readonly Style[] All = { Style.Short, Style.Bob, Style.Long, Style.Quiff };
        public static readonly string[] Names = { "SHORT", "BOB", "LAYERS", "QUIFF" };

        private const int SS = 3;

        public static string Id(Style s) => IconFactory.HairIds[(int)s];

        /// <summary>A customer's head and shoulders, <paramref name="px"/> square (rounded up to a whole multiple of 96).</summary>
        public static Texture2D Head(Style style, int px, HairLook look = HairLook.Clean)
        {
            int keep = Mathf.Max(1, Mathf.CeilToInt(px / 96f));
            var p = new Painter(96, keep * SS);
            IconFactory.SalonHead(p, (int)style, (IconFactory.SalonLook)(int)look);
            return p.ToTexture($"Haircut_{Id(style)}_{look}", keep);
        }

        public static Texture2D Comb(int px)
        {
            int keep = Mathf.Max(1, Mathf.CeilToInt(px / 96f));
            var p = new Painter(96, keep * SS);
            IconFactory.DrawComb(p);
            return p.ToTexture("Haircut_comb", keep);
        }

        public static Texture2D Dryer(int px)
        {
            int keep = Mathf.Max(1, Mathf.CeilToInt(px / 96f));
            var p = new Painter(96, keep * SS);
            IconFactory.DrawDryer(p);
            return p.ToTexture("Haircut_dryer", keep);
        }

        /// <summary>A little tuft of cut hair in the style's colour, falling away from the scissors.</summary>
        public static Texture2D Clump(Style style, int px, float angle)
        {
            int keep = Mathf.Max(1, Mathf.CeilToInt(px / 48f));
            var p = new Painter(48, keep * SS);
            Color l = style == Style.Short ? IconFactory.Rgb(0.62f, 0.40f, 0.24f)
                : style == Style.Bob ? IconFactory.Rgb(0.98f, 0.58f, 0.30f)
                : style == Style.Long ? IconFactory.Rgb(1.00f, 0.88f, 0.46f) : IconFactory.Rgb(0.40f, 0.42f, 0.56f);
            Color s = style == Style.Short ? IconFactory.Rgb(0.20f, 0.10f, 0.05f)
                : style == Style.Bob ? IconFactory.Rgb(0.62f, 0.22f, 0.08f)
                : style == Style.Long ? IconFactory.Rgb(0.82f, 0.56f, 0.14f) : IconFactory.Rgb(0.06f, 0.06f, 0.12f);
            Painter.Shape tuft = (x, y) => Mathf.Max(
                Sd.Egg(x, y, 24f, 24f, 8.5f, 15f, 0.5f, angle),
                Sd.Egg(x, y, 20f, 22f, 6.5f, 12f, 0.5f, angle + 28f),
                Sd.Egg(x, y, 28f, 22f, 6.5f, 12f, 0.5f, angle - 26f));
            p.Blob(tuft, l, s, outline: 1.8f, inflate: 4.5f, spec: 0.35f);
            p.Fill((x, y) => Sd.Capsule(x, y, 22f, 30f, 25f, 20f, 1.0f), IconFactory.Rgb(1f, 1f, 1f, 0.45f), tuft, 0.6f);
            return p.ToTexture("Haircut_clump", keep);
        }

        /// <summary>
        /// The lines to swipe along to cut a style, in the head's own 96-unit frame (y up). Several short lines
        /// rather than one outline: each is one swipe, done in order.
        /// </summary>
        public static List<Vector2[]> Guides(Style style)
        {
            var g = new List<Vector2[]>();
            switch (style)
            {
                case Style.Short:
                    g.Add(new[] { new Vector2(21f, 46f), new Vector2(21.5f, 60f) });
                    g.Add(Arc(48f, 59f, 26f, 25f, 168f, 12f));
                    g.Add(new[] { new Vector2(74.5f, 60f), new Vector2(75f, 46f) });
                    break;
                case Style.Bob:
                    g.Add(new[] { new Vector2(18.5f, 72f), new Vector2(18.5f, 32f) });
                    g.Add(new[] { new Vector2(22f, 26.5f), new Vector2(74f, 26.5f) });
                    g.Add(new[] { new Vector2(77.5f, 32f), new Vector2(77.5f, 72f) });
                    break;
                case Style.Long:
                    g.Add(new[] { new Vector2(17f, 32f), new Vector2(48f, 24f), new Vector2(79f, 32f) });
                    g.Add(new[] { new Vector2(17f, 14f), new Vector2(48f, 6f), new Vector2(79f, 14f) });
                    break;
                default:
                    g.Add(new[] { new Vector2(23f, 46f), new Vector2(23f, 62f) });
                    g.Add(new[] { new Vector2(73f, 62f), new Vector2(73f, 46f) });
                    g.Add(new[] { new Vector2(35f, 68f), new Vector2(50f, 87f), new Vector2(72f, 80f) });
                    break;
            }
            return g;
        }

        /// <summary>Points round an ellipse, from angle a0 to a1 (degrees, anticlockwise from +x, either way round).</summary>
        private static Vector2[] Arc(float cx, float cy, float rx, float ry, float a0, float a1, int n = 14)
        {
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = Mathf.Lerp(a0, a1, i / (float)n) * Mathf.Deg2Rad;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry);
            }
            return pts;
        }
    }
}
