using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Florist round 2 art (approved by Filip from FloristPreview2's sheets): the seed box and the seed pouch you carry, the bed in its
    /// growth stages (empty, seedlings, half-grown, in bloom, plus a wet-soil look) and the derelict
    /// shop fittings (broken stands, broken counter, dead flowers). Same builder, same materials
    /// style, same asset plumbing as <see cref="FloristModels"/>; meshes are baked on demand under
    /// Meshes/Florist by the scene build (FloristBuilder), except the carried seed pouch, which is a good
    /// like the others and is built by <see cref="FloristModels.BuildAll"/> (<see cref="BuildSeedGood"/>).
    /// </summary>
    public static partial class FloristModels
    {
        private static Material LibRound2(string k)
        {
            switch (k)
            {
                case "soil_thirsty": return M("Fl_SoilThirsty", C(0.52f, 0.38f, 0.24f), 0.10f);
                case "soil_wet": return M("Fl_SoilWet", C(0.30f, 0.20f, 0.13f), 0.80f);
                case "furrow": return M("Fl_Furrow", C(0.34f, 0.23f, 0.14f), 0.10f);
                case "puddle": return M("Fl_Puddle", C(0.50f, 0.68f, 0.82f), 0.95f);
                case "drop": return M("Fl_Drop", C(0.66f, 0.86f, 1.00f), 0.95f);
                case "kraft": return M("Fl_Kraft", C(0.93f, 0.70f, 0.40f), 0.25f);
                case "kraft_dark": return M("Fl_KraftDark", C(0.74f, 0.48f, 0.22f), 0.25f);
                case "sack": return M("Fl_Sack", C(0.82f, 0.70f, 0.48f), 0.12f);
                case "sack_dark": return M("Fl_SackDark", C(0.64f, 0.52f, 0.33f), 0.12f);
                case "seed": return M("Fl_Seed", C(0.56f, 0.36f, 0.18f), 0.40f);
                case "grub_wood": return M("Fl_GrubWood", C(0.45f, 0.36f, 0.27f), 0.15f);
                case "grub_wood_dark": return M("Fl_GrubWoodDark", C(0.30f, 0.23f, 0.17f), 0.15f);
                case "grub_cream": return M("Fl_GrubCream", C(0.68f, 0.63f, 0.53f), 0.15f);
                case "grub_pink": return M("Fl_GrubPink", C(0.66f, 0.50f, 0.50f), 0.15f);
                case "grub_tin": return M("Fl_GrubTin", C(0.50f, 0.52f, 0.50f), 0.25f, 0.1f);
                case "grub_paper": return M("Fl_GrubPaper", C(0.74f, 0.69f, 0.56f), 0.10f);
                case "weed": return M("Fl_Weed", C(0.44f, 0.52f, 0.24f), 0.15f);
            }
            return LibSalon(k);
        }

        private const string PreviewKeyFolder = "";

        // ------------------------------------------------------------------ seed pouch and seed box

        /// <summary>
        /// A stand-up seed pouch about 0.4 tall: kraft body, crimped top, a cream label with a green
        /// sprout, and a coloured band (the flower it is for; green for the generic carried one).
        /// Drawn at the origin, base on y = 0, label facing both +z and -z.
        /// </summary>
        private static void Pouch(GoodsMeshBuilder b, Pal p, Vector3 pos, float yaw, float s, string bandKey, float lean = 0f)
        {
            Put(b, pos, Quaternion.Euler(lean, yaw, 0f), s, () =>
            {
                int kraft = p.I("kraft"), dark = p.I("kraft_dark"), cream = p.I("cream"), band = p.I(bandKey);
                Slab(b, kraft, new Vector3(0f, 0.19f, 0f), new Vector3(0.15f, 0.19f, 0.05f), 0.30f);
                Slab(b, dark, new Vector3(0f, 0.375f, 0f), new Vector3(0.155f, 0.026f, 0.052f), 0.35f);
                foreach (float sz in new[] { -1f, 1f })
                {
                    Slab(b, band, new Vector3(0f, 0.30f, sz * 0.050f), new Vector3(0.135f, 0.032f, 0.006f), 0.4f);
                    Slab(b, cream, new Vector3(0f, 0.17f, sz * 0.052f), new Vector3(0.105f, 0.10f, 0.006f), 0.4f);
                    // The sprout: a short stalk and two leaves.
                    Tube(b, p.I("stem"), t => new Vector3(0f, Mathf.Lerp(0.09f, 0.20f, t), sz * 0.056f), Vector3.right,
                        0.007f, 0.006f, 3, 6);
                    Leaf(b, p.I("leaf"), new Vector3(0.004f * sz, 0.175f, sz * 0.058f), sz > 0 ? 70f : -110f, 30f, 0.075f, 0.040f, 0.1f);
                    Leaf(b, p.I("leaf"), new Vector3(-0.004f * sz, 0.155f, sz * 0.058f), sz > 0 ? -70f : 110f, 30f, 0.065f, 0.036f, 0.1f);
                }
            });
        }

        private static Model SeedPouchModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            Pouch(b, p, Vector3.zero, 0f, 1f, "leaf");
            b.Fit(0.30f * GoodsModels.Scale, 0.34f * GoodsModels.Scale);
            return new Model { Mesh = b, Pal = p };
        }

        private static readonly string[] FlowerBand = { "rose_a", "tulip_a", "sun_petal", "hyd_a" };

        private static Model SeedBoxModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            int wood = p.I("wood"), woodD = p.I("wood_dark"), woodL = p.I("wood_light");
            const float W = 1.30f, D = 0.74f, LegH = 0.30f, Wall = 0.025f;

            // Four legs and the tray floor.
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Slab(b, woodD, new Vector3(sx * (W * 0.5f - 0.06f), LegH * 0.5f, sz * (D * 0.5f - 0.06f)), new Vector3(0.055f, LegH * 0.5f, 0.055f), 0.25f);
            Slab(b, woodD, new Vector3(0f, LegH, 0f), new Vector3(W * 0.5f, 0.03f, D * 0.5f), 0.25f);

            // Tray walls: tall back, low front, side boards.
            Slab(b, wood, new Vector3(0f, LegH + 0.19f, D * 0.5f - Wall), new Vector3(W * 0.5f, 0.19f, Wall), 0.25f);
            Slab(b, wood, new Vector3(0f, LegH + 0.075f, -D * 0.5f + Wall), new Vector3(W * 0.5f, 0.075f, Wall), 0.25f);
            foreach (float sx in new[] { -1f, 1f })
                Slab(b, wood, new Vector3(sx * (W * 0.5f - Wall), LegH + 0.12f, 0f), new Vector3(Wall, 0.12f, D * 0.5f), 0.25f);

            // Four compartments, one per flower, each full of upright pouches.
            for (int i = 1; i < 4; i++)
                Slab(b, woodL, new Vector3(-W * 0.5f + i * (W / 4f), LegH + 0.11f, 0f), new Vector3(0.014f, 0.095f, D * 0.5f - Wall), 0.3f);
            for (int c = 0; c < 4; c++)
            {
                float cx = -W * 0.5f + (c + 0.5f) * (W / 4f);
                for (int row = 0; row < 3; row++)
                    for (int k = 0; k < 2; k++)
                    {
                        float x = cx + (k == 0 ? -0.055f : 0.055f) + (row % 2) * 0.012f;
                        float z = -0.20f + row * 0.18f;
                        Pouch(b, p, new Vector3(x, LegH + 0.03f, z), (row * 31 + k * 17 + c * 11) % 14 - 7f, 0.62f + row * 0.03f,
                            FlowerBand[c], lean: -10f);
                    }
            }

            // Two posts and a hanging sign over the back board, painted with a sprout.
            foreach (float sx in new[] { -1f, 1f })
                Slab(b, woodD, new Vector3(sx * (W * 0.5f - 0.10f), LegH + 0.62f, D * 0.5f - 0.05f), new Vector3(0.04f, 0.43f, 0.04f), 0.25f);
            Slab(b, woodD, new Vector3(0f, LegH + 1.00f, D * 0.5f - 0.05f), new Vector3(W * 0.5f - 0.04f, 0.04f, 0.04f), 0.3f);
            int cream = p.I("cream"), pink = p.I("pink");
            Slab(b, pink, new Vector3(0f, LegH + 0.84f, D * 0.5f - 0.095f), new Vector3(0.42f, 0.18f, 0.016f), 0.3f);
            Slab(b, cream, new Vector3(0f, LegH + 0.84f, D * 0.5f - 0.113f), new Vector3(0.38f, 0.14f, 0.012f), 0.3f);
            Tube(b, p.I("stem"), t => new Vector3(0f, Mathf.Lerp(LegH + 0.76f, LegH + 0.88f, t), D * 0.5f - 0.124f), Vector3.right, 0.012f, 0.010f, 3, 6);
            Leaf(b, p.I("leaf"), new Vector3(0.006f, LegH + 0.84f, D * 0.5f - 0.124f), -75f, 28f, 0.13f, 0.065f, 0.1f);
            Leaf(b, p.I("leaf"), new Vector3(-0.006f, LegH + 0.82f, D * 0.5f - 0.124f), 75f, 28f, 0.12f, 0.060f, 0.1f);
            // Hanging chains (two thin ropes) from the beam to the sign.
            foreach (float sx in new[] { -1f, 1f })
                Slab(b, p.I("rope"), new Vector3(sx * 0.33f, LegH + 0.945f, D * 0.5f - 0.095f), new Vector3(0.008f, 0.05f, 0.008f), 0.3f);

            // A burlap sack of seed beside the tray, with a little spilled out in front.
            var sackAt = new Vector3(W * 0.5f + 0.34f, 0f, 0.04f);
            Put(b, sackAt, Quaternion.Euler(0f, 20f, 0f), 1f, () =>
            {
                b.Revolve(p.I("sack"), 28, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, 0f), V(0.19f, 0f), V(0.25f, 0.10f), V(0.26f, 0.22f), V(0.20f, 0.36f), V(0.11f, 0.44f), V(0.075f, 0.48f), V(0f, 0.48f)
                }, 4));
                b.Revolve(p.I("rope"), 20, GoodsMeshBuilder.Spline(new[]
                {
                    V(0.062f, 0.46f), V(0.095f, 0.46f), V(0.095f, 0.49f), V(0.062f, 0.49f), V(0.062f, 0.46f)
                }, 2));
                // Frill at the mouth.
                b.Revolve(p.I("sack_dark"), 20, GoodsMeshBuilder.Spline(new[]
                {
                    V(0.07f, 0.49f), V(0.15f, 0.54f), V(0.12f, 0.58f), V(0.05f, 0.52f), V(0.07f, 0.49f)
                }, 3));
                // Seeds peeking out of the mouth.
                Ell(b, p.I("seed"), new Vector3(0f, 0.52f, 0f), new Vector3(0.075f, 0.04f, 0.075f), 10, 6);
            });
            var rnd = new System.Random(5);
            for (int i = 0; i < 26; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 0.10f + (float)rnd.NextDouble() * 0.30f;
                var at = sackAt + new Vector3(Mathf.Cos(a) * r * 1.3f, 0.012f, -0.22f - Mathf.Abs(Mathf.Sin(a)) * r);
                Put(b, at, Quaternion.Euler(0f, a * Mathf.Rad2Deg, 0f), 1f, () =>
                    Ell(b, p.I("seed"), Vector3.zero, new Vector3(0.020f, 0.011f, 0.012f), 6, 4));
            }
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ bed stages

        public static readonly string[] BedStageNames = { "empty", "seedlings", "half", "bloom" };

        private static void Mound(GoodsMeshBuilder b, Pal p, Vector3 at, float r, bool wet) =>
            Ell(b, p.I(wet ? "soil_wet" : "soil_thirsty"), at + Vector3.up * 0.004f, new Vector3(r, r * 0.30f, r * 0.9f), 12, 6);

        private static void Sprout(GoodsMeshBuilder b, Pal p, Fl f, Vector3 at, float yaw, System.Random rnd)
        {
            float R() => (float)rnd.NextDouble();
            switch (f)
            {
                case Fl.Rose:
                    Tube(b, p.I("stem"), t => at + Vector3.up * (0.11f * t), Vector3.right, 0.011f, 0.008f, 3, 6);
                    for (int i = 0; i < 3; i++)
                        foreach (float side in new[] { 0f, 180f })
                            Leaf(b, p.I("leaf"), at + Vector3.up * (0.04f + 0.03f * i), yaw + side + i * 50f, 38f, 0.085f - i * 0.012f, 0.052f - i * 0.006f, 0.2f);
                    break;
                case Fl.Tulip:
                    for (int i = 0; i < 3; i++)
                        Leaf(b, p.I("leaf"), at, yaw + i * 120f + R() * 20f, 66f, 0.19f, 0.046f, 0.35f);
                    break;
                case Fl.Sunflower:
                    Tube(b, p.I("stem"), t => at + Vector3.up * (0.12f * t), Vector3.right, 0.016f, 0.012f, 3, 6);
                    foreach (float side in new[] { 0f, 180f })
                        Leaf(b, p.I("leaf"), at + Vector3.up * 0.10f, yaw + side, 24f, 0.15f, 0.12f, 0.25f);
                    foreach (float side in new[] { 90f, 270f })
                        Leaf(b, p.I("leaf"), at + Vector3.up * 0.115f, yaw + side, 40f, 0.075f, 0.05f, 0.15f);
                    break;
                default:
                    Tube(b, p.I("stem"), t => at + Vector3.up * (0.06f * t), Vector3.right, 0.012f, 0.009f, 2, 6);
                    for (int i = 0; i < 4; i++)
                        Leaf(b, p.I("leaf"), at + Vector3.up * 0.045f, yaw + i * 90f + R() * 16f, 32f, 0.125f, 0.092f, 0.3f);
                    break;
            }
        }

        /// <summary>A closed bud for the half-grown stage: green calyx with a hint of the flower's colour.</summary>
        private static void Bud(GoodsMeshBuilder b, Pal p, Fl f, Vector3 at, Vector3 dir, float yaw)
        {
            int leaf = p.I("leaf"), stem = p.I("stem");
            var q = Facing(dir) * Quaternion.Euler(0f, yaw, 0f);
            Put(b, at, q, 1f, () =>
            {
                switch (f)
                {
                    case Fl.Rose:
                        // Pink, not red: a red bud on a slim stem reads as a tulip.
                        Ell(b, p.I("rose_a"), new Vector3(0f, 0.075f, 0f), new Vector3(0.050f, 0.085f, 0.050f), 12, 8);
                        Ell(b, p.I("pink"), new Vector3(0f, 0.105f, 0f), new Vector3(0.036f, 0.060f, 0.036f), 10, 6);
                        for (int i = 0; i < 5; i++)
                        {
                            var rq = Quaternion.Euler(0f, i * 72f, 0f);
                            Put(b, rq * new Vector3(0f, 0.040f, 0.030f), rq * Quaternion.Euler(24f, 0f, 0f), 1f,
                                () => Petal(b, leaf, new Vector3(0.020f, 0.060f, 0.012f), 0.6f, 8, 6));
                        }
                        break;
                    case Fl.Tulip:
                        Ell(b, p.I("tulip_a"), new Vector3(0f, 0.095f, 0f), new Vector3(0.060f, 0.100f, 0.060f), 14, 8);
                        Put(b, new Vector3(0f, 0.165f, 0f), Quaternion.identity, 1f,
                            () => Petal(b, p.I("tulip_b"), new Vector3(0.040f, 0.050f, 0.040f), 0.9f, 10, 6));
                        Ell(b, leaf, new Vector3(0f, 0.02f, 0f), new Vector3(0.030f, 0.030f, 0.030f), 8, 5);
                        break;
                    case Fl.Sunflower:
                        // A nodding green disc rimmed with yellow petal tips.
                        Ell(b, p.I("sun_petal"), new Vector3(0f, 0.040f, 0f), new Vector3(0.140f, 0.030f, 0.140f), 18, 6);
                        Ell(b, leaf, new Vector3(0f, 0.058f, 0f), new Vector3(0.115f, 0.040f, 0.115f), 18, 8);
                        for (int i = 0; i < 9; i++)
                        {
                            var rq = Quaternion.Euler(0f, i * 40f, 0f);
                            Put(b, rq * new Vector3(0f, 0.045f, 0.105f), rq * Quaternion.Euler(70f, 0f, 0f), 1f,
                                () => Petal(b, leaf, new Vector3(0.028f, 0.050f, 0.012f), 0.7f, 8, 5));
                        }
                        break;
                    default:
                        // Hydrangea: a tight ball of green buds with the first blue florets showing.
                        Ell(b, leaf, new Vector3(0f, 0.11f, 0f), new Vector3(0.150f, 0.115f, 0.150f), 16, 10);
                        for (int i = 0; i < 12; i++)
                        {
                            float a = i * 2.4f, t = (i + 0.5f) / 12f;
                            float y = 0.11f + 0.10f * (1f - t * 1.6f) * 0.9f;
                            float r = 0.118f * Mathf.Sqrt(Mathf.Max(0.05f, 1f - Mathf.Pow((y - 0.11f) / 0.115f, 2f)));
                            Ell(b, p.I(i % 3 == 0 ? "hyd_b" : "hyd_a"), new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r),
                                new Vector3(0.034f, 0.026f, 0.034f), 8, 5);
                        }
                        break;
                }
            });
        }

        private static void HalfGrown(GoodsMeshBuilder b, Pal p, Fl f, Vector3 at, float yaw, System.Random rnd)
        {
            int fi = (int)f;
            float R() => (float)rnd.NextDouble();
            float H = StemH[fi] * 0.56f * (0.92f + 0.16f * R());
            var dir = Quaternion.Euler(R() * 6f - 3f, yaw, R() * 6f - 3f) * Vector3.up;
            if (f != Fl.Tulip)
                Tube(b, p.I("stem"), t => at + dir * (H * t), Vector3.right + Vector3.forward * 0.3f, 0.019f, 0.014f, 5, 6);
            else
                Tube(b, p.I("stem"), t => at + dir * (H * 1.15f * t), Vector3.right + Vector3.forward * 0.3f, 0.016f, 0.012f, 5, 6);
            switch (f)
            {
                case Fl.Rose:
                    for (int i = 0; i < 4; i++)
                        Leaf(b, p.I("leaf"), at + dir * (H * (0.20f + 0.17f * i)), yaw + i * 100f, 46f, 0.15f, 0.080f);
                    Bud(b, p, f, at + dir * H, dir, yaw);
                    break;
                case Fl.Tulip:
                    for (int i = 0; i < 3; i++)
                        Leaf(b, p.I("leaf"), at + Vector3.up * 0.02f, yaw + i * 120f + R() * 20f, 62f, 0.34f, 0.080f, 0.40f);
                    Bud(b, p, f, at + dir * (H * 1.15f), dir, yaw);
                    break;
                case Fl.Sunflower:
                    for (int i = 0; i < 4; i++)
                        Leaf(b, p.I("leaf"), at + dir * (H * (0.22f + 0.17f * i)), yaw + i * 130f, 36f, 0.26f, 0.17f);
                    Bud(b, p, f, at + dir * H, Quaternion.Euler(30f, yaw, 0f) * Vector3.up, yaw);
                    break;
                default:
                    for (int i = 0; i < 5; i++)
                        Leaf(b, p.I("leaf"), at + Vector3.up * (0.04f + 0.03f * (i % 2)), yaw + i * 72f, 34f, 0.21f, 0.150f);
                    Bud(b, p, f, at + dir * H, dir, yaw);
                    break;
            }
        }

        private static Model BedStageModel(Fl f, int stage, bool wet)
        {
            if (stage >= 3) return BedModel(f, 1);

            var b = new GoodsMeshBuilder();
            var p = new Pal();
            float hx = BedSize.x * 0.5f, hz = BedSize.y * 0.5f;

            Slab(b, p.I(wet ? "soil_wet" : "soil_thirsty"), new Vector3(0f, 0.10f, 0f), new Vector3(hx - 0.08f, 0.10f, hz - 0.08f), 0.30f);
            int wood = p.I("wood");
            foreach (float sx in new[] { -1f, 1f })
                Slab(b, wood, new Vector3(sx * (hx - 0.05f), 0.11f, 0f), new Vector3(0.05f, 0.12f, hz), 0.25f);
            foreach (float sz in new[] { -1f, 1f })
                Slab(b, wood, new Vector3(0f, 0.11f, sz * (hz - 0.05f)), new Vector3(hx, 0.12f, 0.05f), 0.25f);
            int post = p.I("wood_dark");
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    Slab(b, post, new Vector3(sx * (hx - 0.05f), 0.15f, sz * (hz - 0.05f)), new Vector3(0.075f, 0.15f, 0.075f), 0.25f);

            var rnd = new System.Random(11 + (int)f * 7);
            bool big = f == Fl.Sunflower;
            float[] xs = big ? new[] { -0.38f, 0.38f } : new[] { -0.50f, 0f, 0.50f };
            float[] zs = { -0.80f, 0f, 0.80f };

            if (stage == 0)
            {
                // Raked rows waiting for seed: shallow ridges and furrows along the bed.
                int furrow = p.I("furrow");
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.72f + i * 0.36f;
                    Slab(b, furrow, new Vector3(x, SoilTop - 0.004f, 0f), new Vector3(0.030f, 0.012f, hz - 0.20f), 0.5f, 0f, 0f, 0f, 8, 6);
                }
                // A couple of pebbles so the bare soil is not a flat colour.
                for (int i = 0; i < 7; i++)
                {
                    var at = new Vector3(((float)rnd.NextDouble() - 0.5f) * 1.4f, SoilTop, ((float)rnd.NextDouble() - 0.5f) * 2.0f);
                    Ell(b, p.I("stone_dark"), at, new Vector3(0.035f, 0.016f, 0.028f), 8, 5);
                }
                return new Model { Mesh = b, Pal = p };
            }

            foreach (float x in xs)
                foreach (float z in zs)
                {
                    var at = new Vector3(x + ((float)rnd.NextDouble() - 0.5f) * 0.10f, SoilTop - 0.01f,
                        z + ((float)rnd.NextDouble() - 0.5f) * 0.10f);
                    float yaw = (float)rnd.NextDouble() * 360f;
                    Mound(b, p, at, stage == 1 ? 0.14f : 0.18f, wet);
                    if (stage == 1) Sprout(b, p, f, at, yaw, rnd);
                    else HalfGrown(b, p, f, at, yaw, rnd);

                    if (wet)
                    {
                        // A shiny film of water pooled round the plant and beads on the soil.
                        Ell(b, p.I("puddle"), at + new Vector3(0f, 0.014f, 0f), new Vector3(0.16f, 0.008f, 0.14f), 14, 5);
                        for (int i = 0; i < 4; i++)
                        {
                            float a = i * 1.7f + yaw;
                            Ell(b, p.I("drop"), at + new Vector3(Mathf.Cos(a) * 0.12f, 0.04f, Mathf.Sin(a) * 0.12f),
                                new Vector3(0.020f, 0.024f, 0.020f), 8, 6);
                        }
                    }
                }
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ the derelict shop

        /// <summary>Withered flowers lying in a heap: dry stems, brown petals, flopped leaves.</summary>
        private static void DeadBunch(GoodsMeshBuilder b, Pal p, Vector3 at, float yaw, float s)
        {
            Put(b, at, Quaternion.Euler(0f, yaw, 0f), s, () =>
            {
                var rnd = new System.Random((int)(yaw * 7f) + 3);
                for (int i = 0; i < 7; i++)
                {
                    float a = (float)rnd.NextDouble() * 360f * Mathf.Deg2Rad;
                    var from = new Vector3(Mathf.Cos(a) * 0.05f, 0.02f, Mathf.Sin(a) * 0.05f);
                    var to = from + new Vector3(Mathf.Cos(a + 0.4f), 0.05f * i % 3, Mathf.Sin(a + 0.4f)) * (0.32f + 0.1f * (float)rnd.NextDouble());
                    Tube(b, p.I("dry_stem"), t => Vector3.Lerp(from, to, t) + Vector3.up * (0.05f * Mathf.Sin(t * Mathf.PI)),
                        Vector3.up, 0.011f, 0.008f, 4, 6);
                    Ell(b, p.I(i % 2 == 0 ? "dry_petal" : "dry_petal_b"), to + Vector3.up * 0.02f,
                        new Vector3(0.060f, 0.022f, 0.060f), 10, 5);
                    Ell(b, p.I("dry_core"), to + Vector3.up * 0.035f, new Vector3(0.026f, 0.014f, 0.026f), 8, 4);
                    Leaf(b, p.I("dry_leaf"), Vector3.Lerp(from, to, 0.5f) + Vector3.up * 0.02f, a * Mathf.Rad2Deg + 90f, 10f, 0.13f, 0.06f, 0.5f);
                }
            });
        }

        private static Model BrokenWallStandModel(float width, bool mitre)
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            int cream = p.I("grub_cream"), wood = p.I("grub_wood"), dark = p.I("grub_wood_dark"), pink = p.I("grub_pink");
            float hw = width * 0.5f;
            float depth0 = StandBack - TierFront[0], cz0 = (StandBack + TierFront[0]) * 0.5f;
            Func<Vector3, Vector3> mod = null;
            if (mitre)
                mod = q =>
                {
                    float fz = Mathf.InverseLerp(depth0 * 0.5f, -depth0 * 0.5f, q.z);
                    q.x *= Mathf.Lerp(1f, 0.74f, fz);
                    return q;
                };

            // Bottom tier: still there, but dusty, with its front panel kicked in.
            Slab(b, cream, new Vector3(0f, TierTop[0] * 0.5f, cz0), new Vector3(hw, TierTop[0] * 0.5f, depth0 * 0.5f), 0.20f, modify: mod);
            Slab(b, wood, new Vector3(0f, TierTop[0] + 0.012f, cz0), new Vector3(hw + 0.02f, 0.028f, depth0 * 0.5f + 0.02f), 0.30f, modify: mod);
            Slab(b, pink, new Vector3(hw * 0.35f, 0.16f, TierFront[0] - 0.02f), new Vector3(hw * 0.35f, 0.035f, 0.018f), 0.4f, 0f, 0f, 14f);
            // Dark gap where the front board is missing.
            Slab(b, dark, new Vector3(-hw * 0.30f, 0.12f, TierFront[0] + 0.01f), new Vector3(hw * 0.28f, 0.10f, 0.02f), 0.3f);

            // Middle tier: hanging off the back by one corner.
            Slab(b, cream, new Vector3(-hw * 0.12f, 0.62f, 0.30f), new Vector3(hw * 0.88f, 0.12f, 0.20f), 0.22f, 0f, 0f, mitre ? 18f : 22f);
            // Top tier: collapsed onto the floor in front.
            Slab(b, cream, new Vector3(hw * 0.10f, 0.07f, TierFront[0] - 0.46f), new Vector3(hw * 0.80f, 0.07f, 0.20f), 0.22f, 12f, 0f, 4f);
            Slab(b, wood, new Vector3(hw * 0.10f, 0.15f, TierFront[0] - 0.46f), new Vector3(hw * 0.80f, 0.018f, 0.21f), 0.3f, 12f, 0f, 4f);
            // A snapped upright and a loose plank.
            Slab(b, dark, new Vector3(hw * 0.95f, 0.42f, 0.15f), new Vector3(0.04f, 0.42f, 0.04f), 0.25f, 0f, 0f, -16f);
            Slab(b, wood, new Vector3(-hw * 0.60f, 0.045f, TierFront[0] - 0.12f), new Vector3(0.45f, 0.022f, 0.065f), 0.25f, -28f);
            DeadBunch(b, p, new Vector3(-hw * 0.45f, 0.30f, 0.10f), 20f, 0.9f);
            return new Model { Mesh = b, Pal = p };
        }

        private static Model BrokenCentreStandModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            int cream = p.I("grub_cream"), wood = p.I("grub_wood"), dark = p.I("grub_wood_dark"), pink = p.I("grub_pink");
            float h = CentreHalf[0];
            Slab(b, cream, new Vector3(0f, TierTop[0] * 0.5f, 0f), new Vector3(h, TierTop[0] * 0.5f, h), 0.22f);
            Slab(b, wood, new Vector3(0f, TierTop[0] + 0.012f, 0f), new Vector3(h + 0.02f, 0.028f, h + 0.02f), 0.30f);
            Slab(b, pink, new Vector3(0.1f, 0.18f, -h - 0.008f), new Vector3(h * 0.5f, 0.035f, 0.018f), 0.4f, 0f, 0f, -12f);
            // Second tier slid off to one side, third on the floor.
            Slab(b, cream, new Vector3(0.34f, TierTop[0] + 0.19f, 0.10f), new Vector3(CentreHalf[1], 0.16f, CentreHalf[1]), 0.22f, 16f, 5f, 10f);
            Slab(b, cream, new Vector3(-0.46f, 0.10f, -h - 0.55f), new Vector3(CentreHalf[2], 0.10f, CentreHalf[2]), 0.22f, -24f, 0f, 3f);
            Slab(b, dark, new Vector3(0.9f, 0.04f, -h - 0.30f), new Vector3(0.5f, 0.022f, 0.06f), 0.25f, 32f);
            DeadBunch(b, p, new Vector3(0.25f, TierTop[0] + 0.30f, 0.20f), 70f, 1.0f);
            return new Model { Mesh = b, Pal = p };
        }

        private static Model BrokenCounterModel()
        {
            var b = new GoodsMeshBuilder();
            var p = new Pal();
            int cream = p.I("grub_cream"), wood = p.I("grub_wood"), dark = p.I("grub_wood_dark"), pink = p.I("grub_pink"), paper = p.I("grub_paper");
            const float W = 3.2f;
            // Carcass, one end sagging.
            Slab(b, cream, new Vector3(-0.55f, 0.46f, 0f), new Vector3(W * 0.5f - 0.55f, 0.46f, 0.42f), 0.16f, 0f, 0f, 0f);
            Slab(b, cream, new Vector3(1.20f, 0.36f, 0f), new Vector3(0.40f, 0.36f, 0.42f), 0.16f, 0f, 0f, -8f);
            Slab(b, dark, new Vector3(0f, 0.05f, 0f), new Vector3(W * 0.5f + 0.02f, 0.05f, 0.45f), 0.3f);
            // Worktop snapped in two: one half on, one half dropped across the end.
            Slab(b, wood, new Vector3(-0.60f, 1.00f, -0.02f), new Vector3(1.0f, 0.045f, 0.50f), 0.28f, 0f, 0f, 2f);
            Slab(b, wood, new Vector3(1.05f, 0.80f, -0.02f), new Vector3(0.62f, 0.045f, 0.50f), 0.28f, 8f, 0f, -22f);
            // Front panels: one left, one hanging, one gone.
            Slab(b, pink, new Vector3(-1.05f, 0.50f, -0.44f), new Vector3(0.44f, 0.27f, 0.02f), 0.35f);
            Slab(b, pink, new Vector3(0.10f, 0.40f, -0.50f), new Vector3(0.44f, 0.27f, 0.02f), 0.35f, 0f, 14f, 22f);
            Slab(b, dark, new Vector3(0.98f, 0.42f, -0.38f), new Vector3(0.40f, 0.30f, 0.02f), 0.3f);
            // A dead roll of wrapping paper on the floor, ribbon spool gone, a cracked vase.
            Put(b, new Vector3(-1.0f, 0.12f, -0.85f), Quaternion.Euler(0f, 30f, 90f), 1f, () =>
                b.Revolve(paper, 24, GoodsMeshBuilder.Spline(new[]
                {
                    V(0f, -0.26f), V(0.11f, -0.26f), V(0.12f, -0.24f), V(0.12f, 0.24f), V(0.11f, 0.26f), V(0f, 0.26f)
                }, 3)));
            DeadBunch(b, p, new Vector3(0.45f, 0.0f, -0.95f), 140f, 1.1f);
            return new Model { Mesh = b, Pal = p };
        }

        // ------------------------------------------------------------------ public spawners

        /// <summary>The seed item's good: mesh in Meshes/Goods and prefab in Prefabs/Goods, like every good.</summary>
        internal static void BuildSeedGood()
        {
            var (mesh, mats) = Bake(GoodsModels.MeshPath("seed"), "Good_seed", SeedPouchModel());
            var go = new GameObject("Good_seed");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            PrefabUtility.SaveAsPrefabAsset(go, GoodsModels.PrefabPath("seed"));
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[Florist] Good_seed: use stackHeight {mesh.bounds.size.y:0.00}");
        }

        public static GameObject SeedBox(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("SeedBox", parent, pos, yaw, Get(PreviewKeyFolder + "SeedBox", SeedBoxModel));

        public static GameObject SeedPouch(Transform parent, Vector3 localPos, float yaw = 0f)
        {
            var go = Obj("Good_seed", parent, localPos, yaw, Get(PreviewKeyFolder + "Good_seed", SeedPouchModel), false);
            return go;
        }

        /// <summary>A bed in a growth stage: 0 empty, 1 seedlings, 2 half-grown, 3 full bloom (ready).</summary>
        public static GameObject BedStage(Fl f, int stage, bool wet, Transform parent, Vector3 pos, float yaw = 0f)
        {
            string key = $"BedS_{FlowerIds[(int)f]}_{BedStageNames[stage]}{(wet ? "_wet" : "")}";
            return Obj(key, parent, pos, yaw, Get(PreviewKeyFolder + key, () => BedStageModel(f, stage, wet)), false);
        }

        public static GameObject BrokenWallStand(Transform parent, Vector3 pos, float yaw = 0f, bool corner = false)
        {
            float w = corner ? CornerStandWidth : WallStandWidth;
            var go = Obj("BrokenWallStand", parent, pos, yaw, Get(PreviewKeyFolder + (corner ? "BrokenCornerStand" : "BrokenWallStand"),
                () => BrokenWallStandModel(w, corner)));
            AddToppledBucket(go.transform, new Vector3(w * 0.28f, 0.10f, TierFront[0] - 0.85f), 70f);
            return go;
        }

        public static GameObject BrokenCentreStand(Transform parent, Vector3 pos, float yaw = 0f)
        {
            var go = Obj("BrokenCentreStand", parent, pos, yaw, Get(PreviewKeyFolder + "BrokenCentreStand", BrokenCentreStandModel));
            AddToppledBucket(go.transform, new Vector3(-0.2f, 0.10f, -CentreHalf[0] - 1.45f), -50f);
            return go;
        }

        public static GameObject BucketObj(Transform parent, Vector3 pos)
        {
            var bk = Get("Bucket", BucketModel);
            return Obj("Bucket", parent, pos, 0f, bk, false);
        }

        public static GameObject BrokenCounter(Transform parent, Vector3 pos, float yaw = 0f) =>
            Obj("BrokenCounter", parent, pos, yaw, Get(PreviewKeyFolder + "BrokenCounter", BrokenCounterModel));

        private static void AddToppledBucket(Transform parent, Vector3 pos, float yaw)
        {
            var bk = Get("Bucket", BucketModel);
            var tin = new GameObject("ToppledBucket");
            tin.transform.SetParent(parent, false);
            tin.transform.localPosition = pos;
            tin.transform.localRotation = Quaternion.Euler(0f, yaw, 90f);
            tin.AddComponent<MeshFilter>().sharedMesh = bk.mesh;
            var r = tin.AddComponent<MeshRenderer>();
            r.sharedMaterials = bk.mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>A loose heap of dead flowers, for scattering round the derelict shop and meadow door.</summary>
        public static GameObject DeadFlowers(Transform parent, Vector3 pos, float yaw = 0f, float scale = 1f) =>
            Obj("DeadFlowers", parent, pos, yaw, Get(PreviewKeyFolder + "DeadFlowers", () =>
            {
                var b = new GoodsMeshBuilder();
                var p = new Pal();
                DeadBunch(b, p, Vector3.zero, 15f, scale);
                return new Model { Mesh = b, Pal = p };
            }), false);
    }
}
