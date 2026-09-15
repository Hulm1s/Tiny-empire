using System;
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
            BuildGroundCover(root);
            BuildBoundary(root);
            BuildNavigation();
            // Start in the corridor between the wings, within sight of both the first coop
            // and the market.
            CreatePlayer(new Vector3(0f, 0.2f, -7f), root.rotation);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath) ?? "Assets");
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RegisterOnlyScene();
            RememberSceneForNextOpen();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[FarmSceneBuilder] Built {ScenePath}");
            Debug.Log("[FarmSceneBuilder] SCENE_OK");

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Makes the editor open the farm next time somebody launches the project.
        ///
        /// A headless rebuild quits through EditorApplication.Exit, which skips the shutdown
        /// that normally records which scenes were open. Unity then starts with no scene loaded
        /// at all, so opening the project shows an empty grey viewport and looks for all the
        /// world like the game has vanished - the scene asset is fine, nothing is displaying it.
        ///
        /// Writing the file the editor reads on startup is blunt, but it is the only thing that
        /// survives Exit, and being wrong costs nothing: a bad file just means no scene opens,
        /// which is exactly where we were.
        /// </summary>
        private static void RememberSceneForNextOpen()
        {
            try
            {
                const string setupFile = "Library/LastSceneManagerSetup.txt";
                Directory.CreateDirectory("Library");
                File.WriteAllText(setupFile,
                    "sceneSetups:\n" +
                    $"- path: {ScenePath}\n" +
                    "  isLoaded: 1\n" +
                    "  isActive: 1\n" +
                    "  isSubScene: 0\n");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Only a convenience; never worth failing a build over.
                Debug.LogWarning($"[FarmSceneBuilder] Could not record the open scene: {e.Message}");
            }
        }

        private class Items
        {
            public ItemDefinition Corn;
            public ItemDefinition Egg;
            public ItemDefinition Hay;
            public ItemDefinition Milk;
        }

        private static Items CreateItems()
        {
            return new Items
            {
                Corn = LevelBuildKit.Item(
                    "corn", "Corn", new Color(0.96f, 0.78f, 0.22f),
                    price: 1d, perishable: false, stackHeight: 0.3f),

                // Perishables rot, so hoarding output loses money. It is the gentlest of the
                // upkeep pressures and the first one players notice.
                Egg = LevelBuildKit.Item(
                    "egg", "Egg", new Color(0.96f, 0.94f, 0.86f),
                    price: 5d, perishable: true, spoilSeconds: 90f, stackHeight: 0.26f),

                Hay = LevelBuildKit.Item(
                    "hay", "Hay", new Color(0.85f, 0.72f, 0.33f),
                    price: 1d, perishable: false, stackHeight: 0.32f),

                // Slower to make and worth far more than an egg, so the dairy wing is a
                // genuine step up rather than a reskin of the chickens.
                Milk = LevelBuildKit.Item(
                    "milk", "Milk", new Color(0.95f, 0.96f, 0.98f),
                    price: 12d, perishable: true, spoilSeconds: 120f, stackHeight: 0.3f)
            };
        }

        private static Transform CreateLevelRoot()
        {
            var go = new GameObject("Farm");
            go.transform.rotation = Quaternion.Euler(0f, LevelYaw, 0f);
            return go.transform;
        }

        // The farm is two production wings either side of a central walking corridor, with
        // the market across the south end. Two wings rather than one long column keeps the
        // walk from the fields to the counter reasonable; the camera follows the player, so
        // only one wing is on screen at a time and that is fine.
        private const float LeftWing = -5f;    // corn -> chickens -> eggs
        private const float RightWing = 5f;    // hay  -> cows     -> milk

        // ---- demand --------------------------------------------------------------
        // Every number that decides how busy the market is lives here.
        //
        // Shoppers arrive twice as often as they used to (the interval halved), and
        // faster again as the farm grows, because demand is pegged to what the farm can
        // actually produce. Opening the second coop or buying another hen is felt at the
        // counter straight away. The dairy till runs on the same rule against cows.
        private const float BaseSpawnInterval = 2.5f;
        private const float DemandPerCapacity = 0.35f;
        private const float MaxDemand = 3f;

        private static void BuildFarm(Transform root, Items items)
        {
            // ---- the market ----------------------------------------------------------
            var street = LevelBuildKit.BuildShopfront("farm.market", "Market", root,
                new Vector3(0f, 0f, -13f));

            // One till per product line, and this is load-bearing.
            //
            // Both counters used to share a menu of everything the farm makes. Nothing carries
            // milk to the egg counter - the egg sellers work the left wing and the milk runs
            // work the right - so a milk shopper sent to counter A could only ever be served by
            // the player personally walking it across the farm. Worse, the queue only offers its
            // head to the till, so that one stranded shopper blocked every egg customer behind
            // them for a full 75 seconds. It read as the game randomly deciding to punish you.
            //
            // Shoppers still only ask for what the farm can currently make (ProductRegistry), so
            // the dairy till simply stands empty until the first cow shed opens.
            var counterA = LevelBuildKit.AddCounter(street, "farm.counterA", "CounterA", -3f,
                new[] { items.Egg }, queueLength: 3, enterFromWest: true);

            var counterB = LevelBuildKit.AddCounter(street, "farm.counterB", "CounterB", 3f,
                new[] { items.Milk }, queueLength: 3, enterFromWest: false);

            foreach (var till in new[] { counterA, counterB })
            {
                till.Queue.spawnIntervalSeconds = BaseSpawnInterval;
                till.Queue.demandPerCapacity = DemandPerCapacity;
                till.Queue.maxDemand = MaxDemand;
            }

            // ---- left wing: corn, chickens, eggs --------------------------------------
            var cornA = LevelBuildKit.BuildField("farm.cornA", "CornFieldA", root,
                new Vector3(LeftWing, 0f, 17f), items.Corn,
                plots: 8, regrowSeconds: 2.2f, size: new Vector2(5.5f, 3.5f));

            var cornB = LevelBuildKit.BuildField("farm.cornB", "CornFieldB", root,
                new Vector3(LeftWing, 0f, 10f), items.Corn,
                plots: 8, regrowSeconds: 2.2f, size: new Vector2(5.5f, 3.5f));

            var coopA = LevelBuildKit.BuildWorkshop(
                "farm.coopA", "CoopA", root, new Vector3(LeftWing, 0f, 2f),
                input: items.Corn, output: items.Egg,
                // Paced against demand: a till takes a shopper every 5s wanting 1-4 items, so
                // one counter absorbs roughly 30 items a minute. One chicken at 6s an egg makes
                // 10 - visibly short, which is what makes the second chicken worth buying.
                secondsPerOutput: 6f, inputCapacity: 10, outputCapacity: 12,
                bodyColor: new Color(0.86f, 0.42f, 0.34f), wearPerOutput: 1.5f,
                startUnits: 1, maxUnits: 3, unitPrice: 120d,
                unitName: "Chicken", livestock: LevelBuildKit.Livestock.Chicken);
            coopA.Repair.costPerPoint = 0.25d;

            var coopB = LevelBuildKit.BuildWorkshop(
                "farm.coopB", "CoopB", root, new Vector3(LeftWing, 0f, -6f),
                input: items.Corn, output: items.Egg,
                secondsPerOutput: 6f, inputCapacity: 10, outputCapacity: 12,
                bodyColor: new Color(0.62f, 0.5f, 0.85f), wearPerOutput: 1.5f,
                startUnits: 1, maxUnits: 3, unitPrice: 150d,
                unitName: "Chicken", livestock: LevelBuildKit.Livestock.Chicken);
            coopB.Repair.costPerPoint = 0.25d;

            // ---- right wing: hay, cows, milk ------------------------------------------
            // A meadow, not a second corn field: low golden clumps and bales on pale stubble.
            // Side by side the two were the same shape in slightly different colours.
            var hayField = LevelBuildKit.BuildField("farm.hayfield", "HayField", root,
                new Vector3(RightWing, 0f, 17f), items.Hay,
                plots: 8, regrowSeconds: 2.6f, size: new Vector2(5.5f, 3.5f),
                style: LevelBuildKit.FieldStyle.Meadow);

            var cowA = LevelBuildKit.BuildWorkshop(
                "farm.cowA", "CowShedA", root, new Vector3(RightWing, 0f, 8f),
                input: items.Hay, output: items.Milk,
                // Much slower than a chicken, and worth more than twice as much per unit.
                secondsPerOutput: 10f, inputCapacity: 10, outputCapacity: 10,
                bodyColor: new Color(0.75f, 0.72f, 0.66f), wearPerOutput: 2f,
                startUnits: 1, maxUnits: 3, unitPrice: 400d,
                unitName: "Cow", livestock: LevelBuildKit.Livestock.Cow);
            cowA.Repair.costPerPoint = 0.35d;

            var cowB = LevelBuildKit.BuildWorkshop(
                "farm.cowB", "CowShedB", root, new Vector3(RightWing, 0f, 0f),
                input: items.Hay, output: items.Milk,
                secondsPerOutput: 10f, inputCapacity: 10, outputCapacity: 10,
                bodyColor: new Color(0.6f, 0.66f, 0.72f), wearPerOutput: 2f,
                startUnits: 1, maxUnits: 3, unitPrice: 450d,
                unitName: "Cow", livestock: LevelBuildKit.Livestock.Cow);
            cowB.Repair.costPerPoint = 0.35d;

            // ---- hired hands ----------------------------------------------------------
            // A hire square goes where its worker's job visibly happens, not on some separate
            // staffing row: the hand who cuts the crop and carries it to the birds is taken on
            // at the field, and the one who works the till is taken on at the till. Standing in
            // front of a counter and buying a cashier is immediately understandable in a way
            // that a square out in the corridor never was.
            //
            // Workers take a cut of every unit they deliver, so automation is a running cost
            // that scales with throughput.
            var hireFarmerA = BuildHire(root, "Farmer", "HarvesterA", AtField(root, cornA, true),
                price: 250d, pickup: cornA, dropoff: coopA.Feed,
                color: new Color(0.95f, 0.58f, 0.25f), feePerDelivery: 0.5d, beacon: coopA.Beacon);

            var hireCashierA = BuildHire(root, "Cashier", "SellerA", AtTill(root, counterA, -1),
                price: 400d, pickup: coopA.Collect, dropoff: counterA.Register,
                color: new Color(0.35f, 0.75f, 0.55f), feePerDelivery: 0.8d, beacon: coopA.Beacon);

            var hireFarmerB = BuildHire(root, "Farmer 2", "HarvesterB", AtField(root, cornB, true),
                price: 700d, pickup: cornB, dropoff: coopB.Feed,
                color: new Color(0.95f, 0.58f, 0.25f), feePerDelivery: 0.5d, beacon: coopB.Beacon);

            var hireCashierB = BuildHire(root, "Cashier 2", "SellerB", AtTill(root, counterA, -2),
                price: 800d, pickup: coopB.Collect, dropoff: counterA.Register,
                color: new Color(0.35f, 0.75f, 0.55f), feePerDelivery: 0.8d, beacon: coopB.Beacon);

            // Both hay hands are hired at the one hay field they both cut from, side by side.
            var hireHayA = BuildHire(root, "Hay Hand", "HayHandA", AtField(root, hayField, false, 1.2f),
                price: 1500d, pickup: hayField, dropoff: cowA.Feed,
                color: new Color(0.88f, 0.74f, 0.3f), feePerDelivery: 0.6d, beacon: cowA.Beacon);

            var hireMilkA = BuildHire(root, "Milk Run", "MilkRunA", AtTill(root, counterB, 1),
                price: 1700d, pickup: cowA.Collect, dropoff: counterB.Register,
                color: new Color(0.55f, 0.8f, 0.9f), feePerDelivery: 1.4d, beacon: cowA.Beacon);

            var hireHayB = BuildHire(root, "Hay Hand 2", "HayHandB", AtField(root, hayField, false, -1.2f),
                price: 2200d, pickup: hayField, dropoff: cowB.Feed,
                color: new Color(0.88f, 0.74f, 0.3f), feePerDelivery: 0.6d, beacon: cowB.Beacon);

            var hireMilkB = BuildHire(root, "Milk Run 2", "MilkRunB", AtTill(root, counterB, 2),
                price: 2400d, pickup: cowB.Collect, dropoff: counterB.Register,
                color: new Color(0.55f, 0.8f, 0.9f), feePerDelivery: 1.4d, beacon: cowB.Beacon);

            // ---- what has to be bought ------------------------------------------------
            // Each gate reveals its building AND the hire squares that go with it, so a worker
            // can never be hired for a building that does not exist yet.
            Gate(root, "farm.unlock.cornB", "UnlockCornB", new Vector3(LeftWing, 0f, 10f),
                new Vector2(5.5f, 3.5f), "Field", 600d,
                cornB.gameObject, hireFarmerB.gameObject);

            Gate(root, "farm.unlock.coopB", "UnlockCoopB", new Vector3(LeftWing, 0f, -6f),
                new Vector2(3.4f, 2.6f), "Coop", 900d,
                coopB.Root, hireCashierB.gameObject);

            Gate(root, "farm.unlock.counterB", "UnlockCounterB", new Vector3(3f, 0f, -13f),
                new Vector2(3.4f, 2.2f), "Dairy Till", 1400d,
                counterB.Root);

            // The meadow comes with the cow shed, deliberately, and this is a softlock fix
            // rather than a tidy-up.
            //
            // The hay field used to be its own cheaper purchase. Hay has exactly one place it
            // can go - a cow's feed hopper - so a player who bought the field first could fill
            // their arms with hay that had nowhere to be put down. A full stack cannot pick up
            // eggs, eggs are the only income, and the shed costs money: no way out except
            // starting over. Selling the two together means hay can never exist before
            // something that eats it.
            //
            // Priced under the sum of the two it replaces, because it now has to be saved for
            // in one go rather than in two steps.
            Gate(root, "farm.unlock.cowA", "UnlockCowA", new Vector3(RightWing, 0f, 8f),
                new Vector2(3.4f, 2.6f), "Dairy", 3200d,
                cowA.Root, hayField.gameObject, hireHayA.gameObject, hireMilkA.gameObject);

            Gate(root, "farm.unlock.cowB", "UnlockCowB", new Vector3(RightWing, 0f, 0f),
                new Vector2(3.4f, 2.6f), "Cow Shed", 6000d,
                cowB.Root, hireHayB.gameObject, hireMilkB.gameObject);
        }

        /// <summary>
        /// Shouts at build time if a worker carries goods to a till that does not sell them.
        ///
        /// This is the exact mistake that made milk shoppers unservable: the tills listed a
        /// product nothing delivered to them. It is invisible in the editor and only shows up
        /// as a customer standing at a counter forever, so it is worth catching here - where
        /// the layout is written - rather than in play.
        /// </summary>
        private static void WarnIfRouteBroken(string worker, StationBase pickup, StationBase dropoff)
        {
            var till = dropoff as RegisterStation;
            if (till == null) return;   // delivering into a hopper, not a counter

            var goods = Produces(pickup);
            if (goods == null || till.CanFulfill(goods)) return;

            Debug.LogWarning(
                $"[FarmSceneBuilder] {worker} carries {goods.displayName} to {dropoff.name}, " +
                $"which does not sell it. That worker's deliveries can never be sold.");
        }

        /// <summary>What comes out of a square, for routing checks. Null when it produces nothing.</summary>
        private static ItemDefinition Produces(StationBase station)
        {
            if (station is HarvestStation field) return field.crop;
            if (station is CollectStation collect) return collect.source != null ? collect.source.item : null;
            return null;
        }

        /// <summary>Anything's position expressed in the level's own grid.</summary>
        private static Vector3 LevelLocal(Transform root, Component thing) =>
            root.InverseTransformPoint(thing.transform.position);

        /// <summary>
        /// A hire square at the outer edge of a crop field.
        ///
        /// Deliberately on the far side from the corridor. The player walks the length of the
        /// wing dozens of times a session, and a hire square on that line would be stepped in
        /// constantly - which, since standing in one spends money, is a tax on walking past.
        /// </summary>
        private static Vector3 AtField(Transform root, Component field, bool leftWing, float alongZ = 0f)
        {
            Vector3 local = LevelLocal(root, field);

            // Measured off the field's own trigger rather than assumed. A fixed clearance was
            // tuned against one field size and left the hire square overlapping the crop, so
            // the player standing in it was buying a worker and harvesting at the same time.
            var trigger = field.GetComponent<BoxCollider>();
            float fieldHalf = trigger != null ? trigger.size.x * 0.5f : 2.75f;

            // Measured against the drawn outline, not the trigger. A hire trigger is 1.8 m
            // but its square is never drawn smaller than 2.5, so clearing 0.9 still left the
            // two outlines overlapping on screen.
            const float hireHalf = 1.25f;
            const float gap = 1.0f;

            float clearance = fieldHalf + hireHalf + gap;
            return new Vector3(local.x + (leftWing ? -clearance : clearance), 0f, local.z + alongZ);
        }

        /// <summary>
        /// A hire square beside a till, on the serving side. <paramref name="slot"/> counts
        /// outwards from the counter, away from the middle of the street, so a second cashier
        /// lines up next to the first instead of drifting into the road or the other till.
        /// </summary>
        private static Vector3 AtTill(Transform root, LevelBuildKit.Counter till, int slot)
        {
            Vector3 local = LevelLocal(root, till.Register);

            // Same correction as the fields: these are outline widths, not trigger widths.
            // The till's square is 3.6 wide and a hire square is drawn 2.5 wide, so the first
            // one clears at 1.8 + 1.25 + gap, and each one after is a full outline further out.
            float sign = slot < 0 ? -1f : 1f;
            float offset = 3.45f + (Mathf.Abs(slot) - 1) * 2.9f;

            return new Vector3(local.x + sign * offset, 0f, local.z);
        }

        /// <summary>
        /// Puts content behind a purchase. The revealed objects must be inactive in the saved
        /// scene, so nothing inside a locked plot ticks, saves or can be walked into before it
        /// has actually been bought.
        /// </summary>
        private static void Gate(Transform root, string id, string name, Vector3 position,
            Vector2 size, string label, double price, params GameObject[] reveal)
        {
            var gate = LevelBuildKit.Station<UnlockStation>(id, name, root, position, size,
                label, new Color(1f, 0.85f, 0.35f));
            gate.price = price;
            gate.payPerTick = Mathf.Max(4f, (float)(price / 40d));
            gate.revealOnUnlock = reveal;

            foreach (var go in reveal)
                if (go != null) go.SetActive(false);
        }

        /// <summary>
        /// Creates a worker plus the square that hires them. The worker is inactive until the
        /// square is paid off, so an unhired worker draws no wages and runs no code.
        /// </summary>
        private static UnlockStation BuildHire(Transform root, string role, string id, Vector3 position,
            double price, Tycoon.Stations.StationBase pickup, Tycoon.Stations.StationBase dropoff,
            Color color, double feePerDelivery, Tycoon.Upkeep.AlertBeacon beacon)
        {
            // Field hands wear the straw hat, till staff the cap. Derived from the role
            // name so adding a hire does not mean remembering to pass a model as well.
            string model = role.StartsWith("Cashier") || role.StartsWith("Milk")
                ? CharacterLibrary.Cashier
                : CharacterLibrary.Farmer;

            var worker = LevelBuildKit.BuildWorker(id, root, position, pickup, dropoff,
                color, feePerDelivery, role: model);
            WarnIfRouteBroken(id, pickup, dropoff);

            var hire = LevelBuildKit.Station<UnlockStation>($"farm.hire.{id}", $"Hire{id}", root, position,
                new Vector2(1.8f, 1.8f), role, new Color(0.55f, 0.8f, 1f));
            // A person rather than the padlock every other purchase gets: same station type,
            // very different thing being bought.
            hire.icon = Tycoon.UI.SquareIcon.Hire;
            hire.price = price;
            hire.payPerTick = Mathf.Max(6f, (float)(price / 40d));
            hire.revealOnUnlock = new[] { worker.gameObject };

            // The alert beacon should complain about an unpaid worker at this building.
            if (beacon != null && beacon.worker == null) beacon.worker = worker;

            worker.gameObject.SetActive(false);
            return hire;
        }

        /// <summary>
        /// Grass everywhere the farm is not.
        ///
        /// The keep-clear rectangles matter more than the grass does: a tuft is up to 40 cm
        /// tall and an interaction square is painted 9 cm off the ground, so grass growing
        /// under one pokes straight through it. Rather than list every square, the two
        /// production wings and the market are excluded wholesale - those are exactly the
        /// places covered in buildings, plots and squares anyway.
        /// </summary>
        private static void BuildGroundCover(Transform root)
        {
            var area = new Rect(-22f, -25f, 44f, 50f);

            var keepClear = new[]
            {
                // Left wing: corn fields, coops, their squares and the hire spots beside them.
                new Rect(-11.5f, -9.5f, 10.5f, 29f),
                // Right wing: hay, cow sheds, same again.
                new Rect(1f, -3.5f, 10.5f, 23f),
                // The market, its road, the queues and the till squares.
                new Rect(-12f, -20f, 24f, 10.5f),
            };

            LevelBuildKit.BuildGrass(root, area, keepClear);
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
            const float halfWidth = 23f;
            const float halfDepth = 26f;
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

            // The owner: white shirt and a red cap, so the one character the player
            // actually controls is never confused with the staff or the shoppers.
            CharacterLibrary.Spawn(CharacterLibrary.Owner, visual.transform, "Model");

            var anchor = new GameObject("CarryAnchor");
            anchor.transform.SetParent(visual.transform, false);
            anchor.transform.localPosition = CharacterLibrary.HandAnchor;

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
