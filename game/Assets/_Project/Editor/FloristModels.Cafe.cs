using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The café's 3D art (PROPOSAL, phase 1 - not wired into any builder): tables and chairs (inside and street-garden),
    /// the counter, the basic and premium coffee machines, the bean box, the oven, the display case with cakes, the
    /// fridge, the dishwasher, the delivery crates and the delivery van, the waiting stool, and the goods you carry (coffee
    /// cup, premium cup, cake slice, dirty cup). Each fitting that can be wrecked has a wrecked twin for the derelict café.
    /// Same builder, palette style and asset plumbing as the florist's and salon's models, in espresso brown, cream and
    /// terracotta; meshes bake under Meshes/Florist/Cafe and every material is an asset (Cf_*).
    ///
    /// Orientation: everything faces -z toward the customer / the room. A chair is modelled with the sitter facing -z
    /// (the backrest on +z); the café turns it so the sitter faces the table.
    /// </summary>
    public static partial class FloristModels
    {
        private static Material LibCafe(string k)
        {
            switch (k)
            {
                case "cf_espresso": return M("Cf_Espresso", C(0.30f, 0.18f, 0.10f), 0.35f);
                case "cf_wood": return M("Cf_Wood", C(0.62f, 0.42f, 0.24f), 0.35f);
                case "cf_wood_dark": return M("Cf_WoodDark", C(0.40f, 0.26f, 0.15f), 0.35f);
                case "cf_coffee": return M("Cf_Coffee", C(0.18f, 0.09f, 0.04f), 0.80f);
                case "cf_foam": return M("Cf_Foam", C(0.99f, 0.95f, 0.85f), 0.60f);
                case "cf_latte": return M("Cf_Latte", C(0.77f, 0.50f, 0.28f), 0.70f);
                case "cf_terra": return M("Cf_Terra", C(0.86f, 0.42f, 0.24f), 0.45f);
                case "cf_terra_dark": return M("Cf_TerraDark", C(0.64f, 0.28f, 0.15f), 0.45f);
                case "cf_cream": return M("Cf_Cream", C(0.99f, 0.95f, 0.86f), 0.40f);
                case "cf_ceramic": return M("Cf_Ceramic", C(0.98f, 0.97f, 0.94f), 0.80f);
                case "cf_steel": return M("Cf_Steel", C(0.82f, 0.85f, 0.88f), 0.78f, 0.45f);
                case "cf_steel_dark": return M("Cf_SteelDark", C(0.46f, 0.49f, 0.54f), 0.60f, 0.40f);
                case "cf_copper": return M("Cf_Copper", C(0.82f, 0.46f, 0.26f), 0.75f, 0.55f);
                case "cf_gold": return M("Cf_Gold", C(0.96f, 0.78f, 0.30f), 0.80f, 0.55f);
                case "cf_glass": return M("Cf_Glass", C(0.78f, 0.93f, 0.97f), 0.95f, 0.05f);
                case "cf_black": return M("Cf_Black", C(0.12f, 0.11f, 0.12f), 0.55f);
                case "cf_white": return M("Cf_White", C(0.96f, 0.97f, 0.98f), 0.55f);
                case "cf_mint": return M("Cf_Mint", C(0.74f, 0.92f, 0.86f), 0.50f);
                case "cf_green": return M("Cf_Green", C(0.34f, 0.62f, 0.30f), 0.45f);
                case "cf_teal": return M("Cf_Teal", C(0.22f, 0.55f, 0.52f), 0.50f);
                case "cf_sponge": return M("Cf_Sponge", C(0.80f, 0.52f, 0.30f), 0.30f);
                case "cf_icing": return M("Cf_Icing", C(1.00f, 0.74f, 0.84f), 0.55f);
                case "cf_cherry": return M("Cf_Cherry", C(0.84f, 0.10f, 0.18f), 0.85f);
                case "cf_jam": return M("Cf_Jam", C(0.72f, 0.12f, 0.20f), 0.60f);
                case "cf_glow": return M("Cf_Glow", C(1.00f, 0.62f, 0.18f), 0.60f);
                case "cf_tyre": return M("Cf_Tyre", C(0.13f, 0.13f, 0.15f), 0.20f);
                case "cf_bean": return M("Cf_Bean", C(0.33f, 0.19f, 0.10f), 0.55f);
                case "cf_burlap": return M("Cf_Burlap", C(0.80f, 0.68f, 0.46f), 0.10f);
                case "cf_egg": return M("Cf_Egg", C(0.97f, 0.88f, 0.72f), 0.55f);
                case "cf_blue": return M("Cf_Blue", C(0.45f, 0.70f, 0.98f), 0.55f);
                case "cf_stain": return M("Cf_Stain", C(0.36f, 0.20f, 0.10f), 0.30f);
                case "cf_wicker": return M("Cf_Wicker", C(0.80f, 0.64f, 0.40f), 0.20f);
                case "cf_grub_wood": return M("Cf_GrubWood", C(0.42f, 0.33f, 0.25f), 0.15f);
                case "cf_grub_wood_dark": return M("Cf_GrubWoodDark", C(0.28f, 0.21f, 0.16f), 0.15f);
                case "cf_grub_terra": return M("Cf_GrubTerra", C(0.52f, 0.37f, 0.30f), 0.15f);
                case "cf_grub_steel": return M("Cf_GrubSteel", C(0.48f, 0.49f, 0.48f), 0.25f, 0.15f);
                case "cf_grub_white": return M("Cf_GrubWhite", C(0.72f, 0.70f, 0.64f), 0.15f);
                case "cf_grub_glass": return M("Cf_GrubGlass", C(0.40f, 0.46f, 0.45f), 0.30f);
                case "cf_rust": return M("Cf_Rust", C(0.50f, 0.30f, 0.18f), 0.15f, 0.2f);
                case "cf_soot": return M("Cf_Soot", C(0.10f, 0.09f, 0.08f), 0.10f);
            }
            throw new ArgumentException(k);
        }

        public const string CafeFolder = "Cafe/";

        private static Model Mk(Action<GoodsMeshBuilder, Pal> build)
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            build(b, p);
            return new Model { Mesh = b, Pal = p };
        }

        private static string K(bool broken, string clean, string grub) => broken ? grub : clean;

        /// <summary>A small cylinder along Y (radius, height) standing on <paramref name="y0"/>.</summary>
        private static void Cyl(GoodsMeshBuilder b, int sub, Vector3 at, float r, float h, int segs = 20, float top = 1f)
        {
            b.Revolve(sub, segs, new List<Vector2> { V(0f, at.y), V(r, at.y), V(r * top, at.y + h), V(0f, at.y + h) }, (q, u, v) => q + new Vector3(at.x, 0f, at.z));
        }

        // ------------------------------------------------------------------ inside table and chair

        /// <summary>
        /// A round café table 0.9 across, top at 0.74: wooden top with a terracotta rim on a steel pedestal, a napkin holder and
        /// a little vase on it. Wrecked: the top slipped and tilted on a bent pole, grubby, a gouge in it.
        /// </summary>
        private static Model CafeTableModel(bool broken) => Mk((b, p) =>
        {
            int wood = p.I(K(broken, "cf_wood", "cf_grub_wood")), rim = p.I(K(broken, "cf_terra", "cf_grub_terra"));
            int steel = p.I(K(broken, "cf_steel_dark", "cf_grub_steel")), dark = p.I(K(broken, "cf_wood_dark", "cf_grub_wood_dark"));
            b.Revolve(dark, 24, new List<Vector2> { V(0f, 0f), V(0.30f, 0f), V(0.31f, 0.02f), V(0.26f, 0.05f), V(0f, 0.05f) });
            var lean = broken ? Quaternion.Euler(6f, 0f, 9f) : Quaternion.identity;
            Put(b, new Vector3(broken ? 0.04f : 0f, 0f, 0f), lean, 1f, () =>
            {
                Cyl(b, steel, new Vector3(0f, 0.04f, 0f), 0.04f, 0.68f, 12);
                b.Revolve(wood, 36, new List<Vector2> { V(0f, 0.70f), V(0.43f, 0.70f), V(0.45f, 0.715f), V(0.45f, 0.745f), V(0.43f, 0.76f), V(0f, 0.76f) });
                b.Revolve(rim, 36, new List<Vector2> { V(0.43f, 0.705f), V(0.462f, 0.71f), V(0.462f, 0.752f), V(0.43f, 0.758f) });
                Slab(b, steel, new Vector3(0f, 0.71f, 0f), new Vector3(0.12f, 0.012f, 0.012f), 0.3f);
                if (!broken)
                {
                    Slab(b, p.I("cf_white"), new Vector3(0.24f, 0.80f, 0.14f), new Vector3(0.05f, 0.04f, 0.015f), 0.3f);   // napkin holder
                    Cyl(b, p.I("cf_ceramic"), new Vector3(-0.22f, 0.76f, 0.16f), 0.032f, 0.06f, 14, 0.85f);                    // vase
                    Tube(b, p.I("cf_green"), t => new Vector3(-0.22f, 0.82f + t * 0.10f, 0.16f), Vector3.right, 0.005f, 0.004f, 3, 5);
                    Ell(b, p.I("cf_terra"), new Vector3(-0.22f, 0.935f, 0.16f), new Vector3(0.028f, 0.022f, 0.028f), 10, 7);
                }
                else Ell(b, p.I("cf_soot"), new Vector3(0.10f, 0.762f, -0.10f), new Vector3(0.09f, 0.004f, 0.05f), 10, 5);
            });
        });

        /// <summary>
        /// A café chair, seat top 0.445 (so the customer is seated at 0.285, like the salon's waiting chair), a terracotta
        /// cushion on a wooden frame with a slatted back on +z. Wrecked: one leg short so it leans, cushion torn and grey,
        /// a back slat missing.
        /// </summary>
        private static Model CafeChairModel(bool broken) => Mk((b, p) =>
        {
            int wood = p.I(K(broken, "cf_wood", "cf_grub_wood")), dark = p.I(K(broken, "cf_wood_dark", "cf_grub_wood_dark"));
            int cushion = p.I(K(broken, "cf_terra", "cf_grub_terra"));
            var lean = broken ? Quaternion.Euler(0f, 0f, 7f) : Quaternion.identity;
            Put(b, Vector3.zero, lean, 1f, () =>
            {
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                    {
                        float h = broken && sx > 0f && sz < 0f ? 0.32f : 0.40f;
                        Slab(b, dark, new Vector3(sx * 0.19f, h * 0.5f, sz * 0.19f), new Vector3(0.022f, h * 0.5f, 0.022f), 0.3f, nu: 24, nv: 6);
                    }
                Slab(b, wood, new Vector3(0f, 0.405f, 0f), new Vector3(0.22f, 0.017f, 0.22f), 0.2f);
                Slab(b, cushion, new Vector3(0f, 0.43f, -0.005f), new Vector3(0.20f, 0.02f, 0.20f), 0.45f);
                foreach (float sx in new[] { -1f, 1f })
                    Slab(b, dark, new Vector3(sx * 0.19f, 0.68f, 0.20f), new Vector3(0.02f, 0.28f, 0.018f), 0.3f, pitch: 4f);
                for (int i = 0; i < 3; i++)
                {
                    if (broken && i == 1) continue;
                    Slab(b, wood, new Vector3(0f, 0.58f + i * 0.13f, 0.205f), new Vector3(0.19f, 0.04f, 0.014f), 0.25f, pitch: 4f);
                }
                if (broken) Ell(b, p.I("cf_sponge"), new Vector3(0.06f, 0.455f, -0.04f), new Vector3(0.07f, 0.02f, 0.06f), 8, 5);
            });
        });

        /// <summary>A waiting stool by the street door: a round cushion on three legs, seat top 0.45.</summary>
        private static Model CafeStoolModel() => Mk((b, p) =>
        {
            int dark = p.I("cf_espresso"), cushion = p.I("cf_terra");
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                Tube(b, dark, t => new Vector3(Mathf.Cos(a) * Mathf.Lerp(0.07f, 0.17f, t), 0.40f * (1f - t), Mathf.Sin(a) * Mathf.Lerp(0.07f, 0.17f, t)), Vector3.up, 0.018f, 0.014f, 4, 6);
            }
            b.Revolve(cushion, 24, new List<Vector2> { V(0f, 0.40f), V(0.19f, 0.40f), V(0.21f, 0.425f), V(0.19f, 0.45f), V(0f, 0.455f) });
        });

        // ------------------------------------------------------------------ the street garden

        /// <summary>A bistro table for the pavement: a small teal metal top on a tripod, a flower pot on it. Rusty and bent when wrecked.</summary>
        private static Model GardenTableModel(bool broken) => Mk((b, p) =>
        {
            int top = p.I(K(broken, "cf_teal", "cf_rust")), steel = p.I(K(broken, "cf_steel_dark", "cf_grub_steel"));
            var lean = broken ? Quaternion.Euler(5f, 0f, -8f) : Quaternion.identity;
            Put(b, Vector3.zero, lean, 1f, () =>
            {
                for (int i = 0; i < 3; i++)
                {
                    float a = (i * 120f + 30f) * Mathf.Deg2Rad;
                    Tube(b, steel, t => new Vector3(Mathf.Cos(a) * Mathf.Lerp(0.05f, 0.32f, 1f - t), 0.70f * (1f - t), Mathf.Sin(a) * Mathf.Lerp(0.05f, 0.32f, 1f - t)), Vector3.up, 0.016f, 0.02f, 4, 6);
                }
                b.Revolve(top, 32, new List<Vector2> { V(0f, 0.68f), V(0.36f, 0.68f), V(0.38f, 0.70f), V(0.36f, 0.725f), V(0f, 0.725f) });
                if (!broken)
                {
                    Cyl(b, p.I("cf_terra"), new Vector3(0.05f, 0.725f, 0.06f), 0.05f, 0.085f, 14, 0.8f);
                    for (int k = 0; k < 5; k++)
                        Ell(b, p.I(k % 2 == 0 ? "cf_icing" : "cf_gold"), new Vector3(0.05f + 0.035f * Mathf.Cos(k * 72f * Mathf.Deg2Rad), 0.84f, 0.06f + 0.035f * Mathf.Sin(k * 72f * Mathf.Deg2Rad)), new Vector3(0.024f, 0.02f, 0.024f), 8, 6);
                    Ell(b, p.I("cf_green"), new Vector3(0.05f, 0.82f, 0.06f), new Vector3(0.04f, 0.03f, 0.04f), 8, 6);
                }
            });
        });

        /// <summary>A garden chair (sitter faces -z, backrest +z): a curved wicker seat and back on a steel frame, seat top 0.45.</summary>
        private static Model GardenChairModel(bool broken) => Mk((b, p) =>
        {
            int wick = p.I(K(broken, "cf_wicker", "cf_grub_terra")), steel = p.I(K(broken, "cf_steel_dark", "cf_grub_steel"));
            var lean = broken ? Quaternion.Euler(0f, 0f, -9f) : Quaternion.identity;
            Put(b, Vector3.zero, lean, 1f, () =>
            {
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sz in new[] { -1f, 1f })
                        Slab(b, steel, new Vector3(sx * 0.19f, 0.20f, sz * 0.19f), new Vector3(0.014f, 0.20f, 0.014f), 0.3f, nu: 24, nv: 6);
                Slab(b, wick, new Vector3(0f, 0.415f, 0f), new Vector3(0.22f, 0.025f, 0.22f), 0.3f);
                foreach (float sx in new[] { -1f, 1f })
                    Slab(b, steel, new Vector3(sx * 0.21f, 0.68f, 0.20f), new Vector3(0.014f, 0.27f, 0.014f), 0.3f, pitch: 5f);
                Slab(b, wick, new Vector3(0f, 0.70f, 0.205f), new Vector3(0.21f, 0.20f, 0.02f), 0.3f, pitch: 5f);
                if (!broken)
                    for (int i = 0; i < 4; i++) Slab(b, steel, new Vector3(0f, 0.56f + i * 0.09f, 0.185f), new Vector3(0.19f, 0.008f, 0.008f), 0.3f, nu: 12, nv: 6);
            });
        });

        // ------------------------------------------------------------------ the counter, the machines, the bean box

        /// <summary>
        /// The service counter 4.0 long, 0.9 deep, top at 1.0, facing -z: a terracotta front with cream panels and a wooden top, a
        /// cup shelf and a small till at the east end. Wrecked: dull, panels missing and a drawer hanging.
        /// </summary>
        private static Model CafeCounterModel(bool broken) => Mk((b, p) =>
        {
            const float W = 4.0f;
            int terra = p.I(K(broken, "cf_terra", "cf_grub_terra")), cream = p.I(K(broken, "cf_cream", "cf_grub_white"));
            int wood = p.I(K(broken, "cf_wood", "cf_grub_wood")), dark = p.I(K(broken, "cf_espresso", "cf_grub_wood_dark"));
            Slab(b, dark, new Vector3(0f, 0.04f, 0f), new Vector3(W * 0.5f + 0.02f, 0.04f, 0.46f), 0.2f);
            Slab(b, terra, new Vector3(0f, 0.49f, 0.02f), new Vector3(W * 0.5f, 0.45f, 0.40f), 0.12f);
            Slab(b, wood, new Vector3(0f, 0.985f, -0.01f), new Vector3(W * 0.5f + 0.04f, 0.03f, 0.46f), 0.2f, roll: broken ? 0.8f : 0f);
            for (int i = 0; i < 7; i++)
            {
                if (broken && (i == 2 || i == 5)) continue;
                float x = -1.72f + i * 0.575f;
                Slab(b, cream, new Vector3(x, 0.50f, -0.385f), new Vector3(0.23f, 0.31f, 0.014f), 0.25f);
                Slab(b, terra, new Vector3(x, 0.50f, -0.40f), new Vector3(0.17f, 0.25f, 0.006f), 0.3f);
            }
            if (broken) Slab(b, dark, new Vector3(0.60f, 0.52f, -0.50f), new Vector3(0.22f, 0.12f, 0.14f), 0.25f, yaw: 12f);
            else
            {
                // A till at the east end and a row of cups on the back of the counter.
                Slab(b, p.I("cf_black"), new Vector3(1.55f, 1.075f, 0.02f), new Vector3(0.20f, 0.06f, 0.16f), 0.3f, pitch: -8f);
                Slab(b, p.I("cf_steel"), new Vector3(1.55f, 1.16f, 0.12f), new Vector3(0.16f, 0.06f, 0.012f), 0.3f);
                for (int i = 0; i < 4; i++)
                {
                    Cyl(b, p.I("cf_ceramic"), new Vector3(-1.75f + i * 0.13f, 1.015f, 0.26f), 0.045f, 0.075f, 14, 1.15f);
                    Cyl(b, p.I("cf_terra"), new Vector3(-1.75f + i * 0.13f, 1.035f, 0.26f), 0.047f, 0.012f, 14, 1.15f);
                }
            }
        });

        /// <summary>
        /// A basic espresso machine 0.9 wide, 0.55 tall (origin at its base): a steel body with a black top, two group heads,
        /// a drip tray, a steam wand and a row of cups on top. <paramref name="premium"/>: a bigger copper and gold machine
        /// 1.1 wide, 0.75 tall, with arched side panels, two pressure dials, a cup warmer rail, a milk jug and a glowing power gem.
        /// </summary>
        private static Model CoffeeMachineModel(bool premium) => Mk((b, p) =>
        {
            float W = premium ? 1.10f : 0.86f, H = premium ? 0.74f : 0.52f, D = premium ? 0.58f : 0.50f;
            int body = p.I(premium ? "cf_copper" : "cf_steel"), trim = p.I(premium ? "cf_gold" : "cf_steel_dark");
            int blk = p.I("cf_black"), steel = p.I("cf_steel");
            Slab(b, trim, new Vector3(0f, 0.025f, 0f), new Vector3(W * 0.5f + 0.02f, 0.025f, D * 0.5f + 0.02f), 0.2f);
            Slab(b, body, new Vector3(0f, 0.03f + (H - 0.03f) * 0.5f, 0.02f), new Vector3(W * 0.5f, (H - 0.03f) * 0.5f, D * 0.5f - 0.02f), premium ? 0.3f : 0.18f);
            Slab(b, blk, new Vector3(0f, H + 0.005f, 0.02f), new Vector3(W * 0.5f + 0.01f, 0.02f, D * 0.5f - 0.01f), 0.2f);
            Slab(b, blk, new Vector3(0f, H * 0.58f, -D * 0.5f + 0.01f), new Vector3(W * 0.40f, H * 0.18f, 0.012f), 0.3f);       // front plate
            // Two group heads with portafilters and spouts.
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * W * 0.23f;
                Cyl(b, trim, new Vector3(x, H * 0.38f, -D * 0.5f - 0.02f), 0.055f, 0.06f, 16, 0.9f);
                Slab(b, blk, new Vector3(x, H * 0.36f, -D * 0.5f - 0.13f), new Vector3(0.02f, 0.02f, 0.10f), 0.4f);
                Cyl(b, steel, new Vector3(x, H * 0.30f, -D * 0.5f - 0.02f), 0.007f, 0.05f, 8);
            }
            // Drip tray.
            Slab(b, steel, new Vector3(0f, 0.065f, -D * 0.5f - 0.04f), new Vector3(W * 0.42f, 0.014f, 0.09f), 0.25f);
            for (int i = 0; i < 7; i++) Slab(b, blk, new Vector3(-W * 0.36f + i * W * 0.12f, 0.08f, -D * 0.5f - 0.04f), new Vector3(0.012f, 0.004f, 0.07f), 0.4f);
            // The steam wand at the east end.
            Tube(b, steel, t => new Vector3(W * 0.5f + 0.03f, H * 0.55f - t * 0.20f, -D * 0.2f - t * 0.05f), Vector3.up, 0.012f, 0.008f, 5, 6);
            Ell(b, p.I(premium ? "cf_glow" : "cf_cherry"), new Vector3(W * 0.36f, H * 0.78f, -D * 0.5f - 0.005f), new Vector3(0.016f, 0.016f, 0.01f), 8, 6);
            if (premium)
            {
                foreach (float sx in new[] { -1f, 1f })
                {
                    Cyl(b, p.I("cf_ceramic"), new Vector3(sx * 0.26f, H * 0.80f, -D * 0.5f - 0.005f), 0.052f, 0.012f, 20);
                    Slab(b, p.I("cf_black"), new Vector3(sx * 0.26f, H * 0.80f + 0.002f, -D * 0.5f - 0.014f), new Vector3(0.003f, 0.032f, 0.003f), 0.4f, roll: sx * 35f);
                }
                // Arched copper crest and a gold cup-warmer rail with cups.
                Slab(b, trim, new Vector3(0f, H + 0.07f, 0.02f), new Vector3(W * 0.42f, 0.012f, D * 0.30f), 0.25f);
                foreach (float sx in new[] { -1f, 1f })
                    Slab(b, trim, new Vector3(sx * W * 0.42f, H + 0.035f, 0.02f), new Vector3(0.012f, 0.04f, D * 0.30f), 0.3f);
                for (int i = 0; i < 4; i++)
                {
                    Cyl(b, p.I("cf_ceramic"), new Vector3(-0.32f + i * 0.21f, H + 0.08f, 0.04f), 0.045f, 0.065f, 14, 1.15f);
                    Cyl(b, p.I("cf_gold"), new Vector3(-0.32f + i * 0.21f, H + 0.105f, 0.04f), 0.047f, 0.012f, 14, 1.15f);
                }
                // The milk jug beside it.
                Put(b, new Vector3(-W * 0.5f - 0.17f, 0f, -0.10f), Quaternion.identity, 1f, () =>
                {
                    b.Revolve(p.I("cf_steel"), 16, GoodsMeshBuilder.Spline(new[] { V(0f, 0f), V(0.055f, 0f), V(0.07f, 0.09f), V(0.055f, 0.17f), V(0f, 0.17f) }, 3));
                    Slab(b, p.I("cf_black"), new Vector3(0.08f, 0.10f, 0f), new Vector3(0.012f, 0.05f, 0.012f), 0.3f);
                });
            }
        });

        /// <summary>The wrecked machine of the derelict café: a dented grey box, a door off, a cracked gauge, steam-black soot.</summary>
        private static Model BrokenCoffeeMachineModel() => Mk((b, p) =>
        {
            int body = p.I("cf_grub_steel"), blk = p.I("cf_soot"), rust = p.I("cf_rust");
            Put(b, Vector3.zero, Quaternion.Euler(0f, 0f, 6f), 1f, () =>
            {
                Slab(b, body, new Vector3(0f, 0.26f, 0.02f), new Vector3(0.43f, 0.25f, 0.23f), 0.16f);
                Slab(b, blk, new Vector3(0f, 0.53f, 0.02f), new Vector3(0.44f, 0.02f, 0.24f), 0.2f);
                Slab(b, rust, new Vector3(0.25f, 0.46f, -0.25f), new Vector3(0.10f, 0.07f, 0.008f), 0.3f);
                Cyl(b, p.I("cf_grub_glass"), new Vector3(-0.15f, 0.30f, -0.255f), 0.05f, 0.04f, 14);
            });
            Slab(b, body, new Vector3(0.36f, 0.04f, -0.34f), new Vector3(0.14f, 0.02f, 0.10f), 0.3f, yaw: 30f);     // the drip tray, on the floor
        });

        /// <summary>The free bean box: an open wooden crate full of roasted beans and a burlap sack beside it, 1.1 x 0.7.</summary>
        private static Model BeanBoxModel() => Mk((b, p) =>
        {
            int wood = p.I("cf_wood"), dark = p.I("cf_wood_dark"), bean = p.I("cf_bean"), sack = p.I("cf_burlap");
            Slab(b, wood, new Vector3(-0.15f, 0.17f, 0f), new Vector3(0.38f, 0.17f, 0.30f), 0.12f);
            Slab(b, dark, new Vector3(-0.15f, 0.34f, 0f), new Vector3(0.40f, 0.015f, 0.32f), 0.2f);
            var rnd = new System.Random(5);
            for (int i = 0; i < 38; i++)
            {
                float x = -0.15f + ((float)rnd.NextDouble() - 0.5f) * 0.62f, z = ((float)rnd.NextDouble() - 0.5f) * 0.48f;
                float h = 0.36f + 0.06f * (1f - Mathf.Min(1f, (Mathf.Abs(x + 0.15f) / 0.31f + Mathf.Abs(z) / 0.24f) * 0.6f));
                b.Ellipsoid(bean, new Vector3(x, h, z), new Vector3(0.034f, 0.022f, 0.022f), 8, 6, (float)rnd.NextDouble() * 180f);
            }
            Put(b, new Vector3(0.42f, 0f, 0.02f), Quaternion.Euler(0f, 12f, 0f), 1f, () =>
            {
                b.Revolve(sack, 20, GoodsMeshBuilder.Spline(new[] { V(0f, 0f), V(0.17f, 0f), V(0.20f, 0.16f), V(0.16f, 0.34f), V(0.07f, 0.40f), V(0f, 0.40f) }, 3));
                Slab(b, p.I("cf_terra"), new Vector3(0f, 0.19f, -0.19f), new Vector3(0.09f, 0.07f, 0.008f), 0.3f);
            });
        });

        // ------------------------------------------------------------------ the kitchen: oven, display case, fridge, dishwasher

        /// <summary>
        /// A baking oven 1.15 wide, 0.85 deep, 1.55 tall, facing -z: a steel body with a big glass door and a glowing orange cavity
        /// with a tray of cakes, three knobs, a clock and a chimney vent. Wrecked: black with soot, the door hanging off, cold and empty.
        /// </summary>
        private static Model OvenModel(bool broken) => Mk((b, p) =>
        {
            int body = p.I(K(broken, "cf_steel", "cf_grub_steel")), dark = p.I(K(broken, "cf_steel_dark", "cf_soot"));
            int glass = p.I(K(broken, "cf_glow", "cf_grub_glass")), glow = p.I(K(broken, "cf_glow", "cf_soot"));
            Slab(b, dark, new Vector3(0f, 0.04f, 0f), new Vector3(0.58f, 0.04f, 0.43f), 0.2f);
            Slab(b, body, new Vector3(0f, 0.78f, 0.02f), new Vector3(0.57f, 0.74f, 0.40f), 0.12f);
            Slab(b, dark, new Vector3(0f, 1.50f, 0.1f), new Vector3(0.30f, 0.07f, 0.18f), 0.2f);         // the chimney
            // The control strip with knobs and a clock.
            Slab(b, p.I("cf_black"), new Vector3(0f, 1.34f, -0.395f), new Vector3(0.52f, 0.12f, 0.015f), 0.3f);
            for (int i = 0; i < 3; i++) Cyl(b, p.I(K(broken, "cf_gold", "cf_rust")), new Vector3(-0.32f + i * 0.17f, 1.34f, -0.405f), 0.035f, 0.02f, 12);
            Ell(b, p.I(K(broken, "cf_cream", "cf_grub_white")), new Vector3(0.34f, 1.34f, -0.41f), new Vector3(0.06f, 0.06f, 0.012f), 12, 8);
            // The door: a dark frame round a glass window, a long handle.
            var hang = broken ? Quaternion.Euler(0f, 0f, 14f) : Quaternion.identity;
            Slab(b, glow, new Vector3(0f, 0.74f, -0.30f), new Vector3(0.42f, 0.33f, 0.04f), 0.15f);        // the cavity, glowing
            if (!broken)
            {
                // A tray with two cakes inside.
                Slab(b, p.I("cf_steel"), new Vector3(0f, 0.58f, -0.31f), new Vector3(0.36f, 0.012f, 0.05f), 0.3f);
                for (int i = 0; i < 2; i++)
                {
                    Ell(b, p.I("cf_sponge"), new Vector3(-0.17f + i * 0.34f, 0.69f, -0.455f), new Vector3(0.12f, 0.075f, 0.03f), 14, 8);
                    Ell(b, p.I("cf_terra_dark"), new Vector3(-0.17f + i * 0.34f, 0.72f, -0.462f), new Vector3(0.10f, 0.04f, 0.02f), 12, 6);
                }
            }
            Put(b, new Vector3(broken ? -0.05f : 0f, 0f, broken ? -0.03f : 0f), hang, 1f, () =>
            {
                Slab(b, dark, new Vector3(0f, 0.74f, -0.415f), new Vector3(0.50f, 0.38f, 0.02f), 0.2f);
                Slab(b, glass, new Vector3(0f, 0.74f, -0.43f), new Vector3(0.42f, 0.30f, 0.008f), 0.2f);
                if (!broken) Slab(b, p.I("cf_steel"), new Vector3(0f, 0.62f, -0.445f), new Vector3(0.38f, 0.012f, 0.03f), 0.3f);
                Slab(b, p.I(K(broken, "cf_steel", "cf_rust")), new Vector3(0f, 1.11f, -0.46f), new Vector3(0.40f, 0.018f, 0.018f), 0.3f);
            });
        });

        /// <summary>A whole round cake on a stand: sponge, cream, sponge, pink icing, cherries.</summary>
        private static void WholeCake(GoodsMeshBuilder b, Pal p, Vector3 at, float s)
        {
            Put(b, at, Quaternion.identity, s, () =>
            {
                Cyl(b, p.I("cf_cream"), new Vector3(0f, 0f, 0f), 0.14f, 0.01f, 20);
                Cyl(b, p.I("cf_sponge"), new Vector3(0f, 0.01f, 0f), 0.115f, 0.05f, 20);
                Cyl(b, p.I("cf_cream"), new Vector3(0f, 0.06f, 0f), 0.117f, 0.018f, 20);
                Cyl(b, p.I("cf_sponge"), new Vector3(0f, 0.078f, 0f), 0.115f, 0.05f, 20);
                Cyl(b, p.I("cf_icing"), new Vector3(0f, 0.128f, 0f), 0.12f, 0.02f, 20);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad;
                    Ell(b, p.I("cf_cherry"), new Vector3(Mathf.Cos(a) * 0.075f, 0.168f, Mathf.Sin(a) * 0.075f), new Vector3(0.017f, 0.017f, 0.017f), 8, 6);
                }
            });
        }

        /// <summary>
        /// The display case 1.6 wide, 0.7 deep, 1.2 tall, facing -z: a terracotta base, a glass box with two shelves of whole cakes
        /// and slices, a gold-trimmed top with a little price sign. Wrecked: grubby, a smashed pane, the shelves bare.
        /// </summary>
        private static Model DisplayCaseModel(bool broken) => Mk((b, p) =>
        {
            const float W = 1.6f, D = 0.7f;
            int terra = p.I(K(broken, "cf_terra", "cf_grub_terra")), glass = p.I(K(broken, "cf_glass", "cf_grub_glass"));
            int trim = p.I(K(broken, "cf_gold", "cf_rust")), cream = p.I(K(broken, "cf_cream", "cf_grub_white"));
            int darkw = p.I(K(broken, "cf_wood_dark", "cf_grub_wood_dark"));
            Slab(b, p.I(K(broken, "cf_espresso", "cf_grub_wood_dark")), new Vector3(0f, 0.04f, 0f), new Vector3(W * 0.5f + 0.02f, 0.04f, D * 0.5f + 0.02f), 0.2f);
            Slab(b, terra, new Vector3(0f, 0.30f, 0.02f), new Vector3(W * 0.5f, 0.26f, D * 0.5f - 0.02f), 0.12f);
            Slab(b, cream, new Vector3(0f, 0.31f, -D * 0.5f + 0.02f), new Vector3(W * 0.42f, 0.16f, 0.01f), 0.3f);
            Slab(b, trim, new Vector3(0f, 0.575f, 0f), new Vector3(W * 0.5f + 0.01f, 0.015f, D * 0.5f + 0.01f), 0.25f);        // the counter top the glass stands on
            // A tall back panel and a stepped display: the lower shelf at the front, a higher one at the back, open to the top so you see the cakes.
            Slab(b, darkw, new Vector3(0f, 0.92f, D * 0.5f - 0.03f), new Vector3(W * 0.5f - 0.01f, 0.34f, 0.018f), 0.2f);
            Slab(b, glass, new Vector3(0f, 0.68f, -D * 0.5f + 0.04f), new Vector3(W * 0.5f - 0.02f, 0.10f, 0.008f), 0.2f, pitch: broken ? 8f : 0f);   // the front pane, low
            Slab(b, glass, new Vector3(0f, 0.97f, 0.12f), new Vector3(W * 0.5f - 0.02f, 0.012f, 0.008f), 0.2f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Slab(b, glass, new Vector3(sx * (W * 0.5f - 0.02f), 0.74f, 0f), new Vector3(0.008f, 0.17f, D * 0.5f - 0.05f), 0.2f);
                Slab(b, trim, new Vector3(sx * (W * 0.5f - 0.02f), 0.74f, -D * 0.5f + 0.04f), new Vector3(0.014f, 0.18f, 0.014f), 0.3f);
            }
            Slab(b, trim, new Vector3(0f, 0.855f, -D * 0.5f + 0.04f), new Vector3(W * 0.5f - 0.01f, 0.012f, 0.014f), 0.3f);
            Slab(b, glass, new Vector3(0f, 0.80f, 0.17f), new Vector3(W * 0.5f - 0.04f, 0.008f, 0.14f), 0.25f);               // the back, higher shelf
            if (!broken)
            {
                for (int i = 0; i < 3; i++) WholeCake(b, p, new Vector3(-0.50f + i * 0.50f, 0.81f, 0.17f), 1.0f);
                for (int i = 0; i < 4; i++)
                    Put(b, new Vector3(-0.60f + i * 0.40f, 0.59f, -0.10f), Quaternion.Euler(0f, 90f, 0f), 1f, () => CakeSliceShape(b, p, false));
                Slab(b, p.I("cf_white"), new Vector3(0.62f, 0.64f, -0.30f), new Vector3(0.10f, 0.045f, 0.01f), 0.3f, pitch: 14f);
                Slab(b, p.I("cf_terra"), new Vector3(0.62f, 0.64f, -0.309f), new Vector3(0.07f, 0.008f, 0.004f), 0.3f);
            }
            else
            {
                Slab(b, p.I("cf_soot"), new Vector3(0.45f, 0.585f, -0.10f), new Vector3(0.08f, 0.012f, 0.06f), 0.3f);
                Slab(b, p.I("sl_crack"), new Vector3(0.25f, 0.68f, -D * 0.5f + 0.035f), new Vector3(0.012f, 0.09f, 0.004f), 0.3f, roll: 28f);
            }
        });

        /// <summary>The fridge, 0.95 x 0.75 x 1.9: a mint cabinet with two doors, chrome handles, a few magnets. Wrecked: dented grey, a door ajar.</summary>
        private static Model FridgeModel(bool broken) => Mk((b, p) =>
        {
            int body = p.I(K(broken, "cf_mint", "cf_grub_white")), chrome = p.I(K(broken, "cf_steel", "cf_grub_steel"));
            int dark = p.I(K(broken, "cf_steel_dark", "cf_soot"));
            Slab(b, dark, new Vector3(0f, 0.04f, 0f), new Vector3(0.47f, 0.04f, 0.37f), 0.2f);
            Slab(b, body, new Vector3(0f, 0.97f, 0.02f), new Vector3(0.47f, 0.93f, 0.34f), 0.12f);
            var ajar = broken ? Quaternion.Euler(0f, -22f, 0f) : Quaternion.identity;
            Slab(b, chrome, new Vector3(0f, 1.15f, -0.355f), new Vector3(0.45f, 0.008f, 0.008f), 0.3f);                 // the door seam
            Put(b, new Vector3(-0.47f, 0f, -0.37f), ajar, 1f, () =>
                {
                    Slab(b, body, new Vector3(0.47f, 1.45f, -0.01f), new Vector3(0.45f, 0.40f, 0.015f), 0.2f);       // freezer door
                    Slab(b, chrome, new Vector3(0.85f, 1.35f, -0.04f), new Vector3(0.012f, 0.18f, 0.012f), 0.3f);
                    if (!broken)
                    {
                        Slab(b, p.I("cf_terra"), new Vector3(0.20f, 1.52f, -0.03f), new Vector3(0.035f, 0.035f, 0.006f), 0.3f);
                        Slab(b, p.I("cf_gold"), new Vector3(0.30f, 1.40f, -0.03f), new Vector3(0.03f, 0.03f, 0.006f), 0.3f);
                        Slab(b, p.I("cf_cream"), new Vector3(0.26f, 1.60f, -0.03f), new Vector3(0.07f, 0.05f, 0.004f), 0.3f, roll: -8f);
                    }
                });
            Slab(b, body, new Vector3(0f, 0.58f, -0.36f), new Vector3(0.45f, 0.53f, 0.015f), 0.2f);                      // the big door
            Slab(b, chrome, new Vector3(0.38f, 0.82f, -0.39f), new Vector3(0.012f, 0.22f, 0.012f), 0.3f);
            if (broken) Slab(b, p.I("cf_rust"), new Vector3(-0.2f, 0.25f, -0.375f), new Vector3(0.12f, 0.10f, 0.004f), 0.3f);
        });

        /// <summary>
        /// The dishwasher: a counter-high steel unit 0.95 x 0.75 x 1.0 with a sink and a tap on top, a stack of cups in the basin, a
        /// front door with a dial. Wrecked: rusty, the door hanging and dripping.
        /// </summary>
        private static Model DishwasherModel(bool broken) => Mk((b, p) =>
        {
            int body = p.I(K(broken, "cf_steel", "cf_grub_steel")), dark = p.I(K(broken, "cf_steel_dark", "cf_soot"));
            int teal = p.I(K(broken, "cf_teal", "cf_rust"));
            Slab(b, dark, new Vector3(0f, 0.04f, 0f), new Vector3(0.47f, 0.04f, 0.37f), 0.2f);
            Slab(b, body, new Vector3(0f, 0.48f, 0.01f), new Vector3(0.47f, 0.44f, 0.35f), 0.12f);
            Slab(b, teal, new Vector3(0f, 0.96f, 0.0f), new Vector3(0.48f, 0.04f, 0.37f), 0.2f);
            Slab(b, p.I(K(broken, "cf_steel_dark", "cf_soot")), new Vector3(-0.12f, 0.995f, 0f), new Vector3(0.26f, 0.012f, 0.26f), 0.3f);   // the basin
            Tube(b, p.I(K(broken, "cf_steel", "cf_grub_steel")), t => new Vector3(0.28f, 1.0f + Mathf.Sin(t * 2.6f) * 0.20f, 0.18f - t * 0.12f), Vector3.right, 0.016f, 0.012f, 6, 6);
            var ajar = broken ? Quaternion.Euler(24f, 0f, 0f) : Quaternion.identity;
            Put(b, new Vector3(0f, 0.12f, -0.36f), ajar, 1f, () =>
            {
                Slab(b, p.I(K(broken, "cf_steel", "cf_grub_steel")), new Vector3(0f, 0.34f, 0f), new Vector3(0.43f, 0.32f, 0.015f), 0.2f);
                Slab(b, p.I("cf_black"), new Vector3(0f, 0.56f, -0.02f), new Vector3(0.12f, 0.03f, 0.008f), 0.3f);
                Cyl(b, p.I(K(broken, "cf_gold", "cf_rust")), new Vector3(0.28f, 0.54f, -0.02f), 0.03f, 0.02f, 12);
            });
            if (!broken)
                for (int i = 0; i < 4; i++)
                {
                    Cyl(b, p.I("cf_ceramic"), new Vector3(-0.2f + (i % 2) * 0.14f, 1.01f + (i / 2) * 0.07f, 0.06f - (i / 2) * 0.03f), 0.045f, 0.065f, 14, 1.15f);
                    Cyl(b, p.I("cf_stain"), new Vector3(-0.2f + (i % 2) * 0.14f, 1.07f + (i / 2) * 0.07f, 0.06f - (i / 2) * 0.03f), 0.04f, 0.005f, 12);
                }
        });

        // ------------------------------------------------------------------ the delivery

        /// <summary>A wooden delivery crate 0.9 x 0.7 x 0.55, kind "milk" (six bottles) or "eggs" (three trays of eggs).</summary>
        private static Model SupplyCrateModel(bool milk) => Mk((b, p) =>
        {
            int wood = p.I("cf_wood"), dark = p.I("cf_wood_dark");
            Slab(b, dark, new Vector3(0f, 0.04f, 0f), new Vector3(0.45f, 0.03f, 0.34f), 0.2f);
            for (int i = 0; i < 3; i++)
                Slab(b, wood, new Vector3(0f, 0.12f + i * 0.13f, -0.325f), new Vector3(0.45f, 0.055f, 0.012f), 0.2f);
            for (int i = 0; i < 3; i++)
                Slab(b, wood, new Vector3(0f, 0.12f + i * 0.13f, 0.325f), new Vector3(0.45f, 0.055f, 0.012f), 0.2f);
            foreach (float sx in new[] { -1f, 1f })
            {
                for (int i = 0; i < 3; i++) Slab(b, wood, new Vector3(sx * 0.44f, 0.12f + i * 0.13f, 0f), new Vector3(0.012f, 0.055f, 0.33f), 0.2f);
                Slab(b, dark, new Vector3(sx * 0.44f, 0.27f, 0f), new Vector3(0.02f, 0.30f - 0.05f, 0.03f), 0.3f);
            }
            Slab(b, p.I("cf_cream"), new Vector3(0f, 0.25f, -0.34f), new Vector3(0.16f, 0.07f, 0.006f), 0.3f);
            Slab(b, p.I(milk ? "cf_blue" : "cf_terra"), new Vector3(0f, 0.25f, -0.346f), new Vector3(0.12f, 0.012f, 0.004f), 0.3f);
            if (milk)
            {
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 2; j++)
                    {
                        Put(b, new Vector3(-0.28f + i * 0.28f, 0.07f, -0.15f + j * 0.30f), Quaternion.identity, 1f, () =>
                        {
                            b.Revolve(p.I("cf_white"), 16, GoodsMeshBuilder.Spline(new[] { V(0f, 0f), V(0.075f, 0f), V(0.085f, 0.04f), V(0.085f, 0.24f), V(0.05f, 0.32f), V(0.035f, 0.40f), V(0f, 0.40f) }, 3));
                            Cyl(b, p.I("cf_blue"), new Vector3(0f, 0.40f, 0f), 0.038f, 0.04f, 12);
                            Slab(b, p.I("cf_blue"), new Vector3(0f, 0.13f, -0.084f), new Vector3(0.05f, 0.05f, 0.006f), 0.3f);
                        });
                    }
            }
            else
            {
                for (int t = 0; t < 2; t++)
                {
                    Slab(b, p.I("cf_burlap"), new Vector3(0f, 0.115f + t * 0.14f, 0f), new Vector3(0.40f, 0.012f, 0.29f), 0.25f);
                    for (int i = 0; i < 5; i++)
                        for (int j = 0; j < 3; j++)
                            Ell(b, p.I("cf_egg"), new Vector3(-0.32f + i * 0.16f, 0.17f + t * 0.14f, -0.18f + j * 0.18f), new Vector3(0.048f, 0.062f, 0.048f), 10, 8);
                }
            }
        });

        /// <summary>
        /// The delivery van that arrives with the milk and eggs, 1.5 wide x 3.2 long, front at -z: a white box body with a green
        /// stripe, a cab with a windscreen, a round cow-and-egg badge on the side, four wheels. Origin at the middle of its ground footprint.
        /// </summary>
        private static Model DeliveryVanModel() => Mk((b, p) =>
        {
            int white = p.I("cf_white"), green = p.I("cf_green"), glass = p.I("cf_glass"), tyre = p.I("cf_tyre"), steel = p.I("cf_steel");
            Slab(b, white, new Vector3(0f, 0.95f, 0.45f), new Vector3(0.74f, 0.70f, 1.15f), 0.14f);                  // the cargo box
            Slab(b, white, new Vector3(0f, 0.62f, -1.05f), new Vector3(0.72f, 0.38f, 0.48f), 0.2f);                  // the bonnet
            Slab(b, white, new Vector3(0f, 1.08f, -0.78f), new Vector3(0.70f, 0.42f, 0.34f), 0.2f);                  // the cab
            Slab(b, glass, new Vector3(0f, 1.18f, -1.075f), new Vector3(0.58f, 0.26f, 0.012f), 0.3f, pitch: -16f);    // the windscreen
            foreach (float sx in new[] { -1f, 1f })
            {
                Slab(b, glass, new Vector3(sx * 0.715f, 1.16f, -0.78f), new Vector3(0.012f, 0.22f, 0.26f), 0.3f);
                Slab(b, green, new Vector3(sx * 0.755f, 0.92f, 0.45f), new Vector3(0.008f, 0.10f, 1.15f), 0.3f);       // the stripe
                // The badge: a round disc with an egg on it.
                Put(b, new Vector3(sx * 0.752f, 1.12f, 0.45f), Quaternion.Euler(0f, 0f, 90f), 1f, () =>
                {
                    Cyl(b, p.I("cf_cream"), Vector3.zero, 0.26f, 0.012f, 24);
                });
                Ell(b, p.I("cf_egg"), new Vector3(sx * 0.77f, 1.12f, 0.45f), new Vector3(0.012f, 0.14f, 0.10f), 10, 8);
                foreach (float sz in new[] { -0.82f, 0.88f })
                {
                    Put(b, new Vector3(sx * 0.70f, 0.26f, sz), Quaternion.Euler(0f, 0f, 90f), 1f, () =>
                    {
                        Cyl(b, tyre, new Vector3(0f, -0.09f, 0f), 0.27f, 0.18f, 22);
                        Cyl(b, steel, new Vector3(0f, 0.09f, 0f), 0.13f, 0.012f, 16);
                    });
                }
            }
            Slab(b, p.I("cf_black"), new Vector3(0f, 0.22f, -1.52f), new Vector3(0.70f, 0.07f, 0.04f), 0.3f);       // the bumper
            foreach (float sx in new[] { -1f, 1f })
                Ell(b, p.I("cf_glow"), new Vector3(sx * 0.48f, 0.66f, -1.53f), new Vector3(0.10f, 0.07f, 0.03f), 10, 6);
            Slab(b, green, new Vector3(0f, 1.655f, 0.45f), new Vector3(0.14f, 0.012f, 1.15f), 0.3f);                   // the roof line
        });

        // ------------------------------------------------------------------ carried goods

        private static void CupShape(GoodsMeshBuilder b, Pal p, bool premium, bool dirty)
        {
            int cer = p.I(dirty ? "cf_grub_white" : "cf_ceramic");
            // Saucer.
            b.Revolve(p.I(premium && !dirty ? "cf_gold" : dirty ? "cf_grub_white" : "cf_ceramic"), 28,
                new List<Vector2> { V(0f, 0.0f), V(0.088f, 0.0f), V(0.098f, 0.016f), V(0.07f, 0.016f), V(0f, 0.018f) });
            // The cup: a tapered bowl.
            float top = premium ? 0.062f : 0.052f;
            b.Revolve(cer, 28, GoodsMeshBuilder.Spline(new[] { V(0f, 0.018f), V(0.032f, 0.018f), V(top * 0.85f, 0.05f), V(top, 0.095f), V(top - 0.008f, 0.095f), V(top - 0.016f, 0.07f), V(0.03f, 0.03f), V(0f, 0.03f) }, 2));
            // Handle.
            Tube(b, cer, t => new Vector3(top - 0.004f + Mathf.Sin(t * Mathf.PI) * 0.045f, 0.082f - t * 0.045f, 0f), Vector3.forward, 0.009f, 0.009f, 8, 6);
            // The drink.
            if (!dirty)
            {
                b.Revolve(p.I(premium ? "cf_latte" : "cf_coffee"), 28, new List<Vector2> { V(0f, 0.083f), V(top - 0.011f, 0.083f), V(top - 0.011f, 0.084f), V(0f, 0.084f) });
                if (premium)
                {
                    // Latte art: a cream heart.
                    int foam = p.I("cf_foam");
                    Ell(b, foam, new Vector3(-0.012f, 0.0855f, 0.004f), new Vector3(0.016f, 0.002f, 0.016f), 10, 5);
                    Ell(b, foam, new Vector3(0.012f, 0.0855f, 0.004f), new Vector3(0.016f, 0.002f, 0.016f), 10, 5);
                    Ell(b, foam, new Vector3(0f, 0.0855f, -0.012f), new Vector3(0.014f, 0.002f, 0.020f), 10, 5);
                                    }
            }
            else
            {
                b.Revolve(p.I("cf_stain"), 28, new List<Vector2> { V(0f, 0.05f), V(top - 0.02f, 0.05f), V(top - 0.02f, 0.0505f), V(0f, 0.0505f) });
                Ell(b, p.I("cf_stain"), new Vector3(0.05f, 0.019f, 0.03f), new Vector3(0.035f, 0.002f, 0.022f), 10, 5);          // a puddle on the saucer
                Slab(b, p.I("cf_grub_steel"), new Vector3(-0.045f, 0.022f, 0.045f), new Vector3(0.002f, 0.002f, 0.04f), 0.4f, yaw: 40f);   // the spoon
                for (int i = 0; i < 3; i++)
                    Slab(b, p.I("cf_stain"), new Vector3(-0.02f + i * 0.02f, 0.07f, -top + 0.003f), new Vector3(0.004f, 0.022f, 0.002f), 0.4f);   // drips
            }
        }

        private static void CakeSliceShape(GoodsMeshBuilder b, Pal p, bool onPlate)
        {
            if (onPlate)
                b.Revolve(p.I("cf_ceramic"), 24, new List<Vector2> { V(0f, 0f), V(0.14f, 0f), V(0.155f, 0.02f), V(0.12f, 0.02f), V(0f, 0.022f) });
            Func<float, Func<Vector3, Vector3>> wedge = h => q =>
            {
                float t = Mathf.Clamp01(q.z / 0.115f * 0.5f + 0.5f);
                q.x *= Mathf.Lerp(1f, 0.10f, t * t);
                return q;
            };
            float y0 = onPlate ? 0.022f : 0f;
            Slab(b, p.I("cf_sponge"), new Vector3(0f, y0 + 0.020f, 0f), new Vector3(0.07f, 0.020f, 0.115f), 0.25f, nu: 14, nv: 8, modify: wedge(0.02f));
            Slab(b, p.I("cf_cream"), new Vector3(0f, y0 + 0.044f, 0f), new Vector3(0.071f, 0.006f, 0.117f), 0.3f, nu: 14, nv: 8, modify: wedge(0.006f));
            Slab(b, p.I("cf_sponge"), new Vector3(0f, y0 + 0.068f, 0f), new Vector3(0.07f, 0.020f, 0.115f), 0.25f, nu: 14, nv: 8, modify: wedge(0.02f));
            Slab(b, p.I("cf_icing"), new Vector3(0f, y0 + 0.092f, 0f), new Vector3(0.072f, 0.008f, 0.117f), 0.3f, nu: 14, nv: 8, modify: wedge(0.008f));
            Ell(b, p.I("cf_cherry"), new Vector3(0f, y0 + 0.112f, -0.06f), new Vector3(0.018f, 0.018f, 0.018f), 8, 6);
            Slab(b, p.I("cf_jam"), new Vector3(0f, y0 + 0.052f, -0.113f), new Vector3(0.062f, 0.004f, 0.006f), 0.3f);
        }

        /// <summary>
        /// The baker's chef hat (a toque): a pleated band and a puffed, domed top, about 0.30 tall, pivot at the bottom of the band, up = +y,
        /// meant to be a child of the Head bone (<c>CafeBuilder.AttachChefHat</c>) so it follows every animation.
        /// </summary>
        private static Model ChefHatModel() => Mk((b, p) =>
        {
            int white = p.I("cf_white"), cream = p.I("cf_cream");
            b.Revolve(white, 28, new List<Vector2> { V(0f, 0f), V(0.112f, 0f), V(0.118f, 0.02f), V(0.118f, 0.085f), V(0.112f, 0.09f), V(0f, 0.09f) });
            b.Revolve(cream, 28, new List<Vector2> { V(0.110f, 0.012f), V(0.121f, 0.016f), V(0.121f, 0.026f), V(0.110f, 0.030f) });
            for (int i = 0; i < 7; i++)
            {
                float a = i * (360f / 7f) * Mathf.Deg2Rad;
                Ell(b, white, new Vector3(Mathf.Cos(a) * 0.085f, 0.17f, Mathf.Sin(a) * 0.085f), new Vector3(0.062f, 0.078f, 0.062f), 12, 9);
            }
            Ell(b, white, new Vector3(0f, 0.20f, 0f), new Vector3(0.115f, 0.095f, 0.115f), 18, 12);
        });

        public static GameObject ChefHat(Transform parent) =>
            Obj("ChefHat", parent, Vector3.zero, 0f, Get(CafeFolder + "ChefHat", ChefHatModel), false);

        // ------------------------------------------------------------------ public spawners

        public static GameObject CafeTable(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenCafeTable" : "CafeTable", parent, pos, yaw, Get(CafeFolder + (broken ? "BrokenCafeTable" : "CafeTable"), () => CafeTableModel(broken)));
        public static GameObject CafeChair(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenCafeChair" : "CafeChair", parent, pos, yaw, Get(CafeFolder + (broken ? "BrokenCafeChair" : "CafeChair"), () => CafeChairModel(broken)));
        public static GameObject CafeStool(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("CafeStool", parent, pos, yaw, Get(CafeFolder + "CafeStool", CafeStoolModel));
        public static GameObject GardenTable(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenGardenTable" : "GardenTable", parent, pos, yaw, Get(CafeFolder + (broken ? "BrokenGardenTable" : "GardenTable"), () => GardenTableModel(broken)));
        public static GameObject GardenChair(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenGardenChair" : "GardenChair", parent, pos, yaw, Get(CafeFolder + (broken ? "BrokenGardenChair" : "GardenChair"), () => GardenChairModel(broken)));
        public static GameObject CafeCounter(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenCafeCounter" : "CafeCounter", parent, pos, yaw, Get(CafeFolder + (broken ? "BrokenCafeCounter" : "CafeCounter"), () => CafeCounterModel(broken)));
        public static GameObject CoffeeMachine(Transform parent, Vector3 pos, float yaw = 0f, bool premium = false, bool broken = false) =>
            Obj(broken ? "BrokenCoffeeMachine" : premium ? "PremiumCoffeeMachine" : "CoffeeMachine", parent, pos, yaw,
                Get(CafeFolder + (broken ? "BrokenCoffeeMachine" : premium ? "PremiumCoffeeMachine" : "CoffeeMachine"),
                    () => broken ? BrokenCoffeeMachineModel() : CoffeeMachineModel(premium)));
        public static GameObject BeanBox(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("BeanBox", parent, pos, yaw, Get(CafeFolder + "BeanBox", BeanBoxModel));
        public static GameObject CafeOven(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenCafeOven" : "CafeOven", parent, pos, yaw, Get(CafeFolder + (broken ? "BrokenCafeOven" : "CafeOven"), () => OvenModel(broken)));
        public static GameObject CafeCase(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenCafeCase" : "CafeCase", parent, pos, yaw, Get(CafeFolder + (broken ? "BrokenCafeCase" : "CafeCase"), () => DisplayCaseModel(broken)));
        public static GameObject CafeFridge(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenCafeFridge" : "CafeFridge", parent, pos, yaw, Get(CafeFolder + (broken ? "BrokenCafeFridge" : "CafeFridge"), () => FridgeModel(broken)));
        public static GameObject CafeDishwasher(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenCafeDishwasher" : "CafeDishwasher", parent, pos, yaw, Get(CafeFolder + (broken ? "BrokenCafeDishwasher" : "CafeDishwasher"), () => DishwasherModel(broken)));
        public static GameObject SupplyCrate(Transform parent, Vector3 pos, float yaw = 0f, bool milk = true) =>
            Obj(milk ? "MilkCrate" : "EggCrate", parent, pos, yaw, Get(CafeFolder + (milk ? "MilkCrate" : "EggCrate"), () => SupplyCrateModel(milk)));
        public static GameObject DeliveryVan(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("DeliveryVan", parent, pos, yaw, Get(CafeFolder + "DeliveryVan", DeliveryVanModel));

        /// <summary>The goods you carry. Pivot at the centre of the stack cell, x1.6 convention, no shadow (the GoodsModels contract).</summary>
        public static GameObject CafeGood(string id, Transform parent, Vector3 localPos, float yaw = 0f)
        {
            Func<Model> make;
            switch (id)
            {
                case "coffee": make = () => GoodFit(Mk((b, p) => CupShape(b, p, false, false)), 0.24f, 0.30f); break;
                case "premium": case "premium_coffee": make = () => GoodFit(Mk((b, p) => CupShape(b, p, true, false)), 0.24f, 0.32f); break;
                case "dirty_cup": make = () => GoodFit(Mk((b, p) => CupShape(b, p, false, true)), 0.24f, 0.30f); break;
                case "cake": make = () => GoodFit(Mk((b, p) => CakeSliceShape(b, p, true)), 0.26f, 0.32f); break;
                default: throw new ArgumentException(id);
            }
            return Obj("Good_" + id, parent, localPos, yaw, Get(CafeFolder + "Good_" + (id == "premium_coffee" ? "premium" : id), make), false);
        }

        /// <summary>
        /// Writes the cafe's four goods (coffee, premium_coffee, cake, dirty_cup) as goods - mesh under Meshes/Goods, prefab under
        /// Prefabs/Goods, as every other good - if they are not there yet, so <see cref="LevelBuildKit.Item"/> finds their prefab by id.
        /// </summary>
        public static void EnsureCafeGoods()
        {
            ButcherModels.EnsureFolder(GoodsModels.MeshFolder);
            ButcherModels.EnsureFolder(GoodsModels.PrefabFolder);
            foreach (var id in new[] { "coffee", "premium_coffee", "cake", "dirty_cup" })
            {
                string prefabPath = GoodsModels.PrefabPath(id);
                if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) continue;

                var make = GoodMake(id);
                var (mesh, mats) = Bake(GoodsModels.MeshPath(id), "Good_" + id, make());
                var go = new GameObject("Good_" + id);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                UnityEngine.Object.DestroyImmediate(go);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Func<Model> GoodMake(string id)
        {
            switch (id)
            {
                case "coffee": return () => GoodFit(Mk((b, p) => CupShape(b, p, false, false)), 0.24f, 0.30f);
                case "premium_coffee": return () => GoodFit(Mk((b, p) => CupShape(b, p, true, false)), 0.24f, 0.32f);
                case "dirty_cup": return () => GoodFit(Mk((b, p) => CupShape(b, p, false, true)), 0.24f, 0.30f);
                default: return () => GoodFit(Mk((b, p) => CakeSliceShape(b, p, true)), 0.26f, 0.32f);
            }
        }

        private static Model GoodFit(Model m, float h, float w)
        {
            m.Mesh.Fit(h * GoodsModels.Scale, w * GoodsModels.Scale);
            return m;
        }
    }
}
