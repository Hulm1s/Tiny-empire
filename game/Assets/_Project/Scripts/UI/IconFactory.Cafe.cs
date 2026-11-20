using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// The café's icons (PROPOSAL, phase 1 - nothing calls <see cref="CafeIcon"/> yet): a coffee, a premium
    /// (latte-art) coffee, a slice of cake, a dirty cup and the delivery crate (milk bottle + eggs). Drawn exactly
    /// like the other product icons (same Painter, same dark outline, vertical gradient, soft highlight) and
    /// asked for by name, like <see cref="GardenIcon"/>. When approved they become the icons of the café's
    /// items (<c>For()</c> / <c>HasArtwork()</c>) in phase 2.
    /// </summary>
    public static partial class IconFactory
    {
        /// <summary>"coffee", "premium", "cake", "dirty_cup" or "crate"; null for anything else.</summary>
        public static Sprite CafeIcon(string id)
        {
            switch (id)
            {
                case "coffee": return Get("cafe_coffee", DrawCoffee, Supersample);
                case "premium": return Get("cafe_premium", DrawPremiumCoffee, Supersample);
                case "cake": return Get("cafe_cake", DrawCakeSlice, Supersample);
                case "dirty_cup": return Get("cafe_dirty_cup", DrawDirtyCup, Supersample);
                case "crate": return Get("cafe_crate", DrawSupplyCrate, Supersample);
                default: return null;
            }
        }

        private static readonly Vector2[] CupBody =
            { new Vector2(30f, 52f), new Vector2(66f, 52f), new Vector2(60f, 22f), new Vector2(36f, 22f) };

        private static void Saucer(Painter p, Color lit, Color shade)
        {
            p.Blob((x, y) => Sd.Ellipse(x, y, 48f, 16f, 38f, 9f), lit, shade, outline: 3f, inflate: 7f, spec: 0.4f);
            p.Fill((x, y) => Sd.Ellipse(x, y, 48f, 18f, 22f, 4f), new Color(0f, 0f, 0f, 0.13f), null, 2f);
        }

        /// <summary>A white cup on a saucer, black coffee, a terracotta band and a curl of steam.</summary>
        private static void DrawCoffee(Painter p)
        {
            Saucer(p, Rgb(1f, 1f, 0.98f), Rgb(0.72f, 0.78f, 0.88f));

            // Handle first, so the body covers its inner end.
            p.Blob((x, y) => Sd.Sub(Sd.Circle(x, y, 71f, 38f, 12.5f), Sd.Circle(x, y, 71f, 38f, 6.2f)),
                Rgb(1f, 1f, 0.98f), Rgb(0.74f, 0.78f, 0.88f), outline: 2.6f, inflate: 5f, spec: 0.35f);

            Painter.Shape cup = (x, y) => Sd.Poly(x, y, CupBody, 6f);
            p.Blob(cup, Rgb(1f, 1f, 0.98f), Rgb(0.74f, 0.78f, 0.90f), outline: 3.2f, inflate: 15f, spec: 0.45f);

            p.Blob((x, y) => Sd.Box(x, y, 48f, 33f, 40f, 3.6f, 1.5f),
                Rgb(0.96f, 0.58f, 0.34f), Rgb(0.74f, 0.34f, 0.16f), outline: 1.8f, inflate: 5f, spec: 0.3f, clip: cup);

            // The rim and the coffee in it.
            p.Blob((x, y) => Sd.Ellipse(x, y, 48f, 56f, 24f, 7f), Rgb(1f, 1f, 0.98f), Rgb(0.80f, 0.84f, 0.92f),
                outline: 2.8f, inflate: 5f, spec: 0.3f);
            p.Blob((x, y) => Sd.Ellipse(x, y, 48f, 56.5f, 20.5f, 4.8f), Rgb(0.50f, 0.28f, 0.14f), Rgb(0.20f, 0.09f, 0.04f),
                outline: 0f, inflate: 3f, spec: 0.5f);

            // Steam.
            p.Fill((x, y) => Mathf.Max(Sd.Capsule(x, y, 40f, 66f, 36f, 74f, 2.2f), Sd.Capsule(x, y, 36f, 74f, 41f, 83f, 2.2f)),
                Rgb(1f, 1f, 1f, 0.92f), null, 1f);
            p.Fill((x, y) => Mathf.Max(Sd.Capsule(x, y, 54f, 66f, 58f, 73f, 2.2f), Sd.Capsule(x, y, 58f, 73f, 53f, 81f, 2.2f)),
                Rgb(1f, 1f, 1f, 0.92f), null, 1f);

            p.Fill((x, y) => Sd.Capsule(x, y, 34.5f, 28f, 36.5f, 46f, 2.4f), Rgb(1f, 1f, 1f, 0.85f), cup, 1.5f);
            p.GroundShadow();
        }

        /// <summary>A wide cup seen from higher up: latte art (a white heart) on the foam, a gold band and saucer, a sparkle.</summary>
        private static void DrawPremiumCoffee(Painter p)
        {
            Saucer(p, Rgb(1f, 0.93f, 0.62f), Rgb(0.78f, 0.52f, 0.14f));

            p.Blob((x, y) => Sd.Sub(Sd.Circle(x, y, 74f, 40f, 12f), Sd.Circle(x, y, 74f, 40f, 6f)),
                Rgb(1f, 1f, 0.98f), Rgb(0.74f, 0.78f, 0.88f), outline: 2.6f, inflate: 5f, spec: 0.35f);

            var body = new[] { new Vector2(27f, 50f), new Vector2(69f, 50f), new Vector2(62f, 22f), new Vector2(34f, 22f) };
            Painter.Shape cup = (x, y) => Sd.Poly(x, y, body, 6f);
            p.Blob(cup, Rgb(1f, 1f, 0.98f), Rgb(0.74f, 0.78f, 0.90f), outline: 3.2f, inflate: 15f, spec: 0.45f);
            p.Blob((x, y) => Sd.Box(x, y, 48f, 33f, 40f, 3.2f, 1.5f),
                Rgb(1f, 0.90f, 0.46f), Rgb(0.80f, 0.54f, 0.10f), outline: 1.8f, inflate: 4f, spec: 0.5f, clip: cup);

            p.Blob((x, y) => Sd.Ellipse(x, y, 48f, 54f, 27f, 12.5f), Rgb(1f, 1f, 0.98f), Rgb(0.80f, 0.84f, 0.92f),
                outline: 2.8f, inflate: 6f, spec: 0.3f);
            Painter.Shape foam = (x, y) => Sd.Ellipse(x, y, 48f, 54.5f, 23.5f, 10f);
            p.Blob(foam, Rgb(0.80f, 0.52f, 0.30f), Rgb(0.38f, 0.18f, 0.08f), outline: 0f, inflate: 4f, spec: 0.3f);

            // The heart, drawn in white milk foam.
            Painter.Shape heart = (x, y) => Mathf.Max(
                Mathf.Max(Sd.Circle(x, y, 42.5f, 57.5f, 5.2f), Sd.Circle(x, y, 53.5f, 57.5f, 5.2f)),
                Sd.Poly(x, y, new[] { new Vector2(38.5f, 55f), new Vector2(57.5f, 55f), new Vector2(48f, 47.5f) }, 1.2f));
            p.Fill(heart, Rgb(1f, 0.97f, 0.88f), foam, 0.8f);

            p.Fill((x, y) => Sd.Capsule(x, y, 31f, 28f, 35f, 44f, 2.2f), Rgb(1f, 1f, 1f, 0.85f), cup, 1.5f);

            // A gold sparkle.
            Painter.Shape spark = (x, y) => Sd.Poly(x, y,
                new[] { new Vector2(78f, 74f), new Vector2(81f, 79f), new Vector2(78f, 84f), new Vector2(75f, 79f) }, 1.5f);
            p.Blob(spark, Rgb(1f, 0.98f, 0.70f), Rgb(1f, 0.70f, 0.12f), outline: 1.4f, inflate: 3f, spec: 0.6f);
            p.Fill((x, y) => Mathf.Max(Sd.Box(x, y, 78f, 79f, 7.5f, 1f, 0.5f), Sd.Box(x, y, 78f, 79f, 1f, 7.5f, 0.5f)),
                Rgb(1f, 0.92f, 0.45f, 0.95f), null, 0.6f);
            p.GroundShadow();
        }

        /// <summary>A slice of layer cake on a plate: sponge, cream, sponge, pink icing, a cherry.</summary>
        private static void DrawCakeSlice(Painter p)
        {
            Saucer(p, Rgb(1f, 1f, 0.98f), Rgb(0.74f, 0.80f, 0.90f));

            Painter.Shape body = (x, y) => Sd.Box(x, y, 48f, 38f, 27f, 18f, 5f);
            p.Blob(body, Rgb(0.84f, 0.54f, 0.30f), Rgb(0.46f, 0.22f, 0.08f), outline: 3.2f, inflate: 14f, spec: 0.3f);
            // The cream between the layers.
            p.Fill((x, y) => Sd.Box(x, y, 48f, 32f, 29f, 2.6f, 1f), Rgb(1f, 0.97f, 0.84f), body, 0.8f);
            p.Fill((x, y) => Sd.Box(x, y, 48f, 44.5f, 29f, 2.6f, 1f), Rgb(1f, 0.97f, 0.84f), body, 0.8f);
            // A jam line in the middle of the cream.
            p.Fill((x, y) => Sd.Box(x, y, 48f, 38.2f, 29f, 1.1f, 0.5f), Rgb(0.78f, 0.12f, 0.20f, 0.9f), body, 0.5f);

            // Pink icing with drips.
            Painter.Shape icing = (x, y) => Sd.Smooth(Sd.Box(x, y, 48f, 56.5f, 30f, 5.5f, 4f),
                Mathf.Max(Sd.Capsule(x, y, 30f, 55f, 30f, 47f, 3.2f),
                    Mathf.Max(Sd.Capsule(x, y, 52f, 55f, 52f, 44f, 3.4f), Sd.Capsule(x, y, 70f, 55f, 70f, 49f, 3f))), 4f);
            p.Blob(icing, Rgb(1f, 0.78f, 0.88f), Rgb(0.88f, 0.34f, 0.56f), outline: 2.8f, inflate: 8f, spec: 0.55f);

            // The cherry.
            p.Fill((x, y) => Sd.Capsule(x, y, 50f, 66f, 56f, 78f, 1.2f), Rgb(0.30f, 0.20f, 0.10f), null, 0.6f);
            p.Blob((x, y) => Sd.Circle(x, y, 48f, 66f, 7.2f), Rgb(1f, 0.46f, 0.40f), Rgb(0.62f, 0.04f, 0.14f), outline: 2.2f, inflate: 6f, spec: 0.7f);

            p.Fill((x, y) => Sd.Capsule(x, y, 26f, 28f, 26f, 44f, 2f), Rgb(1f, 1f, 1f, 0.5f), body, 1.4f);
            p.GroundShadow();
        }

        /// <summary>The same cup, left behind: dull, a brown ring and dribbles, a spoon on the saucer.</summary>
        private static void DrawDirtyCup(Painter p)
        {
            Saucer(p, Rgb(0.88f, 0.86f, 0.80f), Rgb(0.52f, 0.50f, 0.46f));
            // A spilt puddle and the spoon.
            p.Fill((x, y) => Sd.Ellipse(x, y, 62f, 14f, 13f, 3.4f), Rgb(0.34f, 0.18f, 0.08f, 0.8f), null, 1.5f);
            p.Blob((x, y) => Sd.Capsule(x, y, 24f, 14f, 44f, 12f, 2.4f), Rgb(0.90f, 0.92f, 0.95f), Rgb(0.50f, 0.54f, 0.62f),
                outline: 1.8f, inflate: 2.4f, spec: 0.5f);

            p.Blob((x, y) => Sd.Sub(Sd.Circle(x, y, 71f, 38f, 12.5f), Sd.Circle(x, y, 71f, 38f, 6.2f)),
                Rgb(0.88f, 0.86f, 0.80f), Rgb(0.52f, 0.50f, 0.46f), outline: 2.6f, inflate: 5f, spec: 0.2f);
            Painter.Shape cup = (x, y) => Sd.Poly(x, y, CupBody, 6f);
            p.Blob(cup, Rgb(0.90f, 0.88f, 0.82f), Rgb(0.52f, 0.50f, 0.48f), outline: 3.2f, inflate: 15f, spec: 0.2f);

            // Dribbles down the side, and a stain on the lower half.
            p.Fill((x, y) => Mathf.Max(Sd.Capsule(x, y, 40f, 52f, 40f, 34f, 2.6f),
                Mathf.Max(Sd.Capsule(x, y, 56f, 52f, 56f, 40f, 2.2f), Sd.Capsule(x, y, 48f, 52f, 48f, 44f, 1.8f))),
                Rgb(0.42f, 0.22f, 0.09f, 0.95f), cup, 0.8f);
            p.Fill((x, y) => Sd.Ellipse(x, y, 40f, 26f, 9f, 3.4f), Rgb(0.42f, 0.22f, 0.09f, 0.6f), cup, 1.5f);

            p.Blob((x, y) => Sd.Ellipse(x, y, 48f, 56f, 24f, 7f), Rgb(0.90f, 0.88f, 0.82f), Rgb(0.62f, 0.60f, 0.56f),
                outline: 2.8f, inflate: 5f, spec: 0.2f);
            p.Blob((x, y) => Sd.Ellipse(x, y, 48f, 56.5f, 20.5f, 4.8f), Rgb(0.34f, 0.18f, 0.08f), Rgb(0.18f, 0.08f, 0.03f),
                outline: 0f, inflate: 3f, spec: 0.1f);
            // The dried ring of coffee around the inside.
            p.Fill((x, y) => Sd.Sub(Sd.Ellipse(x, y, 48f, 56.5f, 21.5f, 5.6f), Sd.Ellipse(x, y, 48f, 56.5f, 17f, 3.6f)),
                Rgb(0.55f, 0.34f, 0.16f, 0.9f), null, 0.8f);
            p.GroundShadow();
        }

        /// <summary>A slatted wooden delivery crate with a milk bottle and a few eggs standing in it.</summary>
        private static void DrawSupplyCrate(Painter p)
        {
            // What is in it first (the crate's front covers the lower part).
            Painter.Shape bottle = (x, y) => Sd.Smooth(Sd.Box(x, y, 34f, 50f, 10f, 14f, 6f), Sd.Box(x, y, 34f, 68f, 4.6f, 7f, 2.5f), 5f);
            p.Blob(bottle, Rgb(1f, 1f, 1f), Rgb(0.70f, 0.79f, 0.93f), outline: 3f, inflate: 9f, spec: 0.5f);
            p.Blob((x, y) => Sd.Box(x, y, 34f, 50f, 10f, 5f, 1.5f), Rgb(0.50f, 0.76f, 1f), Rgb(0.18f, 0.44f, 0.86f),
                outline: 1.6f, inflate: 3f, spec: 0.3f, clip: bottle);
            p.Blob((x, y) => Sd.Box(x, y, 34f, 76f, 5.4f, 3f, 2f), Rgb(0.48f, 0.72f, 1f), Rgb(0.15f, 0.34f, 0.76f),
                outline: 2f, inflate: 3f, spec: 0.5f);

            float[] ex = { 56f, 68f, 80f };
            float[] ey = { 54f, 58f, 53f };
            for (int i = 0; i < 3; i++)
            {
                float cx = ex[i], cy = ey[i];
                p.Blob((x, y) => Sd.Egg(x, y, cx, cy, 6.2f, 8f, 0.28f, i == 1 ? -8f : 8f),
                    Rgb(1f, 0.99f, 0.94f), Rgb(0.88f, 0.76f, 0.58f), outline: 2.2f, inflate: 6f, spec: 0.5f);
            }

            // The crate.
            Painter.Shape crate = (x, y) => Sd.Box(x, y, 48f, 28f, 41f, 21f, 4f);
            p.Blob(crate, Rgb(0.90f, 0.66f, 0.38f), Rgb(0.52f, 0.30f, 0.12f), outline: 3.4f, inflate: 12f, spec: 0.2f);
            p.Fill((x, y) => Mathf.Max(Sd.Box(x, y, 48f, 28.5f, 42f, 1.1f, 0.4f), Sd.Box(x, y, 48f, 18f, 42f, 1.1f, 0.4f)),
                Rgb(0.36f, 0.18f, 0.06f, 0.75f), crate, 0.5f);
            // The corner posts and the label.
            p.Fill((x, y) => Mathf.Max(Sd.Box(x, y, 11f, 28f, 3.2f, 22f, 0.5f), Sd.Box(x, y, 85f, 28f, 3.2f, 22f, 0.5f)),
                Rgb(0.62f, 0.36f, 0.14f), crate, 0.6f);
            p.Blob((x, y) => Sd.Box(x, y, 48f, 26f, 15f, 8.5f, 2.5f), Rgb(1f, 0.99f, 0.90f), Rgb(0.86f, 0.80f, 0.62f),
                outline: 1.8f, inflate: 5f, spec: 0.1f, clip: crate);
            p.Fill((x, y) => Mathf.Max(Sd.Capsule(x, y, 40f, 26f, 56f, 26f, 1.3f), Sd.Capsule(x, y, 43f, 22f, 53f, 22f, 1.1f)),
                Rgb(0.30f, 0.20f, 0.12f, 0.85f), null, 0.4f);
            p.Fill((x, y) => Sd.Capsule(x, y, 13f, 12f, 13f, 40f, 1.4f), Rgb(1f, 1f, 1f, 0.35f), crate, 1.2f);
            p.GroundShadow();
        }
    }
}
