using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The café's 3D art as pictures for Filip (PROPOSAL, phase 1; nothing here is wired into the scene build): the parts one by one
    /// (tables and chairs inside and in the street garden, the counter with the basic and premium coffee machine, the oven, the display
    /// case, the fridge, the dishwasher, the bean box, the delivery crates and van, the goods you carry, and the barista, the waiter and the
    /// baker), each clean and wrecked where it has a wrecked twin; and the whole café grubby and clean at game scale, laid out from
    /// <c>cafe-layout.json</c> (the numbers of the layout picture, written by the layout script). Rendered in an empty scene with the game
    /// camera (pitch 50, yaw 45, orthographic) like <see cref="SalonPreview"/>, reusing its stage, camera and worker helpers.
    /// Needs a GPU (no -nographics). Run: <c>-executeMethod Tycoon.EditorTools.CafePreview.ExportArt</c> (cafe-art-3d.png) or
    /// <c>...ExportScene</c> (cafe-scene-3d.png).
    /// </summary>
    public static class CafePreview
    {
        private static string OutDir => CafeIconSheet.OutDir;

        [Serializable] private class LItem { public string kind; public float x, z, yaw; public string name, tag; }
        [Serializable] private class Layout { public LItem[] items; }

        private static Material M(string n, Color c, float s = 0.2f) => LevelBuildKit.Mat("CfPrev_" + n, c, s);

        // The café, in the town's grid.
        private const float SW = 24f, SS = 2f, SE = 38f, SN = 13f;
        private static readonly Vector3 Centre = new Vector3(33.5f, 0f, 5.5f);
        private static Vector3 At(float x, float z) => new Vector3(x - Centre.x, 0f, z - Centre.z);

        // ------------------------------------------------------------------ the parts

        [MenuItem("Tycoon/Export Cafe Preview")]
        public static void ExportArt()
        {
            const int W = 2400, H = 4260;
            var root = FloristPreview.NewStage(out var right, out var fwd);
            int slot = 0;
            Vector3 Slot() => right * (slot++ * 60f);

            var tableA = Slot(); FloristModels.CafeTable(root, tableA, 0f);
            var tableB = Slot(); FloristModels.CafeTable(root, tableB, 0f, true);
            var chairA = Slot(); FloristModels.CafeChair(root, chairA, 0f);
            var chairB = Slot(); FloristModels.CafeChair(root, chairB, 0f, true);
            var stoolA = Slot(); FloristModels.CafeStool(root, stoolA, 0f);
            var setA = Slot(); TableSet(root, setA, false, false);
            var setB = Slot(); TableSet(root, setB, false, true);

            var gtA = Slot(); FloristModels.GardenTable(root, gtA, 0f);
            var gtB = Slot(); FloristModels.GardenTable(root, gtB, 0f, true);
            var gcA = Slot(); FloristModels.GardenChair(root, gcA, 0f);
            var gcB = Slot(); FloristModels.GardenChair(root, gcB, 0f, true);
            var gsA = Slot(); TableSet(root, gsA, true, false);
            var gsB = Slot(); TableSet(root, gsB, true, true);

            var cnt1 = Slot(); FloristModels.CafeCounter(root, cnt1, 0f); FloristModels.CoffeeMachine(root, cnt1 + new Vector3(-1.0f, 1.02f, 0f), 0f);
            var cnt2 = Slot(); FloristModels.CafeCounter(root, cnt2, 0f); FloristModels.CoffeeMachine(root, cnt2 + new Vector3(-1.0f, 1.02f, 0f), 0f, premium: true);
            var cnt3 = Slot(); FloristModels.CafeCounter(root, cnt3, 0f, true); FloristModels.CoffeeMachine(root, cnt3 + new Vector3(-1.0f, 1.02f, 0f), 0f, broken: true);

            var ovenA = Slot(); FloristModels.CafeOven(root, ovenA, 0f);
            var ovenB = Slot(); FloristModels.CafeOven(root, ovenB, 0f, true);
            var caseA = Slot(); FloristModels.CafeCase(root, caseA, 0f);
            var caseB = Slot(); FloristModels.CafeCase(root, caseB, 0f, true);
            var fridgeA = Slot(); FloristModels.CafeFridge(root, fridgeA, 0f);
            var dishA = Slot(); FloristModels.CafeDishwasher(root, dishA, 0f);
            var fridgeB = Slot(); FloristModels.CafeFridge(root, fridgeB, 0f, true);
            var dishB = Slot(); FloristModels.CafeDishwasher(root, dishB, 0f, true);
            var beans = Slot(); FloristModels.BeanBox(root, beans, 0f);
            var milk = Slot(); FloristModels.SupplyCrate(root, milk, 0f, true);
            var eggs = Slot(); FloristModels.SupplyCrate(root, eggs, 0f, false);
            var van = Slot(); FloristModels.DeliveryVan(root, van, 150f);

            // The goods, at their carried size, and the farm's own milk and egg beside them.
            var goods = new[] { "coffee", "premium", "cake", "dirty_cup" };
            var goodAt = new Vector3[6];
            for (int i = 0; i < 4; i++) { goodAt[i] = Slot() + Vector3.up * 0.25f; FloristModels.CafeGood(goods[i], root, goodAt[i], 200f); }
            goodAt[4] = Slot() + Vector3.up * 0.25f; FloristModels.Good("milk", root, goodAt[4], 200f);
            goodAt[5] = Slot() + Vector3.up * 0.25f; FloristModels.Good("egg", root, goodAt[5], 200f);

            // People: the three new roles, each with what they carry.
            var peopleAt = Slot();
            Barista(root, peopleAt + right * -2.2f, 200f, "premium");
            Waiter(root, peopleAt + right * -0.75f, 190f, "dirty_cup");
            Baker(root, peopleAt + right * 0.75f, 180f, "cake");
            Customer(root, peopleAt + right * 2.2f, 160f, new Color(0.95f, 0.62f, 0.62f));

            // ------------------------------------------------------------------ render
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            int y = H - 520;
            void C(int x, int w, int h, Vector3 focus, float ortho) => FloristPreview.Cell(root, rt, new RectInt(x, y, w, h), focus, ortho);

            C(0, 400, 520, tableA + Vector3.up * 0.45f, 0.95f);
            C(400, 400, 520, tableB + Vector3.up * 0.45f, 0.95f);
            C(800, 400, 520, chairA + Vector3.up * 0.4f, 0.8f);
            C(1200, 400, 520, chairB + Vector3.up * 0.4f, 0.8f);
            C(1600, 400, 520, stoolA + Vector3.up * 0.25f, 0.55f);
            C(2000, 400, 520, setA + Vector3.up * 0.35f, 1.1f);
            y -= 520;
            C(0, 400, 520, gtA + Vector3.up * 0.45f, 0.85f);
            C(400, 400, 520, gtB + Vector3.up * 0.45f, 0.85f);
            C(800, 400, 520, gcA + Vector3.up * 0.4f, 0.8f);
            C(1200, 400, 520, gcB + Vector3.up * 0.4f, 0.8f);
            C(1600, 400, 520, gsA + new Vector3(0f, 0.25f, -0.2f), 1.3f);
            C(2000, 400, 520, gsB + new Vector3(0f, 0.25f, -0.2f), 1.3f);
            y -= 700;
            C(0, 800, 700, cnt1 + new Vector3(0f, 0.75f, 0f), 2.05f);
            C(800, 800, 700, cnt2 + new Vector3(0f, 0.75f, 0f), 2.05f);
            C(1600, 800, 700, cnt3 + new Vector3(0f, 0.75f, 0f), 2.05f);
            y -= 720;
            C(0, 400, 720, ovenA + Vector3.up * 0.8f, 1.5f);
            C(400, 400, 720, ovenB + Vector3.up * 0.8f, 1.5f);
            C(800, 500, 720, caseA + Vector3.up * 0.6f, 1.5f);
            C(1300, 500, 720, caseB + Vector3.up * 0.6f, 1.5f);
            C(1800, 300, 720, fridgeA + Vector3.up * 0.95f, 1.75f);
            C(2100, 300, 720, dishA + Vector3.up * 0.55f, 1.3f);
            y -= 560;
            C(0, 300, 560, fridgeB + Vector3.up * 0.95f, 1.55f);
            C(300, 300, 560, dishB + Vector3.up * 0.55f, 1.0f);
            C(600, 400, 560, beans + Vector3.up * 0.25f, 0.95f);
            C(1000, 350, 560, milk + Vector3.up * 0.3f, 0.8f);
            C(1350, 350, 560, eggs + Vector3.up * 0.3f, 0.8f);
            C(1700, 700, 560, van + Vector3.up * 0.8f, 2.0f);
            y -= 600;
            for (int i = 0; i < 6; i++) C(i * 400, 400, 600, goodAt[i], 0.52f);
            y -= 640;
            C(0, 2400, 640, peopleAt + Vector3.up * 0.85f, 1.05f);

            WritePng(rt, "cafe-art-3d.png");
            AssetDatabase.SaveAssets();
            Debug.Log("CAFEPREVIEW_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Tycoon/Export Cafe Scene")]
        public static void ExportScene()
        {
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(OutDir + "cafe-layout.json"));
            var root = FloristPreview.NewStage(out var right, out var fwd);
            var grubbyAt = fwd * 400f;
            var cleanAt = fwd * 400f + right * 120f;
            BuildCafe(root, grubbyAt, true, layout);
            BuildCafe(root, cleanAt, false, layout);

            const int W = 2400, H = 3300;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            float ortho = 12.9f;
            FloristPreview.Cell(root, rt, new RectInt(0, 1650, W, 1650), grubbyAt + new Vector3(0.5f, 0.2f, 0.5f), ortho);
            FloristPreview.Cell(root, rt, new RectInt(0, 0, W, 1650), cleanAt + new Vector3(0.5f, 0.2f, 0.5f), ortho);
            WritePng(rt, "cafe-scene-3d.png");
            AssetDatabase.SaveAssets();
            Debug.Log("CAFESCENE_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static void WritePng(RenderTexture rt, string file)
        {
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(OutDir);
            File.WriteAllBytes(OutDir + file, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            Debug.Log("[CafePreview] -> " + OutDir + file);
        }

        /// <summary>A table with its two chairs, the customer's (west, facing the table) and a spare, and a drink on it.</summary>
        private static void TableSet(Transform root, Vector3 at, bool garden, bool wrecked)
        {
            if (garden) { FloristModels.GardenTable(root, at, 0f, wrecked); }
            else FloristModels.CafeTable(root, at, 0f, wrecked);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var pos = at + new Vector3(side * 0.78f, 0f, 0f);
                float yaw = i == 0 ? -90f : 90f;
                if (wrecked && i == 1) yaw += 70f;
                var c = garden ? FloristModels.GardenChair(root, pos, yaw, wrecked) : FloristModels.CafeChair(root, pos, yaw, wrecked);
                if (wrecked && i == 0) { c.transform.localRotation = Quaternion.Euler(0f, -90f, 82f); c.transform.localPosition += new Vector3(-0.1f, 0.2f, 0f); }
            }
            if (!wrecked) Cup(root, at + new Vector3(0.06f, garden ? 0.725f : 0.76f, -0.08f), garden ? "premium" : "coffee", 0.3f, false);
        }

        /// <summary>A carried good put on a table or counter (scaled down; its pivot is the middle).</summary>
        private static GameObject Cup(Transform root, Vector3 pos, string id, float scale, bool raise = true)
        {
            var g = FloristModels.CafeGood(id, root, pos, 20f);
            g.transform.localScale = Vector3.one * (scale > 0.9f ? 0.3f : scale);
            g.transform.localPosition = pos + Vector3.up * (0.058f);
            return g;
        }

        // ------------------------------------------------------------------ people

        private static Transform Barista(Transform parent, Vector3 pos, float yaw, string carry)
        {
            var t = FloristPreview.Worker(parent, pos, yaw, CharacterLibrary.Cashier, "CafeBarista", new Dictionary<int, Color>
            {
                { CharacterLibrary.ShirtSlot, new Color(0.99f, 0.95f, 0.86f) },
                { CharacterLibrary.TrouserSlot, new Color(0.30f, 0.18f, 0.10f) },
                { CharacterLibrary.ShoeSlot, new Color(0.16f, 0.12f, 0.10f) },
                { 4, new Color(0.86f, 0.42f, 0.24f) },
                { 5, new Color(0.30f, 0.18f, 0.10f) },
            }, null);
            if (t != null && carry != null) FloristModels.CafeGood(carry, t, CharacterLibrary.HandAnchor).name = "Carried_" + carry;
            return t;
        }

        private static Transform Waiter(Transform parent, Vector3 pos, float yaw, string carry)
        {
            var t = FloristPreview.Worker(parent, pos, yaw, CharacterLibrary.Customer, "CafeWaiter", new Dictionary<int, Color>
            {
                { CharacterLibrary.ShirtSlot, new Color(0.97f, 0.97f, 0.98f) },
                { CharacterLibrary.TrouserSlot, new Color(0.14f, 0.13f, 0.16f) },
                { CharacterLibrary.ShoeSlot, new Color(0.10f, 0.09f, 0.10f) },
            }, null);
            if (t != null && carry != null) FloristModels.CafeGood(carry, t, CharacterLibrary.HandAnchor).name = "Carried_" + carry;
            return t;
        }

        private static Transform Baker(Transform parent, Vector3 pos, float yaw, string carry)
        {
            var t = FloristPreview.Worker(parent, pos, yaw, CharacterLibrary.Farmer, "CafeBaker", new Dictionary<int, Color>
            {
                { CharacterLibrary.ShirtSlot, new Color(0.99f, 0.97f, 0.92f) },
                { CharacterLibrary.TrouserSlot, new Color(0.22f, 0.26f, 0.40f) },
                { CharacterLibrary.ShoeSlot, new Color(0.96f, 0.96f, 0.98f) },
                { 4, new Color(0.99f, 0.97f, 0.92f) },
                { 5, new Color(0.86f, 0.42f, 0.24f) },
            }, null);
            if (t != null)
            {
                if (carry != null) FloristModels.CafeGood(carry, t, CharacterLibrary.HandAnchor).name = "Carried_" + carry;
                // The chef's hat (a proposal prop: phase 2 would hang it on the head bone).
                var white = M("Toque", new Color(0.99f, 0.99f, 1f), 0.3f);
                var hat = new GameObject("Toque").transform;
                hat.SetParent(t, false);
                hat.localPosition = new Vector3(0f, 1.57f, 0f);
                LevelBuildKit.Cylinder("Band", hat, new Vector3(0f, 0.0f, 0f), new Vector3(0.21f, 0.045f, 0.21f), white, false);
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(puff.GetComponent<Collider>());
                puff.transform.SetParent(hat, false);
                puff.transform.localPosition = new Vector3(0f, 0.11f, 0f);
                puff.transform.localScale = new Vector3(0.27f, 0.22f, 0.27f);
                puff.GetComponent<Renderer>().sharedMaterial = white;
            }
            return t;
        }

        private static Transform Customer(Transform parent, Vector3 pos, float yaw, Color shirt) =>
            FloristPreview.Worker(parent, pos, yaw, CharacterLibrary.Customer, "CafeCustomer" + Mathf.RoundToInt(shirt.r * 100f),
                new Dictionary<int, Color> { { CharacterLibrary.ShirtSlot, shirt } }, null);

        // ------------------------------------------------------------------ the whole café

        private static void BuildCafe(Transform stage, Vector3 at, bool derelict, Layout layout)
        {
            var g = new GameObject(derelict ? "CafeGrubby" : "CafeClean").transform;
            g.SetParent(stage, false);
            g.position = at;
            const float f = 0.06f;
            var pave = M("Pave", new Color(0.69f, 0.67f, 0.64f), 0.1f);
            var road = M("Road", new Color(0.36f, 0.36f, 0.39f), 0.1f);
            var line = M("RoadLine", new Color(0.88f, 0.88f, 0.8f), 0.1f);
            var floorA = derelict ? M("FloorDirtyA", new Color(0.36f, 0.31f, 0.27f), 0.1f) : M("FloorA", new Color(0.97f, 0.92f, 0.82f), 0.35f);
            var floorB = derelict ? M("FloorDirtyB", new Color(0.30f, 0.26f, 0.23f), 0.1f) : M("FloorB", new Color(0.88f, 0.70f, 0.52f), 0.35f);
            var kitA = derelict ? M("KitDirtyA", new Color(0.36f, 0.37f, 0.36f), 0.1f) : M("KitA", new Color(0.95f, 0.98f, 0.97f), 0.4f);
            var kitB = derelict ? M("KitDirtyB", new Color(0.30f, 0.32f, 0.31f), 0.1f) : M("KitB", new Color(0.72f, 0.88f, 0.84f), 0.4f);
            var wall = derelict ? M("WallDirty", new Color(0.55f, 0.49f, 0.42f), 0.1f) : M("Wall", new Color(0.99f, 0.96f, 0.90f), 0.4f);
            var cap = derelict ? M("CapDirty", new Color(0.43f, 0.38f, 0.33f), 0.1f) : M("Cap", new Color(0.86f, 0.42f, 0.24f), 0.4f);
            var matIn = M("MatIn", new Color(0.46f, 0.78f, 0.52f), 0.2f);
            var matOut = M("MatOut", new Color(0.98f, 0.66f, 0.30f), 0.2f);
            var matBack = M("MatBack", new Color(0.52f, 0.68f, 0.92f), 0.2f);

            LevelBuildKit.Box("Pave", g, At(34.7f, 6.0f) + new Vector3(0f, -0.01f, 0f), new Vector3(22.6f, 0.04f, 15.8f), pave, castShadow: false);
            LevelBuildKit.Box("Road", g, At(34.2f, -3.0f) + new Vector3(0f, -0.005f, 0f), new Vector3(26f, 0.04f, 2.8f), road, castShadow: false);
            for (float x = 22f; x < 46f; x += 2f)
                LevelBuildKit.Box("Line", g, At(x, -3.0f) + new Vector3(0f, 0.03f, 0f), new Vector3(0.9f, 0.04f, 0.14f), line, castShadow: false);

            for (int i = 0; i * 0.8f < SE - SW; i++)
                for (int j = 0; j * 0.8f < SN - SS; j++)
                {
                    float x0 = SW + i * 0.8f, z0 = SS + j * 0.8f;
                    float x1 = Mathf.Min(x0 + 0.8f, SE), z1 = Mathf.Min(z0 + 0.8f, SN);
                    bool kitchen = x0 >= 33f && z0 >= 6.8f;
                    LevelBuildKit.Box("Tile", g, At((x0 + x1) * 0.5f, (z0 + z1) * 0.5f) + new Vector3(0f, 0.03f, 0f),
                        new Vector3(x1 - x0, 0.06f, z1 - z0), (i + j) % 2 == 0 ? (kitchen ? kitA : floorA) : (kitchen ? kitB : floorB), castShadow: false);
                }

            const float wallH = 0.7f;
            void Wall(float x0, float z0, float x1, float z1)
            {
                var c = At((x0 + x1) * 0.5f, (z0 + z1) * 0.5f) + new Vector3(0f, wallH * 0.5f, 0f);
                var s = new Vector3(Mathf.Max(0.2f, x1 - x0), wallH, Mathf.Max(0.2f, z1 - z0));
                LevelBuildKit.Box("Wall", g, c, s, wall, castShadow: false);
                LevelBuildKit.Box("WallCap", g, c + Vector3.up * (wallH * 0.5f + 0.03f), new Vector3(s.x + 0.04f, 0.07f, s.z + 0.04f), cap, castShadow: false);
            }
            Wall(SW, SS, 27f, SS); Wall(29f, SS, 33f, SS); Wall(35f, SS, SE, SS);
            Wall(SW, SS, SW, SN); Wall(SW, SN, SE, SN);
            Wall(SE, SS, SE, 7.6f); Wall(SE, 9.6f, SE, SN);
            Wall(33f, 6.8f, 33f, 7.4f); Wall(33f, 9.6f, 33f, SN); Wall(33f, 6.8f, SE, 6.8f);
            LevelBuildKit.Box("MatIn", g, At(28f, SS - 0.0f) + new Vector3(0f, 0.07f, 0.1f), new Vector3(2.0f, 0.03f, 0.9f), matIn, castShadow: false);
            LevelBuildKit.Box("MatOut", g, At(34f, SS - 0.0f) + new Vector3(0f, 0.07f, 0.1f), new Vector3(2.0f, 0.03f, 0.9f), matOut, castShadow: false);
            LevelBuildKit.Box("MatBack", g, At(SE - 0.1f, 8.6f) + new Vector3(0f, 0.07f, 0f), new Vector3(0.9f, 0.03f, 2.0f), matBack, castShadow: false);

            // The street garden's low rail.
            var rail = M("Rail", derelict ? new Color(0.40f, 0.33f, 0.27f) : new Color(0.55f, 0.36f, 0.20f), 0.3f);
            void Rail(float x0, float z0, float x1, float z1)
            {
                var c = At((x0 + x1) * 0.5f, (z0 + z1) * 0.5f) + new Vector3(0f, 0.22f, 0f);
                LevelBuildKit.Box("Rail", g, c, new Vector3(Mathf.Max(0.05f, x1 - x0), 0.05f, Mathf.Max(0.05f, z1 - z0)), rail, castShadow: false);
                LevelBuildKit.Box("RailLow", g, c - Vector3.up * 0.12f, new Vector3(Mathf.Max(0.05f, x1 - x0), 0.03f, Mathf.Max(0.05f, z1 - z0)), rail, castShadow: false);
            }
            Rail(23.6f, -1.3f, 29.6f, -1.3f); Rail(32.6f, -1.3f, 41.4f, -1.3f); Rail(41.4f, -1.3f, 41.4f, 5.6f); Rail(23.6f, -1.3f, 23.6f, 1.9f);

            // Furniture, from the layout picture's numbers.
            foreach (var it in layout.items)
            {
                var pos = At(it.x, it.z) + new Vector3(0f, f - 0.02f, 0f);
                switch (it.kind)
                {
                    case "table": FloristModels.CafeTable(g, pos, 0f, derelict); break;
                    case "gtable": FloristModels.GardenTable(g, pos, 0f, derelict); break;
                    case "chair":
                    {
                        var c = FloristModels.CafeChair(g, pos, it.yaw, derelict);
                        if (derelict && (it.name.EndsWith("1a") || it.name.EndsWith("5b") || it.name.EndsWith("3a")))
                        { c.transform.localRotation = Quaternion.Euler(0f, it.yaw + 35f, 82f); c.transform.localPosition += new Vector3(0f, 0.2f, 0f); }
                        break;
                    }
                    case "gchair":
                    {
                        var c = FloristModels.GardenChair(g, pos, it.yaw, derelict);
                        if (derelict && it.name.EndsWith("2a")) { c.transform.localRotation = Quaternion.Euler(0f, it.yaw + 20f, 80f); c.transform.localPosition += new Vector3(0f, 0.2f, 0f); }
                        break;
                    }
                    case "counter": FloristModels.CafeCounter(g, pos, 0f, derelict); break;
                    case "machine": FloristModels.CoffeeMachine(g, pos + Vector3.up * 1.0f, 0f, premium: false, broken: derelict); break;
                    case "beans": if (!derelict) FloristModels.BeanBox(g, pos, 0f); break;
                    case "case": FloristModels.CafeCase(g, pos, 0f, derelict); break;
                    case "fridge": FloristModels.CafeFridge(g, pos, 0f, derelict); break;
                    case "oven": FloristModels.CafeOven(g, pos, 0f, derelict); break;
                    case "dishwasher": FloristModels.CafeDishwasher(g, pos, it.yaw, derelict); break;
                    case "crate_milk": if (!derelict) FloristModels.SupplyCrate(g, pos, it.yaw, true); break;
                    case "crate_egg": if (!derelict) FloristModels.SupplyCrate(g, pos, it.yaw, false); break;
                    case "van": if (!derelict) FloristModels.DeliveryVan(g, pos, it.yaw); break;
                    case "stool": FloristModels.CafeStool(g, pos, 0f); break;
                }
            }

            // Plants at the doors.
            foreach (var px in new[] { 26.4f, 29.6f, 32.4f, 35.6f })
            {
                var pot = LevelBuildKit.Cylinder("Pot", g, At(px, 1.6f) + new Vector3(0f, 0.2f, 0f), new Vector3(0.42f, 0.2f, 0.42f), M("Pot", derelict ? new Color(0.50f, 0.40f, 0.34f) : new Color(0.86f, 0.42f, 0.24f), 0.4f));
                for (int k = 0; k < (derelict ? 2 : 6); k++)
                    LevelBuildKit.Box("Leaf", g, At(px, 1.6f) + new Vector3(0f, 0.55f, 0f), new Vector3(0.06f, 0.5f, 0.06f),
                        M(derelict ? "LeafDry" : "Leaf", derelict ? new Color(0.50f, 0.44f, 0.24f) : new Color(0.30f, 0.62f, 0.22f), 0.3f), castShadow: false, rot: new Vector3(20f, k * 60f, 0f));
            }

            if (!derelict)
            {
                // Drinks and cake on the tables: coffee, premium, cake, a dirty cup left on T5, a coffee in the garden.
                foreach (var it in layout.items)
                {
                    if (it.kind != "table" && it.kind != "gtable") continue;
                    var tp = At(it.x, it.z) + new Vector3(0.06f, f + (it.kind == "gtable" ? 0.725f : 0.76f), -0.08f);
                    string good = it.name.EndsWith("T1") ? "coffee" : it.name.EndsWith("T2") ? "premium" : it.name.EndsWith("T3") ? "cake" :
                        it.name.EndsWith("T5") ? "dirty_cup" : it.name.EndsWith("G1") ? "coffee" : it.name.EndsWith("G2") ? "premium" : null;
                    if (good == null) continue;
                    var go = FloristModels.CafeGood(good, g, tp + Vector3.up * 0.058f, 20f);
                    go.transform.localScale = Vector3.one * 0.3f;
                }
                // The staff and a few customers.
                Barista(g, At(27.2f, 10.35f) + Vector3.up * f, 0f, "premium");
                Waiter(g, At(27.55f, 6.7f) + Vector3.up * f, 90f, "dirty_cup");
                Baker(g, At(36.6f, 10.2f) + Vector3.up * f, 0f, "cake");
                Customer(g, At(30.4f, 0.4f) + Vector3.up * f, 180f, new Color(0.95f, 0.62f, 0.62f));
                Customer(g, At(31.7f, 0.4f) + Vector3.up * f, 180f, new Color(0.98f, 0.82f, 0.40f));
                Customer(g, At(25.4f, 3.3f) + Vector3.up * f, 90f, new Color(0.62f, 0.78f, 0.95f));
            }
            else
            {
                BuildLitter(g, At(26.4f, 6.7f), 11);
                BuildLitter(g, At(31.0f, 6.7f), 23);
                BuildLitter(g, At(35.6f, 5.6f), 37);
                var dirt = M("Dirt", new Color(0.22f, 0.20f, 0.17f), 0.05f);
                var rnd = new System.Random(77);
                for (int i = 0; i < 18; i++)
                {
                    float x = SW + 0.8f + (float)rnd.NextDouble() * (SE - SW - 1.6f), z = SS + 0.8f + (float)rnd.NextDouble() * (SN - SS - 1.6f);
                    var o = LevelBuildKit.Cylinder("Dirt", g, At(x, z) + new Vector3(0f, 0.065f, 0f),
                        new Vector3(0.8f + (float)rnd.NextDouble() * 1.3f, 0.01f, 0.6f + (float)rnd.NextDouble() * 0.9f), dirt, false);
                    o.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
                }
                // Cups and cake gone off on a few tables.
                foreach (var it in layout.items)
                    if (it.kind == "table" && (it.name.EndsWith("T2") || it.name.EndsWith("T4")))
                    {
                        var go = FloristModels.CafeGood("dirty_cup", g, At(it.x, it.z) + new Vector3(0.0f, f + 0.76f + 0.058f, 0f), 70f);
                        go.transform.localScale = Vector3.one * 0.3f;
                    }
            }
        }

        /// <summary>A heap of rubbish in the café's own mix: bin bags, cardboard, broken crockery, bean sacks and cups.</summary>
        private static void BuildLitter(Transform parent, Vector3 position, int seed)
        {
            var pile = new GameObject("Litter").transform;
            pile.SetParent(parent, false);
            pile.localPosition = position;
            var rnd = new System.Random(seed);
            var mats = new[]
            {
                M("LBag", new Color(0.18f, 0.20f, 0.19f), 0.3f), M("LCard", new Color(0.62f, 0.49f, 0.32f), 0.1f),
                M("LSack", new Color(0.66f, 0.58f, 0.40f), 0.1f), M("LCup", new Color(0.78f, 0.76f, 0.70f), 0.3f),
            };
            for (int i = 0; i < 11; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * 1.5f, z = ((float)rnd.NextDouble() - 0.5f) * 1.2f;
                float size = 0.22f + (float)rnd.NextDouble() * 0.34f, yaw = (float)rnd.NextDouble() * 180f;
                if (i % 4 == 3)
                {
                    var cup = FloristModels.CafeGood("dirty_cup", pile, new Vector3(x, 0.1f, z), yaw);
                    cup.transform.localScale = Vector3.one * 0.35f;
                    continue;
                }
                LevelBuildKit.Box("Litter", pile, new Vector3(x, size * 0.5f, z), new Vector3(size, size * 0.8f, size * 1.1f),
                    mats[i % mats.Length], castShadow: false, rot: new Vector3(0f, yaw, 0f));
            }
        }
    }
}
