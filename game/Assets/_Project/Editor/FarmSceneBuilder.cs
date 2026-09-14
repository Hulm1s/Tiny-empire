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
            BuildBoundary(root);
            BuildNavigation();
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
            var cornField = LevelBuildKit.BuildField("farm.cornfield", "CornField", root, new Vector3(0f, 0f, 12f),
                items.Corn, plots: 8, regrowSeconds: 2.2f, size: new Vector2(6.5f, 3.5f));

            var coopA = LevelBuildKit.BuildWorkshop(
                "farm.coopA", "CoopA", root, new Vector3(0f, 0f, 6f),
                input: items.Corn, output: items.Egg,
                secondsPerOutput: 2.5f, inputCapacity: 10, outputCapacity: 12,
                bodyColor: new Color(0.86f, 0.42f, 0.34f), wearPerOutput: 1.5f);
            coopA.Repair.costPerPoint = 0.25d;

            // The shopfront: till, stall, queue and the road shoppers walk in along. Eggs are
            // only worth money when somebody at the counter is asking for them, so the farm
            // has to keep pace with demand rather than just pile up stock.
            var shop = LevelBuildKit.BuildShopfront("farm.market", "Market", root, new Vector3(0f, 0f, -8f),
                catalogue: new[] { items.Egg }, queueLength: 3);

            // --- the first expansion --------------------------------------------------
            var coopB = LevelBuildKit.BuildWorkshop(
                "farm.coopB", "CoopB", root, new Vector3(0f, 0f, -2f),
                input: items.Corn, output: items.Egg,
                secondsPerOutput: 2.5f, inputCapacity: 10, outputCapacity: 12,
                bodyColor: new Color(0.62f, 0.5f, 0.85f), wearPerOutput: 1.5f);
            coopB.Repair.costPerPoint = 0.25d;

            // The buy-square sits exactly where the coop will appear, so paying it off reads
            // as the building rising out of the plot you were standing on.
            var unlock = LevelBuildKit.Station<UnlockStation>("farm.unlock.coopB", "UnlockCoopB", root,
                new Vector3(0f, 0f, -2f), new Vector2(3.4f, 2.6f),
                "New Coop", new Color(1f, 0.85f, 0.35f));
            unlock.price = 150d;
            unlock.payPerTick = 4d;
            unlock.revealOnUnlock = new[] { coopB.Root };

            // --- automation, and the running cost that comes with it ------------------
            // Each hire square sits beside the leg of the chain it takes over, so it is
            // obvious what you are buying. Workers take a cut of every unit they deliver, so
            // automation is a running cost that scales with throughput - and a fully automated
            // farm still jams and still spoils, so it is never the end of the game.
            BuildHire(root, "Harvester", new Vector3(3.3f, 0f, 8.8f), price: 250d,
                pickup: cornField, dropoff: coopA.Feed,
                color: new Color(0.95f, 0.58f, 0.25f), feePerDelivery: 0.5d, beacon: coopA.Beacon);

            BuildHire(root, "Seller", new Vector3(3.3f, 0f, 3.2f), price: 400d,
                pickup: coopA.Collect, dropoff: shop.Register,
                color: new Color(0.35f, 0.75f, 0.55f), feePerDelivery: 0.8d, beacon: coopA.Beacon);

            // Must be inactive in the saved scene: nothing inside a locked plot should tick,
            // save or be reachable until it has actually been bought.
            coopB.Root.SetActive(false);
        }

        /// <summary>
        /// Creates a worker plus the square that hires them. The worker is inactive until the
        /// square is paid off, so an unhired worker draws no wages and runs no code.
        /// </summary>
        private static void BuildHire(Transform root, string name, Vector3 position, double price,
            Tycoon.Stations.StationBase pickup, Tycoon.Stations.StationBase dropoff,
            Color color, double feePerDelivery, Tycoon.Upkeep.AlertBeacon beacon)
        {
            var worker = LevelBuildKit.BuildWorker(name, root, position, pickup, dropoff, color, feePerDelivery);

            var hire = LevelBuildKit.Station<UnlockStation>($"farm.hire.{name}", $"Hire{name}", root, position,
                new Vector2(1.8f, 1.8f), $"Hire {name}", new Color(0.55f, 0.8f, 1f));
            hire.price = price;
            hire.payPerTick = 6d;
            hire.revealOnUnlock = new[] { worker.gameObject };

            // The alert beacon should complain about an unpaid worker on this farm.
            if (beacon != null && beacon.worker == null) beacon.worker = worker;

            worker.gameObject.SetActive(false);
        }

        /// <summary>
        /// Invisible walls around the playable area.
        ///
        /// Without these the player can simply walk off the edge of the ground plane and fall
        /// out of the world forever, with no way back except reloading. Found by holding a
        /// movement key for twenty seconds, which a bored player will absolutely do.
        ///
        /// Sized to comfortably contain the farm and the full length of the road.
        /// </summary>
        private static void BuildBoundary(Transform root)
        {
            const float halfWidth = 20f;
            const float halfDepth = 22f;
            const float thickness = 2f;
            const float height = 6f;

            var boundary = new GameObject("Boundary");
            boundary.transform.SetParent(root, false);

            AddWall(boundary.transform, "North", new Vector3(0f, height * 0.5f, halfDepth),
                new Vector3(halfWidth * 2f, height, thickness));
            AddWall(boundary.transform, "South", new Vector3(0f, height * 0.5f, -halfDepth),
                new Vector3(halfWidth * 2f, height, thickness));
            AddWall(boundary.transform, "East", new Vector3(halfWidth, height * 0.5f, 0f),
                new Vector3(thickness, height, halfDepth * 2f));
            AddWall(boundary.transform, "West", new Vector3(-halfWidth, height * 0.5f, 0f),
                new Vector3(thickness, height, halfDepth * 2f));
        }

        private static void AddWall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            // Solid, not a trigger: the CharacterController must actually be stopped by it.
            box.isTrigger = false;
        }

        /// <summary>
        /// The navigation surface workers path across. Baked at runtime by NavigationBaker.
        ///
        /// Built from physics colliders rather than render meshes so the ground plane and the
        /// boundary walls define the walkable area, and the buildings punch holes in it.
        /// </summary>
        private static void BuildNavigation()
        {
            var go = new GameObject("Navigation");

            var surface = go.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
            surface.collectObjects = Unity.AI.Navigation.CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            // Roughly the worker's build, so paths keep clear of walls by a sensible margin.
            surface.agentTypeID = 0;

            go.AddComponent<Tycoon.Core.NavigationBaker>();
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
            // Pitch drives how readable the interaction squares are: a ground square is
            // squashed to sin(pitch) of its true height on screen. At 30 degrees that is half,
            // which made the squares hard to read and let buildings hide the ones behind them.
            // 50 degrees keeps a clear view down onto every square while still showing the
            // fronts of the buildings, so the world still reads as 3D rather than a floor plan.
            // Yaw is 30 degrees off the level grid (which sits at 45). That offset is what
            // makes buildings show two faces instead of presenting flat-on, and it is the
            // single value to change if the viewing angle needs tuning.
            rig.pitchYaw = new Vector2(50f, 75f);
            // Widened to match: the steeper angle makes the level occupy more vertical screen.
            rig.orthographicSize = 9.5f;
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
