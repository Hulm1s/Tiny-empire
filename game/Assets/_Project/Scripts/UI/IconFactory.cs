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
        Paint,

        // Added with physical cash, appended for the same reason.
        Cash,
        Bank
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
    public static partial class IconFactory
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
                case "egg": return Get("egg", DrawEgg, Supersample);
                case "milk": return Get("milk", DrawMilk, Supersample);
                case "corn": return Get("corn", DrawCorn, Supersample);
                case "hay": return Get("hay", DrawHay, Supersample);
                case "trash": return Get("trash", DrawTrash, Supersample);
                case "bread": return Get("bread", DrawBread, Supersample);
                case "apples": return Get("apples", DrawApple, Supersample);
                case "yogurt": return Get("yogurt", DrawYogurt, Supersample);
                case "meat": return Get("meat", DrawMeat, Supersample);
                case "steak": return Get("steak", DrawSteak, Supersample);
                case "sausage": return Get("sausage", DrawSausage, Supersample);
                case "pig": return Get("pig", DrawPig, Supersample);
                case "rose": return Get("rose", DrawRose, Supersample);
                case "tulip": return Get("tulip", DrawTulip, Supersample);
                case "sunflower": return Get("sunflower", DrawSunflower, Supersample);
                case "hydrangea": return Get("hydrangea", DrawHydrangea, Supersample);
                case "watering_can": return Get("watering_can", DrawWateringCan, Supersample);
                case "seed": return Get("seed", DrawSeedPacket, Supersample);
                case "bouquet": return Get("bouquet", DrawBouquet, Supersample);
                case "hair_bag": return Get("hair_bag", DrawHairBag, Supersample);
                case "hair_short": case "hair_bob": case "hair_long": case "hair_quiff": return SalonIcon(item.id);
            }

            // Unknown product: the old behaviour, a plain disc the caller tints.
            return UIFactory.Circle;
        }

        /// <summary>
        /// Paints one of the florist products into a 96-unit painter, exactly as its icon, so the
        /// bouquet minigame (<see cref="BouquetArt"/>) can render it at any size. False if the id
        /// is not one of them.
        /// </summary>
        internal static bool DrawFlorist(Painter p, string id)
        {
            switch (id)
            {
                case "rose": DrawRose(p); return true;
                case "tulip": DrawTulip(p); return true;
                case "sunflower": DrawSunflower(p); return true;
                case "hydrangea": DrawHydrangea(p); return true;
                case "bouquet": DrawBouquet(p); return true;
                default: return false;
            }
        }

        /// <summary>True when <see cref="For"/> returns something better than a plain disc.</summary>
        public static bool HasArtwork(ItemDefinition item) =>
            item != null && (item.icon != null ||
                item.id == "egg" || item.id == "milk" || item.id == "corn" || item.id == "hay" ||
                item.id == "trash" || item.id == "bread" || item.id == "apples" ||
                item.id == "yogurt" || item.id == "meat" || item.id == "steak" ||
                item.id == "sausage" || item.id == "pig" ||
                item.id == "rose" || item.id == "tulip" || item.id == "sunflower" ||
                item.id == "hydrangea" || item.id == "watering_can" || item.id == "bouquet" ||
                item.id == "seed" || item.id == "hair_bag" || item.id.StartsWith("hair_"));

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
                case SquareIcon.Cash: return Get("cash", DrawCash);
                case SquareIcon.Bank: return Get("bank", DrawBank);
                default: return null;
            }
        }

        private static Sprite Get(string key, System.Action<Painter> draw, int supersample = 1)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var painter = new Painter(Size, supersample);
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
        //
        // Drawn with the logo/arrow treatment: a thick warm-dark outline, an "inflated" body lit from
        // the upper left (cool shade bottom right, bounce light, glossy specular), flat details
        // clipped inside it, white gloss streaks and a soft ground shadow. 3x supersampled and
        // averaged down, so the edges are clean at the 96 px the game uses. Same icon sheet slot,
        // same entry points - only the drawing changed.

        /// <summary>Supersampling for the polished product icons.</summary>
        private const int Supersample = 3;

        private static readonly Color White = Color.white;

        internal static Color Rgb(float r, float g, float b, float a = 1f) => new Color(r, g, b, a);

        private static void DrawEgg(Painter p)
        {
            Painter.Shape egg = (x, y) => Sd.Egg(x, y, 48f, 43f, 28f, 36f, 0.30f);

            p.Blob(egg, Rgb(1f, 0.99f, 0.94f), Rgb(0.88f, 0.76f, 0.58f), outline: 3.4f, inflate: 17f, spec: 0.5f);

            // Freckles.
            Painter.Shape freckles = (x, y) => Mathf.Max(
                Mathf.Max(Sd.Circle(x, y, 58f, 50f, 2.4f), Sd.Circle(x, y, 38f, 33f, 2.0f)),
                Mathf.Max(Mathf.Max(Sd.Circle(x, y, 57f, 29f, 1.7f), Sd.Circle(x, y, 45f, 58f, 1.6f)),
                          Sd.Circle(x, y, 31f, 50f, 1.4f)));
            p.Fill(freckles, Rgb(0.70f, 0.46f, 0.26f, 0.6f), egg);

            // Gloss.
            p.Fill((x, y) => Sd.Capsule(x, y, 33f, 50f, 38f, 68f, 3.4f), Rgb(1f, 1f, 1f, 0.8f), egg, 1.6f);
            p.Fill((x, y) => Sd.Circle(x, y, 45f, 74f, 2.2f), Rgb(1f, 1f, 1f, 0.7f), egg, 1.2f);
            p.GroundShadow();
        }

        private static void DrawMilk(Painter p)
        {
            Painter.Shape bottle = (x, y) => Sd.Smooth(
                Sd.Box(x, y, 48f, 31f, 22f, 26f, 13f),
                Sd.Box(x, y, 48f, 66f, 10f, 12f, 5f), 8f);
            Painter.Shape cap = (x, y) => Sd.Box(x, y, 48f, 80f, 12.5f, 6.5f, 4f);

            p.Blob(bottle, Rgb(1f, 1f, 1f), Rgb(0.70f, 0.79f, 0.93f), outline: 3.2f, inflate: 15f, spec: 0.5f);

            // Blue label wrapped round the body, with a white drop on it.
            p.Blob((x, y) => Sd.Box(x, y, 48f, 28f, 32f, 11.5f, 3f),
                Rgb(0.50f, 0.76f, 1f), Rgb(0.18f, 0.44f, 0.86f), outline: 2.2f, inflate: 7f, spec: 0.3f, clip: bottle);
            p.Fill((x, y) => Mathf.Max(Sd.Circle(x, y, 48f, 25.5f, 5.3f),
                Sd.Poly(x, y, new[] { new Vector2(43.4f, 27.5f), new Vector2(52.6f, 27.5f), new Vector2(48f, 37.5f) }, 0.9f)),
                Rgb(1f, 1f, 1f, 0.95f), bottle);

            // Cap with two grip ridges.
            p.Blob(cap, Rgb(0.48f, 0.72f, 1f), Rgb(0.15f, 0.34f, 0.76f), outline: 3f, inflate: 8f, spec: 0.5f);
            p.Fill((x, y) => Mathf.Max(Sd.Box(x, y, 43f, 80f, 0.9f, 4.5f, 0.5f), Sd.Box(x, y, 53f, 80f, 0.9f, 4.5f, 0.5f)),
                Rgb(0.08f, 0.2f, 0.5f, 0.35f), cap);

            // Gloss.
            p.Fill((x, y) => Sd.Capsule(x, y, 34.5f, 19f, 34.5f, 50f, 2.9f), Rgb(1f, 1f, 1f, 0.85f), bottle, 1.6f);
            p.Fill((x, y) => Sd.Capsule(x, y, 43.5f, 62f, 43.5f, 70f, 1.8f), Rgb(1f, 1f, 1f, 0.8f), bottle, 1.2f);
            p.Fill((x, y) => Sd.Capsule(x, y, 40f, 78.5f, 46f, 78.5f, 1.4f), Rgb(1f, 1f, 1f, 0.7f), cap, 1.2f);
            p.GroundShadow();
        }

        private static void DrawCorn(Painter p)
        {
            var leafLit = Rgb(0.66f, 0.89f, 0.40f);
            var leafShade = Rgb(0.18f, 0.44f, 0.17f);

            // Stub of stalk, then the two back leaves.
            p.Blob((x, y) => Sd.Box(x, y, 48f, 9f, 5f, 5f, 2f), Rgb(0.78f, 0.58f, 0.32f), Rgb(0.4f, 0.26f, 0.12f),
                outline: 2.2f, inflate: 4f);
            Painter.Shape left = (x, y) => Sd.Leaf(x, y, 46f, 11f, 34f, 46f, 13.5f);
            Painter.Shape right = (x, y) => Sd.Leaf(x, y, 50f, 11f, -34f, 46f, 13.5f);
            p.Blob(left, leafLit, leafShade, outline: 3f, inflate: 8f, spec: 0.3f);
            p.Blob(right, leafLit, leafShade, outline: 3f, inflate: 8f, spec: 0.3f);

            // The cob, with its rows of kernels.
            Painter.Shape cob = (x, y) => Sd.Egg(x, y, 48f, 56f, 17.5f, 33f, 0.40f);
            p.Blob(cob, Rgb(1f, 0.92f, 0.42f), Rgb(0.88f, 0.54f, 0.06f), outline: 3.2f, inflate: 12f, spec: 0.35f);

            Painter.Shape kernels = (x, y) =>
            {
                float dy = (y - 56f) / 33f;
                if (dy <= -0.98f || dy >= 0.98f) return -1f;
                float hw = 17.5f * (1f - 0.40f * Mathf.Max(0f, dy)) * Mathf.Sqrt(1f - dy * dy);
                float u = Mathf.Clamp((x - 48f) / Mathf.Max(hw, 1f), -0.995f, 0.995f);
                float th = Mathf.Asin(u);
                const float rowH = 8.4f, colW = 0.56f;
                float ry = (y - 23f) / rowH;
                int row = Mathf.FloorToInt(ry);
                float fy = ry - row - 0.5f;
                float cx = th / colW + ((row & 1) == 1 ? 0.5f : 0f);
                float fx = cx - Mathf.Floor(cx) - 0.5f;
                float dxPx = fx * colW * hw * Mathf.Cos(th);
                float dyPx = fy * rowH;
                return 3.7f - Mathf.Sqrt(dxPx * dxPx + dyPx * dyPx);
            };
            p.Fill((x, y) => kernels(x, y) + 1.0f, Rgb(0.60f, 0.34f, 0.02f, 0.55f), cob);
            p.Fill(kernels, Rgb(1f, 0.89f, 0.40f, 0.7f), cob);
            p.Fill((x, y) => kernels(x + 1.2f, y - 1.2f) - 1.6f, Rgb(1f, 1f, 0.85f, 0.55f), cob, 0.8f);

            // Front leaf, over the foot of the cob, with a centre vein.
            Painter.Shape front = (x, y) => Sd.Leaf(x, y, 48f, 9f, 3f, 40f, 15f);
            p.Blob(front, Rgb(0.74f, 0.93f, 0.46f), Rgb(0.2f, 0.5f, 0.18f), outline: 3f, inflate: 9f, spec: 0.3f);
            p.Fill((x, y) => Sd.Capsule(x, y, 48.2f, 12f, 49.6f, 38f, 0.9f), Rgb(0.16f, 0.4f, 0.12f, 0.5f), front);

            // Gloss on the cob.
            p.Fill((x, y) => Sd.Capsule(x, y, 40f, 62f, 41f, 80f, 2.3f), Rgb(1f, 1f, 1f, 0.55f), cob, 1.4f);
            p.GroundShadow();
        }

        private static void DrawHay(Painter p)
        {
            var strawLit = Rgb(1f, 0.90f, 0.52f);
            var strawShade = Rgb(0.72f, 0.50f, 0.13f);

            // Loose blades poking out over the top, behind the bale.
            p.Blob((x, y) => Sd.Leaf(x, y, 25f, 63f, 30f, 17f, 3.0f), strawLit, strawShade, outline: 2f, inflate: 3f, spec: 0.1f);
            p.Blob((x, y) => Sd.Leaf(x, y, 38f, 66f, -6f, 19f, 3.0f), strawLit, strawShade, outline: 2f, inflate: 3f, spec: 0.1f);
            p.Blob((x, y) => Sd.Leaf(x, y, 55f, 66f, 10f, 18f, 3.0f), strawLit, strawShade, outline: 2f, inflate: 3f, spec: 0.1f);
            p.Blob((x, y) => Sd.Leaf(x, y, 68f, 63f, -26f, 16f, 3.0f), strawLit, strawShade, outline: 2f, inflate: 3f, spec: 0.1f);

            Painter.Shape bale = (x, y) => Sd.Box(x, y, 48f, 43f, 37f, 25f, 12f);
            p.Blob(bale, Rgb(1f, 0.89f, 0.50f), Rgb(0.72f, 0.50f, 0.13f), outline: 3.4f, inflate: 17f, spec: 0.2f);

            // Straw: fine wavy lines in two tones.
            Painter.Shape lines = (x, y) =>
            {
                float v = y + 1.5f * Mathf.Sin(x * 0.21f);
                float m = v - 5.6f * Mathf.Floor(v / 5.6f);
                return 0.8f - Mathf.Abs(m - 2.8f);
            };
            p.Fill(lines, Rgb(0.58f, 0.38f, 0.07f, 0.34f), bale);
            p.Fill((x, y) => lines(x, y + 2.8f), Rgb(1f, 0.97f, 0.75f, 0.35f), bale);

            // The two straps, rounded and a little proud of the bale.
            Painter.Shape strapA = (x, y) => Sd.Box(x, y, 31f, 43f, 4.6f, 27.5f, 2.6f);
            Painter.Shape strapB = (x, y) => Sd.Box(x, y, 65f, 43f, 4.6f, 27.5f, 2.6f);
            var twineLit = Rgb(0.80f, 0.56f, 0.30f);
            var twineShade = Rgb(0.34f, 0.19f, 0.08f);
            p.Blob(strapA, twineLit, twineShade, outline: 2.4f, inflate: 4.6f, spec: 0.3f);
            p.Blob(strapB, twineLit, twineShade, outline: 2.4f, inflate: 4.6f, spec: 0.3f);

            // Gloss.
            p.Fill((x, y) => Sd.Capsule(x, y, 17f, 57f, 24f, 60f, 2.6f), Rgb(1f, 1f, 1f, 0.5f), bale, 1.4f);
            p.Fill((x, y) => Sd.Capsule(x, y, 40f, 62f, 56f, 62f, 2.2f), Rgb(1f, 1f, 0.9f, 0.45f), bale, 1.4f);
            p.GroundShadow();
        }

        /// <summary>
        /// Raw meat: a glossy pink-red joint with a cream fat edge, white marbling and a cartoon
        /// bone sticking out. Pinker and bonier than the steak so the two never get mixed up.
        /// </summary>
        private static void DrawMeat(Painter p)
        {
            // Bone first, behind the joint: a shaft with a knuckle at the end.
            Painter.Shape shaft = (x, y) => Sd.Capsule(x, y, 58f, 54f, 76f, 72f, 4.6f);
            Painter.Shape knuckle = (x, y) => Mathf.Max(Sd.Circle(x, y, 75f, 78f, 6.4f), Sd.Circle(x, y, 82f, 70f, 6.4f));
            var boneLit = Rgb(1f, 0.98f, 0.92f);
            var boneShade = Rgb(0.72f, 0.66f, 0.56f);
            p.Blob(shaft, boneLit, boneShade, outline: 2.8f, inflate: 5f, spec: 0.4f);
            p.Blob(knuckle, boneLit, boneShade, outline: 2.8f, inflate: 6f, spec: 0.4f);

            Painter.Shape joint = (x, y) => Sd.Smooth(
                Sd.Ellipse(x, y, 40f, 38f, 29f, 23f, 12f),
                Sd.Circle(x, y, 58f, 46f, 18f), 12f);
            p.Blob(joint, Rgb(1f, 0.50f, 0.54f), Rgb(0.66f, 0.07f, 0.20f), outline: 3.4f, inflate: 17f, spec: 0.55f);

            // Fat edge: the crescent of the joint that is not covered by a copy nudged down-left.
            p.Fill((x, y) => Sd.Sub(joint(x, y), joint(x + 5f, y + 5f)), Rgb(1f, 0.93f, 0.86f), joint, 0.9f);

            // Marbling.
            var fat = Rgb(1f, 0.88f, 0.84f, 0.8f);
            p.Fill((x, y) => Sd.Capsule(x, y, 26f, 36f, 44f, 44f, 1.5f), fat, joint, 0.8f);
            p.Fill((x, y) => Sd.Capsule(x, y, 36f, 26f, 56f, 36f, 1.4f), fat, joint, 0.8f);
            p.Fill((x, y) => Sd.Capsule(x, y, 48f, 50f, 60f, 58f, 1.3f), fat, joint, 0.8f);

            // Gloss.
            p.Fill((x, y) => Sd.Capsule(x, y, 23f, 44f, 30f, 55f, 3f), Rgb(1f, 1f, 1f, 0.7f), joint, 1.5f);
            p.GroundShadow();
        }

        /// <summary>A seared steak: brown, grill-marked, ringed with a cream fat rim, with a bone circle.</summary>
        private static void DrawSteak(Painter p)
        {
            Painter.Shape steak = (x, y) => Sd.Smooth(
                Sd.Ellipse(x, y, 38f, 42f, 28f, 22f, 20f),
                Sd.Ellipse(x, y, 60f, 46f, 22f, 25f, -10f), 10f);
            // Fat rim = the whole steak in cream, then the browned meat over it, smaller.
            p.Blob(steak, Rgb(1f, 0.95f, 0.84f), Rgb(0.80f, 0.64f, 0.46f), outline: 3.4f, inflate: 15f, spec: 0.4f);

            Painter.Shape meat = (x, y) => steak(x, y) - 5.5f;
            p.Blob(meat, Rgb(0.94f, 0.50f, 0.34f), Rgb(0.46f, 0.15f, 0.08f), outline: 2.2f, inflate: 14f, spec: 0.45f);

            // Grill marks.
            var grill = Rgb(0.26f, 0.08f, 0.04f, 0.65f);
            for (int i = -1; i <= 1; i++)
            {
                float o = i * 15f;
                p.Fill((x, y) => Sd.Capsule(x, y, 28f + o, 28f, 52f + o, 66f, 2.4f), grill, meat, 0.8f);
            }

            // The bone, as a small cream ring in the lower left.
            Painter.Shape bone = (x, y) => Sd.Circle(x, y, 30f, 30f, 6.2f);
            p.Blob(bone, Rgb(1f, 0.99f, 0.93f), Rgb(0.74f, 0.67f, 0.56f), outline: 2.2f, inflate: 5f, spec: 0.4f);

            // Gloss.
            p.Fill((x, y) => Sd.Capsule(x, y, 24f, 56f, 36f, 66f, 3f), Rgb(1f, 1f, 1f, 0.55f), meat, 1.5f);
            p.GroundShadow();
        }

        /// <summary>A pair of sausages side by side, plump, glossy, with darker pinched ends.</summary>
        private static void DrawSausage(Painter p)
        {
            var lit = Rgb(1f, 0.64f, 0.46f);
            var shade = Rgb(0.62f, 0.18f, 0.10f);
            var tip = Rgb(0.45f, 0.12f, 0.06f, 0.75f);
            Painter.Shape back = (x, y) => Sd.Capsule(x, y, 38f, 16f, 74f, 52f, 11.5f);
            Painter.Shape front = (x, y) => Sd.Capsule(x, y, 22f, 30f, 58f, 66f, 11.5f);

            p.Blob(back, lit, shade, outline: 3.3f, inflate: 11f, spec: 0.6f);
            p.Fill((x, y) => Sd.Circle(x, y, 38f, 16f, 3.2f), tip, back, 1f);
            p.Fill((x, y) => Sd.Circle(x, y, 74f, 52f, 3.2f), tip, back, 1f);
            p.Blob(front, lit, shade, outline: 3.3f, inflate: 11f, spec: 0.6f);
            p.Fill((x, y) => Sd.Circle(x, y, 22f, 30f, 3.2f), tip, front, 1f);
            p.Fill((x, y) => Sd.Circle(x, y, 58f, 66f, 3.2f), tip, front, 1f);

            // Gloss along each one.
            p.Fill((x, y) => Sd.Capsule(x, y, 30f, 41f, 48f, 59f, 2.2f), Rgb(1f, 1f, 1f, 0.75f), front, 1.4f);
            p.Fill((x, y) => Sd.Capsule(x, y, 46f, 27f, 64f, 45f, 2.2f), Rgb(1f, 1f, 1f, 0.65f), back, 1.4f);
            p.GroundShadow();
        }

        /// <summary>A pig's face: round, pink, floppy ears, a big snout.</summary>
        private static void DrawPig(Painter p)
        {
            var lit = Rgb(1f, 0.78f, 0.82f);
            var shade = Rgb(0.84f, 0.38f, 0.52f);
            p.Blob((x, y) => Sd.Poly(x, y, new[] { new Vector2(17f, 76f), new Vector2(38f, 80f), new Vector2(28f, 58f) }, 4f),
                Rgb(1f, 0.70f, 0.76f), Rgb(0.76f, 0.30f, 0.44f), outline: 3f, inflate: 7f, spec: 0.3f);
            p.Blob((x, y) => Sd.Poly(x, y, new[] { new Vector2(79f, 76f), new Vector2(58f, 80f), new Vector2(68f, 58f) }, 4f),
                Rgb(1f, 0.70f, 0.76f), Rgb(0.76f, 0.30f, 0.44f), outline: 3f, inflate: 7f, spec: 0.3f);

            Painter.Shape head = (x, y) => Sd.Ellipse(x, y, 48f, 46f, 33f, 29f);
            p.Blob(head, lit, shade, outline: 3.4f, inflate: 20f, spec: 0.55f);

            // Snout with two nostrils.
            Painter.Shape snout = (x, y) => Sd.Ellipse(x, y, 48f, 35f, 16f, 12f);
            p.Blob(snout, Rgb(1f, 0.64f, 0.70f), Rgb(0.80f, 0.30f, 0.42f), outline: 2.4f, inflate: 9f, spec: 0.45f, clip: head);
            var dark = Rgb(0.42f, 0.12f, 0.20f);
            p.Fill((x, y) => Sd.Ellipse(x, y, 42.5f, 35f, 2.6f, 4.2f), dark, snout, 0.8f);
            p.Fill((x, y) => Sd.Ellipse(x, y, 53.5f, 35f, 2.6f, 4.2f), dark, snout, 0.8f);

            // Eyes with a glint.
            var eye = Rgb(0.16f, 0.10f, 0.12f);
            p.Fill((x, y) => Sd.Circle(x, y, 34f, 55f, 4f), eye, head, 0.8f);
            p.Fill((x, y) => Sd.Circle(x, y, 62f, 55f, 4f), eye, head, 0.8f);
            p.Fill((x, y) => Sd.Circle(x, y, 35.2f, 56.6f, 1.3f), Rgb(1f, 1f, 1f, 0.95f), head, 0.5f);
            p.Fill((x, y) => Sd.Circle(x, y, 63.2f, 56.6f, 1.3f), Rgb(1f, 1f, 1f, 0.95f), head, 0.5f);

            p.Fill((x, y) => Sd.Capsule(x, y, 28f, 62f, 38f, 70f, 2.8f), Rgb(1f, 1f, 1f, 0.6f), head, 1.4f);
            p.GroundShadow();
        }

        /// <summary>A loaf: a domed golden crust with three pale scoring slashes.</summary>
        private static void DrawBread(Painter p)
        {
            Painter.Shape loaf = (x, y) => Sd.Smooth(
                Sd.Box(x, y, 48f, 29f, 37f, 13f, 11f),
                Sd.Ellipse(x, y, 48f, 42f, 34f, 26f), 9f);

            p.Blob(loaf, Rgb(1f, 0.82f, 0.44f), Rgb(0.62f, 0.31f, 0.09f), outline: 3.4f, inflate: 19f, spec: 0.35f);

            for (int i = -1; i <= 1; i++)
            {
                float cx = 48f + i * 18f, cy = 47f - Mathf.Abs(i) * 3.5f;
                p.Blob((x, y) => Sd.Box(x, y, cx, cy, 3.1f, 9.5f, 3.1f, -32f),
                    Rgb(1f, 0.95f, 0.72f), Rgb(0.92f, 0.62f, 0.28f), outline: 1.7f, inflate: 3.2f, spec: 0.15f, clip: loaf);
            }

            // Darker base band and gloss.
            p.Fill((x, y) => 12f - y + 9f, Rgb(0.45f, 0.2f, 0.04f, 0.28f), loaf, 3f);
            p.Fill((x, y) => Sd.Capsule(x, y, 24f, 49f, 33f, 61f, 2.7f), Rgb(1f, 1f, 1f, 0.55f), loaf, 1.5f);
            p.GroundShadow();
        }

        /// <summary>A red apple with a bent stem and a leaf.</summary>
        private static void DrawApple(Painter p)
        {
            Painter.Shape apple = (x, y) => Sd.Sub(
                Sd.Smooth(Sd.Circle(x, y, 36f, 35f, 27f), Sd.Circle(x, y, 60f, 35f, 27f), 14f),
                Sd.Circle(x, y, 48f, 69f, 10.5f));

            // Leaf and stem first, then the fruit over their feet.
            Painter.Shape leaf = (x, y) => Sd.Leaf(x, y, 52f, 70f, -62f, 29f, 8.8f);
            p.Blob(leaf, Rgb(0.72f, 0.94f, 0.40f), Rgb(0.16f, 0.46f, 0.16f), outline: 2.6f, inflate: 5f, spec: 0.4f);
            p.Fill((x, y) => Sd.Capsule(x, y, 53f, 71f, 73f, 82f, 0.9f), Rgb(0.14f, 0.38f, 0.1f, 0.5f), leaf);

            p.Blob((x, y) => Sd.Capsule(x, y, 48f, 58f, 51f, 79f, 2.7f),
                Rgb(0.70f, 0.48f, 0.26f), Rgb(0.30f, 0.17f, 0.07f), outline: 2.2f, inflate: 3f, spec: 0.2f);

            p.Blob(apple, Rgb(1f, 0.46f, 0.36f), Rgb(0.62f, 0.05f, 0.12f), outline: 3.4f, inflate: 20f, spec: 0.7f);

            // Gloss: a bright streak and a dot on the upper left lobe.
            p.Fill((x, y) => Sd.Capsule(x, y, 20f, 42f, 24f, 56f, 3.4f), Rgb(1f, 1f, 1f, 0.8f), apple, 1.6f);
            p.Fill((x, y) => Sd.Circle(x, y, 30f, 62f, 2.4f), Rgb(1f, 1f, 1f, 0.75f), apple, 1.2f);
            p.GroundShadow();
        }

        // ---- the florist's goods ------------------------------------------------------------

        internal static readonly Color StemLit = new Color(0.55f, 0.82f, 0.32f);
        internal static readonly Color StemShade = new Color(0.14f, 0.42f, 0.16f);

        /// <summary>A green stalk from (x0,y0) to (x1,y1), outlined like the rest.</summary>
        /// <remarks><paramref name="k"/> scales the outline and rounding with the drawing, for
        /// the bouquet minigame's bigger flowers (<see cref="BouquetArt"/>); 1 is the icon.</remarks>
        internal static void IconStem(Painter p, float x0, float y0, float x1, float y1, float r = 2.8f, float k = 1f)
        {
            p.Blob((x, y) => Sd.Capsule(x, y, x0, y0, x1, y1, r), StemLit, StemShade, outline: 2.2f * k, inflate: 3f * k, spec: 0.2f);
        }

        internal static void IconLeaf(Painter p, float bx, float by, float angle, float len, float wid, float k = 1f)
        {
            Painter.Shape leaf = (x, y) => Sd.Leaf(x, y, bx, by, angle, len, wid);
            p.Blob(leaf, Rgb(0.70f, 0.93f, 0.42f), Rgb(0.16f, 0.46f, 0.16f), outline: 2.6f * k, inflate: 6f * k, spec: 0.35f);
            p.Fill((x, y) => Sd.Leaf(x, y, bx, by, angle, len * 0.9f, 0.9f * k), Rgb(0.14f, 0.38f, 0.1f, 0.4f), leaf);
        }

        private static Painter.Shape RingAt(float cx, float cy, float r, float w) =>
            (x, y) => w - Mathf.Abs(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r);

        // ---- the four flowers' heads ------------------------------------------------------
        // Each icon is its stem and leaves plus one of these blooms at scale 1. The bouquet
        // minigame (BouquetArt) draws the very same blooms bigger and on longer stems, so the
        // flower the player cuts and arranges is the flower on the product icon.
        // (cx, cy) is the bloom's anchor and k its scale; the icon's numbers are kept as
        // offsets from the anchor so scale 1 reproduces the icon exactly.

        internal static void RoseBloom(Painter p, float cx, float cy, float k)
        {
            Painter.Shape bloom = (x, y) => Sd.Circle(x, y, cx, cy, 29f * k);
            p.Blob(bloom, Rgb(1f, 0.70f, 0.80f), Rgb(0.78f, 0.16f, 0.42f), outline: 3.4f * k, inflate: 22f * k, spec: 0.6f);

            // Petal edges: arcs of rings off-centre, swirling in.
            var edge = Rgb(0.62f, 0.08f, 0.32f, 0.55f);
            p.Fill(RingAt(cx - 4f * k, cy - 4f * k, 22f * k, 1.3f * k), edge, bloom, 0.6f * k);
            p.Fill(RingAt(cx + 4f * k, cy + 3f * k, 15f * k, 1.3f * k), edge, bloom, 0.6f * k);
            p.Fill(RingAt(cx - 1f * k, cy - 2f * k, 8.5f * k, 1.3f * k), edge, bloom, 0.6f * k);
            p.Fill((x, y) => Sd.Circle(x, y, cx, cy, 4.5f * k), Rgb(0.66f, 0.08f, 0.34f, 0.7f), bloom, 1f * k);

            p.Fill((x, y) => Sd.Capsule(x, y, cx - 18f * k, cy + 3f * k, cx - 12f * k, cy + 19f * k, 3.2f * k), Rgb(1f, 1f, 1f, 0.8f), bloom, 1.6f * k);
            p.Fill((x, y) => Sd.Circle(x, y, cx - 3f * k, cy + 21f * k, 2.2f * k), Rgb(1f, 1f, 1f, 0.65f), bloom, 1.2f * k);
        }

        internal static void TulipBloom(Painter p, float cx, float cy, float k)
        {
            var lit = Rgb(1f, 0.52f, 0.46f);
            var shade = Rgb(0.74f, 0.08f, 0.20f);
            Painter.Shape left = (x, y) => Sd.Egg(x, y, cx - 10f * k, cy, 13f * k, 25f * k, 0.45f, -12f);
            Painter.Shape right = (x, y) => Sd.Egg(x, y, cx + 10f * k, cy, 13f * k, 25f * k, 0.45f, 12f);
            Painter.Shape mid = (x, y) => Sd.Egg(x, y, cx, cy + 2f * k, 14.5f * k, 28f * k, 0.42f);
            p.Blob(left, lit, shade, outline: 3f * k, inflate: 11f * k, spec: 0.4f);
            p.Blob(right, lit, shade, outline: 3f * k, inflate: 11f * k, spec: 0.4f);
            p.Blob(mid, Rgb(1f, 0.62f, 0.52f), Rgb(0.82f, 0.12f, 0.24f), outline: 3.2f * k, inflate: 14f * k, spec: 0.55f);
            p.Fill((x, y) => Sd.Capsule(x, y, cx - 8f * k, cy - 4f * k, cx - 7f * k, cy + 14f * k, 2.8f * k), Rgb(1f, 1f, 1f, 0.75f), mid, 1.4f * k);
        }

        internal static void SunflowerBloom(Painter p, float cx, float cy, float k)
        {
            for (int ring = 0; ring < 2; ring++)
            {
                int n = 13;
                float off = ring == 0 ? 0f : 360f / n * 0.5f;
                float rc = (ring == 0 ? 24f : 21f) * k, ry = (ring == 0 ? 12f : 11f) * k, rx = (ring == 0 ? 6.4f : 6.0f) * k;
                var lit = ring == 0 ? Rgb(1f, 0.82f, 0.30f) : Rgb(1f, 0.94f, 0.40f);
                var shade = ring == 0 ? Rgb(0.90f, 0.48f, 0.04f) : Rgb(0.96f, 0.62f, 0.06f);
                for (int i = 0; i < n; i++)
                {
                    float phi = i * 360f / n + off;
                    float px = cx - rc * Mathf.Sin(phi * Mathf.Deg2Rad), py = cy + rc * Mathf.Cos(phi * Mathf.Deg2Rad);
                    p.Blob((x, y) => Sd.Ellipse(x, y, px, py, rx, ry, phi), lit, shade, outline: 2f * k, inflate: 5f * k, spec: 0.3f);
                }
            }

            Painter.Shape core = (x, y) => Sd.Circle(x, y, cx, cy, 15f * k);
            p.Blob(core, Rgb(0.66f, 0.40f, 0.16f), Rgb(0.26f, 0.12f, 0.05f), outline: 2.6f * k, inflate: 12f * k, spec: 0.3f);
            // Seeds in a spiral.
            var seed = Rgb(0.20f, 0.09f, 0.04f, 0.8f);
            for (int s = 1; s <= 22; s++)
            {
                float a = s * 137.5f * Mathf.Deg2Rad, r = 11.5f * k * Mathf.Sqrt(s / 22f);
                float sx = cx + r * Mathf.Cos(a), sy = cy + r * Mathf.Sin(a);
                p.Fill((x, y) => Sd.Circle(x, y, sx, sy, 1.5f * k), seed, core, 0.6f * k);
            }
            p.Fill((x, y) => Sd.Capsule(x, y, cx - 10f * k, cy + 5f * k, cx - 6f * k, cy + 12f * k, 1.8f * k), Rgb(1f, 1f, 1f, 0.55f), core, 1.2f * k);
        }

        internal static void HydrangeaBloom(Painter p, float cx, float cy, float k)
        {
            Painter.Shape ball = (x, y) => Sd.Circle(x, y, cx, cy, 31f * k);
            p.Blob(ball, Rgb(0.56f, 0.72f, 1f), Rgb(0.20f, 0.30f, 0.80f), outline: 3.4f * k, inflate: 24f * k, spec: 0.4f);

            var tones = new[] { Rgb(0.78f, 0.88f, 1f), Rgb(0.46f, 0.64f, 1f), Rgb(0.62f, 0.60f, 1f) };
            var rim = Rgb(0.14f, 0.22f, 0.66f, 0.7f);
            float o = 2.7f * k, fr = 3.3f * k;
            for (int i = 0; i < 40; i++)
            {
                float a = i * 137.5f * Mathf.Deg2Rad, r = 27f * k * Mathf.Sqrt((i + 0.5f) / 40f);
                float fx = cx + r * Mathf.Cos(a), fy = cy + r * Mathf.Sin(a);
                var tone = tones[(i * 7) % 3];
                Painter.Shape floret = (x, y) => Mathf.Max(
                    Mathf.Max(Sd.Circle(x, y, fx - o, fy, fr), Sd.Circle(x, y, fx + o, fy, fr)),
                    Mathf.Max(Sd.Circle(x, y, fx, fy - o, fr), Sd.Circle(x, y, fx, fy + o, fr)));
                p.Fill((x, y) => floret(x, y) + 0.9f * k, rim, ball, 0.5f * k);
                p.Fill(floret, tone, ball, 0.5f * k);
                p.Fill((x, y) => Sd.Circle(x, y, fx, fy, 1.1f * k), Rgb(1f, 1f, 0.9f, 0.9f), ball, 0.4f * k);
            }
            p.Fill((x, y) => Sd.Capsule(x, y, cx - 20f * k, cy + 6f * k, cx - 14f * k, cy + 22f * k, 3.4f * k), Rgb(1f, 1f, 1f, 0.55f), ball, 1.8f * k);
        }

        private static void DrawRose(Painter p)
        {
            IconStem(p, 48f, 8f, 48f, 40f);
            IconLeaf(p, 47f, 20f, 58f, 24f, 8.5f);
            IconLeaf(p, 49f, 16f, -60f, 22f, 8f);
            RoseBloom(p, 48f, 59f, 1f);
            p.GroundShadow();
        }

        private static void DrawTulip(Painter p)
        {
            IconLeaf(p, 47f, 8f, 24f, 46f, 10f);
            IconLeaf(p, 49f, 8f, -26f, 42f, 9.5f);
            IconStem(p, 48f, 8f, 48f, 42f, 3f);
            TulipBloom(p, 48f, 62f, 1f);
            p.GroundShadow();
        }

        private static void DrawSunflower(Painter p)
        {
            IconStem(p, 48f, 6f, 48f, 34f, 3f);
            IconLeaf(p, 47f, 14f, 62f, 24f, 9f);
            IconLeaf(p, 49f, 12f, -62f, 24f, 9f);
            SunflowerBloom(p, 48f, 58f, 1f);
            p.GroundShadow();
        }

        private static void DrawHydrangea(Painter p)
        {
            IconLeaf(p, 47f, 10f, 58f, 36f, 12f);
            IconLeaf(p, 49f, 10f, -58f, 36f, 12f);
            HydrangeaBloom(p, 48f, 56f, 1f);
            p.GroundShadow();
        }

        private static void DrawWateringCan(Painter p)
        {
            var lit = Rgb(0.50f, 0.88f, 0.92f);
            var shade = Rgb(0.10f, 0.42f, 0.62f);

            // Back handle (a loop) and the carrying arc over the top.
            Painter.Shape back = RingAt(25f, 40f, 13f, 3.4f);
            p.Blob(back, Rgb(0.34f, 0.74f, 0.84f), Rgb(0.08f, 0.34f, 0.52f), outline: 2.4f, inflate: 3.4f, spec: 0.3f);
            var arcRing = RingAt(42f, 52f, 18f, 3.4f);
            Painter.Shape arc = (x, y) => Mathf.Min(arcRing(x, y), y - 52f);
            p.Blob(arc, Rgb(0.34f, 0.74f, 0.84f), Rgb(0.08f, 0.34f, 0.52f), outline: 2.4f, inflate: 3.4f, spec: 0.3f);

            // Spout with its sprinkler rose.
            p.Blob((x, y) => Sd.Capsule(x, y, 58f, 30f, 78f, 58f, 4.2f), lit, shade, outline: 2.6f, inflate: 4.2f, spec: 0.4f);
            Painter.Shape rose = (x, y) => Sd.Ellipse(x, y, 80f, 61f, 11f, 5.6f, 36f);
            p.Blob(rose, Rgb(0.30f, 0.64f, 0.80f), Rgb(0.06f, 0.28f, 0.46f), outline: 2.6f, inflate: 6f, spec: 0.5f);
            var hole = Rgb(0.05f, 0.2f, 0.34f, 0.8f);
            p.Fill((x, y) => Sd.Circle(x, y, 77.5f, 64f, 1.1f), hole, rose, 0.4f);
            p.Fill((x, y) => Sd.Circle(x, y, 81f, 61f, 1.1f), hole, rose, 0.4f);
            p.Fill((x, y) => Sd.Circle(x, y, 84.5f, 58f, 1.1f), hole, rose, 0.4f);

            // Body.
            Painter.Shape body = (x, y) => Sd.Box(x, y, 44f, 36f, 21f, 21f, 9f);
            p.Blob(body, lit, shade, outline: 3.4f, inflate: 14f, spec: 0.5f);
            p.Fill((x, y) => Sd.Box(x, y, 44f, 18f, 21f, 2f, 1f), Rgb(0.06f, 0.30f, 0.50f, 0.55f), body);
            p.Fill((x, y) => Sd.Box(x, y, 44f, 54f, 21f, 2f, 1f), Rgb(0.06f, 0.30f, 0.50f, 0.55f), body);
            p.Fill((x, y) => Sd.Capsule(x, y, 30f, 30f, 30f, 50f, 2.8f), Rgb(1f, 1f, 1f, 0.8f), body, 1.6f);

            // A few drops.
            var drop = Rgb(0.50f, 0.78f, 1f);
            var dropShade = Rgb(0.14f, 0.40f, 0.90f);
            p.Blob((x, y) => Sd.Egg(x, y, 88f, 44f, 3.2f, 4.6f, 0.4f), drop, dropShade, outline: 1.6f, inflate: 3f, spec: 0.5f);
            p.Blob((x, y) => Sd.Egg(x, y, 80f, 40f, 2.8f, 4.0f, 0.4f), drop, dropShade, outline: 1.6f, inflate: 3f, spec: 0.5f);
            p.GroundShadow();
        }

        private static void DrawBouquet(Painter p)
        {
            IconLeaf(p, 40f, 52f, 52f, 26f, 8f);
            IconLeaf(p, 56f, 52f, -52f, 26f, 8f);

            // Blooms peeking over the wrap: hydrangea, sunflower, rose, tulip.
            p.Blob((x, y) => Sd.Circle(x, y, 33f, 68f, 13f), Rgb(0.60f, 0.76f, 1f), Rgb(0.22f, 0.34f, 0.84f), outline: 2.6f, inflate: 10f, spec: 0.4f);
            p.Blob((x, y) => Sd.Circle(x, y, 63f, 68f, 13f), Rgb(1f, 0.90f, 0.36f), Rgb(0.92f, 0.52f, 0.04f), outline: 2.6f, inflate: 10f, spec: 0.4f);
            p.Blob((x, y) => Sd.Circle(x, y, 63f, 68f, 5.5f), Rgb(0.66f, 0.40f, 0.16f), Rgb(0.28f, 0.14f, 0.06f), outline: 1.4f, inflate: 5f);
            p.Blob((x, y) => Sd.Egg(x, y, 48f, 76f, 10f, 15f, 0.4f), Rgb(1f, 0.56f, 0.48f), Rgb(0.76f, 0.10f, 0.22f), outline: 2.6f, inflate: 9f, spec: 0.5f);
            Painter.Shape rose = (x, y) => Sd.Circle(x, y, 47f, 62f, 13.5f);
            p.Blob(rose, Rgb(1f, 0.72f, 0.82f), Rgb(0.80f, 0.18f, 0.44f), outline: 2.8f, inflate: 11f, spec: 0.55f);
            p.Fill((x, y) => Sd.Circle(x, y, 47f, 62f, 4.5f), Rgb(0.66f, 0.08f, 0.34f, 0.6f), rose, 1f);

            // The paper cone, a cream body with a pink flap down the front.
            Painter.Shape cone = (x, y) => Sd.Poly(x, y, new[] { new Vector2(48f, 12f), new Vector2(26f, 56f), new Vector2(70f, 56f) }, 4f);
            p.Blob(cone, Rgb(1f, 0.92f, 0.76f), Rgb(0.84f, 0.62f, 0.38f), outline: 3.2f, inflate: 12f, spec: 0.3f);
            p.Blob((x, y) => Sd.Poly(x, y, new[] { new Vector2(48f, 13f), new Vector2(40f, 54f), new Vector2(66f, 54f) }, 3f),
                Rgb(1f, 0.68f, 0.78f), Rgb(0.84f, 0.28f, 0.50f), outline: 2f, inflate: 8f, spec: 0.3f, clip: cone);

            // Ribbon with a bow.
            var rl = Rgb(0.72f, 0.54f, 1f);
            var rs = Rgb(0.38f, 0.18f, 0.74f);
            p.Blob((x, y) => Sd.Box(x, y, 48f, 31f, 14f, 4f, 2f, -4f), rl, rs, outline: 2.2f, inflate: 4f, spec: 0.4f, clip: cone);
            p.Blob((x, y) => Sd.Ellipse(x, y, 40f, 34f, 7.5f, 4.6f, 28f), rl, rs, outline: 2.2f, inflate: 4f, spec: 0.4f);
            p.Blob((x, y) => Sd.Ellipse(x, y, 56f, 34f, 7.5f, 4.6f, -28f), rl, rs, outline: 2.2f, inflate: 4f, spec: 0.4f);
            p.Blob((x, y) => Sd.Circle(x, y, 48f, 32f, 3.6f), rl, rs, outline: 2f, inflate: 3.6f, spec: 0.4f);
            p.Fill((x, y) => Sd.Capsule(x, y, 30f, 40f, 33f, 52f, 2.4f), Rgb(1f, 1f, 1f, 0.6f), cone, 1.4f);
            p.GroundShadow();
        }

        /// <summary>A yogurt pot: a tapered cup, foil lid with a peel tab, a pink label with a strawberry.</summary>
        private static void DrawYogurt(Painter p)
        {
            Painter.Shape cup = (x, y) => Sd.Poly(x, y,
                new[] { new Vector2(27f, 56f), new Vector2(69f, 56f), new Vector2(62.5f, 17f), new Vector2(33.5f, 17f) }, 6.5f);
            Painter.Shape lid = (x, y) => Sd.Box(x, y, 48f, 68f, 30.5f, 5.5f, 4.5f);

            p.Blob(cup, Rgb(1f, 1f, 1f), Rgb(0.68f, 0.75f, 0.90f), outline: 3.2f, inflate: 15f, spec: 0.45f);

            // Pink label band.
            p.Blob((x, y) => Sd.Box(x, y, 48f, 35f, 40f, 13f, 3f),
                Rgb(1f, 0.66f, 0.80f), Rgb(0.86f, 0.24f, 0.48f), outline: 2.2f, inflate: 8f, spec: 0.3f, clip: cup);

            // Strawberry: red body, green cap, seeds.
            Painter.Shape berry = (x, y) => Sd.Poly(x, y,
                new[] { new Vector2(42.5f, 39.5f), new Vector2(53.5f, 39.5f), new Vector2(48f, 30f) }, 3.8f);
            p.Blob(berry, Rgb(1f, 0.50f, 0.42f), Rgb(0.70f, 0.06f, 0.18f), outline: 1.8f, inflate: 5f, spec: 0.5f);
            p.Fill((x, y) => Sd.Ellipse(x, y, 48f, 44f, 5.4f, 2.3f), Rgb(0.34f, 0.74f, 0.26f), null);
            p.Fill((x, y) => Mathf.Max(Mathf.Max(Sd.Circle(x, y, 45.5f, 36.5f, 0.85f), Sd.Circle(x, y, 50.5f, 37.5f, 0.85f)),
                Sd.Circle(x, y, 48f, 33.5f, 0.8f)), Rgb(1f, 0.95f, 0.7f, 0.9f), berry);

            // Foil lid and its tab.
            p.Blob((x, y) => Sd.Box(x, y, 81f, 66.5f, 6.5f, 3.6f, 2f, 10f),
                Rgb(0.95f, 0.96f, 1f), Rgb(0.5f, 0.56f, 0.68f), outline: 2.2f, inflate: 3.5f, spec: 0.5f);
            p.Blob(lid, Rgb(0.97f, 0.98f, 1f), Rgb(0.48f, 0.54f, 0.67f), outline: 3f, inflate: 6f, spec: 0.7f);
            p.Fill((x, y) => Sd.Capsule(x, y, 30f, 70.5f, 62f, 70.5f, 1.5f), Rgb(1f, 1f, 1f, 0.85f), lid, 1.2f);

            // Gloss on the cup.
            p.Fill((x, y) => Sd.Capsule(x, y, 29.5f, 24f, 32.5f, 51f, 2.5f), Rgb(1f, 1f, 1f, 0.85f), cup, 1.5f);
            p.GroundShadow();
        }

        /// <summary>
        /// A tied-up rubbish bag: lumpy dark body, a pinched neck with two ears, creases and a
        /// glossy plastic sheen. Dark grey on the pale square, so it reads as mess.
        /// </summary>
        private static void DrawTrash(Painter p)
        {
            var lit = Rgb(0.60f, 0.68f, 0.68f);
            var shade = Rgb(0.13f, 0.16f, 0.19f);

            // The two ears of the tie, behind.
            p.Blob((x, y) => Sd.Leaf(x, y, 47f, 66f, 62f, 26f, 7.5f), lit, shade, outline: 2.6f, inflate: 5f, spec: 0.4f);
            p.Blob((x, y) => Sd.Leaf(x, y, 49f, 66f, -62f, 26f, 7.5f), lit, shade, outline: 2.6f, inflate: 5f, spec: 0.4f);

            Painter.Shape bag = (x, y) => Sd.Smooth(
                Sd.Ellipse(x, y, 48f, 33f, 32f, 27f),
                Sd.Box(x, y, 48f, 60f, 10f, 13f, 5f), 14f);
            p.Blob(bag, lit, shade, outline: 3.4f, inflate: 18f, spec: 0.55f);

            // The knot.
            p.Blob((x, y) => Sd.Circle(x, y, 48f, 70f, 6.4f), lit, shade, outline: 2.6f, inflate: 6f, spec: 0.5f);

            // Creases and a plastic sheen.
            var crease = Rgb(0.04f, 0.06f, 0.08f, 0.45f);
            p.Fill((x, y) => Sd.Capsule(x, y, 36f, 46f, 32f, 22f, 1.2f), crease, bag, 0.8f);
            p.Fill((x, y) => Sd.Capsule(x, y, 61f, 42f, 64f, 20f, 1.2f), crease, bag, 0.8f);
            p.Fill((x, y) => Sd.Capsule(x, y, 48f, 50f, 47f, 36f, 1.0f), crease, bag, 0.8f);
            p.Fill((x, y) => Sd.Capsule(x, y, 24f, 38f, 28f, 54f, 3.2f), Rgb(1f, 1f, 1f, 0.55f), bag, 1.6f);
            p.GroundShadow();
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

        /// <summary>A bundle of bills: green block, gold paper band, a dollar mark either side.</summary>
        private static void DrawCash(Painter p)
        {
            var bandTop = new Color(1f, 0.93f, 0.62f);
            var bandBottom = new Color(0.9f, 0.68f, 0.24f);
            var ink = new Color(0.12f, 0.36f, 0.14f, 0.85f);

            // The stack's edge, then the lit top face on it.
            p.Box(48f, 38f, 36f, 22f, 7f, new Color(0.26f, 0.58f, 0.24f), new Color(0.17f, 0.42f, 0.18f), outline: 2.4f);
            p.Box(48f, 48f, 36f, 22f, 7f, new Color(0.66f, 0.93f, 0.5f), new Color(0.32f, 0.7f, 0.3f), outline: 2.4f);
            p.Box(48f, 44f, 8f, 28f, 2f, bandTop, bandBottom, outline: 2.0f);
            p.Ellipse(27f, 48f, 7f, 7f, ink, ink);
            p.Ellipse(69f, 48f, 7f, 7f, ink, ink);

            p.Glow(30f, 62f, 5f, 6f, Highlight);
        }

        /// <summary>A little bank: pediment, columns, steps.</summary>
        private static void DrawBank(Painter p)
        {
            var top = new Color(1f, 0.95f, 0.8f);
            var bottom = new Color(0.86f, 0.74f, 0.5f);

            p.Box(48f, 18f, 36f, 6f, 2f, top, bottom, outline: 2.4f);
            p.Box(48f, 26f, 30f, 5f, 2f, top, bottom, outline: 2.4f);
            for (int i = -1; i <= 1; i++)
                p.Box(48f + i * 21f, 46f, 5.5f, 17f, 2f, top, bottom, outline: 2.2f);
            p.Box(48f, 66f, 33f, 5f, 2f, top, bottom, outline: 2.4f);
            p.Triangle(new Vector2(14f, 70f), new Vector2(82f, 70f), new Vector2(48f, 90f), top, bottom, outline: 2.4f);

            p.Glow(36f, 78f, 4f, 5f, Highlight);
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

        // ------------------------------------------------------------------ guide arrow

        private const int ArrowSize = 256;

        /// <summary>
        /// The guide arrow, drawn pointing DOWN; the caller rotates it. Solid in the given colour
        /// (green tutorial / red problem / gold goal) with the logo's treatment: a thick dark
        /// outline, a gold rim, a glossy cap and a soft drop shadow, on a chunky heraldic head.
        /// Cached per colour. Sprite only - no Material.
        /// </summary>
        public static Sprite GuideArrow(Color fill)
        {
            string key = "guide_arrow_" + ColorUtility.ToHtmlStringRGB(fill);
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var painter = new Painter(ArrowSize);
            DrawGuideArrow(painter, fill);
            var sprite = painter.ToSprite(key);
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Outline of the arrow, down-pointing, in the 256 px canvas: a short square tail, two
        /// upturned horns where it meets the head, then curved shield sides down to a point.
        /// Corners are filleted so the gold rim follows a soft shape, not a sharp polygon.
        /// </summary>
        private static Vector2[] ArrowPolygon()
        {
            var pts = new List<Vector2>();
            var radii = new List<float>();
            void Add(float x, float y, float r) { pts.Add(new Vector2(x, y)); radii.Add(r); }

            Add(98f, 236f, 14f);
            Add(158f, 236f, 14f);
            Add(158f, 178f, 9f);
            Add(228f, 200f, 9f);

            // Right side: quadratic curve from the horn to the tip, bulging outwards a little.
            var horn = new Vector2(228f, 200f);
            var ctrl = new Vector2(236f, 96f);
            var tip = new Vector2(128f, 24f);
            const int steps = 14;
            for (int k = 1; k < steps; k++)
            {
                float t = k / (float)steps;
                var q = (1 - t) * (1 - t) * horn + 2 * (1 - t) * t * ctrl + t * t * tip;
                Add(q.x, q.y, 0f);
            }
            Add(tip.x, tip.y, 7f);

            // Left side is the mirror image, walked back up (the tail corners are already in).
            int rightCount = pts.Count;
            for (int i = rightCount - 2; i >= 2; i--)
                Add(256f - pts[i].x, pts[i].y, radii[i]);

            return Fillet(pts, radii);
        }

        private static Vector2[] Fillet(List<Vector2> pts, List<float> radii)
        {
            var result = new List<Vector2>();
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                var a = pts[(i + n - 1) % n];
                var p = pts[i];
                var b = pts[(i + 1) % n];
                float r = radii[i];
                if (r <= 0f) { result.Add(p); continue; }

                var u = a - p; float lu = u.magnitude; u /= lu;
                var v = b - p; float lv = v.magnitude; v /= lv;
                float theta = Mathf.Acos(Mathf.Clamp(Vector2.Dot(u, v), -1f, 1f));
                if (theta > 3.0f || theta < 0.05f) { result.Add(p); continue; }

                float half = theta * 0.5f;
                float t = Mathf.Min(r / Mathf.Tan(half), 0.45f * Mathf.Min(lu, lv));
                r = t * Mathf.Tan(half);

                var t1 = p + u * t;
                var t2 = p + v * t;
                var centre = p + (u + v).normalized * (r / Mathf.Sin(half));
                float a1 = Mathf.Atan2(t1.y - centre.y, t1.x - centre.x);
                float a2 = Mathf.Atan2(t2.y - centre.y, t2.x - centre.x);
                float delta = Mathf.DeltaAngle(a1 * Mathf.Rad2Deg, a2 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                const int arc = 10;
                for (int k = 0; k <= arc; k++)
                {
                    float ang = a1 + delta * (k / (float)arc);
                    result.Add(centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r);
                }
            }
            return result.ToArray();
        }

        /// <summary>Signed distance to a polygon, positive INSIDE (the painter's convention).</summary>
        internal static float PolygonField(Vector2[] v, float x, float y)
        {
            float d = float.MaxValue;
            float sign = 1f;
            int n = v.Length;
            for (int i = 0, j = n - 1; i < n; j = i, i++)
            {
                float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
                float wx = x - v[i].x, wy = y - v[i].y;
                float h = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float bx = wx - ex * h, by = wy - ey * h;
                d = Mathf.Min(d, bx * bx + by * by);

                bool c1 = y >= v[i].y, c2 = y < v[j].y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
            }
            return -sign * Mathf.Sqrt(d);
        }

        private static void DrawGuideArrow(Painter p, Color fill)
        {
            const int N = ArrowSize;
            var poly = ArrowPolygon();
            float[] f = p.Field((x, y) => PolygonField(poly, x, y));
            // The same shape sampled 9 px higher, i.e. drawn 9 px lower: the drop shadow.
            float[] shadow = p.Field((x, y) => PolygonField(poly, x, y + 9f));

            float Cov(float d) => Mathf.Clamp01(d + 0.5f);
            float Ramp(float y) => Mathf.Clamp01((y - 24f) / 212f);

            var outline = new Color(0.15f, 0.08f, 0.04f);
            var goldTop = new Color(1f, 0.9f, 0.5f);
            var goldBottom = new Color(0.8f, 0.5f, 0.1f);
            var seam = new Color(0.36f, 0.19f, 0.04f);
            var fillTop = Color.Lerp(fill, Color.white, 0.2f);
            var fillBottom = new Color(fill.r * 0.72f, fill.g * 0.66f, fill.b * 0.66f);

            // Drop shadow.
            p.Raster((x, y) =>
            {
                float t = Mathf.Clamp01((shadow[y * N + x] + 14f) / 16f);
                return new Color(0.04f, 0.02f, 0.01f, 0.42f * t * t * (3f - 2f * t));
            });

            // Thick dark outline.
            p.Raster((x, y) => new Color(outline.r, outline.g, outline.b, Cov(f[y * N + x] + 11f)));

            // Gold rim, lit from above.
            p.Raster((x, y) =>
            {
                var c = Color.Lerp(goldBottom, goldTop, Ramp(y));
                c.a = Cov(f[y * N + x]);
                return c;
            });

            // Fine dark seam between rim and body, like the logo's frame.
            p.Raster((x, y) => new Color(seam.r, seam.g, seam.b, Cov(f[y * N + x] - 7.5f)));

            // Body: vertical gradient, the right half a shade darker so the head reads as two
            // heraldic facets rather than a flat blob.
            p.Raster((x, y) =>
            {
                var c = Color.Lerp(fillBottom, fillTop, Ramp(y));
                float right = Mathf.Clamp01(x + 0.5f - 128f);
                float shade = 1f - 0.16f * right;
                return new Color(c.r * shade, c.g * shade, c.b * shade, Cov(f[y * N + x] - 9.5f));
            });

            // Glossy cap: a domed white wash over the upper part, kept off the rim.
            p.Raster((x, y) =>
            {
                float dx = (x - 128f) / 175f, dy = (y - 332f) / 178f;
                float cap = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 14f);
                float a = Mathf.Lerp(0.06f, 0.40f, Mathf.Clamp01((y - 150f) / 90f));
                return new Color(1f, 1f, 1f, a * cap * Cov(f[y * N + x] - 14f));
            });

            // Small specular spot on the left shoulder.
            p.Glow(86f, 172f, 20f, 9f, new Color(1f, 1f, 1f, 0.55f));
        }

        // ------------------------------------------------------------------ shape library

        /// <summary>
        /// Signed-distance shapes for the product icons, in logical pixels, positive INSIDE (the
        /// painter's convention). Union is Max, cut-out is <see cref="Sub"/>, and <see cref="Smooth"/>
        /// melts two shapes together (a bottle's shoulder, an apple's two lobes).
        /// </summary>
        internal static class Sd
        {
            private static void Local(float x, float y, float cx, float cy, float angle, out float lx, out float ly)
            {
                float sin = Mathf.Sin(angle * Mathf.Deg2Rad), cos = Mathf.Cos(angle * Mathf.Deg2Rad);
                float ox = x - cx, oy = y - cy;
                lx = ox * cos + oy * sin;
                ly = -ox * sin + oy * cos;
            }

            public static float Circle(float x, float y, float cx, float cy, float r) =>
                r - Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));

            public static float Box(float x, float y, float cx, float cy, float hw, float hh, float r, float angle = 0f)
            {
                Local(x, y, cx, cy, angle, out float lx, out float ly);
                float qx = Mathf.Abs(lx) - (hw - r), qy = Mathf.Abs(ly) - (hh - r);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                return r - (outside + Mathf.Min(Mathf.Max(qx, qy), 0f));
            }

            public static float Ellipse(float x, float y, float cx, float cy, float rx, float ry, float angle = 0f)
            {
                Local(x, y, cx, cy, angle, out float lx, out float ly);
                float k0 = Mathf.Sqrt((lx / rx) * (lx / rx) + (ly / ry) * (ly / ry));
                float k1 = Mathf.Sqrt((lx / (rx * rx)) * (lx / (rx * rx)) + (ly / (ry * ry)) * (ly / (ry * ry)));
                if (k1 < 1e-6f) return Mathf.Min(rx, ry);
                return -(k0 * (k0 - 1f) / k1);
            }

            /// <summary>An egg: an ellipse that narrows towards its top by <paramref name="taper"/>.</summary>
            public static float Egg(float x, float y, float cx, float cy, float rx, float ry, float taper, float angle = 0f)
            {
                Local(x, y, cx, cy, angle, out float lx, out float ly);
                float dy = ly / ry;
                float width = rx * (1f - taper * Mathf.Max(0f, dy));
                return Ellipse(lx * (rx / Mathf.Max(width, 0.01f)), ly, 0f, 0f, rx, ry);
            }

            public static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
            {
                float px = x - ax, py = y - ay, ex = bx - ax, ey = by - ay;
                float h = Mathf.Clamp01((px * ex + py * ey) / (ex * ex + ey * ey));
                float dx = px - ex * h, dy = py - ey * h;
                return r - Mathf.Sqrt(dx * dx + dy * dy);
            }

            /// <summary>
            /// A pointed leaf (lens) standing on <c>(bx, by)</c>, <paramref name="angle"/> degrees
            /// anticlockwise from straight up, <paramref name="len"/> long and <paramref name="wid"/>
            /// wide at the half-width.
            /// </summary>
            public static float Leaf(float x, float y, float bx, float by, float angle, float len, float wid)
            {
                Local(x, y, bx, by, angle, out float lx, out float ly);
                float t = ly / len;
                float w = (t > 0f && t < 1f) ? wid * Mathf.Pow(Mathf.Sin(Mathf.PI * t), 0.8f) : 0f;
                float side = w - Mathf.Abs(lx);
                float ends = Mathf.Min(ly, len - ly) * 0.9f;
                return Mathf.Min(side, ends);
            }

            /// <summary>Convex polygon whose points are already inset by <paramref name="round"/>; the result is grown by it.</summary>
            public static float Poly(float x, float y, Vector2[] inner, float round) =>
                PolygonField(inner, x, y) + round;

            public static float Sub(float a, float b) => Mathf.Min(a, -b);

            public static float Smooth(float a, float b, float k)
            {
                float h = Mathf.Clamp01(0.5f + 0.5f * (a - b) / k);
                return Mathf.Lerp(b, a, h) + k * h * (1f - h);
            }
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
        internal class Painter
        {
            private delegate float Sdf(float x, float y);

            // Canvas size in (supersampled) pixels. Square for every icon; the bouquet minigame
            // art (BouquetArt) paints on taller canvases with the same Blob/Fill finish.
            private readonly int _w, _h;
            private readonly Color[] _pixels;

            private readonly int _ss;

            // The pixel window Blob and Fill work inside (x0/y0 inclusive, x1/y1 exclusive).
            // Normally the whole canvas; narrowed by SetWindow so a big canvas with many small
            // details does not evaluate every shape over every pixel.
            private int _wx0, _wy0, _wx1, _wy1;

            /// <param name="size">Logical icon size in pixels (what the sprite ends up being).</param>
            /// <param name="supersample">Draw at this multiple and average down, for the polished icons.</param>
            public Painter(int size, int supersample = 1) : this(size, size, supersample) { }

            public Painter(int width, int height, int supersample)
            {
                _ss = supersample;
                _w = width * supersample;
                _h = height * supersample;
                _pixels = new Color[_w * _h];
                ResetWindow();
            }

            /// <summary>Logical size of the canvas.</summary>
            public int Width => _w / _ss;
            public int Height => _h / _ss;

            /// <summary>
            /// Confines Blob and Fill to a logical rectangle (plus a little for outlines). Pixels
            /// outside it are left exactly as they are; shapes that reach past it are cut off,
            /// so give it the shape's bounds with its outline and some margin.
            /// </summary>
            public void SetWindow(float x0, float y0, float x1, float y1)
            {
                _wx0 = Mathf.Clamp(Mathf.FloorToInt(x0 * _ss) - 2, 0, _w);
                _wy0 = Mathf.Clamp(Mathf.FloorToInt(y0 * _ss) - 2, 0, _h);
                _wx1 = Mathf.Clamp(Mathf.CeilToInt(x1 * _ss) + 2, _wx0, _w);
                _wy1 = Mathf.Clamp(Mathf.CeilToInt(y1 * _ss) + 2, _wy0, _h);
            }

            public void ResetWindow()
            {
                _wx0 = 0; _wy0 = 0; _wx1 = _w; _wy1 = _h;
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

            // ---------------------------------------------------------- polished shapes
            // Used by the product icons. Coordinates are LOGICAL (0..96, origin bottom left) whatever
            // the supersampling; distances are in logical pixels, positive inside.

            public delegate float Shape(float x, float y);

            private static readonly Color Ink = new Color(0.17f, 0.09f, 0.05f);
            private static readonly Vector3 LightDir = new Vector3(-0.5f, 0.62f, 0.62f).normalized;
            private static readonly Vector3 BounceDir = new Vector3(0.6f, -0.55f, 0.35f).normalized;
            private static readonly Vector3 HalfDir = (LightDir + Vector3.forward).normalized;

            private static float Edge01(float a, float b, float x)
            {
                float t = Mathf.Clamp01((x - a) / (b - a));
                return t * t * (3f - 2f * t);
            }

            /// <summary>
            /// A glossy, "inflated" body: dark outline, then the shape lit as if it were a puffy
            /// cushion (soft light from the upper left, cool shade low right, a rim of bounce light
            /// and a specular glint). <paramref name="inflate"/> is how many pixels in from the edge
            /// the surface rounds over; <paramref name="clip"/> confines it (labels, bands).
            /// </summary>
            public void Blob(Shape shape, Color lit, Color shade, float outline = 3f, float inflate = 12f,
                float spec = 0.35f, Shape clip = null)
            {
                // Works in the window's own local grid: (x, y) below are window-relative, and
                // (ox, oy) puts them back on the canvas. With the default window this is the
                // whole canvas, exactly as before.
                int ox = _wx0, oy = _wy0;
                int n = _wx1 - _wx0, m = _wy1 - _wy0;
                if (n <= 0 || m <= 0) return;
                float s = _ss;
                var d = new float[n * m];
                float[] c = clip != null ? new float[n * m] : null;
                for (int y = 0; y < m; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float lx = (x + ox + 0.5f) / s, ly = (y + oy + 0.5f) / s;
                        d[y * n + x] = shape(lx, ly);
                        if (c != null) c[y * n + x] = clip(lx, ly);
                    }

                if (outline > 0f)
                    for (int y = 0; y < m; y++)
                        for (int x = 0; x < n; x++)
                        {
                            float cov = Mathf.Clamp01((d[y * n + x] + outline) * s + 0.5f);
                            if (c != null) cov *= Mathf.Clamp01(c[y * n + x] * s + 0.5f);
                            if (cov > 0f) Blend(x + ox, y + oy, Ink, cov);
                        }

                var hot = Color.Lerp(lit, Color.white, 0.25f);
                for (int y = 0; y < m; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int i = y * n + x;
                        float cov = Mathf.Clamp01(d[i] * s + 0.5f);
                        if (c != null) cov *= Mathf.Clamp01(c[i] * s + 0.5f);
                        if (cov <= 0f) continue;

                        int xm = Mathf.Max(x - 1, 0), xp = Mathf.Min(x + 1, n - 1);
                        int ym = Mathf.Max(y - 1, 0), yp = Mathf.Min(y + 1, m - 1);
                        float gx = (d[y * n + xp] - d[y * n + xm]) * s / (xp - xm);
                        float gy = (d[yp * n + x] - d[ym * n + x]) * s / (yp - ym);
                        float gl = Mathf.Sqrt(gx * gx + gy * gy);

                        float t = Mathf.Clamp01(Mathf.Max(d[i], 0f) / inflate);
                        float one = 1f - t;
                        float slope = Mathf.Min(4f, one / Mathf.Sqrt(Mathf.Max(0.02f, 1f - one * one)));
                        var nrm = gl > 1e-4f
                            ? new Vector3(-gx / gl * slope, -gy / gl * slope, 1f).normalized
                            : Vector3.forward;

                        float lam = Mathf.Clamp01(Vector3.Dot(nrm, LightDir));
                        var col = Color.Lerp(shade, lit, Edge01(0.05f, 0.85f, lam));
                        float bounce = Mathf.Clamp01(Vector3.Dot(nrm, BounceDir));
                        col = Color.Lerp(col, hot, bounce * bounce * 0.2f);
                        float sp = Mathf.Pow(Mathf.Clamp01(Vector3.Dot(nrm, HalfDir)), 30f) * spec;
                        col = Color.Lerp(col, Color.white, Mathf.Clamp01(sp));
                        col.a = 1f;
                        Blend(x + ox, y + oy, col, cov);
                    }
            }

            /// <summary>
            /// A flat layer (details, gloss, labels), optionally confined to <paramref name="clip"/>.
            /// <paramref name="soft"/> is the width in pixels of a feathered edge.
            /// </summary>
            public void Fill(Shape shape, Color color, Shape clip = null, float soft = 0f)
            {
                float s = _ss;
                float ramp = Mathf.Max(soft, 1f / s);
                for (int y = _wy0; y < _wy1; y++)
                    for (int x = _wx0; x < _wx1; x++)
                    {
                        float lx = (x + 0.5f) / s, ly = (y + 0.5f) / s;
                        float cov = Mathf.Clamp01(shape(lx, ly) / ramp + 0.5f);
                        if (cov <= 0f) continue;
                        if (clip != null) cov *= Mathf.Clamp01(clip(lx, ly) * s + 0.5f);
                        if (cov > 0f) Blend(x, y, color, cov);
                    }
            }

            /// <summary>A soft contact shadow dropped under whatever has been drawn so far.</summary>
            public void GroundShadow(float down = 3f, float blur = 2.4f, float alpha = 0.32f)
            {
                int n = _w, m = _h;
                int shift = Mathf.RoundToInt(down * _ss);
                int r = Mathf.Max(1, Mathf.RoundToInt(blur * _ss));
                var a = new float[n * m];
                for (int y = 0; y < m; y++)
                    for (int x = 0; x < n; x++)
                        a[y * n + x] = y + shift < m ? _pixels[(y + shift) * n + x].a : 0f;

                var tmp = new float[n * m];
                for (int y = 0; y < m; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float sum = 0f;
                        for (int k = -r; k <= r; k++) sum += a[y * n + Mathf.Clamp(x + k, 0, n - 1)];
                        tmp[y * n + x] = sum / (2 * r + 1);
                    }
                for (int y = 0; y < m; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float sum = 0f;
                        for (int k = -r; k <= r; k++) sum += tmp[Mathf.Clamp(y + k, 0, m - 1) * n + x];
                        a[y * n + x] = sum / (2 * r + 1);
                    }

                var shadow = new Color(0.10f, 0.05f, 0.02f);
                for (int i = 0; i < n * m; i++)
                {
                    float sa = a[i] * alpha;
                    if (sa <= 0.002f) continue;
                    var src = _pixels[i];
                    float outA = src.a + sa * (1f - src.a);
                    float w = sa * (1f - src.a);
                    _pixels[i] = new Color(
                        (src.r * src.a + shadow.r * w) / outA,
                        (src.g * src.a + shadow.g * w) / outA,
                        (src.b * src.a + shadow.b * w) / outA,
                        outA);
                }
            }

            /// <summary>Samples a signed-distance function once per pixel, for reuse across layers.</summary>
            public float[] Field(System.Func<float, float, float> sdf)
            {
                var field = new float[_w * _h];
                for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++)
                        field[y * _w + x] = sdf(x + 0.5f, y + 0.5f);
                return field;
            }

            /// <summary>Paints one layer, per pixel; the colour's alpha is the coverage.</summary>
            public void Raster(System.Func<int, int, Color> colourAt)
            {
                for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++)
                    {
                        var c = colourAt(x, y);
                        if (c.a > 0f) Blend(x, y, new Color(c.r, c.g, c.b, 1f), c.a);
                    }
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
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
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
                for (int y = 0; y < _h; y++)
                {
                    for (int x = 0; x < _w; x++)
                    {
                        if (Mathf.Abs(x + 0.5f - cx) > halfW) continue;
                        if (Mathf.Abs(y + 0.5f - cy) > halfH) continue;
                        _pixels[y * _w + x] = new Color(0f, 0f, 0f, 0f);
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
                for (int y = 0; y < _h; y++)
                {
                    // One gradient across the whole icon rather than per shape, so a stacked
                    // shape like the milk bottle is lit as a single object.
                    Color tone = Color.Lerp(bottom, top, y / (float)(_h - 1));

                    for (int x = 0; x < _w; x++)
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
                int index = y * _w + x;
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
                var texture = ToTexture(name);
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }

            /// <summary>
            /// The canvas as a texture. <paramref name="keep"/> keeps that many output pixels per
            /// logical pixel (it must divide the supersample): 1 is the icon's own size, and the
            /// bouquet art keeps more so it stays sharp on a big screen.
            /// </summary>
            public Texture2D ToTexture(string name, int keep = 1)
            {
                int step = Mathf.Max(1, _ss / Mathf.Max(1, keep));
                int outW = _w / step, outH = _h / step;
                var pixels = _pixels;
                if (step > 1)
                {
                    // Average the supersamples (premultiplied, so soft edges do not fringe).
                    pixels = new Color[outW * outH];
                    float inv = 1f / (step * step);
                    for (int y = 0; y < outH; y++)
                        for (int x = 0; x < outW; x++)
                        {
                            float r = 0f, g = 0f, b = 0f, a = 0f;
                            for (int sy = 0; sy < step; sy++)
                                for (int sx = 0; sx < step; sx++)
                                {
                                    var c = _pixels[(y * step + sy) * _w + x * step + sx];
                                    r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                                }
                            pixels[y * outW + x] = a > 0.0001f
                                ? new Color(r / a, g / a, b / a, a * inv)
                                : new Color(0f, 0f, 0f, 0f);
                        }
                }

                var texture = new Texture2D(outW, outH, TextureFormat.RGBA32, false)
                {
                    name = name,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };

                texture.SetPixels(pixels);
                texture.Apply();
                return texture;
            }
        }
    }
}
