using System.Collections.Generic;
using System.IO;
using Tycoon.Config;
using Tycoon.Core;
using Tycoon.Player;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Generates the Farm level from scratch.
    ///
    /// The scene is built by script rather than hand-placed so that the layout, the economy
    /// numbers and the wiring all live in one readable file. Re-run it any time to get a clean
    /// level back; hand-edit the generated scene afterwards if you prefer working in the editor.
    /// </summary>
    public static class FarmSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Farm.unity";

        // The whole level is rotated 45 degrees so that its local axes line up with the screen
        // under the isometric camera: local +Z runs up the screen, local +X runs right. That
        // makes every coordinate below something you can read off the phone screen directly,
        // and it presents buildings square-on, like the farm ads this is modelled on.
        //
        // Set this to 0 for the classic isometric diamond instead: the camera stays where it
        // is, but the world grid meets it at 45 degrees so buildings are seen corner-first.
        // It is purely a look; the layout numbers below do not change.
        private const float LevelYaw = 45f;

        [MenuItem("Tycoon/Rebuild Farm Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var items = CreateItems();
            BuildEnvironment();
            var root = CreateLevelRoot();

            BuildFarm(root, items);
            // Start between the first coop and the empty plot, so both the corn field above
            // and the locked plot below are on screen from the first frame.
            CreatePlayer(new Vector3(0f, 0.2f, 1f), root.rotation);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterOnlyScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FarmSceneBuilder] Built {ScenePath}");
            Debug.Log("[FarmSceneBuilder] SCENE_OK");

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private class Items
        {
            public ItemDefinition Corn;
            public ItemDefinition Egg;
        }

        private static Items CreateItems()
        {
            return new Items
            {
                Corn = LevelBuildKit.Item(
                    "corn", "Corn", new Color(0.96f, 0.78f, 0.22f),
                    price: 1d, perishable: false, stackHeight: 0.3f),

                // Eggs rot: leaving a full basket sitting in the coop costs real money, which
                // is the gentlest of the upkeep pressures and the first one players notice.
                Egg = LevelBuildKit.Item(
                    "egg", "Egg", new Color(0.96f, 0.94f, 0.86f),
                    price: 5d, perishable: true, spoilSeconds: 90f, stackHeight: 0.26f)
            };
        }

        private static Transform CreateLevelRoot()
        {
            var go = new GameObject("Farm");
            go.transform.rotation = Quaternion.Euler(0f, LevelYaw, 0f);
            return go.transform;
        }

        private static void BuildFarm(Transform root, Items items)
        {
            // --- the production chain, running straight down the screen ---------------
            // A workshop is about 7 units deep once its feed and collect squares are counted,
            // so the buildings are spaced accordingly. Everything stays within roughly 6 units
            // of the centre line, which is what the portrait camera can actually show.
            LevelBuildKit.BuildField("CornField", root, new Vector3(0f, 0f, 12f),
                items.Corn, plots: 8, regrowSeconds: 2.2f, size: new Vector2(6.5f, 3.5f));

            var coopA = LevelBuildKit.BuildWorkshop(
                "CoopA", root, new Vector3(0f, 0f, 6f),
                input: items.Corn, output: items.Egg,
                secondsPerOutput: 2.5f, inputCapacity: 10, outputCapacity: 12,
                bodyColor: new Color(0.86f, 0.42f, 0.34f), wearPerOutput: 1.5f);
            coopA.Repair.costPerPoint = 0.25d;

            var market = LevelBuildKit.Station<SellStation>("Market", root,
                new Vector3(0f, 0f, -8f), new Vector2(4.5f, 3f),
                "Sell", new Color(0.45f, 0.85f, 0.6f));
            market.accepted = items.Egg;
            BuildMarketStall(market.transform);

            // --- the first expansion --------------------------------------------------
            var coopB = LevelBuildKit.BuildWorkshop(
                "CoopB", root, new Vector3(0f, 0f, -1.5f),
                input: items.Corn, output: items.Egg,
                secondsPerOutput: 2.5f, inputCapacity: 10, outputCapacity: 12,
                bodyColor: new Color(0.62f, 0.5f, 0.85f), wearPerOutput: 1.5f);
            coopB.Repair.costPerPoint = 0.25d;

            // The buy-square sits exactly where the coop will appear, so paying it off reads
            // as the building rising out of the plot you were standing on.
            var unlock = LevelBuildKit.Station<UnlockStation>("UnlockCoopB", root,
                new Vector3(0f, 0f, -1.5f), new Vector2(3.4f, 2.6f),
                "New Coop", new Color(1f, 0.85f, 0.35f));
            unlock.price = 150d;
            unlock.payPerTick = 4d;
            unlock.revealOnUnlock = new[] { coopB.Root };

            // Must be inactive in the saved scene: nothing inside a locked plot should tick,
            // save or be reachable until it has actually been bought.
            coopB.Root.SetActive(false);
        }

        private static void BuildMarketStall(Transform parent)
        {
            var wood = LevelBuildKit.Mat("Market_Wood", new Color(0.55f, 0.38f, 0.24f));
            var awning = LevelBuildKit.Mat("Market_Awning", new Color(0.9f, 0.35f, 0.35f));

            // The stall sits on the far side of its square (down-screen), so it never stands
            // between the camera and the player walking in to sell.
            LevelBuildKit.Box("Counter", parent, new Vector3(0f, 0.5f, -1.5f),
                new Vector3(4f, 1f, 0.5f), wood);
            LevelBuildKit.Box("PostL", parent, new Vector3(-1.8f, 1.1f, -1.5f),
                new Vector3(0.16f, 2.2f, 0.16f), wood);
            LevelBuildKit.Box("PostR", parent, new Vector3(1.8f, 1.1f, -1.5f),
                new Vector3(0.16f, 2.2f, 0.16f), wood);
            LevelBuildKit.Box("Awning", parent, new Vector3(0f, 2.2f, -1.8f),
                new Vector3(4.2f, 0.18f, 1.4f), awning);
        }

        private static void BuildEnvironment()
        {
            // --- ground ---------------------------------------------------------------
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f); // 80 x 80 units
            ground.GetComponent<Renderer>().sharedMaterial =
                LevelBuildKit.Mat("Ground", new Color(0.42f, 0.62f, 0.33f));

            // --- light ----------------------------------------------------------------
            var lightGo = new GameObject("Sun");
            lightGo.transform.rotation = Quaternion.Euler(48f, 30f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.97f, 0.9f);
            // Hard shadows: the pipeline asset has soft shadows compiled out for the Web build,
            // so asking for them here would only cost a shader variant and get downgraded anyway.
            light.shadows = LightShadows.Hard;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.45f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.28f, 0.22f);

            // --- camera ---------------------------------------------------------------
            var cameraGo = new GameObject("MainCamera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.47f, 0.72f, 0.87f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 80f;
            cameraGo.AddComponent<AudioListener>();

            var rig = cameraGo.AddComponent<IsometricCameraRig>();
            rig.pitchYaw = new Vector2(30f, 45f);
            rig.orthographicSize = 8.5f;
            rig.distance = 30f;
            rig.lookOffset = new Vector3(0f, 0f, 0.8f);
        }

        private static void CreatePlayer(Vector3 localPosition, Quaternion levelRotation)
        {
            var go = new GameObject("Player") { tag = "Player" };
            go.transform.position = levelRotation * localPosition;

            var controller = go.AddComponent<CharacterController>();
            controller.radius = 0.38f;
            controller.height = 1.5f;
            controller.center = new Vector3(0f, 0.75f, 0f);
            controller.slopeLimit = 60f;
            controller.stepOffset = 0.35f;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);

            var skin = LevelBuildKit.Mat("Player_Body", new Color(0.25f, 0.55f, 0.92f));
            var head = LevelBuildKit.Mat("Player_Head", new Color(0.98f, 0.82f, 0.68f));

            LevelBuildKit.Box("Body", visual.transform, new Vector3(0f, 0.55f, 0f),
                new Vector3(0.62f, 0.8f, 0.45f), skin);
            LevelBuildKit.Box("Head", visual.transform, new Vector3(0f, 1.15f, 0f),
                new Vector3(0.5f, 0.5f, 0.5f), head);
            // A small nose so the facing direction is unmistakable at this camera angle.
            LevelBuildKit.Box("Nose", visual.transform, new Vector3(0f, 1.12f, 0.28f),
                new Vector3(0.12f, 0.12f, 0.12f), skin);

            var anchor = new GameObject("CarryAnchor");
            anchor.transform.SetParent(visual.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            var motor = go.AddComponent<PlayerMotor>();
            motor.visual = visual.transform;
            motor.moveSpeed = 5.2f;

            var carry = go.AddComponent<CarryStack>();
            carry.anchor = anchor.transform;
            carry.capacity = 8;
        }

        private static void RegisterOnlyScene()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }
    }
}
