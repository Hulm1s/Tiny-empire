using System.Collections.Generic;
using Tycoon.Config;
using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>What a square is asking the player to do, when no product says it better.</summary>
    public enum SquareIcon
    {
        None,
        Feed,
        Collect,
        Harvest,
        Fix,
        Buy,
        Unlock,
        Hire,
        Serve,
        Discard,

        // Added with the supermarket. Appended rather than slotted in, because the value is
        // stored in the saved scene and renumbering would silently change every square.
        Travel,
        Clean,
        Open,
        Stock,

        // Added with the paint menu, appended for the same reason.
        Paint
    }

    /// <summary>
    /// Draws the game's icons in code.
    ///
    /// The project has no art assets and the legacy font has no emoji glyphs, so a literal egg
    /// character would render as an empty box. Sprites generated at runtime are the route that
    /// is already proven safe here - <see cref="UIFactory"/> builds its circle and rounded box
    /// exactly this way. (Runtime-created *materials* are the thing that renders magenta in a
    /// URP build; sprites are fine.)
    ///
    /// Every icon is built from the same few primitives and finished the same way - a dark
    /// outline, a vertical gradient body, a soft highlight - so a wrench and an egg look like
    /// they come from the same game.
    /// </summary>
    public static class IconFactory
    {
        /// <summary>Texture resolution. Icons are never drawn larger than ~100 px on a phone.</summary>
        private const int Size = 96;

        private static readonly Color Outline = new Color(0.16f, 0.14f, 0.13f, 0.85f);
        private static readonly Color Highlight = new Color(1f, 1f, 1f, 0.5f);

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>
        /// The icon for a product.
        ///
        /// An explicit sprite on the item always wins - that field exists so real art can be
        /// dropped in later without touching code. Otherwise one is drawn for the item's id,
        /// and anything unrecognised falls back to a disc in the item's own colour.
        ///
        /// Deliberately does not write back into <see cref="ItemDefinition.icon"/>: that would
        /// dirty a ScriptableObject asset from play mode.
        /// </summary>
        public static Sprite For(ItemDefinition item)
        {
            if (item == null) return UIFactory.Circle;
            if (item.icon != null) return item.icon;

            switch (item.id)
            {
                case "egg": return Get("egg", DrawEgg);
                case "milk": return Get("milk", DrawMilk);
                case "corn": return Get("corn", DrawCorn);
                case "hay": return Get("hay", DrawHay);
                case "trash": return Get("trash", DrawTrash);
                case "bread": return Get("bread", DrawBread);
                case "apples": return Get("apples", DrawApple);
                case "yogurt": return Get("yogurt", DrawYogurt);
            }

            // Unknown product: the old behaviour, a plain disc the caller tints.
            return UIFactory.Circle;
        }

        /// <summary>True when <see cref="For"/> returns something better than a plain disc.</summary>
        public static bool HasArtwork(ItemDefinition item) =>
            item != null && (item.icon != null ||
                item.id == "egg" || item.id == "milk" || item.id == "corn" || item.id == "hay" ||
                item.id == "trash" || item.id == "bread" || item.id == "apples" ||
                item.id == "yogurt");

        public static Sprite Action(SquareIcon kind)
        {
            switch (kind)
            {
                case SquareIcon.Feed: return Get("feed", p => DrawArrow(p, down: true));
                case SquareIcon.Collect: return Get("collect", p => DrawArrow(p, down: false));
                case SquareIcon.Harvest: return Get("harvest", DrawHarvest);
                case SquareIcon.Fix: return Get("fix", DrawWrench);
                case SquareIcon.Buy: return Get("buy", DrawPlus);
                case SquareIcon.Unlock: return Get("unlock", DrawPadlock);
                case SquareIcon.Hire: return Get("hire", DrawPerson);
                case SquareIcon.Serve: return Get("serve", DrawCoin);
                case SquareIcon.Discard: return Get("discard", DrawBin);
                case SquareIcon.Travel: return Get("travel", DrawSignpost);
                case SquareIcon.Clean: return Get("clean", DrawBroom);
                case SquareIcon.Open: return Get("open", DrawDoor);
                case SquareIcon.Stock: return Get("stock", DrawShelf);
                case SquareIcon.Paint: return Get("paint", DrawRoller);
                default: return null;
            }
        }

        private static Sprite Get(string key, System.Action<Painter> draw)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var painter = new Painter(Size);
            draw(painter);

            var sprite = painter.ToSprite("Icon_" + key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// The waste bin, for the one square that gives nothing back.
        ///
        /// A closed charcoal can with a domed lid and a handle, drawn from the reference: lid
        /// on rather than tipped off. An open bin was meant to say "put things in here", but at
        /// forty screen pixels the tipped lid read as a second object lying beside the can, and
        /// the whole thing stopped looking like a bin at all. A closed one is instantly
        /// recognisable, and the word BIN above it does the explaining.
        /// </summary>
        private static void DrawBin(Painter p)
        {
            var bodyTop = new Color(0.33f, 0.34f, 0.36f);
            var bodyBottom = new Color(0.20f, 0.21f, 0.23f);
            var lidColour = new Color(0.36f, 0.37f, 0.39f);
            var rib = new Color(0.15f, 0.16f, 0.18f, 0.75f);

            // Tapered body, a touch narrower at the foot. Drawn as two triangles because the
            // painter has no trapezoid and a rounded box cannot taper.
            p.Quad(new Vector2(17f, 64f), new Vector2(79f, 64f),
                   new Vector2(71f, 8f), new Vector2(25f, 8f),
                   bodyTop, bodyBottom, outline: 2.6f);

            // The ribs down the front: upright, evenly spaced, and stopping short of the foot
            // so the taper still reads. Angling them to follow the sides looked like scratches.
            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.Lerp(32f, 64f, i / 3f);
                p.Box(x, 35f, 3.2f, 24f, 2.6f, rib, rib);
            }

            // Lid: a shallow dome sitting on a flat skirt that overhangs the body.
            p.Box(48f, 70f, 35f, 6f, 4.5f, lidColour, bodyTop, outline: 2.6f);
            p.Ellipse(48f, 74f, 29f, 9f, lidColour, bodyTop, outline: 2.6f);

            // Handle, drawn as a ring with its lower half bitten off so it reads as an arch.
            p.Ring(48f, 84f, 11f, 6.5f, lidColour, bodyTop, outline: 2.4f);
            p.Erase(48f, 78f, 14f, 6f);
            p.Box(48f, 82f, 11f, 2.6f, 2f, lidColour, bodyTop, outline: 2.4f);

            p.Glow(31f, 45f, 4f, 16f, new Color(1f, 1f, 1f, 0.16f));
        }

        // ------------------------------------------------------------------ products

        private static void DrawEgg(Painter p)
        {
            // Tapered towards the top, which is the whole difference between reading as an egg
            // and reading as a pebble.
            p.Ellipse(48f, 44f, 27f, 35f,
                new Color(1f, 0.99f, 0.94f), new Color(0.91f, 0.84f, 0.7f),
                taper: 0.26f, outline: 2.4f);

            p.Glow(39f, 62f, 9f, 12f, Highlight);
        }

        private static void DrawMilk(Painter p)
        {
            var glassTop = new Color(0.99f, 1f, 1f);
            var glassBottom = new Color(0.82f, 0.89f, 0.97f);

            // Bottle: body, shoulder, neck, cap. The blue cap is what separates milk from the
            // egg at a glance, since both bodies are near-white.
            p.Box(48f, 32f, 19f, 25f, 9f, glassTop, glassBottom, outline: 2.4f);
            p.Box(48f, 60f, 9f, 9f, 3f, glassTop, glassBottom, outline: 2.4f);
            p.Box(48f, 76f, 12f, 8f, 3f,
                new Color(0.42f, 0.67f, 0.93f), new Color(0.24f, 0.47f, 0.8f), outline: 2.4f);

            // A band of white sitting below a clear shoulder reads as liquid in a bottle.
            p.Box(48f, 24f, 15f, 15f, 6f,
                new Color(1f, 1f, 1f, 0.95f), new Color(0.93f, 0.95f, 1f, 0.95f));

            p.Glow(38f, 40f, 5f, 16f, Highlight);
        }

        private static void DrawCorn(Painter p)
        {
            // Husk leaves first, so the cob sits on top of them.
            var leafTop = new Color(0.55f, 0.79f, 0.36f);
            var leafBottom = new Color(0.3f, 0.56f, 0.24f);
            p.Triangle(new Vector2(46f, 30f), new Vector2(18f, 16f), new Vector2(44f, 8f),
                leafTop, leafBottom, outline: 2f);
            p.Triangle(new Vector2(50f, 30f), new Vector2(78f, 16f), new Vector2(52f, 8f),
                leafTop, leafBottom, outline: 2f);

            p.Ellipse(48f, 52f, 20f, 34f,
                new Color(1f, 0.87f, 0.34f), new Color(0.87f, 0.63f, 0.12f), outline: 2.4f);

            // Kernels: offset rows, so it is a cob rather than a yellow balloon.
            var kernel = new Color(0.72f, 0.51f, 0.08f, 0.55f);
            for (int row = 0; row < 7; row++)
            {
                float y = 26f + row * 8.5f;
                float shift = (row % 2 == 0) ? 0f : 4.5f;
                for (int col = -2; col <= 2; col++)
                    p.Glow(48f + col * 9f + shift, y, 3f, 3f, kernel);
            }

            p.Glow(40f, 68f, 4f, 9f, Highlight);
        }

        private static void DrawHay(Painter p)
        {
            p.Box(48f, 48f, 33f, 26f, 11f,
                new Color(0.95f, 0.84f, 0.47f), new Color(0.74f, 0.59f, 0.25f), outline: 2.4f);

            // Two straps, the way a real bale is bound.
            var strap = new Color(0.55f, 0.42f, 0.18f, 0.7f);
            p.Box(34f, 48f, 2.5f, 26f, 1f, strap, strap);
            p.Box(62f, 48f, 2.5f, 26f, 1f, strap, strap);

            // Loose ends poking out of the cut face.
            var straw = new Color(0.85f, 0.72f, 0.36f, 0.75f);
            for (int i = 0; i < 5; i++)
                p.Box(24f + i * 12f, 68f, 1.5f, 6f, 1f, straw, straw, angle: (i % 2 == 0) ? 12f : -12f);

            p.Glow(36f, 62f, 8f, 5f, Highlight);
        }

        /// <summary>A loaf: a domed golden crust with three slashes, on a darker base.</summary>
        private static void DrawBread(Painter p)
        {
            var crustTop = new Color(0.97f, 0.76f, 0.4f);
            var crustBottom = new Color(0.74f, 0.45f, 0.18f);

            // The loaf is a wide low dome over a slightly narrower base, so it reads as bread
            // and not as a bun or a sausage.
            p.Box(48f, 30f, 34f, 14f, 10f, crustBottom, crustBottom, outline: 2.6f);
            p.Ellipse(48f, 44f, 36f, 26f, crustTop, crustBottom, outline: 2.6f);

            // Three diagonal slashes across the top, the thing that says "baked".
            var slash = new Color(0.62f, 0.34f, 0.12f, 0.8f);
            for (int i = -1; i <= 1; i++)
                p.Box(48f + i * 17f, 54f, 2.4f, 9f, 1.2f, slash, slash, angle: -28f);

            p.Glow(34f, 60f, 10f, 5f, Highlight);
        }

        /// <summary>A red apple with a short stem and one leaf.</summary>
        private static void DrawApple(Painter p)
        {
            var skinTop = new Color(0.96f, 0.36f, 0.3f);
            var skinBottom = new Color(0.7f, 0.12f, 0.14f);

            // Two overlapping lobes make the shoulders and the dip at the stem.
            p.Ellipse(36f, 38f, 24f, 28f, skinTop, skinBottom, outline: 2.6f);
            p.Ellipse(60f, 38f, 24f, 28f, skinTop, skinBottom, outline: 2.6f);
            p.Ellipse(48f, 36f, 26f, 27f, skinTop, skinBottom);

            // Stem, and a leaf made of a tilted ellipse-ish box.
            var stem = new Color(0.4f, 0.27f, 0.14f);
            p.Box(48f, 72f, 2.6f, 8f, 1.2f, stem, stem, outline: 1.8f, angle: 10f);
            p.Box(63f, 76f, 11f, 5f, 4.5f,
                new Color(0.55f, 0.82f, 0.36f), new Color(0.3f, 0.58f, 0.22f), outline: 2f, angle: 24f);

            p.Glow(32f, 50f, 6f, 10f, Highlight);
        }

        /// <summary>A yogurt pot: a tapered cup with a foil lid and a pink label band.</summary>
        private static void DrawYogurt(Painter p)
        {
            var cupTop = new Color(1f, 1f, 1f);
            var cupBottom = new Color(0.86f, 0.9f, 0.95f);

            // Tapered cup, narrower at the foot (the painter has no trapezoid, so a quad).
            p.Quad(new Vector2(20f, 66f), new Vector2(76f, 66f),
                   new Vector2(66f, 10f), new Vector2(30f, 10f),
                   cupTop, cupBottom, outline: 2.6f);

            // Label band with a little fruit dot on it.
            var bandTop = new Color(0.98f, 0.58f, 0.72f);
            var bandBottom = new Color(0.86f, 0.36f, 0.56f);
            p.Box(48f, 36f, 24f, 11f, 4f, bandTop, bandBottom, outline: 2.2f);
            p.Ellipse(48f, 36f, 5.5f, 5.5f, new Color(1f, 0.92f, 0.95f), new Color(0.96f, 0.78f, 0.86f));

            // Foil lid, a rim wider than the cup.
            p.Box(48f, 70f, 31f, 5f, 3f,
                new Color(0.88f, 0.9f, 0.94f), new Color(0.6f, 0.65f, 0.72f), outline: 2.4f);

            p.Glow(30f, 46f, 3.5f, 14f, Highlight);
        }

        /// <summary>
        /// A tied-up rubbish bag: lumpy dark body, a pinched neck with two ears, a bit of
        /// something poking out. Dark grey on the pale square, so it reads as mess.
        /// </summary>
        private static void DrawTrash(Painter p)
        {
            var bagTop = new Color(0.46f, 0.5f, 0.52f);
            var bagBottom = new Color(0.18f, 0.2f, 0.22f);

            p.Ellipse(48f, 36f, 30f, 28f, bagTop, bagBottom, outline: 2.6f);
            // Shoulders narrowing into the knot.
            p.Triangle(new Vector2(24f, 50f), new Vector2(72f, 50f), new Vector2(48f, 76f),
                bagTop, bagBottom, outline: 2.4f);
            // The two ears of the tie.
            p.Triangle(new Vector2(48f, 72f), new Vector2(30f, 90f), new Vector2(42f, 66f),
                bagTop, bagBottom, outline: 2.2f);
            p.Triangle(new Vector2(48f, 72f), new Vector2(66f, 90f), new Vector2(54f, 66f),
                bagTop, bagBottom, outline: 2.2f);

            // Creases, so the bag is not a smooth balloon.
            var crease = new Color(0.08f, 0.09f, 0.1f, 0.55f);
            p.Box(38f, 36f, 1.6f, 14f, 1f, crease, crease, angle: 12f);
            p.Box(57f, 33f, 1.6f, 12f, 1f, crease, crease, angle: -14f);

            p.Glow(36f, 48f, 6f, 10f, Highlight);
        }

        /// <summary>A paint roller on its frame, with a stripe of fresh colour under it.</summary>
        private static void DrawRoller(Painter p)
        {
            var sleeveTop = new Color(0.95f, 0.5f, 0.45f);
            var sleeveBottom = new Color(0.8f, 0.28f, 0.28f);
            var metalTop = new Color(0.86f, 0.89f, 0.93f);
            var metalBottom = new Color(0.55f, 0.6f, 0.68f);
            var gripTop = new Color(0.62f, 0.85f, 1f);
            var gripBottom = new Color(0.25f, 0.55f, 0.9f);

            // Stripe of paint on the wall, then the roller above it.
            p.Box(48f, 14f, 34f, 5f, 2.5f, sleeveTop, sleeveBottom, outline: 2.2f);
            p.Box(46f, 62f, 32f, 13f, 6f, sleeveTop, sleeveBottom, outline: 2.6f);

            // The frame: out of the roller's end, down, then the handle.
            p.Box(82f, 54f, 3f, 12f, 1.5f, metalTop, metalBottom, outline: 2f);
            p.Box(60f, 41f, 22f, 3f, 1.5f, metalTop, metalBottom, outline: 2f);
            p.Box(60f, 30f, 3f, 11f, 1.5f, metalTop, metalBottom, outline: 2f);
            p.Box(60f, 23f, 6f, 10f, 3f, gripTop, gripBottom, outline: 2.2f);

            p.Glow(32f, 68f, 14f, 4f, Highlight);
        }

        // ------------------------------------------------------------------ actions

        private static void DrawArrow(Painter p, bool down)
        {
            var top = down ? new Color(0.55f, 0.79f, 1f) : new Color(0.62f, 0.95f, 0.6f);
            var bottom = down ? new Color(0.24f, 0.53f, 0.9f) : new Color(0.29f, 0.7f, 0.33f);

            // Shaft sits on the far side from the head, so the arrow stays centred overall.
            p.Box(48f, down ? 62f : 34f, 10f, 22f, 4f, top, bottom, outline: 2.4f);

            if (down)
                p.Triangle(new Vector2(48f, 8f), new Vector2(18f, 42f), new Vector2(78f, 42f),
                    top, bottom, outline: 2.4f);
            else
                p.Triangle(new Vector2(48f, 88f), new Vector2(18f, 54f), new Vector2(78f, 54f),
                    top, bottom, outline: 2.4f);

            p.Glow(40f, down ? 70f : 26f, 3f, 8f, Highlight);
        }

        private static void DrawHarvest(Painter p)
        {
            // Three stalks. Generic enough to stand in for any crop that has no icon.
            var top = new Color(1f, 0.89f, 0.42f);
            var bottom = new Color(0.82f, 0.6f, 0.13f);
            var stem = new Color(0.4f, 0.62f, 0.3f);

            for (int i = -1; i <= 1; i++)
            {
                float x = 48f + i * 22f;
                p.Box(x, 30f, 3f, 22f, 2f, stem, stem, outline: 1.8f, angle: i * 8f);
                p.Ellipse(x + i * 3f, 62f, 9f, 18f, top, bottom, taper: 0.3f, outline: 2.2f);
            }

            p.Glow(42f, 70f, 3f, 6f, Highlight);
        }

        private static void DrawWrench(Painter p)
        {
            var top = new Color(0.86f, 0.89f, 0.93f);
            var bottom = new Color(0.55f, 0.6f, 0.68f);

            // Handle on the diagonal, jaw at the top end.
            p.Box(48f, 48f, 9f, 40f, 4f, top, bottom, outline: 2.4f, angle: 38f);

            p.Ring(70f, 72f, 19f, 9f, top, bottom, outline: 2.4f);
            // Bite out of the ring, which is what makes it a spanner and not a doughnut.
            p.Erase(84f, 86f, 13f, 13f);

            p.Glow(36f, 30f, 3f, 9f, Highlight);
        }

        private static void DrawPlus(Painter p)
        {
            var top = new Color(0.66f, 0.95f, 0.6f);
            var bottom = new Color(0.27f, 0.68f, 0.32f);

            p.Box(48f, 48f, 10f, 32f, 5f, top, bottom, outline: 2.4f);
            p.Box(48f, 48f, 32f, 10f, 5f, top, bottom, outline: 2.4f);

            p.Glow(40f, 64f, 3f, 7f, Highlight);
        }

        private static void DrawPadlock(Painter p)
        {
            var shackleTop = new Color(0.82f, 0.85f, 0.9f);
            var shackleBottom = new Color(0.52f, 0.57f, 0.64f);

            p.Ring(48f, 58f, 21f, 12f, shackleTop, shackleBottom, outline: 2.4f);
            // Flatten the bottom of the shackle so the body can sit against it.
            p.Erase(48f, 42f, 24f, 18f);

            p.Box(48f, 34f, 27f, 24f, 8f,
                new Color(1f, 0.85f, 0.4f), new Color(0.85f, 0.6f, 0.13f), outline: 2.4f);

            // Keyhole.
            p.Glow(48f, 36f, 6f, 6f, new Color(0.4f, 0.28f, 0.06f, 0.75f));
            p.Box(48f, 26f, 2.5f, 7f, 1f,
                new Color(0.4f, 0.28f, 0.06f, 0.75f), new Color(0.4f, 0.28f, 0.06f, 0.75f));

            p.Glow(34f, 40f, 4f, 8f, Highlight);
        }

        private static void DrawPerson(Painter p)
        {
            var top = new Color(0.68f, 0.88f, 1f);
            var bottom = new Color(0.27f, 0.56f, 0.86f);

            p.Ellipse(48f, 71f, 15f, 15f,
                new Color(1f, 0.87f, 0.73f), new Color(0.88f, 0.69f, 0.53f), outline: 2.4f);
            // Shoulders: a wide rounded box reads as a torso at this size.
            p.Box(48f, 28f, 25f, 22f, 13f, top, bottom, outline: 2.4f);

            p.Glow(40f, 76f, 4f, 5f, Highlight);
        }

        private static void DrawCoin(Painter p)
        {
            p.Ellipse(48f, 48f, 34f, 34f,
                new Color(1f, 0.89f, 0.45f), new Color(0.85f, 0.62f, 0.14f), outline: 2.6f);
            p.Ring(48f, 48f, 24f, 18f,
                new Color(0.75f, 0.53f, 0.1f, 0.55f), new Color(0.65f, 0.44f, 0.07f, 0.55f));

            p.Glow(36f, 62f, 6f, 8f, Highlight);
        }

        /// <summary>
        /// A wooden signpost with one board pointing right: "this way, down the road".
        /// The arrow head is a Triangle, whose winding the painter sorts out itself.
        /// </summary>
        private static void DrawSignpost(Painter p)
        {
            var woodTop = new Color(0.82f, 0.62f, 0.38f);
            var woodBottom = new Color(0.58f, 0.4f, 0.22f);
            var boardTop = new Color(0.62f, 0.85f, 1f);
            var boardBottom = new Color(0.25f, 0.55f, 0.9f);

            p.Box(40f, 38f, 5f, 34f, 2f, woodTop, woodBottom, outline: 2.4f);
            p.Box(40f, 66f, 25f, 13f, 4f, boardTop, boardBottom, outline: 2.4f);
            p.Triangle(new Vector2(80f, 66f), new Vector2(62f, 82f), new Vector2(62f, 50f),
                boardTop, boardBottom, outline: 2.4f);

            // A white bar on the board, so it reads as a direction and not a plank.
            p.Box(38f, 66f, 10f, 3f, 1.5f, Color.white, Color.white);

            p.Glow(28f, 72f, 5f, 4f, Highlight);
        }

        /// <summary>A broom, bristles down-left, with a couple of sparkles for "clean".</summary>
        private static void DrawBroom(Painter p)
        {
            var handleTop = new Color(0.86f, 0.66f, 0.4f);
            var handleBottom = new Color(0.6f, 0.42f, 0.24f);
            var strawTop = new Color(1f, 0.9f, 0.5f);
            var strawBottom = new Color(0.82f, 0.62f, 0.2f);

            p.Box(56f, 58f, 3.6f, 32f, 1.6f, handleTop, handleBottom, outline: 2.2f, angle: 38f);
            p.Box(33f, 30f, 14f, 12f, 3f, strawTop, strawBottom, outline: 2.4f, angle: 38f);

            // Binding where the bristles meet the handle.
            var binding = new Color(0.5f, 0.36f, 0.2f);
            p.Box(41f, 41f, 11f, 2.6f, 1f, binding, binding, angle: 38f);

            var spark = new Color(1f, 1f, 0.8f, 0.95f);
            p.Glow(72f, 30f, 7f, 7f, spark);
            p.Glow(80f, 44f, 4f, 4f, spark);
            p.Glow(22f, 66f, 5f, 5f, spark);
        }

        /// <summary>A door standing ajar: the shop is open.</summary>
        private static void DrawDoor(Painter p)
        {
            var frameTop = new Color(0.95f, 0.88f, 0.7f);
            var frameBottom = new Color(0.75f, 0.62f, 0.42f);
            var inside = new Color(0.28f, 0.24f, 0.2f);
            var leafTop = new Color(0.55f, 0.82f, 0.5f);
            var leafBottom = new Color(0.27f, 0.58f, 0.3f);

            p.Box(48f, 48f, 26f, 38f, 4f, frameTop, frameBottom, outline: 2.4f);
            p.Box(48f, 46f, 19f, 31f, 2f, inside, inside);

            // The leaf swung open towards the viewer: a quad narrower on its free edge.
            p.Quad(new Vector2(29f, 15f), new Vector2(29f, 77f),
                   new Vector2(50f, 70f), new Vector2(50f, 22f),
                   leafTop, leafBottom, outline: 2.4f);

            p.Glow(45f, 46f, 3f, 3f, new Color(1f, 0.95f, 0.6f, 0.95f));
        }

        /// <summary>A shelf board with two crates on it and an arrow dropping goods onto it.</summary>
        private static void DrawShelf(Painter p)
        {
            var boardTop = new Color(0.82f, 0.62f, 0.38f);
            var boardBottom = new Color(0.58f, 0.4f, 0.22f);
            var crateTop = new Color(0.98f, 0.86f, 0.55f);
            var crateBottom = new Color(0.78f, 0.58f, 0.25f);

            // Uprights, then the board across them.
            p.Box(18f, 30f, 3f, 22f, 1.5f, boardTop, boardBottom, outline: 2f);
            p.Box(78f, 30f, 3f, 22f, 1.5f, boardTop, boardBottom, outline: 2f);
            p.Box(48f, 24f, 36f, 4.5f, 2f, boardTop, boardBottom, outline: 2.4f);

            p.Box(34f, 38f, 11f, 10f, 2f, crateTop, crateBottom, outline: 2.2f);
            p.Box(60f, 36f, 9f, 8f, 2f, crateTop, crateBottom, outline: 2.2f);

            var arrowTop = new Color(0.55f, 0.79f, 1f);
            var arrowBottom = new Color(0.24f, 0.53f, 0.9f);
            p.Box(48f, 80f, 5f, 8f, 2f, arrowTop, arrowBottom, outline: 2f);
            p.Triangle(new Vector2(48f, 52f), new Vector2(34f, 68f), new Vector2(62f, 68f),
                arrowTop, arrowBottom, outline: 2.2f);
        }

        // ------------------------------------------------------------------ rasteriser

        /// <summary>
        /// A tiny software rasteriser. Shapes are described as signed distance fields - positive
        /// inside, zero on the edge - which makes both anti-aliasing and outlining trivial: a
        /// one-pixel ramp gives a smooth edge, and adding a constant grows the shape.
        ///
        /// Coordinates are in pixels with the origin at the bottom left, matching how a texture
        /// is addressed, so "up" in the drawing code is up in the icon.
        /// </summary>
        private class Painter
        {
            private delegate float Sdf(float x, float y);

            private readonly int _size;
            private readonly Color[] _pixels;

            public Painter(int size)
            {
                _size = size;
                _pixels = new Color[size * size];
            }

            public void Ellipse(float cx, float cy, float rx, float ry, Color top, Color bottom,
                float taper = 0f, float outline = 0f)
            {
                float thinnest = Mathf.Min(rx, ry);
                Paint((x, y) =>
                {
                    float dy = (y - cy) / ry;
                    // Narrow the shape towards the top by however much taper asks for.
                    float width = rx * (1f - taper * Mathf.Max(0f, dy));
                    float dx = (x - cx) / Mathf.Max(0.001f, width);
                    return (1f - Mathf.Sqrt(dx * dx + dy * dy)) * thinnest;
                }, top, bottom, outline);
            }

            public void Box(float cx, float cy, float halfW, float halfH, float radius,
                Color top, Color bottom, float outline = 0f, float angle = 0f)
            {
                float sin = Mathf.Sin(angle * Mathf.Deg2Rad);
                float cos = Mathf.Cos(angle * Mathf.Deg2Rad);

                Paint((x, y) =>
                {
                    // Rotate the sample into the box's own space rather than rotating the box.
                    float ox = x - cx, oy = y - cy;
                    float lx = ox * cos + oy * sin;
                    float ly = -ox * sin + oy * cos;

                    float qx = Mathf.Abs(lx) - (halfW - radius);
                    float qy = Mathf.Abs(ly) - (halfH - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                               Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                    float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
                    return radius - (outside + inside);
                }, top, bottom, outline);
            }

            public void Ring(float cx, float cy, float outer, float inner,
                Color top, Color bottom, float outline = 0f)
            {
                Paint((x, y) =>
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    // Inside the band = positive; whichever edge is nearer wins.
                    return Mathf.Min(outer - d, d - inner);
                }, top, bottom, outline);
            }

            public void Triangle(Vector2 a, Vector2 b, Vector2 c, Color top, Color bottom,
                float outline = 0f)
            {
                // Consistent winding, so "inside" is the same sign for all three edges.
                //
                // The sign is the opposite way round from the obvious one, and getting it wrong
                // cost every triangle in the project: Edge returns a LEFT-positive distance, so
                // a counter-clockwise triangle is the one that needs negating. With it the
                // wrong way up the inside tested as outside and nothing drew at all - which is
                // why the feed and collect arrows were plain rectangles with no heads on them,
                // and why the bin was a lid floating over four stripes.
                float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                float flip = area < 0f ? 1f : -1f;

                Paint((x, y) =>
                {
                    float e0 = Edge(a, b, x, y) * flip;
                    float e1 = Edge(b, c, x, y) * flip;
                    float e2 = Edge(c, a, x, y) * flip;
                    return Mathf.Min(e0, Mathf.Min(e1, e2));
                }, top, bottom, outline);
            }

            /// <summary>
            /// A convex four-sided shape, in order around the outline.
            ///
            /// Exists because a tapered can drawn as two outlined triangles gets its shared
            /// diagonal outlined too, which puts a dark slash straight across the middle of the
            /// bin. One shape has one outline, around the outside, where it belongs.
            /// </summary>
            public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color top, Color bottom,
                float outline = 0f)
            {
                float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                float flip = area < 0f ? 1f : -1f;

                Paint((x, y) =>
                {
                    float e0 = Edge(a, b, x, y) * flip;
                    float e1 = Edge(b, c, x, y) * flip;
                    float e2 = Edge(c, d, x, y) * flip;
                    float e3 = Edge(d, a, x, y) * flip;
                    return Mathf.Min(Mathf.Min(e0, e1), Mathf.Min(e2, e3));
                }, top, bottom, outline);
            }

            /// <summary>Distance from a point to an edge, normalised so it is in pixels.</summary>
            private static float Edge(Vector2 from, Vector2 to, float x, float y)
            {
                float ex = to.x - from.x, ey = to.y - from.y;
                float length = Mathf.Sqrt(ex * ex + ey * ey);
                if (length < 0.001f) return 0f;
                return ((x - from.x) * ey - (y - from.y) * ex) / length;
            }

            /// <summary>A soft blob, for highlights and other unoutlined detail.</summary>
            public void Glow(float cx, float cy, float rx, float ry, Color color)
            {
                for (int y = 0; y < _size; y++)
                {
                    for (int x = 0; x < _size; x++)
                    {
                        float dx = (x + 0.5f - cx) / rx;
                        float dy = (y + 0.5f - cy) / ry;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        if (d >= 1f) continue;
                        // Squared falloff, so the centre is solid and the rim fades out.
                        Blend(x, y, color, (1f - d) * (1f - d));
                    }
                }
            }

            /// <summary>Clears an area back to transparent. Used to bite shapes out of others.</summary>
            public void Erase(float cx, float cy, float halfW, float halfH)
            {
                for (int y = 0; y < _size; y++)
                {
                    for (int x = 0; x < _size; x++)
                    {
                        if (Mathf.Abs(x + 0.5f - cx) > halfW) continue;
                        if (Mathf.Abs(y + 0.5f - cy) > halfH) continue;
                        _pixels[y * _size + x] = new Color(0f, 0f, 0f, 0f);
                    }
                }
            }

            private void Paint(Sdf shape, Color top, Color bottom, float outline)
            {
                // The outline is the same shape grown by a couple of pixels, drawn underneath.
                if (outline > 0f) Pass(shape, outline, Outline, Outline);
                Pass(shape, 0f, top, bottom);
            }

            private void Pass(Sdf shape, float grow, Color top, Color bottom)
            {
                for (int y = 0; y < _size; y++)
                {
                    // One gradient across the whole icon rather than per shape, so a stacked
                    // shape like the milk bottle is lit as a single object.
                    Color tone = Color.Lerp(bottom, top, y / (float)(_size - 1));

                    for (int x = 0; x < _size; x++)
                    {
                        float distance = shape(x + 0.5f, y + 0.5f) + grow;
                        // A one-pixel ramp across the edge is all the anti-aliasing this needs.
                        float coverage = Mathf.Clamp01(distance + 0.5f);
                        if (coverage > 0f) Blend(x, y, tone, coverage);
                    }
                }
            }

            /// <summary>Standard source-over, kept un-premultiplied so Unity gets plain RGBA.</summary>
            private void Blend(int x, int y, Color color, float coverage)
            {
                int index = y * _size + x;
                Color dst = _pixels[index];

                float srcA = color.a * coverage;
                if (srcA <= 0f) return;

                float outA = srcA + dst.a * (1f - srcA);
                if (outA <= 0.0001f)
                {
                    _pixels[index] = new Color(0f, 0f, 0f, 0f);
                    return;
                }

                float keep = dst.a * (1f - srcA);
                _pixels[index] = new Color(
                    (color.r * srcA + dst.r * keep) / outA,
                    (color.g * srcA + dst.g * keep) / outA,
                    (color.b * srcA + dst.b * keep) / outA,
                    outA);
            }

            public Sprite ToSprite(string name)
            {
                var texture = new Texture2D(_size, _size, TextureFormat.RGBA32, false)
                {
                    name = name,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                texture.SetPixels(_pixels);
                texture.Apply();

                return Sprite.Create(texture, new Rect(0f, 0f, _size, _size),
                    new Vector2(0.5f, 0.5f), 100f);
            }
        }
    }
}
