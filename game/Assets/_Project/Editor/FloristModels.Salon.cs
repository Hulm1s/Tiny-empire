using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The hairdresser's 3D art (PROPOSAL, phase 1 - not wired into any builder): the salon chair, the mirror
    /// station, the waiting chair, the reception desk, the pile of cut hair and the hair bag you carry, each
    /// with a wrecked twin for the derelict salon. Same builder, same palette style and the same asset plumbing
    /// as the florist's models (<see cref="FloristModels"/>), only teal and navy where the florist is pink and
    /// cream; meshes bake under Meshes/Florist/Salon and every material is an asset (Sl_*).
    ///
    /// Orientation: everything faces -z toward the customer / the room, like the florist's counter. A chair is
    /// modelled with the sitter facing -z too (the backrest is on +z); the salon turns it round so the sitter
    /// faces the mirror.
    /// </summary>
    public static partial class FloristModels
    {
        private static Material LibSalon(string k)
        {
            switch (k)
            {
                case "sl_teal": return M("Sl_Teal", C(0.20f, 0.74f, 0.68f), 0.50f);
                case "sl_teal_dark": return M("Sl_TealDark", C(0.09f, 0.50f, 0.50f), 0.45f);
                case "sl_mint": return M("Sl_Mint", C(0.74f, 0.94f, 0.89f), 0.50f);
                case "sl_navy": return M("Sl_Navy", C(0.17f, 0.21f, 0.34f), 0.55f);
                case "sl_navy_light": return M("Sl_NavyLight", C(0.30f, 0.36f, 0.54f), 0.55f);
                case "sl_chrome": return M("Sl_Chrome", C(0.86f, 0.90f, 0.94f), 0.80f, 0.45f);
                case "sl_mirror": return M("Sl_Mirror", C(0.74f, 0.90f, 0.98f), 0.95f, 0.10f);
                case "sl_glint": return M("Sl_Glint", C(1.00f, 1.00f, 1.00f), 0.90f);
                case "sl_bulb": return M("Sl_Bulb", C(1.00f, 0.94f, 0.66f), 0.80f);
                case "sl_red": return M("Sl_Red", C(0.92f, 0.28f, 0.28f), 0.55f);
                case "sl_white": return M("Sl_White", C(0.97f, 0.97f, 0.98f), 0.50f);
                case "sl_bag": return M("Sl_Bag", C(0.90f, 0.86f, 0.74f), 0.20f);
                case "sl_bag_light": return M("Sl_BagLight", C(0.74f, 0.68f, 0.52f), 0.20f);
                case "sl_hair_brown": return M("Sl_HairBrown", C(0.36f, 0.21f, 0.11f), 0.40f);
                case "sl_hair_dark": return M("Sl_HairDark", C(0.13f, 0.09f, 0.07f), 0.40f);
                case "sl_hair_blond": return M("Sl_HairBlond", C(0.92f, 0.76f, 0.38f), 0.45f);
                case "sl_hair_red": return M("Sl_HairRed", C(0.78f, 0.32f, 0.14f), 0.45f);
                case "sl_grub_navy": return M("Sl_GrubNavy", C(0.27f, 0.28f, 0.31f), 0.15f);
                case "sl_grub_teal": return M("Sl_GrubTeal", C(0.40f, 0.50f, 0.47f), 0.15f);
                case "sl_grub_chrome": return M("Sl_GrubChrome", C(0.52f, 0.53f, 0.52f), 0.30f, 0.2f);
                case "sl_grub_glass": return M("Sl_GrubGlass", C(0.36f, 0.42f, 0.42f), 0.30f);
                case "sl_crack": return M("Sl_Crack", C(0.10f, 0.11f, 0.11f), 0.20f);
                case "sl_stuffing": return M("Sl_Stuffing", C(0.80f, 0.76f, 0.64f), 0.10f);
                case "sl_dead_bulb": return M("Sl_DeadBulb", C(0.30f, 0.29f, 0.25f), 0.20f);
            }
            throw new ArgumentException(k);
        }

        public const string SalonFolder = "Salon/";

        // ------------------------------------------------------------------ the salon chair

        /// <summary>
        /// A hydraulic salon chair about 1.4 tall: round base, chrome pole, navy seat with a teal piping,
        /// padded back and headrest, two teal armrests and a footrest. <paramref name="broken"/>: tilted on its
        /// foot, one arm gone, the seat torn with stuffing coming out, no footrest, all in dull greys.
        /// </summary>
        private static void SalonChairShape(GoodsMeshBuilder b, Pal p, bool broken)
        {
            int navy = p.I(broken ? "sl_grub_navy" : "sl_navy"), navyL = p.I(broken ? "sl_grub_navy" : "sl_navy_light");
            int teal = p.I(broken ? "sl_grub_teal" : "sl_teal"), chrome = p.I(broken ? "sl_grub_chrome" : "sl_chrome");
            int dark = p.I("dark");

            // The foot and the pole stay upright; the rest of a wrecked chair leans.
            b.Revolve(dark, 28, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.40f, 0f), V(0.43f, 0.025f), V(0.38f, 0.07f), V(0.14f, 0.11f), V(0f, 0.11f)
            }, 4));
            b.Revolve(chrome, 20, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0.09f), V(0.075f, 0.09f), V(0.075f, 0.40f), V(0.12f, 0.44f), V(0f, 0.44f)
            }, 3));

            var lean = broken ? Quaternion.Euler(0f, 0f, 11f) : Quaternion.identity;
            Put(b, new Vector3(0f, 0f, 0f), lean, 1f, () =>
            {
                Slab(b, navy, new Vector3(0f, 0.50f, 0f), new Vector3(0.35f, 0.075f, 0.35f), 0.28f);
                Slab(b, teal, new Vector3(0f, 0.52f, -0.345f), new Vector3(0.31f, 0.022f, 0.014f), 0.4f);
                Slab(b, navy, new Vector3(0f, 0.91f, 0.30f), new Vector3(0.32f, 0.37f, 0.08f), 0.26f, pitch: 9f);
                Slab(b, teal, new Vector3(0f, 0.91f, 0.222f), new Vector3(0.20f, 0.30f, 0.012f), 0.4f, pitch: 9f);
                Slab(b, navyL, new Vector3(0f, 1.34f, 0.355f), new Vector3(0.18f, 0.105f, 0.075f), 0.30f, pitch: 9f);
                foreach (float sx in new[] { -1f, 1f })
                {
                    if (broken && sx > 0f) continue;      // one arm is missing
                    Slab(b, chrome, new Vector3(sx * 0.40f, 0.62f, 0.14f), new Vector3(0.024f, 0.11f, 0.024f), 0.25f);
                    Slab(b, teal, new Vector3(sx * 0.40f, 0.745f, 0.10f), new Vector3(0.058f, 0.036f, 0.24f), 0.30f);
                }
                if (!broken)
                {
                    Slab(b, chrome, new Vector3(0f, 0.20f, -0.40f), new Vector3(0.26f, 0.022f, 0.11f), 0.30f);
                    foreach (float sx in new[] { -1f, 1f })
                        Tube(b, chrome, t => Vector3.Lerp(new Vector3(sx * 0.17f, 0.42f, -0.18f), new Vector3(sx * 0.17f, 0.21f, -0.40f), t),
                            Vector3.right, 0.018f, 0.018f, 3, 6);
                }
                else
                {
                    // Torn seat: stuffing bursting out, and a bite out of the back.
                    int stuff = p.I("sl_stuffing");
                    Ell(b, stuff, new Vector3(0.12f, 0.60f, -0.06f), new Vector3(0.14f, 0.07f, 0.11f));
                    Ell(b, stuff, new Vector3(0.20f, 0.58f, 0.02f), new Vector3(0.08f, 0.05f, 0.07f));
                    Ell(b, stuff, new Vector3(-0.15f, 1.00f, 0.22f), new Vector3(0.09f, 0.06f, 0.05f));
                }
            });
            if (broken)
            {
                // The missing arm, lying on the floor.
                Put(b, new Vector3(0.62f, 0.05f, -0.30f), Quaternion.Euler(0f, 35f, 0f), 1f, () =>
                {
                    Slab(b, p.I("sl_grub_teal"), new Vector3(0f, 0.04f, 0f), new Vector3(0.058f, 0.036f, 0.24f), 0.3f);
                    Slab(b, chrome, new Vector3(0f, 0.04f, 0.3f), new Vector3(0.024f, 0.024f, 0.12f), 0.25f);
                });
            }
        }

        private static Model SalonChairModel(bool broken)
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            SalonChairShape(b, p, broken);
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ the mirror station

        /// <summary>
        /// The styling station against a wall: a cabinet with three drawers and a wooden top that carries a
        /// bottle, a jar of combs and a hairdryer, and over it a teal-framed mirror lit by a row of bulbs.
        /// Faces -z, 1.7 wide, 0.45 deep, about 2.4 tall. Broken: a cracked, dark mirror with a corner missing,
        /// dead bulbs, a drawer hanging out and the cabinet sagging on one side.
        /// </summary>
        private static Model MirrorStationModel(bool broken)
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            const float W = 1.70f;
            int cream = p.I(broken ? "grub_cream" : "cream"), teal = p.I(broken ? "sl_grub_teal" : "sl_teal");
            int wood = p.I(broken ? "grub_wood" : "wood_light"), woodD = p.I(broken ? "grub_wood_dark" : "wood_dark");
            int glass = p.I(broken ? "sl_grub_glass" : "sl_mirror"), chrome = p.I(broken ? "sl_grub_chrome" : "sl_chrome");

            // Cabinet.
            Slab(b, cream, new Vector3(0f, 0.42f, 0f), new Vector3(W * 0.5f, 0.42f, 0.22f), 0.16f);
            Slab(b, woodD, new Vector3(0f, 0.04f, 0f), new Vector3(W * 0.5f + 0.02f, 0.04f, 0.24f), 0.3f);
            Slab(b, wood, new Vector3(0f, 0.87f, -0.01f), new Vector3(W * 0.5f + 0.03f, 0.03f, 0.26f), 0.28f,
                roll: broken ? 2.5f : 0f);
            for (int i = 0; i < 3; i++)
            {
                float x = -0.56f + i * 0.56f;
                float out_ = broken && i == 2 ? 0.18f : 0f;                  // a drawer hanging open
                Slab(b, teal, new Vector3(x, 0.45f, -0.225f - out_), new Vector3(0.24f, 0.27f, 0.014f), 0.3f,
                    pitch: broken && i == 2 ? -10f : 0f);
                Ell(b, p.I(broken ? "sl_grub_chrome" : "brass"), new Vector3(x, 0.52f, -0.245f - out_), new Vector3(0.025f, 0.025f, 0.02f), 8, 5);
            }

            // Mirror: a teal frame, the glass in front of it, a glint.
            Slab(b, teal, new Vector3(0f, 1.74f, 0.19f), new Vector3(0.76f, 0.64f, 0.045f), 0.22f);
            Slab(b, glass, new Vector3(0f, 1.74f, 0.14f), new Vector3(0.67f, 0.55f, 0.016f), 0.15f);
            foreach (float sx in new[] { -1f, 1f })
                Slab(b, woodD, new Vector3(sx * 0.62f, 1.10f, 0.19f), new Vector3(0.03f, 0.24f, 0.03f), 0.25f);

            if (!broken)
            {
                int glint = p.I("sl_glint");
                Slab(b, glint, new Vector3(-0.30f, 1.80f, 0.122f), new Vector3(0.035f, 0.40f, 0.004f), 0.4f, roll: 30f);
                Slab(b, glint, new Vector3(-0.17f, 1.72f, 0.122f), new Vector3(0.016f, 0.26f, 0.004f), 0.4f, roll: 30f);
                // The row of bulbs along the top of the frame.
                for (int i = 0; i < 7; i++)
                    Ell(b, p.I("sl_bulb"), new Vector3(-0.60f + i * 0.20f, 2.43f, 0.15f), new Vector3(0.045f, 0.045f, 0.045f), 10, 7);
                Slab(b, chrome, new Vector3(0f, 2.385f, 0.17f), new Vector3(0.70f, 0.012f, 0.03f), 0.4f);

                // On the counter: a bottle, a jar of combs, a hairdryer on its stand.
                Put(b, new Vector3(-0.62f, 0.90f, -0.02f), Quaternion.identity, 1f, () =>
                {
                    b.Revolve(p.I("sl_teal_dark"), 20, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0f), V(0.05f, 0f), V(0.055f, 0.02f), V(0.055f, 0.16f), V(0.03f, 0.21f), V(0.022f, 0.27f), V(0f, 0.27f)
                    }, 3));
                    b.Revolve(p.I("sl_white"), 12, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0.022f, 0.27f), V(0.026f, 0.27f), V(0.026f, 0.30f), V(0.022f, 0.30f), V(0.022f, 0.27f)
                    }, 2));
                });
                Put(b, new Vector3(-0.30f, 0.90f, -0.02f), Quaternion.identity, 1f, () =>
                {
                    b.Revolve(p.I("glass"), 20, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0f), V(0.07f, 0f), V(0.075f, 0.02f), V(0.075f, 0.17f), V(0f, 0.17f)
                    }, 3));
                    for (int i = 0; i < 4; i++)
                        Slab(b, p.I(i % 2 == 0 ? "sl_red" : "sl_teal_dark"), new Vector3(-0.045f + i * 0.03f, 0.22f, 0.0f),
                            new Vector3(0.008f, 0.09f, 0.03f), 0.4f, roll: -8f + i * 5f);
                });
                Put(b, new Vector3(0.45f, 0.90f, -0.02f), Quaternion.identity, 1f, () =>
                {
                    // Stand and hairdryer.
                    Slab(b, chrome, new Vector3(0f, 0.015f, 0f), new Vector3(0.13f, 0.015f, 0.09f), 0.3f);
                    Slab(b, chrome, new Vector3(0f, 0.12f, 0f), new Vector3(0.012f, 0.11f, 0.012f), 0.3f);
                    Put(b, new Vector3(0f, 0.27f, -0.02f), Quaternion.Euler(0f, 0f, 0f), 1f, () =>
                    {
                        b.Revolve(p.I("sl_red"), 20, GoodsMeshBuilder.Spline(new[]
                        {
                            V(0f, -0.10f), V(0.05f, -0.10f), V(0.07f, -0.06f), V(0.07f, 0.07f), V(0.05f, 0.10f), V(0f, 0.10f)
                        }, 3));
                        b.Super(p.I("sl_red"), new Vector3(0f, -0.13f, 0.0f), new Vector3(0.025f, 0.08f, 0.03f), 0.3f, 0.3f, 12, 8, false);
                    });
                });
                // Scissors resting on the counter.
                Put(b, new Vector3(0.88f - 0.2f, 0.905f, -0.12f), Quaternion.Euler(0f, 25f, 0f), 1f, () => ScissorsShape(b, p, 0.35f));
            }
            else
            {
                int crack = p.I("sl_crack");
                // Cracks across the glass: a star from the lower left, and a missing corner (a shard on the top).
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 52f + 10f;
                    Slab(b, crack, new Vector3(-0.30f, 1.55f, 0.128f) + Quaternion.Euler(0f, 0f, a) * new Vector3(0f, 0.18f + 0.02f * i, 0f),
                        new Vector3(0.006f, 0.20f + 0.03f * (i % 3), 0.004f), 0.4f, roll: a);
                }
                Slab(b, p.I("sl_grub_teal"), new Vector3(0.52f, 1.40f, 0.12f), new Vector3(0.14f, 0.14f, 0.02f), 0.3f, roll: 30f);   // missing corner
                for (int i = 0; i < 7; i++)
                    Ell(b, p.I("sl_dead_bulb"), new Vector3(-0.60f + i * 0.20f, 2.43f, 0.15f), new Vector3(0.04f, 0.04f, 0.04f), 8, 6);
                // Shards on the counter and a toppled bottle.
                Put(b, new Vector3(-0.55f, 0.90f, -0.02f), Quaternion.Euler(0f, 20f, 0f), 1f, () =>
                {
                    Slab(b, glass, new Vector3(0f, 0.01f, 0f), new Vector3(0.12f, 0.006f, 0.07f), 0.4f, roll: 6f);
                    Slab(b, glass, new Vector3(0.20f, 0.01f, 0.05f), new Vector3(0.08f, 0.006f, 0.05f), 0.4f, yaw: 40f);
                });
                Put(b, new Vector3(0.20f, 0.95f, -0.05f), Quaternion.Euler(0f, 0f, 90f), 1f, () =>
                    b.Revolve(p.I("sl_grub_teal"), 14, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0f), V(0.05f, 0f), V(0.055f, 0.16f), V(0.03f, 0.21f), V(0.022f, 0.26f), V(0f, 0.26f)
                    }, 3)));
            }
            return new Model { Mesh = b, Pal = p };
        }

        /// <summary>A pair of scissors lying flat, about <paramref name="len"/> long: two blades, two red rings.</summary>
        private static void ScissorsShape(GoodsMeshBuilder b, Pal p, float len)
        {
            int blade = p.I("sl_chrome"), red = p.I("sl_red");
            float s = len / 0.35f;
            foreach (float sx in new[] { -1f, 1f })
            {
                Slab(b, blade, new Vector3(sx * 0.018f * s, 0.01f * s, 0.06f * s), new Vector3(0.012f * s, 0.006f * s, 0.12f * s), 0.4f, yaw: -sx * 5f);
                Put(b, new Vector3(sx * 0.07f * s, 0.012f * s, -0.10f * s), Quaternion.identity, 1f, () =>
                    b.Revolve(red, 16, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0.026f * s, -0.006f * s), V(0.052f * s, -0.006f * s), V(0.054f * s, 0.006f * s), V(0.028f * s, 0.006f * s), V(0.026f * s, -0.006f * s)
                    }, 2)));
                Slab(b, red, new Vector3(sx * 0.045f * s, 0.01f * s, -0.04f * s), new Vector3(0.008f * s, 0.006f * s, 0.07f * s), 0.4f, yaw: sx * 14f);
            }
            Ell(b, p.I("brass"), new Vector3(0f, 0.016f * s, 0f), new Vector3(0.014f * s, 0.01f * s, 0.014f * s), 8, 5);
        }

        // ------------------------------------------------------------------ the waiting chair

        /// <summary>A little padded lounge chair, 0.8 wide, teal cushions on wooden legs, with a wooden arm each side.</summary>
        private static Model WaitingChairModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            int teal = p.I("sl_teal"), tealD = p.I("sl_teal_dark"), wood = p.I("wood"), woodD = p.I("wood_dark"), mint = p.I("sl_mint");
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Slab(b, woodD, new Vector3(sx * 0.31f, 0.13f, sz * 0.27f), new Vector3(0.03f, 0.13f, 0.03f), 0.25f);
            Slab(b, wood, new Vector3(0f, 0.27f, 0f), new Vector3(0.38f, 0.03f, 0.33f), 0.25f);
            Slab(b, teal, new Vector3(0f, 0.36f, -0.01f), new Vector3(0.33f, 0.085f, 0.29f), 0.30f);
            Slab(b, tealD, new Vector3(0f, 0.65f, 0.27f), new Vector3(0.34f, 0.28f, 0.06f), 0.28f, pitch: 7f);
            Slab(b, mint, new Vector3(0f, 0.70f, 0.215f), new Vector3(0.22f, 0.17f, 0.012f), 0.4f, pitch: 7f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Slab(b, wood, new Vector3(sx * 0.395f, 0.50f, 0.04f), new Vector3(0.035f, 0.025f, 0.27f), 0.3f);
                Slab(b, woodD, new Vector3(sx * 0.395f, 0.40f, -0.18f), new Vector3(0.022f, 0.12f, 0.022f), 0.25f);
            }
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ the reception desk

        /// <summary>
        /// The reception desk, 3.0 wide and 0.9 deep, faces -z: a cream body with teal front panels and a crossed
        /// scissors badge, a wooden top with a raised front lip, and on it a bell, a tablet on a stand, the
        /// appointment book, a pen pot and a little plant. Broken: the top slips off one end, a panel is gone, a drawer
        /// hangs open and the things are scattered.
        /// </summary>
        private static Model ReceptionDeskModel(bool broken)
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            const float W = 3.0f;
            int cream = p.I(broken ? "grub_cream" : "cream"), teal = p.I(broken ? "sl_grub_teal" : "sl_teal");
            int wood = p.I(broken ? "grub_wood" : "wood_light"), woodD = p.I(broken ? "grub_wood_dark" : "wood_dark");
            int chrome = p.I(broken ? "sl_grub_chrome" : "sl_chrome");

            Slab(b, cream, new Vector3(0f, 0.52f, 0.03f), new Vector3(W * 0.5f, 0.52f, 0.38f), 0.14f);
            Slab(b, woodD, new Vector3(0f, 0.05f, 0.03f), new Vector3(W * 0.5f + 0.02f, 0.05f, 0.40f), 0.3f);
            Slab(b, wood, new Vector3(broken ? 0.18f : 0f, 1.07f, 0f), new Vector3(W * 0.5f + 0.08f, 0.035f, 0.46f), 0.26f,
                roll: broken ? -3f : 0f, yaw: broken ? 3f : 0f);
            Slab(b, teal, new Vector3(0f, 1.20f, -0.40f), new Vector3(W * 0.5f - 0.1f, 0.075f, 0.035f), 0.3f);
            for (int i = 0; i < 4; i++)
            {
                float x = -1.05f + i * 0.70f;
                if (broken && i == 2) continue;                            // a front panel is gone
                Slab(b, teal, new Vector3(x, 0.53f, -0.355f), new Vector3(0.28f, 0.34f, 0.014f), 0.3f);
                Slab(b, p.I(broken ? "grub_cream" : "sl_mint"), new Vector3(x, 0.53f, -0.368f), new Vector3(0.22f, 0.28f, 0.01f), 0.4f);
            }
            if (broken)
            {
                Slab(b, p.I("grub_wood_dark"), new Vector3(0.0f, 0.46f, -0.38f), new Vector3(0.30f, 0.30f, 0.02f), 0.3f);
                Slab(b, p.I("grub_wood"), new Vector3(0.70f, 0.62f, -0.55f), new Vector3(0.26f, 0.16f, 0.20f), 0.2f, pitch: -9f);   // drawer hanging open
            }
            // Badge: crossed scissors on the middle front panel.
            if (!broken)
            {
                Slab(b, chrome, new Vector3(0f, 0.55f, -0.385f), new Vector3(0.012f, 0.19f, 0.006f), 0.4f, roll: 28f);
                Slab(b, chrome, new Vector3(0f, 0.55f, -0.385f), new Vector3(0.012f, 0.19f, 0.006f), 0.4f, roll: -28f);
                foreach (float sx in new[] { -1f, 1f })
                    Ell(b, p.I("sl_red"), new Vector3(sx * 0.11f, 0.41f, -0.39f), new Vector3(0.04f, 0.04f, 0.012f), 12, 8);

                var top = new Vector3(0f, 1.105f, 0f);
                // Bell.
                Put(b, top + new Vector3(0.9f, 0f, -0.12f), Quaternion.identity, 1f, () =>
                    b.Revolve(p.I("brass"), 24, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0.004f), V(0.095f, 0.004f), V(0.10f, 0.02f), V(0.085f, 0.07f), V(0.05f, 0.11f), V(0.012f, 0.125f), V(0f, 0.13f)
                    }, 4)));
                // Tablet on a stand.
                Put(b, top + new Vector3(-0.30f, 0f, 0.05f), Quaternion.Euler(0f, 10f, 0f), 1f, () =>
                {
                    Slab(b, chrome, new Vector3(0f, 0.012f, 0f), new Vector3(0.12f, 0.012f, 0.09f), 0.3f);
                    Slab(b, p.I("dark"), new Vector3(0f, 0.17f, 0.02f), new Vector3(0.22f, 0.14f, 0.012f), 0.3f, pitch: -12f);
                    Slab(b, p.I("sl_mint"), new Vector3(0f, 0.17f, 0.011f), new Vector3(0.19f, 0.115f, 0.006f), 0.4f, pitch: -12f);
                });
                // Appointment book.
                Put(b, top + new Vector3(0.35f, 0f, 0.02f), Quaternion.Euler(0f, -14f, 0f), 1f, () =>
                {
                    Slab(b, p.I("sl_navy"), new Vector3(0f, 0.025f, 0f), new Vector3(0.20f, 0.025f, 0.15f), 0.25f);
                    Slab(b, p.I("paper"), new Vector3(0.005f, 0.035f, 0f), new Vector3(0.185f, 0.022f, 0.14f), 0.3f);
                    Slab(b, p.I("sl_teal"), new Vector3(-0.19f, 0.03f, 0f), new Vector3(0.012f, 0.03f, 0.15f), 0.3f);
                });
                // Pen pot.
                Put(b, top + new Vector3(1.20f, 0f, 0.12f), Quaternion.identity, 1f, () =>
                {
                    b.Revolve(p.I("sl_teal_dark"), 20, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0f), V(0.055f, 0f), V(0.06f, 0.11f), V(0f, 0.11f)
                    }, 2));
                    Slab(b, p.I("sl_red"), new Vector3(0.015f, 0.17f, 0f), new Vector3(0.006f, 0.08f, 0.006f), 0.4f, roll: -9f);
                    Slab(b, p.I("brass"), new Vector3(-0.015f, 0.18f, 0f), new Vector3(0.006f, 0.08f, 0.006f), 0.4f, roll: 11f);
                });
                // Plant.
                Put(b, top + new Vector3(-1.20f, 0f, 0.10f), Quaternion.identity, 1f, () =>
                {
                    b.Revolve(p.I("sl_white"), 20, GoodsMeshBuilder.Spline(new[]
                    {
                        V(0f, 0f), V(0.09f, 0f), V(0.11f, 0.14f), V(0f, 0.14f)
                    }, 3));
                    for (int i = 0; i < 6; i++)
                        Leaf(b, p.I("leaf"), new Vector3(0f, 0.13f, 0f), i * 60f, 50f, 0.26f, 0.10f, 0.35f);
                });
            }
            else
            {
                Put(b, new Vector3(-0.95f, 1.105f, 0.05f), Quaternion.Euler(0f, 30f, 8f), 1f, () =>
                    Slab(b, p.I("sl_grub_navy"), new Vector3(0f, 0.025f, 0f), new Vector3(0.20f, 0.025f, 0.15f), 0.25f));
                Put(b, new Vector3(0.40f, 0.04f, -0.60f), Quaternion.Euler(0f, 70f, 0f), 1f, () =>
                    Slab(b, p.I("grub_paper"), new Vector3(0f, 0.005f, 0f), new Vector3(0.18f, 0.004f, 0.13f), 0.4f));
            }
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ hair

        /// <summary>
        /// A heap of cut hair as it lies on the floor round a chair's foot, about 1.0 across and 0.12 high: flat
        /// clumps in four hair colours, a few standing wisps and loose strands curling out. Drawn at the origin.
        /// </summary>
        private static void HairHeapShape(GoodsMeshBuilder b, Pal p, float scale)
        {
            var rnd = new System.Random(9);
            Put(b, Vector3.zero, Quaternion.identity, scale, () =>
            {
                // A dome of overlapping soft clumps, taller in the middle, mostly brown with some darker and lighter ones.
                string[] cols = { "sl_hair_brown", "sl_hair_brown", "sl_hair_brown", "sl_hair_dark", "sl_hair_brown", "sl_hair_brown", "sl_hair_brown", "sl_hair_blond", "sl_hair_brown", "sl_hair_brown" };
                for (int i = 0; i < 34; i++)
                {
                    float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
                    float r = Mathf.Sqrt((float)rnd.NextDouble()) * 0.34f;
                    float h = 0.035f + 0.10f * Mathf.Pow(1f - r / 0.36f, 1.3f);
                    float yaw = (float)rnd.NextDouble() * 180f;
                    var half = new Vector3(0.10f + (float)rnd.NextDouble() * 0.07f, 0.040f + (float)rnd.NextDouble() * 0.02f, 0.07f + (float)rnd.NextDouble() * 0.05f);
                    int sub = p.I(cols[i % cols.Length]);
                    Put(b, new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r), Quaternion.Euler(0f, yaw, 0f), 1f, () => Ell(b, sub, Vector3.zero, half, 10, 6));
                }
                // A few short loose curls on the floor round the edge.
                for (int i = 0; i < 10; i++)
                {
                    float a = (i * 36f + 12f) * Mathf.Deg2Rad;
                    float r0 = 0.30f + 0.04f * (i % 3), len = 0.12f + (float)rnd.NextDouble() * 0.10f, bend = (i % 2 == 0 ? 1f : -1f) * (0.5f + (float)rnd.NextDouble());
                    int sub = p.I(cols[(i * 3) % cols.Length]);
                    Tube(b, sub, t =>
                    {
                        float aa = a + bend * t;
                        float rr = r0 + t * len + Mathf.Sin(t * 7f) * 0.025f;
                        return new Vector3(Mathf.Cos(aa) * rr, 0.020f + 0.012f * Mathf.Sin(t * 6f), Mathf.Sin(aa) * rr);
                    }, Vector3.up, 0.015f, 0.008f, 8, 5);
                }
            });
        }

        private static Model HairHeapModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            HairHeapShape(b, p, 1f);
            return new Model { Mesh = b, Pal = p };
        }

        /// <summary>
        /// The hair bag you carry to the bin: a dark sack tied at the neck with a teal ribbon, a little label, and
        /// a tuft of hair sticking out of the top. A good like the others: fitted to the stack-cell height, pivot in
        /// its centre.
        /// </summary>
        private static Model HairBagModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            // A squat cloth sack (boxy, so a stack of them stands up like the other goods): body, a gathered top, a teal tie.
            Slab(b, p.I("sl_bag"), new Vector3(0f, 0.19f, 0f), new Vector3(0.26f, 0.19f, 0.20f), 0.38f, nu: 28, nv: 16);
            Slab(b, p.I("sl_bag_light"), new Vector3(0f, 0.395f, 0f), new Vector3(0.17f, 0.05f, 0.13f), 0.45f);
            Slab(b, p.I("sl_teal"), new Vector3(0f, 0.355f, 0f), new Vector3(0.185f, 0.016f, 0.14f), 0.45f);
            Slab(b, p.I("sl_mint"), new Vector3(0f, 0.19f, -0.198f), new Vector3(0.12f, 0.09f, 0.012f), 0.4f);
            Ell(b, p.I("sl_red"), new Vector3(-0.035f, 0.19f, -0.21f), new Vector3(0.024f, 0.024f, 0.008f), 8, 6);
            Ell(b, p.I("sl_red"), new Vector3(0.035f, 0.19f, -0.21f), new Vector3(0.024f, 0.024f, 0.008f), 8, 6);
            // The tuft of hair sticking out of the top.
            string[] keys = { "sl_hair_brown", "sl_hair_dark", "sl_hair_brown", "sl_hair_blond", "sl_hair_brown", "sl_hair_red" };
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                Tube(b, p.I(keys[i % keys.Length]), t =>
                {
                    float ar = (a + t * 50f) * Mathf.Deg2Rad;
                    float r = 0.02f + t * (0.09f + 0.03f * (i % 3));
                    return new Vector3(Mathf.Cos(ar) * r, 0.43f + 0.15f * Mathf.Sin(t * 1.9f) * (1f - 0.2f * (i % 2)), Mathf.Sin(ar) * r);
                }, Vector3.up, 0.013f, 0.006f, 6, 5);
            }
            Ell(b, p.I("sl_hair_brown"), new Vector3(0f, 0.45f, 0f), new Vector3(0.07f, 0.05f, 0.06f), 10, 7);
            b.Fit(0.34f * GoodsModels.Scale, 0.36f * GoodsModels.Scale);
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ the barber pole

        /// <summary>
        /// A barber's pole: a white glass tube with a red and a blue stripe winding round it, brass caps top and
        /// bottom, on a little foot. About 1.5 tall, 0.4 across - the street door's sign.
        /// </summary>
        private static Model BarberPoleModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            b.Revolve(p.I("brass"), 20, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0f), V(0.20f, 0f), V(0.20f, 0.05f), V(0.12f, 0.09f), V(0.12f, 0.16f), V(0f, 0.16f)
            }, 3));
            b.Revolve(p.I("sl_white"), 24, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 0.16f), V(0.105f, 0.16f), V(0.105f, 1.28f), V(0f, 1.28f)
            }, 2));
            for (int stripe = 0; stripe < 2; stripe++)
            {
                int sub = p.I(stripe == 0 ? "sl_red" : "sl_navy_light");
                float phase = stripe * Mathf.PI;
                Tube(b, sub, t =>
                {
                    float a = phase + t * Mathf.PI * 2f * 2.5f;
                    return new Vector3(Mathf.Cos(a) * 0.108f, 0.18f + t * 1.08f, Mathf.Sin(a) * 0.108f);
                }, Vector3.up, 0.032f, 0.032f, 40, 6);
            }
            b.Revolve(p.I("brass"), 20, GoodsMeshBuilder.Spline(new[]
            {
                V(0f, 1.28f), V(0.13f, 1.28f), V(0.13f, 1.33f), V(0.09f, 1.36f), V(0f, 1.40f)
            }, 3));
            Ell(b, p.I("brass"), new Vector3(0f, 1.46f, 0f), new Vector3(0.07f, 0.07f, 0.07f), 12, 8);
            return new Model { Mesh = b, Pal = p };
        }

        public static GameObject BarberPole(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("BarberPole", parent, pos, yaw, Get(SalonFolder + "BarberPole", BarberPoleModel));

        // ------------------------------------------------------------------ the hair bag as a good

        /// <summary>
        /// Writes the hair bag good (mesh under Meshes/Goods, prefab under Prefabs/Goods, as every other good) if it is
        /// not there yet, so <see cref="LevelBuildKit.Item"/> finds its prefab by id like the others'.
        /// </summary>
        public static void EnsureSalonGoods()
        {
            string prefabPath = GoodsModels.PrefabPath("hair_bag");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null) return;

            ButcherModels.EnsureFolder(GoodsModels.MeshFolder);
            ButcherModels.EnsureFolder(GoodsModels.PrefabFolder);
            var (mesh, mats) = Bake(GoodsModels.MeshPath("hair_bag"), "Good_hair_bag", HairBagModel());
            var go = new GameObject("Good_hair_bag");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Salon] Good_hair_bag: use stackHeight {mesh.bounds.size.y:0.00}");
        }

        // ------------------------------------------------------------------ public spawners

        public static GameObject SalonChair(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenSalonChair" : "SalonChair", parent, pos, yaw,
                Get(SalonFolder + (broken ? "BrokenSalonChair" : "SalonChair"), () => SalonChairModel(broken)));

        public static GameObject MirrorStation(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenMirrorStation" : "MirrorStation", parent, pos, yaw,
                Get(SalonFolder + (broken ? "BrokenMirrorStation" : "MirrorStation"), () => MirrorStationModel(broken)));

        public static GameObject WaitingChair(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("WaitingChair", parent, pos, yaw, Get(SalonFolder + "WaitingChair", WaitingChairModel));

        public static GameObject ReceptionDesk(Transform parent, Vector3 pos, float yaw = 0f, bool broken = false) =>
            Obj(broken ? "BrokenReceptionDesk" : "ReceptionDesk", parent, pos, yaw,
                Get(SalonFolder + (broken ? "BrokenReceptionDesk" : "ReceptionDesk"), () => ReceptionDeskModel(broken)));

        public static GameObject HairHeap(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("HairHeap", parent, pos, yaw, Get(SalonFolder + "HairHeap", HairHeapModel), false);

        /// <summary>The hair bag as a good (a carried item), like <see cref="Good"/>: pivot at the centre of its stack cell.</summary>
        public static GameObject HairBag(Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var go = Obj("Good_hair_bag", parent, localPos, yaw, Get(SalonFolder + "Good_hair_bag", HairBagModel), false);
            return go;
        }
    }
}
