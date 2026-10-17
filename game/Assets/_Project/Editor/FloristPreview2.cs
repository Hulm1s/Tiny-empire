using System.Collections.Generic;
using System.IO;
using Tycoon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Fl = Tycoon.EditorTools.FloristModels.Fl;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// PHASE 1 of "Florist round 2": renders the new art to PNG sheets for Filip to approve BEFORE any
    /// of it is wired into the game. Four sheets, each labelled in the picture:
    /// icons (seed, water drop, ready), the seed box and the bed growth stages with their bubbles,
    /// the derelict shop next to the painted one, and the wall patterns on the florist's low walls.
    /// Nothing here touches the built scene: it runs in an empty scene, and the only assets it writes
    /// are preview meshes under Meshes/Florist/Preview and the shared florist art it always rewrites.
    /// Run: Tycoon > Export Florist Round 2 Preview, or
    /// <c>-executeMethod Tycoon.EditorTools.FloristPreview2.ExportAll</c> (needs a GPU: no -nographics).
    /// </summary>
    public static class FloristPreview2
    {
        private const string OutDir = "C:/Users/ADMINI~1/AppData/Local/Temp/claude/D--iosGame/b50262dc-8611-42da-8650-85247e0c766c/scratchpad/florist2-preview/";

        private static readonly Color Grass = new Color(0.42f, 0.66f, 0.30f);
        private static readonly Quaternion GameRot = Quaternion.Euler(50f, 45f, 0f);
        private static Font _font;

        // ------------------------------------------------------------------ entry points

        [MenuItem("Tycoon/Export Florist Round 2 Preview")]
        public static void ExportAll()
        {
            Directory.CreateDirectory(OutDir);
            try
            {
                Icons();
                Beds();
                Shop();
                Patterns();
                AssetDatabase.SaveAssets();
                Debug.Log("FLORIST2PREVIEW_OK");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[FloristPreview2] " + e);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static void ExportIcons() { Directory.CreateDirectory(OutDir); Icons(); Done(); }
        public static void ExportBeds() { Directory.CreateDirectory(OutDir); Beds(); Done(); }
        public static void ExportShop() { Directory.CreateDirectory(OutDir); Shop(); Done(); }
        public static void ExportPatterns() { Directory.CreateDirectory(OutDir); Patterns(); Done(); }

        private static void Done()
        {
            AssetDatabase.SaveAssets();
            Debug.Log("FLORIST2PREVIEW_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // ------------------------------------------------------------------ common

        private static Transform Stage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Florist2Preview").transform;
            var grass = LevelBuildKit.Mat("Cash_PreviewGrass", Grass, 0.05f);
            LevelBuildKit.Box("Ground", root, new Vector3(0f, -0.05f, 0f), new Vector3(8000f, 0.1f, 8000f), grass);
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return root;
        }

        private static Material Rm(Color c, float smooth = 0.2f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            return m;
        }

        private static RenderTexture NewRt(int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            return rt;
        }

        private static void Save(RenderTexture rt, string file)
        {
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(OutDir + file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[FloristPreview2] -> {OutDir}{file}");
        }

        /// <summary>Renders the scene through a camera into a rectangle of the target, with a caption.</summary>
        private static void Cell(Transform root, RenderTexture rt, RectInt px, Vector3 focus, float ortho,
            string label = null, Quaternion? rot = null, Color? bg = null, float labelPx = 26f)
        {
            var q = rot ?? GameRot;
            var go = new GameObject("Cam");
            go.transform.SetParent(root, false);
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = ortho;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = bg ?? Grass;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 90f;
            go.transform.rotation = q;
            go.transform.position = focus - q * Vector3.forward * 40f;
            go.tag = "MainCamera";
            cam.targetTexture = rt;
            cam.rect = new Rect(px.x / (float)rt.width, px.y / (float)rt.height, px.width / (float)rt.width, px.height / (float)rt.height);

            var labels = new List<GameObject>();
            if (!string.IsNullOrEmpty(label))
            {
                float aspect = px.width / (float)px.height;
                float worldPerPx = ortho * 2f / px.height;
                var origin = focus - q * Vector3.forward * 20f
                    + q * Vector3.right * (-ortho * aspect + 10f * worldPerPx)
                    + q * Vector3.up * (-ortho + 8f * worldPerPx);
                labels.Add(Label(label, q, origin + q * (new Vector3(1.5f, -1.5f, 0.5f) * worldPerPx), worldPerPx * labelPx, new Color(0f, 0f, 0f, 0.9f)));
                labels.Add(Label(label, q, origin, worldPerPx * labelPx, Color.white));
            }

            cam.Render();
            cam.targetTexture = null;
            foreach (var l in labels) Object.DestroyImmediate(l);
            Object.DestroyImmediate(go);
        }

        private static GameObject Label(string text, Quaternion rot, Vector3 pos, float height, Color col)
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label");
            var tm = go.AddComponent<TextMesh>();
            tm.font = _font;
            tm.text = text;
            tm.fontSize = 80;
            tm.characterSize = height / 8f;     // ~0.1 of the font size is the line height in world units
            tm.anchor = TextAnchor.LowerLeft;
            tm.color = col;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
            go.transform.rotation = rot;
            go.transform.position = pos;
            return go;
        }

        /// <summary>A permanent caption in a flat (unrotated) scene, top-left at <paramref name="pos"/>, height in pixels.</summary>
        private static void Caption(string text, Vector3 pos, float px)
        {
            float h = px / 100f;
            Label(text, Quaternion.identity, pos + new Vector3(0.015f, -0.015f, -0.5f) + Vector3.down * h, h, new Color(0f, 0f, 0f, 0.9f));
            Label(text, Quaternion.identity, pos + Vector3.down * h + new Vector3(0f, 0f, -1f), h, Color.white);
        }

        private static GameObject Spr(Transform parent, Sprite sprite, Vector3 pos, Quaternion rot, Vector2 size, Color col, int order, float zRot = 0f)
        {
            var go = new GameObject("Spr_" + sprite.name);
            go.transform.SetParent(parent, false);
            go.transform.rotation = rot * Quaternion.Euler(0f, 0f, zRot);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = col;
            sr.sortingOrder = order;
            var b = sprite.bounds.size;
            go.transform.localScale = new Vector3(size.x / b.x, size.y / b.y, 1f);
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>The world bubble: dark rim, white panel, tail, icon. <paramref name="width"/> in metres.</summary>
        private static void Bubble(Transform parent, Vector3 centre, Quaternion rot, Sprite icon, float width, int order = 10)
        {
            var rim = new Color(0.16f, 0.18f, 0.22f, 0.92f);
            var white = new Color(1f, 1f, 1f, 0.97f);
            float h = width * 0.88f;
            Vector3 At(float dx, float dy, float z = 0f) => centre + rot * new Vector3(dx, dy, -z);
            Spr(parent, UIFactory.RoundedBox, At(0f, -h * 0.5f - width * 0.02f, 0.01f), rot, new Vector2(width * 0.19f, width * 0.19f), rim, order, 45f);
            Spr(parent, UIFactory.RoundedBox, At(0f, -h * 0.5f + width * 0.03f, 0.011f), rot, new Vector2(width * 0.14f, width * 0.14f), white, order + 1, 45f);
            Spr(parent, UIFactory.RoundedBox, At(0f, 0f, 0.02f), rot, new Vector2(width, h), rim, order + 2);
            Spr(parent, UIFactory.RoundedBox, At(0f, 0f, 0.03f), rot, new Vector2(width * 0.94f, h * 0.92f), white, order + 3);
            Spr(parent, icon, At(0f, h * 0.01f, 0.04f), rot, new Vector2(width * 0.76f, width * 0.76f), Color.white, order + 4);
        }

        // ================================================================== 1. icons

        private static void Icons()
        {
            var root = Stage();
            const int W = 1800, H = 1500;
            var rt = NewRt(W, H);
            var bg = new Color(0.30f, 0.32f, 0.35f);
            var flat = Quaternion.identity;

            // Coordinates in sheet pixels, y down from the top.
            Vector3 P(float x, float y) => new Vector3(x / 100f, (H - y) / 100f, 0f);

            var seed = IconFactory.GardenIcon("seed");
            var drop = IconFactory.GardenIcon("drop");
            var ready = IconFactory.GardenIcon("ready");
            var basket = IconFactory.GardenIcon("ready_basket");
            var news = new[] { seed, drop, ready, basket };
            string[] newNames = { "SEED", "WATER DROP", "READY (BLOOM)", "READY (BASKET)" };

            var dark = new Color(0.47f, 0.49f, 0.52f);
            var wash = new Color(0.80f, 0.90f, 0.78f);     // the pale wash of an interaction square
            var tileSpr = UIFactory.RoundedBox;

            // Row 1: the new icons large, on the dark and on the pale wash.
            for (int i = 0; i < 4; i++)
            {
                float cx = 230f + i * 440f;
                Spr(root, tileSpr, P(cx, 215f), flat, new Vector2(3.6f, 3.6f), dark, 0);
                Spr(root, news[i], P(cx, 215f), flat, new Vector2(3.0f, 3.0f), Color.white, 1);
            }
            // Row 2: pale wash, native size and "phone" size.
            for (int i = 0; i < 4; i++)
            {
                float cx = 230f + i * 440f;
                Spr(root, tileSpr, P(cx - 110f, 560f), flat, new Vector2(2.0f, 2.0f), wash, 0);
                Spr(root, news[i], P(cx - 110f, 560f), flat, new Vector2(1.5f, 1.5f), Color.white, 1);
                Spr(root, tileSpr, P(cx + 70f, 560f), flat, new Vector2(1.0f, 1.0f), wash, 0);
                Spr(root, news[i], P(cx + 70f, 560f), flat, new Vector2(0.64f, 0.64f), Color.white, 1);
                Spr(root, tileSpr, P(cx + 170f, 560f), flat, new Vector2(0.62f, 0.62f), dark, 0);
                Spr(root, news[i], P(cx + 170f, 560f), flat, new Vector2(0.40f, 0.40f), Color.white, 1);
            }

            // Row 3: existing icons, for style comparison.
            var existing = new List<Sprite>();
            foreach (var id in new[] { "watering_can", "rose", "sunflower", "hydrangea", "bouquet" })
            {
                var it = ScriptableObject.CreateInstance<Tycoon.Config.ItemDefinition>();
                it.id = id;
                existing.Add(IconFactory.For(it));
                Object.DestroyImmediate(it);
            }
            existing.Add(IconFactory.Action(SquareIcon.Harvest));
            for (int i = 0; i < existing.Count; i++)
            {
                float cx = 160f + i * 290f;
                Spr(root, tileSpr, P(cx, 930f), flat, new Vector2(2.4f, 2.4f), dark, 0);
                Spr(root, existing[i], P(cx, 930f), flat, new Vector2(1.92f, 1.92f), Color.white, 1);
            }

            // Row 4: the bubbles over a bed, at phone-ish size.
            for (int i = 0; i < 4; i++)
            {
                float cx = 230f + i * 440f;
                Bubble(root, P(cx, 1270f), flat, news[i], 2.3f);
            }

            // Captions: persistent labels in the flat scene.
            for (int i = 0; i < 4; i++)
            {
                float cx = 230f + i * 440f;
                Caption(newNames[i], P(cx - 190f, 425f), 34f);
                Caption("pale square: 150 / 64 px", P(cx - 190f, 665f), 22f);
                Caption("dark: 40 px", P(cx + 120f, 665f), 22f);
            }
            Caption("EXISTING ICONS, FOR COMPARISON OF STYLE", P(40f, 790f), 30f);
            Caption("WORLD BUBBLE OVER A BED (SEED / WATER / READY / READY)", P(40f, 1110f), 30f);
            Caption("NEW GARDEN ICONS", P(40f, 20f), 36f);
            Cell(root, rt, new RectInt(0, 0, W, H), P(W * 0.5f, H * 0.5f), H / 200f, null, flat, bg);

            Save(rt, "1-icons.png");
            Object.DestroyImmediate(rt);
        }

        // ================================================================== 2. seed box and beds

        private static void Beds()
        {
            var root = Stage();
            const int W = 2400;
            int rowSeed = 520, rowBubble = 470, rowFlower = 360, rowGame = 360;
            int H = rowSeed + rowBubble + 4 * rowFlower + rowGame;
            var rt = NewRt(W, H);
            var right = GameRot * Vector3.right;
            var fwd = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;
            int slot = 0;
            Vector3 Slot() => new Vector3(0f, 0f, 0f) + right * (60f * slot++);
            int top = H;

            // ---- row 1: the seed box -------------------------------------------------------
            var boxAt = Slot();
            FloristModels.SeedBox(root, boxAt);

            var wellAt = Slot();
            FloristModels.Well(root, wellAt);
            var boxBeside = wellAt + right * 3.2f + fwd * -0.6f;
            FloristModels.SeedBox(root, boxBeside, 0f);
            var gardener = FloristPreview.Worker(root, wellAt + right * 1.7f + fwd * -2.0f, 200f, CharacterLibrary.Farmer, "Gardener",
                new Dictionary<int, Color>
                {
                    { CharacterLibrary.ShirtSlot, new Color(0.46f, 0.78f, 0.42f) },
                    { CharacterLibrary.TrouserSlot, new Color(0.30f, 0.46f, 0.74f) },
                    { CharacterLibrary.ShoeSlot, new Color(0.40f, 0.28f, 0.18f) }
                }, null);
            if (gardener != null) FloristModels.SeedPouch(gardener, CharacterLibrary.HandAnchor, 0f).name = "Carried_seed";

            var pouchAt = Slot();
            FloristModels.SeedPouch(root, pouchAt + Vector3.up * 0.24f, 20f);
            for (int k = 0; k < 3; k++)
                FloristModels.SeedPouch(root, pouchAt + right * 0.9f + Vector3.up * (0.24f + k * 0.48f), 25f * k);
            FloristModels.Good("watering_can", root, pouchAt + right * 2.0f + Vector3.up * 0.26f, 20f);

            Cell(root, rt, new RectInt(0, top - rowSeed, 700, rowSeed), boxAt + Vector3.up * 0.55f, 1.35f, "SEED BOX (FREE)");
            Cell(root, rt, new RectInt(700, top - rowSeed, 1100, rowSeed), wellAt + right * 1.6f + Vector3.up * 0.9f, 2.7f, "SEED BOX BESIDE THE WELL, GARDENER CARRYING A SEED POUCH");
            Cell(root, rt, new RectInt(1800, top - rowSeed, 600, rowSeed), pouchAt + right * 1.1f + Vector3.up * 0.5f, 1.35f, "CARRIED POUCH, STACK, WATERING CAN");

            // ---- row 2: bubbles over a bed -------------------------------------------------
            var seed = IconFactory.GardenIcon("seed");
            var drop = IconFactory.GardenIcon("drop");
            var ready = IconFactory.GardenIcon("ready");
            var bubbleCells = new[]
            {
                (stage: 0, wet: false, icon: seed, text: "EMPTY: SEED BUBBLE"),
                (stage: 1, wet: false, icon: drop, text: "PLANTED, DRY: WATER BUBBLE"),
                (stage: 2, wet: false, icon: drop, text: "HALF-GROWN, DRY: WATER BUBBLE"),
                (stage: 3, wet: false, icon: ready, text: "IN BLOOM: READY BUBBLE"),
            };
            var bubbleAt = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                bubbleAt[i] = Slot();
                var c = bubbleCells[i];
                FloristModels.BedStage(Fl.Tulip, c.stage, c.wet, root, bubbleAt[i]);
                Bubble(root, bubbleAt[i] + Vector3.up * 1.55f, GameRot, c.icon, 0.95f);
            }
            for (int i = 0; i < 4; i++)
                Cell(root, rt, new RectInt(i * 600, top - rowSeed - rowBubble, 600, rowBubble), bubbleAt[i] + Vector3.up * 0.85f, 1.9f, bubbleCells[i].text, null, null, 22f);

            // ---- rows 3-6: every flower, six looks ------------------------------------------
            string[] names = { "ROSE", "TULIP", "SUNFLOWER", "HYDRANGEA" };
            var looks = new[]
            {
                (stage: 0, wet: false, name: "1 EMPTY"),
                (stage: 1, wet: false, name: "2 SEEDLINGS (DRY)"),
                (stage: 1, wet: true, name: "2 SEEDLINGS (WATERED)"),
                (stage: 2, wet: false, name: "3 HALF-GROWN (DRY)"),
                (stage: 2, wet: true, name: "3 HALF-GROWN (WATERED)"),
                (stage: 3, wet: false, name: "4 FULL BLOOM = READY"),
            };
            for (int f = 0; f < 4; f++)
                for (int s = 0; s < 6; s++)
                {
                    var at = Slot();
                    FloristModels.BedStage((Fl)f, looks[s].stage, looks[s].wet, root, at);
                    Cell(root, rt, new RectInt(s * 400, top - rowSeed - rowBubble - rowFlower * (f + 1), 400, rowFlower),
                        at + Vector3.up * 0.30f, 1.55f, names[f] + " " + looks[s].name, null, null, 17f);
                }

            // ---- last row: game scale (64 px per metre) -----------------------------------
            var gameAt = Slot();
            var icons = new[] { seed, drop, null, drop, null, ready };
            for (int s = 0; s < 6; s++)
            {
                var at = gameAt + right * (-8.5f + s * 3.4f);
                FloristModels.BedStage(Fl.Rose, looks[s].stage, looks[s].wet, root, at);
                if (icons[s] != null) Bubble(root, at + Vector3.up * 1.55f, GameRot, icons[s], 0.95f);
            }
            Cell(root, rt, new RectInt(0, 0, W, rowGame), gameAt + Vector3.up * 0.9f, rowGame / (2f * 64f), "SAME SIX BEDS AT IN-GAME SCALE (64 PX PER METRE)", null, null, 20f);

            Save(rt, "2-seedbox-and-beds.png");
            Object.DestroyImmediate(rt);
        }

        // ================================================================== 3. the derelict shop

        private static void Shop()
        {
            var root = Stage();
            const int W = 2800, rowShop = 1100, rowDetail = 460;
            int H = rowShop + rowDetail;
            var rt = NewRt(W, H);
            var right = GameRot * Vector3.right;
            var fwd = Quaternion.Euler(0f, 45f, 0f) * Vector3.forward;

            var wallTex = PatternFromFlowers();
            var floorTex = PatternFactory.NewTexture("PrevFloor");
            PatternFactory.PaintFloor(PatternFactory.FloorWood, PatternFactory.WoodColor(0.30f), floorTex);

            var grubAt = new Vector3(0f, 0f, 0f);
            var cleanAt = right * 60f;
            BuildShell(root, grubAt, true, null, null);
            BuildShell(root, cleanAt, false, wallTex, floorTex);

            // Close-ups: broken vs. repaired counter and stand, a litter heap, the dead flowers.
            var d1 = right * 120f;
            FloristModels.BrokenCounter(root, d1);
            var d2 = right * 180f;
            FloristModels.Counter(root, d2);
            var d3 = right * 240f;
            FloristModels.BrokenWallStand(root, d3);
            var d4 = right * 300f;
            FloristModels.WallStand(root, d4);
            var d5 = right * 360f;
            Litter(root, d5, 3);
            FloristModels.DeadFlowers(root, d5 + right * 1.2f + fwd * -1.1f, 40f, 1.2f);

            // Whole shops at 84 px per metre.
            float ortho = rowShop / (2f * 84f);
            var focusUp = new Vector3(0f, 0.4f, 0f);
            Cell(root, rt, new RectInt(0, rowDetail, 1400, rowShop), grubAt + focusUp + fwd * 0.3f, ortho, "BEFORE: THE SHOP WHEN THE TOWN OPENS (DERELICT)");
            Cell(root, rt, new RectInt(1400, rowDetail, 1400, rowShop), cleanAt + focusUp + fwd * 0.3f, ortho, "AFTER: REPAIRED, PAINTED (FLOWERS WALLPAPER) AND OPEN");

            int cw = W / 5;
            Cell(root, rt, new RectInt(0, 0, cw, rowDetail), d1 + Vector3.up * 0.55f, 1.95f, "BROKEN COUNTER", null, null, 20f);
            Cell(root, rt, new RectInt(cw, 0, cw, rowDetail), d2 + Vector3.up * 0.55f, 1.95f, "REPAIRED COUNTER", null, null, 20f);
            Cell(root, rt, new RectInt(cw * 2, 0, cw, rowDetail), d3 + Vector3.up * 0.55f, 1.8f, "BROKEN STAND", null, null, 20f);
            Cell(root, rt, new RectInt(cw * 3, 0, cw, rowDetail), d4 + Vector3.up * 0.55f, 1.8f, "REPAIRED STAND", null, null, 20f);
            Cell(root, rt, new RectInt(cw * 4, 0, cw, rowDetail), d5 + right * 0.5f + Vector3.up * 0.4f, 1.6f, "LITTER HEAP + DEAD FLOWERS", null, null, 20f);

            Save(rt, "3-grubby-florist.png");
            Object.DestroyImmediate(rt);
        }

        private static Texture2D PatternFromFlowers()
        {
            var tex = PatternFactory.NewTexture("PrevWall");
            var px = new Color32[PatternFactory.Size * PatternFactory.Size];
            PatternFactory.RecolorWall(PatternFactory.Flowers, new Color(1.00f, 0.95f, 0.86f),
                new Color(0.98f, 0.56f, 0.70f), new Color(0.45f, 0.72f, 0.40f), px);
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        private static readonly Color DirtyWall = new Color(0.52f, 0.47f, 0.41f);
        private static readonly Color DirtyFloor = new Color(0.33f, 0.31f, 0.28f);

        private static void SetTex(Renderer r, Texture2D tex, Vector4 st)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetTexture("_BaseMap", tex);
            mpb.SetVector("_BaseMap_ST", st);
            mpb.SetColor("_BaseColor", Color.white);
            r.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// The shop at the florist's real size (12 x 9 m, 0.7 m walls with caps, three doorways), either
        /// derelict (dirty coats, broken fittings, litter, weeds) or repaired, painted and open.
        /// </summary>
        private static void BuildShell(Transform root, Vector3 at, bool grubby, Texture2D wallTex, Texture2D floorTex)
        {
            var g = new GameObject(grubby ? "Grubby" : "Clean").transform;
            g.SetParent(root, false);
            g.position = at;

            var floorMat = grubby ? Rm(DirtyFloor, 0.1f) : Rm(Color.white, 0.25f);
            var wallMat = grubby ? Rm(DirtyWall, 0.1f) : Rm(Color.white, 0.35f);
            var capMat = grubby ? Rm(new Color(0.45f, 0.40f, 0.36f), 0.1f) : Rm(new Color(1.00f, 0.74f, 0.82f), 0.4f);
            var meadow = Rm(new Color(0.50f, 0.76f, 0.34f), 0.05f);
            var path = Rm(new Color(0.88f, 0.84f, 0.76f), 0.3f);

            var floor = LevelBuildKit.Box("ShopFloor", g, new Vector3(0f, 0.03f, 0f), new Vector3(12.2f, 0.06f, 9.2f), floorMat, castShadow: false);
            if (!grubby) SetTex(floor.GetComponent<Renderer>(), floorTex, new Vector4(12.2f / PatternFactory.TileMeters, 9.2f / PatternFactory.TileMeters, 0f, 0f));
            LevelBuildKit.Box("MeadowGround", g, new Vector3(0f, 0.02f, 6.9f), new Vector3(12.2f, 0.04f, 4.8f), meadow, castShadow: false);

            void Wall(float x0, float x1, float z0, float z1)
            {
                var c = new Vector3((x0 + x1) * 0.5f, 0.35f, (z0 + z1) * 0.5f);
                var s = new Vector3(Mathf.Max(0.2f, x1 - x0), 0.7f, Mathf.Max(0.2f, z1 - z0));
                var w = LevelBuildKit.Box("Wall", g, c, s, wallMat, castShadow: false);
                if (!grubby)
                {
                    bool alongX = s.x >= s.z;
                    float len = alongX ? s.x : s.z;
                    SetTex(w.GetComponent<Renderer>(), wallTex, new Vector4(len / PatternFactory.TileMeters, 0.7f / PatternFactory.TileMeters,
                        (alongX ? c.x - s.x * 0.5f : c.z - s.z * 0.5f) / PatternFactory.TileMeters, 0f));
                }
                LevelBuildKit.Box("WallCap", g, c + Vector3.up * 0.38f, new Vector3(s.x + 0.04f, 0.07f, s.z + 0.04f), capMat, castShadow: false);
            }
            Wall(-6.1f, -4f, -4.6f, -4.4f);
            Wall(-2f, 2f, -4.6f, -4.4f);
            Wall(4f, 6.1f, -4.6f, -4.4f);
            Wall(-6.1f, -5.9f, -4.5f, 4.5f);
            Wall(5.9f, 6.1f, -4.5f, 4.5f);
            Wall(-6.1f, 0.1f, 4.4f, 4.6f);
            Wall(1.9f, 6.1f, 4.4f, 4.6f);

            // Meadow fence on its three sides (posts and a rail), and the stepping stones.
            var post = Rm(new Color(0.72f, 0.58f, 0.40f), 0.2f);
            for (float z = 4.6f; z <= 9.3f; z += 1.2f)
                foreach (float sx in new[] { -6f, 6f })
                    LevelBuildKit.Box("Post", g, new Vector3(sx, 0.35f, z), new Vector3(0.14f, 0.7f, 0.14f), post, castShadow: false);
            for (float x = -6f; x <= 6.01f; x += 1.2f)
                LevelBuildKit.Box("PostN", g, new Vector3(x, 0.35f, 9.3f), new Vector3(0.14f, 0.7f, 0.14f), post, castShadow: false);
            for (int i = 0; i < 3; i++)
                LevelBuildKit.Cylinder("Stone", g, new Vector3(1.0f - i * 0.35f, 0.045f, 5.2f + i * 0.6f), new Vector3(0.7f, 0.02f, 0.5f), path, false);

            if (grubby) FillGrubby(g);
            else FillClean(g);
        }

        private static void FillClean(Transform g)
        {
            var matIn = Rm(new Color(0.46f, 0.78f, 0.52f), 0.2f);
            var matOut = Rm(new Color(0.98f, 0.66f, 0.30f), 0.2f);
            LevelBuildKit.Box("DoorMatIn", g, new Vector3(-3f, 0.07f, -3.9f), new Vector3(1.6f, 0.03f, 0.9f), matIn, castShadow: false);
            LevelBuildKit.Box("DoorMatOut", g, new Vector3(3f, 0.07f, -3.9f), new Vector3(1.6f, 0.03f, 0.9f), matOut, castShadow: false);
            LevelBuildKit.Box("BackDoorMat", g, new Vector3(1.0f, 0.07f, 4.0f), new Vector3(1.5f, 0.03f, 0.7f), matOut, castShadow: false);

            FloristModels.Counter(g, new Vector3(0f, 0.06f, 0.7f), 0f);
            FloristModels.CentreStand(g, new Vector3(0f, 0.06f, -2.5f), 0f);
            FloristModels.WallStand(g, new Vector3(-5.3f, 0.06f, -0.3f), -90f);
            FloristModels.WallStand(g, new Vector3(5.3f, 0.06f, -0.3f), 90f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var front = new Vector3(-sx * 0.7071f, 0f, -sz * 0.7071f);
                    var pos = new Vector3(sx * 6f, 0.06f, sz * 4.5f) + front * 1.4f;
                    FloristModels.CornerStand(g, pos, Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg);
                }
            FloristPreview.Worker(g, new Vector3(0f, 0.06f, 2.3f), 180f, CharacterLibrary.Cashier, "Florist",
                new Dictionary<int, Color>
                {
                    { CharacterLibrary.ShirtSlot, new Color(1.00f, 0.62f, 0.74f) },
                    { CharacterLibrary.TrouserSlot, new Color(0.97f, 0.92f, 0.82f) },
                    { CharacterLibrary.ShoeSlot, new Color(0.55f, 0.36f, 0.30f) },
                    { 4, new Color(1.00f, 0.80f, 0.86f) },
                    { 5, new Color(1.00f, 0.80f, 0.86f) }
                }, "bouquet");
        }

        private static void Litter(Transform parent, Vector3 pos, int seed)
        {
            var pile = new GameObject("TrashPile").transform;
            pile.SetParent(parent, false);
            pile.position = pos;
            var rnd = new System.Random(seed * 13 + 5);
            var mats = new[]
            {
                Rm(new Color(0.18f, 0.20f, 0.19f), 0.3f), Rm(new Color(0.62f, 0.49f, 0.32f), 0.1f),
                Rm(new Color(0.30f, 0.50f, 0.38f), 0.4f), Rm(new Color(0.80f, 0.78f, 0.70f), 0.1f),
            };
            for (int i = 0; i < 9; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * 1.3f, z = ((float)rnd.NextDouble() - 0.5f) * 1.1f;
                float size = 0.25f + (float)rnd.NextDouble() * 0.35f, yaw = (float)rnd.NextDouble() * 180f;
                LevelBuildKit.Box($"Litter_{i}", pile, new Vector3(x, size * 0.5f, z), new Vector3(size, size * 0.8f, size * 1.1f),
                    mats[i % mats.Length], castShadow: false, rot: new Vector3(0f, yaw, 0f));
            }
        }

        private static void FillGrubby(Transform g)
        {
            var dirt = Rm(new Color(0.24f, 0.21f, 0.18f), 0.05f);
            var stain = Rm(new Color(0.40f, 0.36f, 0.30f), 0.05f);
            var weed = Rm(new Color(0.40f, 0.52f, 0.22f), 0.1f);
            var rnd = new System.Random(77);

            // Dirt patches on the floor, damp stains up the walls.
            for (int i = 0; i < 14; i++)
            {
                float x = ((float)rnd.NextDouble() - 0.5f) * 10.5f, z = ((float)rnd.NextDouble() - 0.5f) * 7.8f;
                var o = LevelBuildKit.Cylinder("Dirt", g, new Vector3(x, 0.065f, z),
                    new Vector3(0.8f + (float)rnd.NextDouble() * 1.3f, 0.01f, 0.6f + (float)rnd.NextDouble() * 0.9f), dirt, false);
                o.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = -4.5f + i * 3.2f;
                LevelBuildKit.Box("Stain", g, new Vector3(x, 0.35f, 4.385f), new Vector3(0.9f + 0.4f * i % 3, 0.45f, 0.01f), stain, castShadow: false);
            }
            LevelBuildKit.Box("StainE", g, new Vector3(5.885f, 0.30f, 1.2f), new Vector3(0.01f, 0.4f, 1.6f), stain, castShadow: false);
            LevelBuildKit.Box("StainW", g, new Vector3(-5.885f, 0.30f, -2.0f), new Vector3(0.01f, 0.4f, 1.2f), stain, castShadow: false);

            // Weeds pushing up through the floor by the doors.
            foreach (var wp in new[] { new Vector3(-3f, 0f, -3.6f), new Vector3(3f, 0f, -3.5f), new Vector3(-5.2f, 0f, 3.4f), new Vector3(4.6f, 0f, 3.6f), new Vector3(-0.2f, 0f, 3.7f) })
                for (int k = 0; k < 5; k++)
                {
                    var b = LevelBuildKit.Box("Weed", g, wp + new Vector3((k - 2) * 0.12f, 0.2f, ((k * 37) % 5 - 2) * 0.06f),
                        new Vector3(0.05f, 0.4f + 0.1f * (k % 3), 0.05f), weed, castShadow: false,
                        rot: new Vector3((k % 2) * 14f - 7f, k * 40f, (k % 3) * 10f - 10f));
                }

            // Broken fittings.
            FloristModels.BrokenCounter(g, new Vector3(0f, 0.06f, 0.7f), 0f);
            FloristModels.BrokenCentreStand(g, new Vector3(0f, 0.06f, -2.5f), 0f);
            FloristModels.BrokenWallStand(g, new Vector3(-5.3f, 0.06f, -0.3f), -90f);
            FloristModels.BrokenWallStand(g, new Vector3(5.3f, 0.06f, -0.3f), 90f);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var front = new Vector3(-sx * 0.7071f, 0f, -sz * 0.7071f);
                    var pos = new Vector3(sx * 6f, 0.06f, sz * 4.5f) + front * 1.4f;
                    // Only two corners are wrecked; the other two just hold rubbish.
                    if (sx * sz > 0f) FloristModels.BrokenWallStand(g, pos, Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg, corner: true);
                }

            // Litter heaps (what the player clears) and dead flowers.
            Litter(g, new Vector3(-3.4f, 0.06f, -2.0f), 1);
            Litter(g, new Vector3(3.2f, 0.06f, -1.0f), 2);
            Litter(g, new Vector3(-2.3f, 0.06f, 2.6f), 3);
            Litter(g, new Vector3(4.2f, 0.06f, 2.4f), 4);
            FloristModels.DeadFlowers(g, new Vector3(1.8f, 0.06f, 2.9f), 30f, 1.0f);
            FloristModels.DeadFlowers(g, new Vector3(-4.2f, 0.06f, -3.3f), 110f, 1.0f);
            FloristModels.DeadFlowers(g, new Vector3(2.2f, 0.06f, -3.5f), 200f, 0.9f);

            // The bin outside the street door that the litter goes to.
            var binMat = Rm(new Color(0.20f, 0.42f, 0.30f), 0.3f);
            var lidMat = Rm(new Color(0.14f, 0.30f, 0.22f), 0.3f);
            LevelBuildKit.Box("Bin", g, new Vector3(5.2f, 0.5f, -6.2f), new Vector3(1.1f, 1.0f, 0.8f), binMat, castShadow: true);
            LevelBuildKit.Box("BinLid", g, new Vector3(5.2f, 1.05f, -6.2f), new Vector3(1.2f, 0.1f, 0.9f), lidMat, castShadow: true);
        }

        // ================================================================== 4. wall patterns

        private static Texture2D ColourMap(int pattern, bool isNew, Color a, Color b, Color c)
        {
            var tex = PatternFactory.NewTexture("PrevPat");
            var px = new Color32[PatternFactory.Size * PatternFactory.Size];
            PatternFactory.RecolorWall(pattern, a, b, c, px);
            tex.SetPixels32(px);
            tex.Apply(true, false);
            return tex;
        }

        /// <summary>An interior corner of the florist's low wall with caps, a floor, a bouquet and buckets.</summary>
        private static void WallDiorama(Transform root, Vector3 at, Texture2D tex, bool props = true)
        {
            var g = new GameObject("Diorama").transform;
            g.SetParent(root, false);
            g.position = at;
            var wallMat = Rm(Color.white, 0.35f);
            var capMat = Rm(new Color(1.00f, 0.74f, 0.82f), 0.4f);
            var floorMat = Rm(new Color(0.90f, 0.76f, 0.56f), 0.3f);
            const float L = 3.2f;
            LevelBuildKit.Box("Floor", g, new Vector3(0f, 0.03f, 0f), new Vector3(L + 0.4f, 0.06f, L + 0.4f), floorMat, castShadow: false);

            var north = LevelBuildKit.Box("WallN", g, new Vector3(0f, 0.35f, L * 0.5f + 0.1f), new Vector3(L + 0.4f, 0.7f, 0.2f), wallMat, castShadow: false);
            SetTex(north.GetComponent<Renderer>(), tex, new Vector4((L + 0.4f) / PatternFactory.TileMeters, 0.7f / PatternFactory.TileMeters, 0f, 0f));
            LevelBuildKit.Box("CapN", g, new Vector3(0f, 0.73f, L * 0.5f + 0.1f), new Vector3(L + 0.44f, 0.07f, 0.24f), capMat, castShadow: false);

            var east = LevelBuildKit.Box("WallE", g, new Vector3(L * 0.5f + 0.1f, 0.35f, 0f), new Vector3(0.2f, 0.7f, L + 0.4f), wallMat, castShadow: false);
            SetTex(east.GetComponent<Renderer>(), tex, new Vector4((L + 0.4f) / PatternFactory.TileMeters, 0.7f / PatternFactory.TileMeters, 0f, 0f));
            LevelBuildKit.Box("CapE", g, new Vector3(L * 0.5f + 0.1f, 0.73f, 0f), new Vector3(0.24f, 0.07f, L + 0.44f), capMat, castShadow: false);

            if (!props) return;
            FloristModels.Good("bouquet", g, new Vector3(0.7f, 0.35f, 1.15f), 20f);
            FloristModels.BucketObj(g, new Vector3(0.0f, 0.06f, 1.35f));
            FloristModels.Good("sunflower", g, new Vector3(0.0f, 0.45f, 1.35f), 0f);
            FloristModels.BucketObj(g, new Vector3(1.35f, 0.06f, 0.3f));
            FloristModels.Good("hydrangea", g, new Vector3(1.35f, 0.45f, 0.3f), 30f);
        }

        private struct Combo
        {
            public int pattern; public bool isNew; public string name;
            public Color a, b, c;
            public Combo(int pattern, bool isNew, string name, Color a, Color b, Color c)
            { this.pattern = pattern; this.isNew = isNew; this.name = name; this.a = a; this.b = b; this.c = c; }
        }

        private static void Patterns()
        {
            var root = Stage();
            const int cw = 560, ch = 430, cols = 5, rows = 4;
            int W = cw * cols, H = ch * rows;
            var rt = NewRt(W, H);
            var right = GameRot * Vector3.right;
            int slot = 0;
            Vector3 Slot() => right * (60f * slot++) + new Vector3(0f, 0f, 400f);

            Color K(float r, float g, float b) => new Color(r, g, b);
            var cream = K(1.00f, 0.95f, 0.86f); var pink = K(0.98f, 0.56f, 0.70f); var leaf = K(0.45f, 0.72f, 0.40f);

            var existing = new[]
            {
                new Combo(PatternFactory.Plain, false, "PLAIN", cream, cream, cream),
                new Combo(PatternFactory.Stripes, false, "STRIPES", K(1.00f, 0.80f, 0.86f), K(1.00f, 0.95f, 0.88f), K(0.98f, 0.56f, 0.70f)),
                new Combo(PatternFactory.Dots, false, "DOTS", cream, K(1.00f, 0.74f, 0.82f), K(0.60f, 0.82f, 0.62f)),
                new Combo(PatternFactory.Diamonds, false, "DIAMONDS", cream, K(1.00f, 0.80f, 0.86f), K(0.50f, 0.74f, 0.50f)),
                new Combo(PatternFactory.Waves, false, "WAVES", K(0.80f, 0.92f, 0.86f), K(1.00f, 0.95f, 0.88f), K(1.00f, 0.74f, 0.82f)),
            };
            var fl = PatternFactory.Flowers; var lv = PatternFactory.Leaves; var tr = PatternFactory.Trellis;
            var newer = new[]
            {
                new[]
                {
                    new Combo(fl, true, "NEW  FLOWERS: CREAM / PINK / GREEN", cream, pink, leaf),
                    new Combo(fl, true, "NEW  FLOWERS: SKY / WHITE / BLUE", K(0.72f, 0.86f, 0.96f), K(1f, 1f, 1f), K(0.30f, 0.50f, 0.80f)),
                    new Combo(fl, true, "NEW  FLOWERS: MINT / BUTTER / FERN", K(0.74f, 0.92f, 0.82f), K(1.00f, 0.84f, 0.32f), K(0.28f, 0.60f, 0.36f)),
                },
                new[]
                {
                    new Combo(lv, true, "NEW  LEAVES: CREAM / SAGE / FOREST", cream, K(0.52f, 0.76f, 0.46f), K(0.20f, 0.46f, 0.24f)),
                    new Combo(lv, true, "NEW  LEAVES: BLUSH / MINT / BROWN", K(1.00f, 0.88f, 0.90f), K(0.58f, 0.82f, 0.56f), K(0.48f, 0.34f, 0.24f)),
                    new Combo(lv, true, "NEW  LEAVES: FOREST / LIME / GOLD", K(0.22f, 0.42f, 0.30f), K(0.56f, 0.80f, 0.42f), K(0.96f, 0.80f, 0.36f)),
                },
                new[]
                {
                    new Combo(tr, true, "NEW  TRELLIS: WHITE / PINK / WOOD", K(0.98f, 0.96f, 0.92f), pink, K(0.78f, 0.60f, 0.38f)),
                    new Combo(tr, true, "NEW  TRELLIS: BUTTER / LILAC / WHITE", K(1.00f, 0.94f, 0.70f), K(0.72f, 0.55f, 0.92f), K(1f, 1f, 1f)),
                    new Combo(tr, true, "NEW  TRELLIS: SKY / CORAL / CREAM", K(0.68f, 0.84f, 0.96f), K(1.00f, 0.55f, 0.45f), K(1.00f, 0.96f, 0.84f)),
                },
            };

            var cells = new List<(RectInt rect, Vector3 focus, string label)>();
            var textures = new List<Texture2D>();
            int top = H;
            float ortho = ch / (2f * 150f);

            for (int i = 0; i < existing.Length; i++)
            {
                var c = existing[i];
                var tex = ColourMap(c.pattern, c.isNew, c.a, c.b, c.c);
                textures.Add(tex);
                var at = Slot();
                WallDiorama(root, at, tex);
                cells.Add((new RectInt(i * cw, top - ch, cw, ch), at + new Vector3(0.9f, 0.3f, 0.9f), "EXISTING  " + c.name));
            }
            for (int r = 0; r < 3; r++)
            {
                for (int k = 0; k < 3; k++)
                {
                    var c = newer[r][k];
                    var tex = ColourMap(c.pattern, c.isNew, c.a, c.b, c.c);
                    textures.Add(tex);
                    var at = Slot();
                    WallDiorama(root, at, tex);
                    cells.Add((new RectInt(k * cw, top - ch * (r + 2), cw, ch), at + new Vector3(0.9f, 0.3f, 0.9f), c.name));
                }
            }
            foreach (var cell in cells) Cell(root, rt, cell.rect, cell.focus, ortho, cell.label, null, null, 17f);

            // Flat views of the first colour way of each new pattern: the north wall straight on, 3.6 m of it.
            var flat = Quaternion.identity;
            for (int r = 0; r < 3; r++)
            {
                var c = newer[r][0];
                var tex = ColourMap(c.pattern, c.isNew, c.a, c.b, c.c);
                textures.Add(tex);
                var at = Slot();
                WallDiorama(root, at, tex, false);
                // The north wall's inner face is at z = 1.8 + 0.0; look straight at it from the south.
                Cell(root, rt, new RectInt(3 * cw, top - ch * (r + 2), cw * 2, ch),
                    at + new Vector3(0f, 0.38f, 1.8f), 0.50f, "STRAIGHT ON: " + PatternFactory.PatternNames[PatternFactory.Flowers + r] + " ON THE 0.7 M WALL", flat, null, 20f);
            }

            Save(rt, "4-wall-patterns.png");
            Object.DestroyImmediate(rt);
        }
    }
}
