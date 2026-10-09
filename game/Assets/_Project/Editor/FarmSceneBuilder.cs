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

        // ---- TESTING ONLY ----------------------------------------------------------
        // Above zero, this replaces EVERY purchase price on the farm - locked plots, hires
        // and livestock alike - so the whole progression can be walked end to end in a couple
        // of minutes instead of an hour. It is the only thing that has to be changed back:
        // set it to 0 and every real price below takes effect again.
        //
        // Deliberately one constant rather than edited numbers, so reverting cannot miss one.
        private const double TestPriceOverride = 0d;

        /// <summary>The real price, or the test override while one is set.</summary>
        internal static double Price(double real) =>
            TestPriceOverride > 0d ? TestPriceOverride : real;

        [MenuItem("Tycoon/Rebuild Farm Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var items = CreateItems();
            BuildEnvironment();
            var root = CreateLevelRoot();

            var parts = BuildFarm(root, items);
            BuildGroundCover(root);
            BuildBoundary(root);
            BuildFence(root);

            // The second location, and the road that joins it to this one. Built after the
            // farm so the gate that sells it can watch every purchase the farm has.
            BuildMarketLink(root, items, parts);
            BuildNavigation();
            // Between the first coop's collect square and the egg till, so the opening shot
            // shows a complete chain - birds above, counter below - without the player having
            // to walk anywhere to find out what the game is.
            CreatePlayer(new Vector3(EggTill, 0.2f, -11f), root.rotation);

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

        internal class Items
        {
            public ItemDefinition Corn;
            public ItemDefinition Egg;
            public ItemDefinition Hay;
            public ItemDefinition Milk;
            public ItemDefinition Trash;

            // The supermarket's second storage room: bread, then apples, then yogurt. Sold
            // only there, never in a farm till's list, and nothing on the farm makes them.
            public ItemDefinition Bread;
            public ItemDefinition Apples;
            public ItemDefinition Yogurt;
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
                    price: 12d, perishable: true, spoilSeconds: 120f, stackHeight: 0.3f),

                // The market's rubbish. Worth nothing and nothing buys it - it exists only to
                // be carried out of the derelict shop to the bin. Never in any sell list.
                Trash = LevelBuildKit.Item(
                    "trash", "Trash", new Color(0.2f, 0.23f, 0.22f),
                    price: 0d, perishable: false, stackHeight: 0.28f),

                // The market's new lines. The price is what the farm-style till multiplies by
                // 1.5, so the shelf price is that number: bread sells for about 10, apples 6,
                // yogurt 15 (dearer than milk and rots faster than eggs, like the real thing).
                Bread = LevelBuildKit.Item(
                    "bread", "Bread", new Color(0.86f, 0.6f, 0.3f),
                    price: 7d, perishable: true, spoilSeconds: 150f, stackHeight: 0.3f),

                Apples = LevelBuildKit.Item(
                    "apples", "Apples", new Color(0.82f, 0.2f, 0.18f),
                    price: 4d, perishable: false, stackHeight: 0.28f),

                Yogurt = LevelBuildKit.Item(
                    "yogurt", "Yogurt", new Color(0.93f, 0.78f, 0.86f),
                    price: 10d, perishable: true, spoilSeconds: 120f, stackHeight: 0.28f)
            };
        }

        private static Transform CreateLevelRoot()
        {
            var go = new GameObject("Farm");
            go.transform.rotation = Quaternion.Euler(0f, LevelYaw, 0f);

            // Which place this root is. The layout audit walks every Location, so a second
            // business is audited the moment it has one of these.
            var location = go.AddComponent<Location>();
            location.locationId = "farm";
            location.displayName = "Tiny Farm";
            return go.transform;
        }

        // ---- layout ---------------------------------------------------------------
        // The farm is four short production columns standing side by side on one street.
        //
        // It used to be two long wings running north from the market: corn at the top, the
        // coops fifteen metres below them, the counter fifteen metres below that. Every single
        // loop - cut the corn, feed the birds, take the eggs, sell them - was a thirty metre
        // walk down the wing and a thirty metre walk back up it, and because the fields and
        // the tills were at opposite ends of the map the player spent most of the game in
        // transit rather than deciding anything.
        //
        // Now each chain is its own column, read top to bottom exactly the way the goods move:
        //
        //     corn      corn            meadow     meadow
        //     coop      coop            shed       shed
        //        egg till          bin        milk till
        //     =================== road ====================
        //
        // Both halves are the same shape: two fields, two buildings, one till between them.
        // The dairy used to share a single meadow between its two sheds, which made it the odd
        // one out and left the second shed with no field of its own to draw from.
        //
        // A column is about thirteen metres end to end, and every step of a chain is adjacent
        // to the next one, so the walk between two consecutive actions is always short. Going
        // sideways - from the chicken half to the dairy half - is the long trip now, and that
        // is right: the two halves are independent businesses and there is no reason to cross
        // between them during a normal loop.
        //
        // The spacing below is not free choice, and getting it wrong is what put the first
        // dairy on top of the chicken pen and the second dairy's squares across the first
        // one's pasture. A column reaches 5.15 m WEST of its centre (the repair and buy
        // squares) and, for a cow shed, 4.1 m EAST (the pasture) - just over nine metres of
        // occupied ground. Anything under about 9.3 m of spacing overlaps its neighbour, which
        // the layout audit now measures rather than leaving to the eye.
        private const float ColumnSpacing = 10.5f;

        private const float ColumnA = -15.75f;   // corn -> chickens -> eggs
        private const float ColumnB = -5.25f;    // corn -> chickens -> eggs
        private const float ColumnC = 5.25f;     // hay  -> cows     -> milk
        private const float ColumnD = 15.75f;    // hay  -> cows     -> milk

        // Each half's till sits between its two columns, so both chains above it come down
        // the same short distance to sell.
        private const float EggTill = (ColumnA + ColumnB) * 0.5f;
        private const float MilkTill = (ColumnC + ColumnD) * 0.5f;

        // The three rows, north to south. The gaps between them are what is left after the
        // squares: a field's outline reaches 1.75 m from its centre, a workshop's feed square
        // reaches 3.85 m, and 1.2 m of clear grass between the two is enough to walk along
        // without stepping into either by accident.
        /// <summary>
        /// Every crop field. Narrower than it was, and that is a usability fix rather than a
        /// cosmetic one: at 5.5 m a field was well over a metre wider on each side than the
        /// 3.4 m feed square below it, so a player who harvested the west end and walked
        /// straight down missed the square entirely.
        /// </summary>
        private static readonly Vector2 FieldSize = new Vector2(4.5f, 3.5f);

        private const float MarketRow = -14f;
        private const float BuildingRow = MarketRow + 6.15f;    // -7.85
        private const float FieldRow = BuildingRow + 6.8f;      // -1.05

        // ---- demand --------------------------------------------------------------
        // Every number that decides how busy the market is lives here.
        //
        // Shoppers arrive twice as often as they used to (the interval halved), and
        // faster again as the farm grows, because demand is pegged to what the farm can
        // actually produce. Opening the second coop or buying another hen is felt at the
        // counter straight away. The dairy till runs on the same rule against cows.
        // ---- where the world ends --------------------------------------------------
        // The fence, the invisible wall and the grass all read these, so the boundary is one
        // decision rather than three numbers that drift apart. Everything the farm is made of
        // sits inside roughly x -21..21 and z -20..4, and the edge is held a couple of metres
        // clear of that so the fence never crowds a square.
        private const float EdgeWest = -25.5f;
        private const float EdgeEast = 25.5f;
        private const float EdgeNorth = 7f;
        private const float EdgeSouth = -23f;

        /// <summary>Where the tarmac lies, in the level's grid. The road box is 2.8 m deep.</summary>
        private const float RoadZ = MarketRow - 4.6f;

        private const float BaseSpawnInterval = 2.5f;
        private const float DemandPerCapacity = 0.35f;
        private const float MaxDemand = 3f;

        /// <summary>
        /// Every purchase the farm contains, collected as it is built so the supermarket gate
        /// can ask "has the player bought all of it?" without re-deriving the list.
        /// </summary>
        private class FarmParts
        {
            public readonly List<UnlockStation> Unlocks = new List<UnlockStation>();
            public readonly List<UpgradeStation> Upgrades = new List<UpgradeStation>();
        }

        private static FarmParts BuildFarm(Transform root, Items items)
        {
            var parts = new FarmParts();
            // ---- the market ----------------------------------------------------------
            // The tarmac runs far past the farm; the shoppers only use the middle of it.
            //
            // A road that stopped at the last stall looked like the level had been cut off with
            // scissors - the one thing the player sees when they glance away from the farm. It
            // now carries on well beyond the fence and off towards nothing in particular, which
            // is what a road does. Shoppers still arrive from inside the boundary, because a
            // shopper spawned out on the far tarmac would be outside the navmesh entirely.
            var street = LevelBuildKit.BuildShopfront("farm.market", "Market", root,
                new Vector3(0f, 0f, MarketRow), roadHalfLength: 34f, walkHalfLength: 19f);

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
            var counterA = LevelBuildKit.AddCounter(street, "farm.counterA", "CounterA", EggTill,
                new[] { items.Egg }, queueLength: 3, enterFromWest: true);

            var counterB = LevelBuildKit.AddCounter(street, "farm.counterB", "CounterB", MilkTill,
                new[] { items.Milk }, queueLength: 3, enterFromWest: false);

            foreach (var till in new[] { counterA, counterB })
            {
                till.Queue.spawnIntervalSeconds = BaseSpawnInterval;
                till.Queue.demandPerCapacity = DemandPerCapacity;
                till.Queue.maxDemand = MaxDemand;
            }

            // ---- the bins --------------------------------------------------------------
            // Three of them, spread along the market row: one at each end of the farm and one
            // dead centre between the tills.
            //
            // One was enough to prove the idea and not enough to be useful. The farm is fifty
            // metres across now, so a player who filled their arms with the wrong thing out at
            // the dairy had a twenty five metre walk to put it down - long enough that they
            // would rather stand and wait than use it, which defeats the point of having an
            // escape hatch at all. At three, one is always within a few seconds of wherever the
            // mistake was made.
            //
            // They give nothing back, so having more of them cannot be exploited: three ways to
            // throw goods away are no better than one.
            float binInset = 3f;
            LevelBuildKit.BuildBin("farm.bin.west", "BinWest", root,
                new Vector3(EdgeWest + binInset, 0f, MarketRow));

            LevelBuildKit.BuildBin("farm.bin", "Bin", root,
                new Vector3(0f, 0f, MarketRow));

            LevelBuildKit.BuildBin("farm.bin.east", "BinEast", root,
                new Vector3(EdgeEast - binInset, 0f, MarketRow));

            // ---- columns A and B: corn, chickens, eggs ---------------------------------
            var cornA = LevelBuildKit.BuildField("farm.cornA", "CornFieldA", root,
                new Vector3(ColumnA, 0f, FieldRow), items.Corn,
                plots: 8, regrowSeconds: 2.2f, size: FieldSize);

            var cornB = LevelBuildKit.BuildField("farm.cornB", "CornFieldB", root,
                new Vector3(ColumnB, 0f, FieldRow), items.Corn,
                plots: 8, regrowSeconds: 2.2f, size: FieldSize);

            var coopA = LevelBuildKit.BuildWorkshop(
                "farm.coopA", "CoopA", root, new Vector3(ColumnA, 0f, BuildingRow),
                input: items.Corn, output: items.Egg,
                // Paced against demand: a till takes a shopper every 5s wanting 1-4 items, so
                // one counter absorbs roughly 30 items a minute. One chicken at 6s an egg makes
                // 10 - visibly short, which is what makes the second chicken worth buying.
                secondsPerOutput: 6f, inputCapacity: 10, outputCapacity: 12,
                bodyColor: new Color(0.86f, 0.42f, 0.34f), wearPerOutput: 0.75f,
                startUnits: 1, maxUnits: 3, unitPrice: Price(120d),
                unitName: "Chicken", livestock: LevelBuildKit.Livestock.Chicken);
            coopA.Repair.costPerPoint = 0.25d;

            var coopB = LevelBuildKit.BuildWorkshop(
                "farm.coopB", "CoopB", root, new Vector3(ColumnB, 0f, BuildingRow),
                input: items.Corn, output: items.Egg,
                secondsPerOutput: 6f, inputCapacity: 10, outputCapacity: 12,
                bodyColor: new Color(0.62f, 0.5f, 0.85f), wearPerOutput: 0.75f,
                startUnits: 1, maxUnits: 3, unitPrice: Price(150d),
                unitName: "Chicken", livestock: LevelBuildKit.Livestock.Chicken);
            coopB.Repair.costPerPoint = 0.25d;

            // ---- columns C and D: hay, cows, milk --------------------------------------
            // Meadows, not more corn fields: low golden clumps and bales on pale stubble. Side
            // by side with the corn the two were the same shape in slightly different colours.
            //
            // One meadow per shed, in its own column, exactly like the corn. The dairy used to
            // share a single meadow between both sheds, sitting between the two columns - which
            // made the dairy the odd half out, and left a player who had bought a second shed
            // with no second field to keep it fed.
            var hayA = LevelBuildKit.BuildField("farm.hayA", "HayFieldA", root,
                new Vector3(ColumnC, 0f, FieldRow), items.Hay,
                plots: 8, regrowSeconds: 2.6f, size: FieldSize,
                style: LevelBuildKit.FieldStyle.Meadow);

            var hayB = LevelBuildKit.BuildField("farm.hayB", "HayFieldB", root,
                new Vector3(ColumnD, 0f, FieldRow), items.Hay,
                plots: 8, regrowSeconds: 2.6f, size: FieldSize,
                style: LevelBuildKit.FieldStyle.Meadow);

            var cowA = LevelBuildKit.BuildWorkshop(
                "farm.cowA", "CowShedA", root, new Vector3(ColumnC, 0f, BuildingRow),
                input: items.Hay, output: items.Milk,
                // Much slower than a chicken, and worth more than twice as much per unit.
                secondsPerOutput: 10f, inputCapacity: 10, outputCapacity: 10,
                bodyColor: new Color(0.75f, 0.72f, 0.66f), wearPerOutput: 1f,
                startUnits: 1, maxUnits: 3, unitPrice: Price(400d),
                unitName: "Cow", livestock: LevelBuildKit.Livestock.Cow);
            cowA.Repair.costPerPoint = 0.35d;

            var cowB = LevelBuildKit.BuildWorkshop(
                "farm.cowB", "CowShedB", root, new Vector3(ColumnD, 0f, BuildingRow),
                input: items.Hay, output: items.Milk,
                secondsPerOutput: 10f, inputCapacity: 10, outputCapacity: 10,
                bodyColor: new Color(0.6f, 0.66f, 0.72f), wearPerOutput: 1f,
                startUnits: 1, maxUnits: 3, unitPrice: Price(450d),
                unitName: "Cow", livestock: LevelBuildKit.Livestock.Cow);
            cowB.Repair.costPerPoint = 0.35d;

            // ---- hired hands ----------------------------------------------------------
            // A hire square goes where its worker's job visibly happens, not on some separate
            // staffing row: the hand who cuts the crop and carries it to the birds is taken on
            // at the field, and the one who works the till is taken on at the till. Standing in
            // front of a counter and buying a cashier is immediately understandable in a way
            // that a square out in the corridor never was.
            //
            // Workers cost only their hire price; there is no running wage. (A per-minute wage
            // once deadlocked the game when the wallet emptied; piece rates were dropped later
            // as needless friction.)
            var hireFarmerA = BuildHire(root, "Farmer", "HarvesterA", BesideField(root, cornA, -1),
                price: 250d, pickup: cornA, dropoff: coopA.Feed,
                color: new Color(0.95f, 0.58f, 0.25f), feePerDelivery: 0d, beacon: coopA.Beacon);

            var hireCashierA = BuildHire(root, "Cashier", "SellerA", AtTill(root, counterA, -1),
                price: 400d, pickup: coopA.Collect, dropoff: counterA.Register,
                color: new Color(0.35f, 0.75f, 0.55f), feePerDelivery: 0d, beacon: coopA.Beacon);

            var hireFarmerB = BuildHire(root, "Farmer 2", "HarvesterB", BesideField(root, cornB, 1),
                price: 700d, pickup: cornB, dropoff: coopB.Feed,
                color: new Color(0.95f, 0.58f, 0.25f), feePerDelivery: 0d, beacon: coopB.Beacon);

            var hireCashierB = BuildHire(root, "Cashier 2", "SellerB", AtTill(root, counterA, 1),
                price: 800d, pickup: coopB.Collect, dropoff: counterA.Register,
                color: new Color(0.35f, 0.75f, 0.55f), feePerDelivery: 0d, beacon: coopB.Beacon);

            // Each hay hand is hired at the meadow it actually cuts, the same as the farmers.
            var hireHayA = BuildHire(root, "Hay Hand", "HayHandA", BesideField(root, hayA, -1),
                price: 1500d, pickup: hayA, dropoff: cowA.Feed,
                color: new Color(0.88f, 0.74f, 0.3f), feePerDelivery: 0d, beacon: cowA.Beacon);

            var hireMilkA = BuildHire(root, "Milk Run", "MilkRunA", AtTill(root, counterB, -1),
                price: 1700d, pickup: cowA.Collect, dropoff: counterB.Register,
                color: new Color(0.55f, 0.8f, 0.9f), feePerDelivery: 0d, beacon: cowA.Beacon);

            var hireHayB = BuildHire(root, "Hay Hand 2", "HayHandB", BesideField(root, hayB, 1),
                price: 2200d, pickup: hayB, dropoff: cowB.Feed,
                color: new Color(0.88f, 0.74f, 0.3f), feePerDelivery: 0d, beacon: cowB.Beacon);

            var hireMilkB = BuildHire(root, "Milk Run 2", "MilkRunB", AtTill(root, counterB, 1),
                price: 2400d, pickup: cowB.Collect, dropoff: counterB.Register,
                color: new Color(0.55f, 0.8f, 0.9f), feePerDelivery: 0d, beacon: cowB.Beacon);

            parts.Unlocks.AddRange(new[]
            {
                hireFarmerA, hireCashierA, hireFarmerB, hireCashierB,
                hireHayA, hireMilkA, hireHayB, hireMilkB
            });
            parts.Upgrades.AddRange(new[] { coopA.Upgrade, coopB.Upgrade, cowA.Upgrade, cowB.Upgrade });

            // ---- what has to be bought ------------------------------------------------
            // A gate reveals its building and BOTH hire squares whose workers end their round
            // trip at it. A worker delivering into a building that does not exist yet walks to
            // where it will be, finds no trigger, and stands there full for ever - so a hire is
            // only ever offered once its destination is standing. The farm hand who carries
            // corn to coop B is therefore sold with coop B, not with the field he cuts.
            parts.Unlocks.Add(Gate(root, "farm.unlock.cornB", "UnlockCornB", new Vector3(ColumnB, 0f, FieldRow),
                FieldSize, "Field", 400d,
                cornB.gameObject));

            parts.Unlocks.Add(Gate(root, "farm.unlock.coopB", "UnlockCoopB", new Vector3(ColumnB, 0f, BuildingRow),
                new Vector2(3.4f, 2.6f), "Coop", 500d,
                coopB.Root, hireFarmerB.gameObject, hireCashierB.gameObject));

            parts.Unlocks.Add(Gate(root, "farm.unlock.counterB", "UnlockCounterB", new Vector3(MilkTill, 0f, MarketRow),
                new Vector2(3.4f, 2.2f), "Dairy Till", 1400d,
                counterB.Root));

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
            parts.Unlocks.Add(Gate(root, "farm.unlock.cowA", "UnlockCowA", new Vector3(ColumnC, 0f, BuildingRow),
                new Vector2(3.4f, 2.6f), "Dairy", 3200d,
                cowA.Root, hayA.gameObject, hireHayA.gameObject, hireMilkA.gameObject));

            // The second meadow, bought on its own before the shed that will need it - exactly
            // the shape the corn side already has, where the second field is the cheaper step
            // and the second coop the expensive one.
            //
            // It is not decoration bought early: one meadow regrows roughly enough hay to keep
            // one shed of three cows fed, so until this is standing a second shed would spend
            // its life waiting. Buying it also immediately doubles what the player can cut in
            // one trip for the shed they already own.
            parts.Unlocks.Add(Gate(root, "farm.unlock.hayB", "UnlockHayB", new Vector3(ColumnD, 0f, FieldRow),
                FieldSize, "Meadow", 4500d,
                hayB.gameObject));

            parts.Unlocks.Add(Gate(root, "farm.unlock.cowB", "UnlockCowB", new Vector3(ColumnD, 0f, BuildingRow),
                new Vector2(3.4f, 2.6f), "Cow Shed", 6000d,
                cowB.Root, hireHayB.gameObject, hireMilkB.gameObject));

            // ---- the walkthrough ------------------------------------------------------
            // Harvest, feed, collect, sell, bin - the first loop, pointed out by the task list.
            // The squares are wired here, where they exist; the bin is picked at runtime
            // (whichever is nearest), so it is not listed. Saved as step number under
            // "game.tutorial", and always active: nothing about it is gated.
            var tutorial = new GameObject("Tutorial");
            tutorial.transform.SetParent(root, false);
            LevelBuildKit.Identify(tutorial, "game.tutorial");
            var walkthrough = tutorial.AddComponent<Tycoon.Tasks.TutorialProgress>();
            walkthrough.field = cornA;
            walkthrough.feed = coopA.Feed;
            walkthrough.collect = coopA.Collect;
            walkthrough.till = counterA.Register;

            return parts;
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
        internal static Vector3 LevelLocal(Transform root, Component thing) =>
            root.InverseTransformPoint(thing.transform.position);

        /// <summary>
        /// A hire square level with a field, on the side given by <paramref name="side"/>
        /// (-1 west, +1 east), so the farm hand is visibly taken on at the crop they cut.
        /// </summary>
        private static Vector3 BesideField(Transform root, Component field, int side)
        {
            Vector3 local = LevelLocal(root, field);

            // Measured off the field's own trigger, so resizing a field cannot leave its hire
            // square sitting on the crop.
            var trigger = field.GetComponent<BoxCollider>();
            float fieldHalf = trigger != null ? trigger.size.x * 0.5f : 2.25f;

            // Against the drawn card, not the trigger: a hire card is never narrower than 2.5 m.
            // The gap is small because two of these share the 6 m between corn B and meadow A.
            const float hireHalf = 1.25f;
            const float gap = 0.3f;

            return new Vector3(local.x + Mathf.Sign(side) * (fieldHalf + hireHalf + gap), 0f, local.z);
        }

        /// <summary>
        /// A hire square on the till's row, directly beside the serve square.
        ///
        /// A till's two cashiers flank it, one each side, so they read as "these two work
        /// here". <paramref name="slot"/> counts outwards; its sign picks the side.
        /// </summary>
        private static Vector3 AtTill(Transform root, LevelBuildKit.Counter till, int slot)
        {
            Vector3 local = LevelLocal(root, till.Register);

            float sign = slot < 0 ? -1f : 1f;
            // Directly beside the serve square: its card is 3.6 wide, a hire card 2.5.
            float offset = 1.8f + 0.5f + 1.25f + (Mathf.Abs(slot) - 1) * 3.0f;

            return new Vector3(local.x + sign * offset, 0f, local.z);
        }

        /// <summary>
        /// Puts content behind a purchase. The revealed objects must be inactive in the saved
        /// scene, so nothing inside a locked plot ticks, saves or can be walked into before it
        /// has actually been bought.
        /// </summary>
        internal static UnlockStation Gate(Transform root, string id, string name, Vector3 position,
            Vector2 size, string label, double price, params GameObject[] reveal)
        {
            var gate = LevelBuildKit.Station<UnlockStation>(id, name, root, position, size,
                label, new Color(1f, 0.85f, 0.35f));
            gate.price = Price(price);
            gate.payPerTick = Mathf.Max(4f, (float)(gate.price / 40d));
            gate.revealOnUnlock = reveal;

            foreach (var go in reveal)
                if (go != null) go.SetActive(false);

            return gate;
        }

        /// <summary>
        /// Creates a worker plus the square that hires them. The worker is inactive until the
        /// square is paid off, so an unhired worker draws no wages and runs no code.
        /// </summary>
        internal static UnlockStation BuildHire(Transform root, string role, string id, Vector3 position,
            double price, Tycoon.Stations.StationBase pickup, Tycoon.Stations.StationBase dropoff,
            Color color, double feePerDelivery, Tycoon.Upkeep.AlertBeacon beacon,
            string idPrefix = "farm.hire.")
        {
            // Field hands wear the straw hat, till staff the cap. Derived from the role
            // name so adding a hire does not mean remembering to pass a model as well.
            string model = role.StartsWith("Cashier") || role.StartsWith("Milk")
                ? CharacterLibrary.Cashier
                : CharacterLibrary.Farmer;

            var worker = LevelBuildKit.BuildWorker(id, root, position, pickup, dropoff,
                color, feePerDelivery, role: model);
            WarnIfRouteBroken(id, pickup, dropoff);

            var hire = LevelBuildKit.Station<UnlockStation>($"{idPrefix}{id}", $"Hire{id}", root, position,
                new Vector2(1.8f, 1.8f), role, new Color(0.55f, 0.8f, 1f));
            // A person rather than the padlock every other purchase gets: same station type,
            // very different thing being bought.
            hire.icon = Tycoon.UI.SquareIcon.Hire;
            hire.price = Price(price);
            hire.payPerTick = Mathf.Max(6f, (float)(hire.price / 40d));
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
        /// under one pokes straight through it. Rather than list every square, the whole built
        /// area is excluded in two blocks - which is exactly where the buildings, plots and
        /// squares are anyway.
        /// </summary>
        private static void BuildGroundCover(Transform root)
        {
            // Spread a little wider than the fence, so the fence stands IN the grass rather
            // than at the edge of it - a boundary the meadow runs up to and past reads as a
            // boundary; one the greenery stops dead at reads as the end of the level.
            const float beyond = 11f;
            var area = new Rect(EdgeWest - beyond, EdgeSouth - beyond,
                                (EdgeEast - EdgeWest) + beyond * 2f,
                                (EdgeNorth - EdgeSouth) + beyond * 2f);

            var keepClear = new[]
            {
                // The four columns: fields, buildings, and every square that belongs to them,
                // including the hire squares standing behind the fields.
                new Rect(-22f, -12.5f, 44f, 17f),
                // The market: both tills, the bin, the hire squares flanking the counters,
                // the queues and the road.
                new Rect(EdgeWest - 1f, -21f, (EdgeEast - EdgeWest) + 2f, 8.5f),
            };

            LevelBuildKit.BuildGrass(root, area, keepClear, tufts: 2200);
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
            // Deliberately not centred on the origin. The farm sits well south of it -
            // everything happens between the hire squares at z = +4 and the road at z = -20 -
            // so a symmetric boundary would leave twenty metres of empty grass to the north
            // that exists only to be walked across and got lost in.
            //
            // The wall lines up with the fence exactly, so the thing that stops the player is
            // the thing they can see. A wall standing anywhere else is either an invisible
            // barrier in open grass or a fence you can walk through.
            LevelBuildKit.BuildBoundary(root, EdgeWest, EdgeEast, EdgeSouth, EdgeNorth);
        }

        /// <summary>
        /// The fence that makes the boundary something the player can see.
        ///
        /// Runs along exactly the same lines as the invisible walls, with one deliberate break
        /// on each side where the road passes through - so the tarmac leaves the farm the way
        /// a road should, through a gap, rather than stopping at a plank or running under it.
        /// </summary>
        private static void BuildFence(Transform root)
        {
            LevelBuildKit.BuildFenceAround(root, EdgeWest, EdgeEast, EdgeSouth, EdgeNorth, RoadZ);
        }

        // ---- the road to town ------------------------------------------------------
        // The supermarket is a second location a long way off across the grass, reached only by
        // the travel squares at the two ends of this link. Its entry in the farm is two things
        // on the same patch of road, at the east end where the tarmac leaves through the fence:
        // the gate that sells it, and the square that takes the player there once it is bought.
        //
        // Both sit on the road itself rather than inside the farm proper. The shoppers who use
        // the east end of the road spawn at x = 19 and walk west, so x = 23 is behind them, and
        // the east bin and the dairy hire square are well clear (the audit measures it).
        private const float TownSquareX = 23f;
        private static readonly Vector3 TownSquarePosition = new Vector3(TownSquareX, 0f, RoadZ);

        /// <summary>
        /// Where the player lands coming back from town. Clear of the travel square - landing
        /// inside it would be a loop - and of the east bin and the shoppers' lane.
        /// </summary>
        private static readonly Vector3 ReturnFromTown = new Vector3(19.8f, 0f, -15.8f);

        /// <summary>How far along the farm's own road axis the market stands.</summary>
        internal const float MarketOffsetX = 75f;

        /// <summary>Price of the supermarket itself, before the test override.</summary>
        private const double MarketPrice = 10000d;

        private static void BuildMarketLink(Transform farmRoot, Items items, FarmParts parts)
        {
            var farmLocation = farmRoot.GetComponent<Location>();

            var spawn = new GameObject("Spawn");
            spawn.transform.SetParent(farmRoot, false);
            spawn.transform.localPosition = ReturnFromTown;
            farmLocation.spawnPoint = spawn.transform;

            var market = MarketBuilder.Build(farmRoot, items, farmLocation, MarketOffsetX);

            var toTown = LevelBuildKit.Station<TravelStation>("farm.travel.town", "TravelToTown",
                farmRoot, TownSquarePosition, new Vector2(2.5f, 2.1f), "Town",
                new Color(0.55f, 0.8f, 1f));
            toTown.destination = market.Location;
            toTown.taskDuration = 1.2f;

            // The gate sits exactly where the travel square will be, the way every other gate
            // sits on what it sells. Paying the last of it leaves the player standing in the
            // square, which is the right moment to go: the ring fills, and they are on their way.
            var gate = Gate(farmRoot, "farm.unlock.market", "UnlockMarket", TownSquarePosition,
                new Vector2(3.0f, 2.4f), "Supermarket", MarketPrice,
                market.Root, toTown.gameObject);

            // Offered only when the whole farm is owned: every gate, every hire, every
            // building at its maximum. Until then there is nothing at the end of the road.
            var watcher = new GameObject("MarketUnlock");
            watcher.transform.SetParent(farmRoot, false);
            var reveal = watcher.AddComponent<RevealWhenAll>();
            reveal.unlocks = parts.Unlocks.ToArray();
            reveal.maxed = parts.Upgrades.ToArray();
            reveal.reveal = new[] { gate.gameObject };
            gate.gameObject.SetActive(false);
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
            // 160 x 160. Big enough that the camera can never find its edge: the player can
            // walk right up to the fence, on the widest screen the game runs on, and still not
            // see where the ground stops. The layout audit measures that margin rather than
            // trusting it. The plane is a single quad, so the size costs nothing at all.
            ground.transform.localScale = new Vector3(16f, 1f, 16f);
            ground.GetComponent<Renderer>().sharedMaterial =
                LevelBuildKit.Mat("Ground", new Color(0.42f, 0.62f, 0.33f));

            // The same grass, drawn much further out and with no collider. The ground above
            // is the only thing anyone can stand on (and the only thing the navmesh bakes), but
            // the second location sits seventy-odd metres out and the camera sees well past
            // wherever the player is standing. Without this the edge of the world is visible
            // from the supermarket, and the audit says so.
            var horizon = GameObject.CreatePrimitive(PrimitiveType.Plane);
            horizon.name = "GroundHorizon";
            horizon.transform.position = new Vector3(0f, -0.02f, 0f);
            horizon.transform.localScale = new Vector3(40f, 1f, 40f);
            UnityEngine.Object.DestroyImmediate(horizon.GetComponent<Collider>());
            horizon.GetComponent<Renderer>().sharedMaterial =
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
