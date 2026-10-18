using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The hairdresser's 3D art as one picture for Filip (PROPOSAL, phase 1; nothing here is wired into the scene
    /// build): the salon chair, the mirror station, the waiting chair, the reception desk, the pile of cut hair, the
    /// hair bag you carry and the bin, each clean and wrecked where it has a wrecked twin; the hairdresser,
    /// receptionist and cleaner; and the whole salon grubby and clean at game scale, laid out as in
    /// <c>salon-layout.png</c>. Rendered in an empty scene with the game camera (pitch 50, yaw 45, orthographic), like
    /// <see cref="FloristPreview"/>, and reusing its stage, camera and worker helpers. Needs a GPU (no -nographics).
    /// Run: <c>-executeMethod Tycoon.EditorTools.SalonPreview.ExportArt</c> (writes salon-art-3d.png).
    /// </summary>
    public static class SalonPreview
    {
        private const string OutDir = "C:/Users/ADMINI~1/AppData/Local/Temp/claude/D--iosGame/b50262dc-8611-42da-8650-85247e0c766c/scratchpad/salon-preview/";
        private const int W = 2400, H = 3000;

        // The salon, in the town's grid (the numbers of the layout picture).
        private const float SW = 4.8f, SE = 18.2f, SS = -0.8f, SN = 9.6f;
        private static readonly Vector3 SalonCentre = new Vector3((SW + SE) * 0.5f, 0f, (SS + SN) * 0.5f);

        private static Material M(string n, Color c, float s = 0.2f) => LevelBuildKit.Mat("SlPrev_" + n, c, s);

        [MenuItem("Tycoon/Export Salon Preview")]
        public static void ExportArt()
        {
            // (no FloristModels.BuildAll here: it rewrites the florist prefabs; the salon meshes bake on demand)
            var root = FloristPreview.NewStage(out var right, out var fwd);
            Vector3 Slot(int i) => right * (i * 60f);
            int slot = 0;

            // ---------------------------------------------------------------- the pieces
            var chairAt = Slot(slot++); FloristModels.SalonChair(root, chairAt, 0f);
            var chairBrokenAt = Slot(slot++); FloristModels.SalonChair(root, chairBrokenAt, 0f, true);
            var mirrorAt = Slot(slot++); FloristModels.MirrorStation(root, mirrorAt, 0f);
            var mirrorBrokenAt = Slot(slot++); FloristModels.MirrorStation(root, mirrorBrokenAt, 0f, true);
            var waitAt = Slot(slot++); FloristModels.WaitingChair(root, waitAt, 0f);

            // A chair in front of its mirror, the way the salon stands them (sitter faces the mirror).
            var stationAt = Slot(slot++);
            FloristModels.MirrorStation(root, stationAt + new Vector3(0f, 0f, 1.35f), 0f);
            FloristModels.SalonChair(root, stationAt, 180f);

            var deskAt = Slot(slot++); FloristModels.ReceptionDesk(root, deskAt, 0f);
            var deskBrokenAt = Slot(slot++); FloristModels.ReceptionDesk(root, deskBrokenAt, 0f, true);
            var heapAt = Slot(slot++); FloristModels.HairHeap(root, heapAt, 0f);

            var bagAt = Slot(slot++);
            float bagH = 0.34f * GoodsModels.Scale;
            FloristModels.HairBag(root, bagAt + right * -0.40f + Vector3.up * (bagH * 0.5f), 20f);
            for (int k = 0; k < 2; k++)
                FloristModels.HairBag(root, bagAt + right * 0.40f + Vector3.up * (bagH * 0.5f + k * bagH), 20f + 25f * k);

            var binAt = Slot(slot++);
            var binRoot = new GameObject("BinPreview").transform;
            binRoot.SetParent(root, false);
            binRoot.position = binAt;
            LevelBuildKit.BuildBin("salon.preview.bin", "Bin", binRoot, Vector3.zero, squareToSouth: false);

            // ---------------------------------------------------------------- people
            var peopleAt = Slot(slot++);
            Hairdresser(root, peopleAt + right * -1.7f, 200f);
            Receptionist(root, peopleAt, 190f);
            Cleaner(root, peopleAt + right * 1.7f, 160f, true);

            // ---------------------------------------------------------------- the whole salon, grubby and clean
            var grubbyAt = fwd * 400f;
            var cleanAt = fwd * 400f + right * 90f;
            BuildSalon(root, grubbyAt, true);
            BuildSalon(root, cleanAt, false);

            // ---------------------------------------------------------------- render
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            const int top = H;
            int y = top - 560;
            FloristPreview.Cell(root, rt, new RectInt(0, y, 480, 560), chairAt + Vector3.up * 0.65f, 1.30f);
            FloristPreview.Cell(root, rt, new RectInt(480, y, 480, 560), chairBrokenAt + Vector3.up * 0.65f, 1.30f);
            FloristPreview.Cell(root, rt, new RectInt(960, y, 480, 560), mirrorAt + Vector3.up * 1.2f, 1.62f);
            FloristPreview.Cell(root, rt, new RectInt(1440, y, 480, 560), mirrorBrokenAt + Vector3.up * 1.2f, 1.62f);
            FloristPreview.Cell(root, rt, new RectInt(1920, y, 480, 560), waitAt + Vector3.up * 0.4f, 0.95f);

            y -= 600;
            FloristPreview.Cell(root, rt, new RectInt(0, y, 800, 600), deskAt + Vector3.up * 0.7f, 1.75f);
            FloristPreview.Cell(root, rt, new RectInt(800, y, 800, 600), deskBrokenAt + Vector3.up * 0.7f, 1.75f);
            FloristPreview.Cell(root, rt, new RectInt(1600, y, 400, 600), heapAt + Vector3.up * 0.05f, 0.85f);
            FloristPreview.Cell(root, rt, new RectInt(2000, y, 400, 600), bagAt + Vector3.up * 0.55f, 1.25f);

            y -= 620;
            FloristPreview.Cell(root, rt, new RectInt(0, y, 500, 620), binAt + Vector3.up * 0.5f, 1.55f);
            FloristPreview.Cell(root, rt, new RectInt(500, y, 700, 620), stationAt + new Vector3(0f, 1.0f, 0.6f), 1.75f);
            FloristPreview.Cell(root, rt, new RectInt(1200, y, 1200, 620), peopleAt + Vector3.up * 0.75f, 1.40f);

            y -= 1200;
            FloristPreview.Cell(root, rt, new RectInt(0, y, 1200, 1200), grubbyAt + new Vector3(0f, 0.3f, 0f), 1200f / (2f * 64f) * 1.05f);
            FloristPreview.Cell(root, rt, new RectInt(1200, y, 1200, 1200), cleanAt + new Vector3(0f, 0.3f, 0f), 1200f / (2f * 64f) * 1.05f);

            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes(OutDir + "salon-art-3d.png", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log("[SalonPreview] -> " + OutDir + "salon-art-3d.png");
            AssetDatabase.SaveAssets();
            Debug.Log("SALONPREVIEW_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }


        // ------------------------------------------------------------------ the staff (the looks to approve)

        private static Transform Hairdresser(Transform parent, Vector3 pos, float yaw) =>
            FloristPreview.Worker(parent, pos, yaw, CharacterLibrary.Customer, "SalonHairdresser",
                new Dictionary<int, Color>
                {
                    { CharacterLibrary.ShirtSlot, new Color(0.20f, 0.74f, 0.68f) },
                    { CharacterLibrary.TrouserSlot, new Color(0.17f, 0.21f, 0.34f) },
                    { CharacterLibrary.ShoeSlot, new Color(0.96f, 0.96f, 0.98f) },
                }, null);

        private static Transform Receptionist(Transform parent, Vector3 pos, float yaw) =>
            FloristPreview.Worker(parent, pos, yaw, CharacterLibrary.Cashier, "SalonReceptionist",
                new Dictionary<int, Color>
                {
                    { CharacterLibrary.ShirtSlot, new Color(0.74f, 0.94f, 0.89f) },
                    { CharacterLibrary.TrouserSlot, new Color(0.17f, 0.21f, 0.34f) },
                    { CharacterLibrary.ShoeSlot, new Color(0.17f, 0.21f, 0.34f) },
                    { 4, new Color(0.20f, 0.74f, 0.68f) },
                    { 5, new Color(0.96f, 0.96f, 0.98f) },
                }, null);

        private static Transform Cleaner(Transform parent, Vector3 pos, float yaw, bool carrying)
        {
            var t = FloristPreview.Worker(parent, pos, yaw, CharacterLibrary.Farmer, "SalonCleaner",
                new Dictionary<int, Color>
                {
                    { CharacterLibrary.ShirtSlot, new Color(0.30f, 0.36f, 0.54f) },
                    { CharacterLibrary.TrouserSlot, new Color(0.17f, 0.21f, 0.34f) },
                    { CharacterLibrary.ShoeSlot, new Color(0.96f, 0.96f, 0.98f) },
                    { 4, new Color(0.74f, 0.94f, 0.89f) },
                    { 5, new Color(0.20f, 0.74f, 0.68f) },
                }, null);
            if (t != null && carrying) FloristModels.HairBag(t, CharacterLibrary.HandAnchor, 0f).name = "Carried_hair_bag";
            return t;
        }

        // ------------------------------------------------------------------ the whole salon

        private static Vector3 At(Transform g, float x, float z) => new Vector3(x - SalonCentre.x, 0f, z - SalonCentre.z);

        private static void BuildSalon(Transform stage, Vector3 at, bool derelict)
        {
            var g = new GameObject(derelict ? "SalonGrubby" : "SalonClean").transform;
            g.SetParent(stage, false);
            g.position = at;

            var pave = M("Pave", new Color(0.69f, 0.67f, 0.64f), 0.1f);
            var floorA = derelict ? M("FloorDirtyA", new Color(0.34f, 0.32f, 0.29f), 0.1f) : M("FloorA", new Color(0.93f, 0.91f, 0.87f), 0.35f);
            var floorB = derelict ? M("FloorDirtyB", new Color(0.29f, 0.28f, 0.26f), 0.1f) : M("FloorB", new Color(0.78f, 0.86f, 0.85f), 0.35f);
            var wall = derelict ? M("WallDirty", new Color(0.55f, 0.50f, 0.43f), 0.1f) : M("Wall", new Color(0.99f, 0.97f, 0.93f), 0.4f);
            var cap = derelict ? M("CapDirty", new Color(0.43f, 0.40f, 0.36f), 0.1f) : M("Cap", new Color(0.20f, 0.74f, 0.68f), 0.4f);
            var matIn = M("MatIn", new Color(0.46f, 0.78f, 0.52f), 0.2f);
            var matOut = M("MatOut", new Color(0.98f, 0.66f, 0.30f), 0.2f);

            float cx = SalonCentre.x, cz = SalonCentre.z;
            LevelBuildKit.Box("Pave", g, new Vector3(0f, -0.01f, 0f), new Vector3(SE - SW + 6f, 0.04f, SN - SS + 4f), pave, castShadow: false);
            for (int i = 0; i * 0.8f < SE - SW; i++)
                for (int j = 0; j * 0.8f < SN - SS; j++)
                {
                    float x0 = SW + i * 0.8f, z0 = SS + j * 0.8f;
                    float x1 = Mathf.Min(x0 + 0.8f, SE), z1 = Mathf.Min(z0 + 0.8f, SN);
                    LevelBuildKit.Box("Tile", g, At(g, (x0 + x1) * 0.5f, (z0 + z1) * 0.5f) + new Vector3(0f, 0.03f, 0f),
                        new Vector3(x1 - x0, 0.06f, z1 - z0), (i + j) % 2 == 0 ? floorA : floorB, castShadow: false);
                }

            const float wallH = 0.7f;
            void Wall(float x0, float x1, float z0, float z1)
            {
                var c = At(g, (x0 + x1) * 0.5f, (z0 + z1) * 0.5f) + new Vector3(0f, wallH * 0.5f, 0f);
                var s = new Vector3(Mathf.Max(0.2f, x1 - x0), wallH, Mathf.Max(0.2f, z1 - z0));
                LevelBuildKit.Box("Wall", g, c, s, wall, castShadow: false);
                LevelBuildKit.Box("WallCap", g, c + Vector3.up * (wallH * 0.5f + 0.03f), new Vector3(s.x + 0.04f, 0.07f, s.z + 0.04f), cap, castShadow: false);
            }
            Wall(SW - 0.1f, 6.0f, SS - 0.1f, SS + 0.1f); Wall(8.0f, 14.0f, SS - 0.1f, SS + 0.1f); Wall(16.0f, SE + 0.1f, SS - 0.1f, SS + 0.1f);
            Wall(SW - 0.1f, SW + 0.1f, SS, SN);
            Wall(SW - 0.1f, SE + 0.1f, SN - 0.1f, SN + 0.1f);
            Wall(SE - 0.1f, SE + 0.1f, SS, 3.4f); Wall(SE - 0.1f, SE + 0.1f, 4.8f, SN);
            LevelBuildKit.Box("MatIn", g, At(g, 7.0f, SS + 0.6f) + new Vector3(0f, 0.07f, 0f), new Vector3(1.6f, 0.03f, 0.9f), matIn, castShadow: false);
            LevelBuildKit.Box("MatOut", g, At(g, 15.0f, SS + 0.6f) + new Vector3(0f, 0.07f, 0f), new Vector3(1.6f, 0.03f, 0.9f), matOut, castShadow: false);
            LevelBuildKit.Box("MatSide", g, At(g, SE - 0.5f, 4.1f) + new Vector3(0f, 0.07f, 0f), new Vector3(0.9f, 0.03f, 1.2f), matOut, castShadow: false);

            // The bin, outside the side door.
            var binHolder = new GameObject("BinHolder").transform;
            binHolder.SetParent(g, false);
            LevelBuildKit.BuildBin("salon.preview.bin." + (derelict ? "g" : "c"), "Bin", binHolder, At(g, 20.2f, 4.0f), squareToSouth: false);

            // Furniture. Stations: (chair x, chair z, wall) - the numbers of the layout picture.
            // The mirror stands against the wall, the chair 1.1 m from it, the sitter facing the mirror.
            (float x, float z, bool north)[] st = { (10.0f, 8.5f, true), (13.4f, 8.5f, true), (17.0f, 5.9f, false), (17.0f, 2.4f, false) };
            foreach (var s in st)
            {
                if (s.north)
                {
                    FloristModels.MirrorStation(g, At(g, s.x, SN - 0.34f) + Vector3.up * 0.06f, 0f, derelict);
                    FloristModels.SalonChair(g, At(g, s.x, s.z) + Vector3.up * 0.06f, derelict ? 180f - 12f : 180f, derelict);
                }
                else
                {
                    FloristModels.MirrorStation(g, At(g, SE - 0.34f, s.z) + Vector3.up * 0.06f, 90f, derelict);
                    FloristModels.SalonChair(g, At(g, s.x, s.z) + Vector3.up * 0.06f, derelict ? -90f + 10f : -90f, derelict);
                }
            }
            FloristModels.ReceptionDesk(g, At(g, 10.8f, 2.05f) + Vector3.up * 0.06f, 0f, derelict);
            for (int i = 0; i < 4; i++)
            {
                var chair = FloristModels.WaitingChair(g, At(g, 5.55f, 2.2f + i * 1.6f) + Vector3.up * 0.06f, -90f);
                if (derelict && i == 1) chair.transform.localRotation = Quaternion.Euler(0f, -60f, 82f);          // tipped over
                if (derelict && i == 2) chair.transform.localPosition += new Vector3(0.3f, 0f, 0.1f);
            }
            if (!derelict)
            {
                // Hair on the floor at chair 1 and 3; the staff.
                FloristModels.HairHeap(g, At(g, 11.75f, 8.7f) + Vector3.up * 0.06f, 20f);
                FloristModels.HairHeap(g, At(g, 16.95f, 7.6f) + Vector3.up * 0.06f, 80f);
                Hairdresser(g, At(g, 10.0f, 7.0f) + Vector3.up * 0.06f, 0f);
                Receptionist(g, At(g, 10.8f, 3.3f) + Vector3.up * 0.06f, 180f);
                Cleaner(g, At(g, 16.6f, 6.9f) + Vector3.up * 0.06f, 150f, true);
                for (int i = 0; i < 2; i++)
                    FloristPreview.Worker(g, At(g, 6.4f, 2.2f + i * 1.6f) + Vector3.up * 0.06f, 90f, CharacterLibrary.Customer,
                        "SalonCustomer" + i, new Dictionary<int, Color>
                        {
                            { CharacterLibrary.ShirtSlot, i == 0 ? new Color(0.95f, 0.62f, 0.62f) : new Color(0.98f, 0.82f, 0.40f) },
                        }, null);
                // A plant by the street door.
                LevelBuildKit.Cylinder("Pot", g, At(g, 8.6f, -0.1f) + new Vector3(0f, 0.2f, 0f), new Vector3(0.45f, 0.2f, 0.45f), M("Pot", new Color(0.97f, 0.97f, 0.98f), 0.5f));
                for (int k = 0; k < 6; k++)
                    LevelBuildKit.Box("Leaf", g, At(g, 8.6f, -0.1f) + new Vector3(0f, 0.55f, 0f), new Vector3(0.06f, 0.5f, 0.06f), M("Leaf", new Color(0.30f, 0.62f, 0.22f), 0.3f),
                        castShadow: false, rot: new Vector3(20f, k * 60f, 0f));
            }
            else
            {
                BuildLitter(g, At(g, 7.6f, 2.0f), 11);
                BuildLitter(g, At(g, 13.4f, 4.8f), 23);
                BuildLitter(g, At(g, 7.6f, 6.4f), 37);
                var dirt = M("Dirt", new Color(0.22f, 0.20f, 0.17f), 0.05f);
                var rnd = new System.Random(77);
                for (int i = 0; i < 16; i++)
                {
                    float x = SW + 0.8f + (float)rnd.NextDouble() * (SE - SW - 1.6f), z = SS + 0.8f + (float)rnd.NextDouble() * (SN - SS - 1.6f);
                    var o = LevelBuildKit.Cylinder("Dirt", g, At(g, x, z) + new Vector3(0f, 0.065f, 0f),
                        new Vector3(0.8f + (float)rnd.NextDouble() * 1.3f, 0.01f, 0.6f + (float)rnd.NextDouble() * 0.9f), dirt, false);
                    o.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
                }
                // Hair left on the floor round the wrecked chairs.
                FloristModels.HairHeap(g, At(g, 11.75f, 8.4f) + Vector3.up * 0.06f, 20f);
                FloristModels.HairHeap(g, At(g, 15.9f, 3.4f) + Vector3.up * 0.06f, 140f);
            }
        }

        /// <summary>A heap of rubbish in the salon's own mix: bin bags, boxes, towels, empty bottles and hair.</summary>
        private static void BuildLitter(Transform parent, Vector3 position, int seed)
        {
            var pile = new GameObject("Litter").transform;
            pile.SetParent(parent, false);
            pile.localPosition = position;
            var rnd = new System.Random(seed);
            var mats = new[]
            {
                M("LBag", new Color(0.18f, 0.20f, 0.19f), 0.3f), M("LCard", new Color(0.62f, 0.49f, 0.32f), 0.1f),
                M("LTowel", new Color(0.50f, 0.66f, 0.64f), 0.1f), M("LBottle", new Color(0.30f, 0.50f, 0.50f), 0.5f),
            };
            for (int i = 0; i < 10; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * 1.3f, z = ((float)rnd.NextDouble() - 0.5f) * 1.1f;
                float size = 0.25f + (float)rnd.NextDouble() * 0.35f, yaw = (float)rnd.NextDouble() * 180f;
                if (i % 4 == 3)
                {
                    var hair = FloristModels.HairHeap(pile, new Vector3(x, 0.02f, z), yaw);
                    hair.transform.localScale = Vector3.one * 0.55f;
                    continue;
                }
                LevelBuildKit.Box("Litter", pile, new Vector3(x, size * 0.5f, z), new Vector3(size, size * 0.8f, size * 1.1f),
                    mats[i % mats.Length], castShadow: false, rot: new Vector3(0f, yaw, 0f));
            }
        }
    }
}
