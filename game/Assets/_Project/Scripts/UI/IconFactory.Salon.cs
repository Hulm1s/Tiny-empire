using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// The hairdresser's icons (PROPOSAL, phase 1 - nothing calls <see cref="SalonIcon"/> yet): the four starting
    /// hairstyles - short, bob, long layered and quiff - each a head and shoulders in a teal salon cape, drawn
    /// exactly like the other product icons (the same painter, outline, lit-from-upper-left finish). They are
    /// what a customer asks for in their order bubble and what the haircut minigame shows as the target, so the
    /// minigame art (<see cref="HaircutArt"/>) draws the same head, only bigger.
    ///
    /// A style is told apart by its silhouette and its colour at a glance: short dark brown, bob copper, long
    /// layered blonde, quiff black with a swept front.
    /// </summary>
    public static partial class IconFactory
    {
        /// <summary>The four starting styles, in the order the minigame and the order bubble know them.</summary>
        public static readonly string[] HairIds = { "hair_short", "hair_bob", "hair_long", "hair_quiff" };

        /// <summary>"hair_short", "hair_bob", "hair_long" or "hair_quiff"; null for anything else.</summary>
        public static Sprite SalonIcon(string id)
        {
            int style = System.Array.IndexOf(HairIds, id);
            if (style < 0) return null;
            return Get(id, p => SalonHead(p, style, SalonLook.Clean), Supersample);
        }

        /// <summary>How the hair on the head is: finished, grown out and shaggy, a little messy after the cut, or glossy.</summary>
        internal enum SalonLook { Clean, Raw, Messy, Shiny }

        private static Color HairLit(int style)
        {
            switch (style)
            {
                case 0: return Rgb(0.62f, 0.40f, 0.24f);   // short: brown
                case 1: return Rgb(0.98f, 0.58f, 0.30f);   // bob: copper
                case 2: return Rgb(1.00f, 0.88f, 0.46f);   // long layered: blonde
                default: return Rgb(0.40f, 0.42f, 0.56f);  // quiff: black-blue
            }
        }

        private static Color HairShade(int style)
        {
            switch (style)
            {
                case 0: return Rgb(0.20f, 0.10f, 0.05f);
                case 1: return Rgb(0.62f, 0.22f, 0.08f);
                case 2: return Rgb(0.82f, 0.56f, 0.14f);
                default: return Rgb(0.06f, 0.06f, 0.12f);
            }
        }

        /// <summary>
        /// A head and shoulders in a salon cape with the given style, in the 96-unit icon frame (y up). Used for
        /// the icons and, on a bigger painter, for the minigame's head. <paramref name="look"/> only changes the
        /// hair: Raw grows every hair shape out and frays its edge, Messy adds loose strands, Shiny a glossy streak.
        /// </summary>
        internal static void SalonHead(Painter p, int style, SalonLook look)
        {
            float grow = look == SalonLook.Raw ? 4.2f : 0f;
            // Grown-out hair: the hair behind and beside the head swells (k 1), the fringe only a little (k about a third) so
            // it never covers the eyes, and the edge is gently wavy.
            Painter.Shape Hair(Painter.Shape s, float k = 1f) => look == SalonLook.Raw
                ? (x, y) => s(x, y) + grow * k + 1.1f * k * Mathf.Sin(x * 0.55f + y * 0.35f) * Mathf.Cos(y * 0.5f - x * 0.2f)
                : s;

            var hl = HairLit(style); var hs = HairShade(style);
            var skinL = Rgb(1f, 0.87f, 0.73f); var skinS = Rgb(0.86f, 0.56f, 0.40f);
            var capeL = Rgb(0.46f, 0.92f, 0.86f); var capeS = Rgb(0.08f, 0.50f, 0.52f);

            // ---- the hair behind the head ----------------------------------------------------------
            Painter.Shape back;
            switch (style)
            {
                case 0: back = (x, y) => Sd.Ellipse(x, y, 48f, 59f, 25.5f, 24f); break;
                case 1: back = (x, y) => Sd.Box(x, y, 48f, 56f, 29.5f, 29f, 13f); break;
                case 2: back = (x, y) => Sd.Smooth(Sd.Box(x, y, 48f, 40f, 30f, 44f, 13f), Sd.Ellipse(x, y, 48f, 62f, 28f, 26f), 6f); break;
                default: back = (x, y) => Sd.Ellipse(x, y, 48f, 59f, 24.5f, 23f); break;
            }
            var backShape = Hair(back);
            p.Blob(backShape, hl, hs, outline: 3f, inflate: 12f, spec: 0.45f);

            // ---- neck and cape -----------------------------------------------------------------------
            p.Blob((x, y) => Sd.Box(x, y, 48f, 30f, 7.5f, 10f, 3f), skinL, skinS, outline: 2.6f, inflate: 6f, spec: 0.2f);
            Painter.Shape cape = (x, y) => Sd.Box(x, y, 48f, 16.5f, 34f, 11.5f, 9f);
            p.Blob(cape, capeL, capeS, outline: 3f, inflate: 11f, spec: 0.4f);
            p.Fill((x, y) => Sd.Capsule(x, y, 36f, 23f, 48f, 17f, 1.6f), Rgb(1f, 1f, 1f, 0.55f), cape, 0.6f);
            p.Fill((x, y) => Sd.Capsule(x, y, 60f, 23f, 48f, 17f, 1.6f), Rgb(1f, 1f, 1f, 0.55f), cape, 0.6f);
            p.Fill((x, y) => Sd.Capsule(x, y, 24f, 18f, 30f, 14f, 2.4f), Rgb(1f, 1f, 1f, 0.45f), cape, 1.2f);

            // Long hair falls in front of the shoulders.
            if (style == 2)
            {
                Painter.Shape lockL = Hair((x, y) => Sd.Capsule(x, y, 25f, 60f, 22f, 12f, 6.2f));
                Painter.Shape lockR = Hair((x, y) => Sd.Capsule(x, y, 71f, 60f, 74f, 12f, 6.2f));
                p.Blob(lockL, hl, hs, outline: 2.8f, inflate: 6f, spec: 0.4f);
                p.Blob(lockR, hl, hs, outline: 2.8f, inflate: 6f, spec: 0.4f);
                // Layers: lighter streaks across the length.
                p.Fill((x, y) => Sd.Capsule(x, y, 24.5f, 52f, 22.5f, 30f, 1.3f), Rgb(1f, 1f, 0.8f, 0.55f), lockL, 0.8f);
                p.Fill((x, y) => Sd.Capsule(x, y, 71.5f, 52f, 73.5f, 30f, 1.3f), Rgb(1f, 1f, 0.8f, 0.55f), lockR, 0.8f);
                p.Fill((x, y) => Sd.Capsule(x, y, 20f, 24f, 28f, 22f, 1.2f), hs, lockL, 0.6f);
                p.Fill((x, y) => Sd.Capsule(x, y, 68f, 22f, 76f, 24f, 1.2f), hs, lockR, 0.6f);
            }

            // ---- the face ----------------------------------------------------------------------------
            Painter.Shape face = (x, y) => Sd.Egg(x, y, 48f, 49f, 21.5f, 27f, 0.20f, 180f);
            p.Blob(face, skinL, skinS, outline: 3f, inflate: 16f, spec: 0.3f);
            p.Fill((x, y) => Sd.Circle(x, y, 33.5f, 38f, 4.2f), Rgb(1f, 0.55f, 0.55f, 0.45f), face, 3f);
            p.Fill((x, y) => Sd.Circle(x, y, 62.5f, 38f, 4.2f), Rgb(1f, 0.55f, 0.55f, 0.45f), face, 3f);
            var ink = Rgb(0.17f, 0.09f, 0.05f);
            p.Fill((x, y) => Mathf.Max(Sd.Ellipse(x, y, 40f, 45f, 2.5f, 3.1f), Sd.Ellipse(x, y, 56f, 45f, 2.5f, 3.1f)), ink, null, 0.6f);
            p.Fill((x, y) => Mathf.Max(Sd.Circle(x, y, 40.8f, 46.3f, 0.9f), Sd.Circle(x, y, 56.8f, 46.3f, 0.9f)), Rgb(1f, 1f, 1f, 0.9f), null, 0.4f);
            p.Fill((x, y) => Mathf.Max(Sd.Capsule(x, y, 43.5f, 35.5f, 48f, 33.2f, 1.25f), Sd.Capsule(x, y, 48f, 33.2f, 52.5f, 35.5f, 1.25f)),
                Rgb(0.55f, 0.20f, 0.18f), null, 0.5f);

            // ---- the hair in front of the face -------------------------------------------------------
            switch (style)
            {
                case 0: // short: a cropped cap with a rounded hairline and sideburns
                {
                    var cap = Hair((x, y) => Sd.Sub(Sd.Ellipse(x, y, 48f, 62f, 24.5f, 21f), Sd.Circle(x, y, 48f, 40.5f, 22.5f)), 0.35f);
                    p.Blob(cap, hl, hs, outline: 3f, inflate: 8f, spec: 0.5f);
                    var burnL = Hair((x, y) => Sd.Box(x, y, 26.5f, 52f, 3.2f, 8f, 1.6f), 0.5f);
                    var burnR = Hair((x, y) => Sd.Box(x, y, 69.5f, 52f, 3.2f, 8f, 1.6f), 0.5f);
                    p.Blob(burnL, hl, hs, outline: 2f, inflate: 3f, spec: 0.3f);
                    p.Blob(burnR, hl, hs, outline: 2f, inflate: 3f, spec: 0.3f);
                    p.Fill((x, y) => Sd.Capsule(x, y, 34f, 71f, 48f, 76f, 1.6f), Rgb(1f, 1f, 1f, 0.5f), cap, 1f);
                    break;
                }
                case 1: // bob: a straight fringe and the sides coming forward to the chin, one piece
                {
                    var frame = Hair((x, y) => Mathf.Max(Sd.Box(x, y, 48f, 66f, 24f, 11f, 5f), Sd.Box(x, y, 24.5f, 46f, 4.6f, 17f, 3f),
                        Sd.Box(x, y, 71.5f, 46f, 4.6f, 17f, 3f)), 0.35f);
                    p.Blob(frame, hl, hs, outline: 3f, inflate: 8f, spec: 0.5f);
                    p.Fill((x, y) => Sd.Capsule(x, y, 34f, 69f, 44f, 69f, 1.4f), Rgb(1f, 1f, 1f, 0.55f), frame, 0.8f);
                    p.Fill((x, y) => Sd.Capsule(x, y, 24.5f, 52f, 24.5f, 36f, 1.0f), Rgb(1f, 1f, 1f, 0.35f), frame, 0.8f);
                    break;
                }
                case 2: // long layered: a side parting sweeping over one brow
                {
                    var sweep = Hair((x, y) => Sd.Sub(Sd.Ellipse(x, y, 48f, 63f, 25.5f, 22f), Sd.Ellipse(x, y, 62f, 40f, 25f, 23f)), 0.35f);
                    p.Blob(sweep, hl, hs, outline: 3f, inflate: 9f, spec: 0.5f);
                    p.Fill((x, y) => Sd.Capsule(x, y, 30f, 62f, 50f, 78f, 1.4f), Rgb(1f, 1f, 0.82f, 0.6f), sweep, 0.9f);
                    p.Fill((x, y) => Sd.Capsule(x, y, 28f, 56f, 40f, 66f, 1.1f), hs, sweep, 0.6f);
                    break;
                }
                default: // quiff: short sides and a big swept-up front
                {
                    var cap = Hair((x, y) => Sd.Sub(Sd.Ellipse(x, y, 48f, 62f, 24f, 20f), Sd.Circle(x, y, 48f, 40.5f, 22.5f)), 0.35f);
                    p.Blob(cap, hl, hs, outline: 3f, inflate: 8f, spec: 0.5f);
                    var quiff = Hair((x, y) => Sd.Smooth(Sd.Egg(x, y, 55f, 75f, 20.5f, 15f, 0.35f, -20f), Sd.Ellipse(x, y, 40f, 69f, 12f, 8f, -25f), 5f), 0.5f);
                    p.Blob(quiff, hl, hs, outline: 3f, inflate: 12f, spec: 0.6f);
                    p.Fill((x, y) => Sd.Capsule(x, y, 44f, 78f, 62f, 82f, 1.6f), Rgb(1f, 1f, 1f, 0.55f), quiff, 1f);
                    p.Fill((x, y) => Sd.Capsule(x, y, 40f, 72f, 56f, 77f, 1.0f), hs, quiff, 0.6f);
                    break;
                }
            }

            if (look == SalonLook.Messy)
            {
                // A few short flyaway strands standing out of the cut, in the hair's own colour.
                p.Fill((x, y) => Mathf.Max(Sd.Capsule(x, y, 31f, 76f, 26f, 82f, 1.0f), Sd.Capsule(x, y, 62f, 80f, 67f, 85f, 1.0f),
                    Sd.Capsule(x, y, 76f, 55f, 82f, 60f, 1.0f), Sd.Capsule(x, y, 44f, 82f, 41f, 88f, 1.0f),
                    Sd.Capsule(x, y, 20f, 52f, 14f, 55f, 1.0f)), Color.Lerp(hl, hs, 0.35f), null, 0.4f);
            }
            if (look == SalonLook.Shiny)
            {
                p.Fill((x, y) => Sd.Ellipse(x, y, 38f, 70f, 9f, 3f, 20f), Rgb(1f, 1f, 1f, 0.8f), null, 1.5f);
                p.Fill((x, y) => Sd.Ellipse(x, y, 60f, 66f, 5f, 2f, -30f), Rgb(1f, 1f, 1f, 0.6f), null, 1.2f);
            }
            p.GroundShadow();
        }

        /// <summary>A comb: a teal spine with a row of teeth, tilted. For the minigame's combing step.</summary>
        internal static void DrawComb(Painter p)
        {
            var spineL = Rgb(0.46f, 0.92f, 0.86f); var spineS = Rgb(0.08f, 0.50f, 0.52f);
            Painter.Shape spine = (x, y) => Sd.Box(x, y, 48f, 60f, 38f, 9f, 5f, -12f);
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                float bx = 14f + t * 68f, by = 66f - t * 14.5f;
                p.Blob((x, y) => Sd.Box(x, y, bx, by - 17f, 1.9f, 15f, 1.2f, -12f), Rgb(0.98f, 0.98f, 1f), Rgb(0.62f, 0.72f, 0.82f), outline: 1.4f, inflate: 1.6f, spec: 0.3f);
            }
            p.Blob(spine, spineL, spineS, outline: 3f, inflate: 8f, spec: 0.55f);
            p.Fill((x, y) => Sd.Capsule(x, y, 18f, 63f, 52f, 69f, 1.5f), Rgb(1f, 1f, 1f, 0.65f), spine, 1f);
            p.GroundShadow();
        }

        /// <summary>A hairdryer, nozzle to the left, with wind lines: for the blow-dry step.</summary>
        internal static void DrawDryer(Painter p)
        {
            var redL = Rgb(1f, 0.52f, 0.46f); var redS = Rgb(0.72f, 0.12f, 0.14f);
            // Wind first, so the dryer is drawn over it.
            for (int i = 0; i < 3; i++)
            {
                float yy = 50f + i * 10f - 8f;
                p.Fill((x, y) => Sd.Capsule(x, y, 6f, yy, 20f - i * 3f, yy + (i - 1) * 1.5f, 1.8f), Rgb(0.86f, 0.96f, 1f, 0.9f), null, 0.6f);
            }
            Painter.Shape handle = (x, y) => Sd.Box(x, y, 64f, 30f, 7.5f, 19f, 5f, 14f);
            p.Blob(handle, Rgb(0.30f, 0.34f, 0.46f), Rgb(0.10f, 0.12f, 0.20f), outline: 3f, inflate: 6f, spec: 0.4f);
            Painter.Shape body = (x, y) => Sd.Smooth(Sd.Ellipse(x, y, 58f, 62f, 28f, 15f, 0f), Sd.Box(x, y, 30f, 62f, 12f, 11f, 4f), 8f);
            p.Blob(body, redL, redS, outline: 3.2f, inflate: 12f, spec: 0.6f);
            p.Blob((x, y) => Sd.Box(x, y, 24f, 62f, 5f, 13f, 3f), Rgb(0.62f, 0.66f, 0.74f), Rgb(0.30f, 0.34f, 0.44f), outline: 2.2f, inflate: 4f, spec: 0.5f);
            p.Fill((x, y) => Sd.Capsule(x, y, 48f, 69f, 74f, 70f, 1.6f), Rgb(1f, 1f, 1f, 0.6f), body, 1f);
            p.GroundShadow();
        }

        /// <summary>The hair bag the sweep square gives: a cream sack tied with a teal ribbon, a tuft of hair out of the top.</summary>
        private static void DrawHairBag(Painter p)
        {
            Painter.Shape sack = (x, y) => Sd.Smooth(Sd.Box(x, y, 48f, 36f, 27f, 25f, 13f), Sd.Box(x, y, 48f, 62f, 14f, 8f, 5f), 9f);
            // The tuft first, so the sack's mouth is drawn over its roots.
            var hair = Rgb(0.62f, 0.40f, 0.24f); var hairS = Rgb(0.20f, 0.10f, 0.05f);
            p.Blob((x, y) => Mathf.Max(Sd.Capsule(x, y, 46f, 64f, 36f, 86f, 4f), Sd.Capsule(x, y, 50f, 64f, 52f, 88f, 4f),
                Sd.Capsule(x, y, 54f, 64f, 66f, 84f, 4f), Sd.Capsule(x, y, 48f, 64f, 46f, 90f, 3.4f)), hair, hairS, outline: 2f, inflate: 4f, spec: 0.3f);
            p.Blob(sack, Rgb(1f, 0.95f, 0.80f), Rgb(0.78f, 0.62f, 0.38f), outline: 3.4f, inflate: 16f, spec: 0.3f);
            p.Blob((x, y) => Sd.Box(x, y, 48f, 59f, 15f, 4.6f, 2.4f), Rgb(0.46f, 0.92f, 0.86f), Rgb(0.08f, 0.50f, 0.52f), outline: 2f, inflate: 4f, spec: 0.5f, clip: sack);
            p.Blob((x, y) => Sd.Box(x, y, 48f, 33f, 13f, 10f, 4f), Rgb(0.74f, 0.94f, 0.89f), Rgb(0.40f, 0.72f, 0.66f), outline: 1.6f, inflate: 5f, spec: 0.2f, clip: sack);
            p.Fill((x, y) => Mathf.Max(Sd.Circle(x, y, 43f, 33f, 3.1f), Sd.Circle(x, y, 53f, 33f, 3.1f)), Rgb(0.92f, 0.28f, 0.28f), null, 0.5f);
            p.GroundShadow();
        }
    }
}
