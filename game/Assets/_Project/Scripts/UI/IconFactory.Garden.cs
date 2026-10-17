using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// The garden icons for the florist's plant, water, harvest cycle: a seed packet, a water drop
    /// and a ready bloom (plus a basket alternative). Drawn exactly like the other product icons.
    ///
    /// The seed is also the seed item's icon (<see cref="For"/>). The drop and the ready bloom are the
    /// bubble over a flower bed (<see cref="Tycoon.UI.BedBubble"/>) - there is no item for them - so they
    /// are asked for by name through <see cref="GardenIcon"/>.
    /// </summary>
    public static partial class IconFactory
    {
        /// <summary>"seed", "drop" or "ready" (and the unused alternative "ready_basket"); null for anything else.</summary>
        public static Sprite GardenIcon(string id)
        {
            switch (id)
            {
                case "seed": return Get("seed", DrawSeedPacket, Supersample);
                case "drop": return Get("drop", DrawWaterDrop, Supersample);
                case "ready": return Get("ready", DrawReadyBloom, Supersample);
                case "ready_basket": return Get("ready_basket", DrawReadyBasket, Supersample);
                default: return null;
            }
        }

        /// <summary>A kraft seed packet with a crimped top, a cream label with a sprout, and loose seeds.</summary>
        private static void DrawSeedPacket(Painter p)
        {
            var seedLit = Rgb(0.82f, 0.60f, 0.34f);
            var seedShade = Rgb(0.34f, 0.18f, 0.07f);

            Painter.Shape packet = (x, y) => Sd.Box(x, y, 44f, 43f, 24f, 35f, 5f);
            p.Blob(packet, Rgb(1f, 0.80f, 0.46f), Rgb(0.78f, 0.44f, 0.14f), outline: 3.4f, inflate: 14f, spec: 0.25f);

            // The crimped top: a darker band with ridges.
            Painter.Shape crimp = (x, y) => Sd.Box(x, y, 44f, 71f, 24f, 6f, 2f);
            p.Fill(crimp, Rgb(0.72f, 0.40f, 0.12f, 0.75f), packet);
            for (int i = 0; i < 8; i++)
            {
                float x = 25.5f + i * 5.3f;
                p.Fill((px, py) => Sd.Capsule(px, py, x, 67f, x, 75f, 1.0f), Rgb(0.34f, 0.16f, 0.04f, 0.55f), packet, 0.5f);
            }

            // The cream label with a green sprout coming out of a mound of soil.
            Painter.Shape label = (x, y) => Sd.Box(x, y, 44f, 38f, 17.5f, 22f, 6f);
            p.Blob(label, Rgb(1f, 0.99f, 0.92f), Rgb(0.90f, 0.82f, 0.64f), outline: 2.2f, inflate: 9f, spec: 0.2f, clip: packet);
            p.Blob((x, y) => Sd.Ellipse(x, y, 44f, 22f, 11f, 4.5f), Rgb(0.62f, 0.42f, 0.24f), Rgb(0.28f, 0.16f, 0.08f),
                outline: 1.8f, inflate: 4f, spec: 0.1f, clip: label);
            IconStem(p, 44f, 22f, 44f, 42f, 2.4f);
            IconLeaf(p, 44f, 35f, 62f, 17f, 6.5f);
            IconLeaf(p, 44f, 30f, -62f, 16f, 6f);

            // Loose seeds beside it.
            p.Blob((x, y) => Sd.Egg(x, y, 76f, 15f, 3.8f, 6f, 0.35f, 28f), seedLit, seedShade, outline: 1.8f, inflate: 3.4f, spec: 0.3f);
            p.Blob((x, y) => Sd.Egg(x, y, 84f, 23f, 3.8f, 6f, 0.35f, -20f), seedLit, seedShade, outline: 1.8f, inflate: 3.4f, spec: 0.3f);
            p.Blob((x, y) => Sd.Egg(x, y, 72f, 26f, 3.6f, 5.6f, 0.35f, 70f), seedLit, seedShade, outline: 1.8f, inflate: 3.4f, spec: 0.3f);
            p.GroundShadow();
        }

        /// <summary>One big glossy water drop.</summary>
        private static void DrawWaterDrop(Painter p)
        {
            Painter.Shape drop = (x, y) => Sd.Smooth(
                Sd.Circle(x, y, 48f, 33f, 25f),
                Sd.Poly(x, y, new[] { new Vector2(48f, 80f), new Vector2(33f, 42f), new Vector2(63f, 42f) }, 3f), 12f);

            p.Blob(drop, Rgb(0.68f, 0.92f, 1f), Rgb(0.08f, 0.36f, 0.92f), outline: 3.6f, inflate: 22f, spec: 0.8f);

            // A lighter belly and gloss streaks.
            p.Fill((x, y) => Sd.Circle(x, y, 52f, 26f, 11f), Rgb(0.80f, 0.96f, 1f, 0.35f), drop, 6f);
            p.Fill((x, y) => Sd.Capsule(x, y, 34f, 26f, 33f, 44f, 3.2f), Rgb(1f, 1f, 1f, 0.85f), drop, 1.6f);
            p.Fill((x, y) => Sd.Circle(x, y, 38f, 53f, 2.4f), Rgb(1f, 1f, 1f, 0.8f), drop, 1.2f);
            p.GroundShadow();
        }

        /// <summary>A pink bloom on a short stem with two leaves and a sparkle: "ready, come and pick".</summary>
        private static void DrawReadyBloom(Painter p)
        {
            IconStem(p, 46f, 6f, 46f, 40f, 3f);
            IconLeaf(p, 46f, 14f, 60f, 26f, 8.5f);
            IconLeaf(p, 46f, 12f, -62f, 24f, 8f);

            var petalLit = Rgb(1f, 0.80f, 0.88f);
            var petalShade = Rgb(0.88f, 0.22f, 0.50f);
            float cx = 46f, cy = 58f;
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                float px = cx - 17f * Mathf.Sin(a * Mathf.Deg2Rad), py = cy + 17f * Mathf.Cos(a * Mathf.Deg2Rad);
                p.Blob((x, y) => Sd.Ellipse(x, y, px, py, 10.5f, 13.5f, a), petalLit, petalShade, outline: 2.6f, inflate: 8f, spec: 0.4f);
            }
            Painter.Shape core = (x, y) => Sd.Circle(x, y, cx, cy, 11f);
            p.Blob(core, Rgb(1f, 0.92f, 0.40f), Rgb(0.94f, 0.58f, 0.06f), outline: 2.6f, inflate: 9f, spec: 0.5f);
            p.Fill((x, y) => Sd.Capsule(x, y, cx - 5f, cy + 2f, cx - 3f, cy + 7f, 1.6f), Rgb(1f, 1f, 1f, 0.7f), core, 1.2f);

            // Sparkle top right.
            Painter.Shape spark = (x, y) => Mathf.Max(
                Sd.Ellipse(x, y, 79f, 77f, 3.2f, 11f), Sd.Ellipse(x, y, 79f, 77f, 11f, 3.2f));
            p.Blob(spark, Rgb(1f, 0.98f, 0.62f), Rgb(1f, 0.74f, 0.12f), outline: 2f, inflate: 3.2f, spec: 0.3f);
            p.Blob((x, y) => Sd.Circle(x, y, 18f, 80f, 4f), Rgb(1f, 0.98f, 0.62f), Rgb(1f, 0.74f, 0.12f), outline: 1.6f, inflate: 3.5f, spec: 0.3f);
            p.GroundShadow();
        }

        /// <summary>The alternative: a wicker basket brimming with blooms.</summary>
        private static void DrawReadyBasket(Painter p)
        {
            // Leaves behind, then the handle.
            IconLeaf(p, 36f, 48f, 40f, 24f, 8f);
            IconLeaf(p, 60f, 48f, -42f, 24f, 8f);
            Painter.Shape ring = RingAt(48f, 46f, 27f, 3.2f);
            Painter.Shape handle = (x, y) => Mathf.Min(ring(x, y), y - 46f);
            p.Blob(handle, Rgb(0.80f, 0.56f, 0.28f), Rgb(0.46f, 0.26f, 0.08f), outline: 2.4f, inflate: 3.2f, spec: 0.3f);

            // Blooms: hydrangea, sunflower, rose.
            p.Blob((x, y) => Sd.Circle(x, y, 63f, 60f, 10.5f), Rgb(0.66f, 0.80f, 1f), Rgb(0.24f, 0.36f, 0.88f), outline: 2.4f, inflate: 8f, spec: 0.4f);
            p.Blob((x, y) => Sd.Circle(x, y, 49f, 66f, 12.5f), Rgb(1f, 0.90f, 0.36f), Rgb(0.94f, 0.54f, 0.04f), outline: 2.6f, inflate: 9f, spec: 0.4f);
            p.Blob((x, y) => Sd.Circle(x, y, 49f, 66f, 5f), Rgb(0.66f, 0.40f, 0.16f), Rgb(0.28f, 0.14f, 0.06f), outline: 1.4f, inflate: 5f);
            Painter.Shape rose = (x, y) => Sd.Circle(x, y, 34f, 58f, 12.5f);
            p.Blob(rose, Rgb(1f, 0.72f, 0.82f), Rgb(0.82f, 0.18f, 0.44f), outline: 2.6f, inflate: 10f, spec: 0.55f);
            p.Fill((x, y) => Sd.Circle(x, y, 34f, 58f, 4f), Rgb(0.66f, 0.08f, 0.34f, 0.6f), rose, 1f);

            // The basket.
            Painter.Shape body = (x, y) => Sd.Poly(x, y, new[]
                { new Vector2(24f, 48f), new Vector2(72f, 48f), new Vector2(65f, 14f), new Vector2(31f, 14f) }, 4f);
            p.Blob(body, Rgb(0.98f, 0.74f, 0.40f), Rgb(0.60f, 0.32f, 0.10f), outline: 3.2f, inflate: 12f, spec: 0.25f);
            var weave = Rgb(0.34f, 0.16f, 0.04f, 0.45f);
            foreach (float wy in new[] { 22f, 31f, 40f })
                p.Fill((x, y) => Sd.Capsule(x, y, 20f, wy, 76f, wy, 1.0f), weave, body, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                float wx = 30f + i * 7.2f;
                p.Fill((x, y) => Sd.Capsule(x, y, wx, 14f, wx + 2f, 48f, 0.9f), weave, body, 0.5f);
            }
            Painter.Shape rim = (x, y) => Sd.Box(x, y, 48f, 48f, 28f, 4.2f, 3f);
            p.Blob(rim, Rgb(0.92f, 0.66f, 0.34f), Rgb(0.52f, 0.28f, 0.08f), outline: 2.6f, inflate: 4f, spec: 0.3f);
            p.GroundShadow();
        }
    }
}
