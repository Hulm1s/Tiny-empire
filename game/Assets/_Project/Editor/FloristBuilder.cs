using System.Collections.Generic;
using Tycoon.Config;
using Tycoon.Customers;
using Tycoon.Stations;
using Tycoon.UI;
using Tycoon.Upkeep;
using UnityEditor;
using UnityEngine;
using Fl = Tycoon.EditorTools.FloristModels.Fl;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The florist: the first business on the town street, built from Filip's sketch
    /// (plans/florist-layout-sketch.png) and the approved art (FloristModels, BouquetArt).
    ///
    ///     north   the MEADOW, fenced: eight beds - roses and tulips on the left, sunflowers and
    ///             hydrangeas on the right - the well in the middle and the seed box beside it
    ///             ------------------------- back door (to the meadow) ------------------------
    ///             the SHOP: the serve square behind the counter, next to the back door; the counter
    ///             across the middle; the square stand in the middle of the floor; stands along the
    ///             walls, angled at the corners
    ///     south   entrance (bottom left) .................................. exit (bottom right)
    ///             the street
    ///
    /// Customers come in on the left, walk up round the central stand to the counter, queue along
    /// it, are served, and leave round the other side and out of the right-hand door.
    ///
    /// It starts DERELICT, and is brought back the way the supermarket is (<see cref="MarketBuilder"/>'s
    /// renovation, the same parts): carry the litter heaps out to the bin by the street door (free), then
    /// REPAIR (a gate: the counter and stands come back, the wrecks go) and PAINT (the paint menu, with
    /// the florist's own patterns), then OPEN (a free chore), which brings in the garden and starts the
    /// purchases. The shop has two coats built in - grubby and painted - that <see cref="MarketDecor"/>
    /// swaps; everything that blocks anyone is built once and never blinks.
    ///
    /// How it works, all from the existing parts:
    ///   - each flower has a STOCK (an ItemBuffer behind the counter) that a harvest of its two beds
    ///     (<see cref="FlowerBed"/>) fills - nobody carries flowers;
    ///   - a bed is a cycle: bare soil, plant a SEED (free, from the seed box), water it, let it grow,
    ///     water it again, let it grow, pick it. Each bed has one square (<see cref="BedStation"/>) that
    ///     does whichever step is next; a bubble over the bed asks for the seed, the water or the pick;
    ///   - seeds and water are items (an ItemBuffer with a SupplyFeed and a COLLECT square each: the
    ///     seed box and the well), so the player carries them and the gardener, an ordinary WorkerAgent
    ///     with a route per job per bed, carries them for him (harvest, then water, then plant);
    ///   - the counter is a <see cref="BouquetCounter"/> (a RegisterStation display counter over those
    ///     stocks); customers (<see cref="CustomerQueue.bouquets"/>) want two to six flowers; the hired
    ///     florist only serves, flower by flower at the base price, and the player makes the bouquet in
    ///     the bouquet minigame and earns a tip on top;
    ///   - the stands' flower buckets belong to each flower's purchase, so the shop shows only the
    ///     flowers that have been bought;
    ///   - takings go onto a cash pile at the counter, as everywhere.
    ///
    /// One purchase at a time, each revealing the next (all prices through Price()):
    ///     Litter -> Repair + Paint -> OPEN (shop, roses, well, seed box) -> Tulips -> Hydrangeas
    ///       -> Sunflowers -> Florist (hire) -> Gardener (hire)
    ///
    /// Everything here is a child of the town root (saved inactive until the town is bought). New
    /// save ids only, all under "town.florist.".
    /// </summary>
    internal static class FloristBuilder
    {
        // ---- prices (plans/econ/market_sim.py, FLORIST, 2026-10-17): repair + the first paint are bought within
        // ~2.5 minutes of buying the town (Filip's limit is 3) on what the farm and the supermarket (both let go
        // at half value from then on) earn; the rest follow every 2.8-4.2 minutes, the gardener ~21 minutes after
        // the town. The beds' roses come with OPEN, so there is no separate purchase for the first beds. ----
        private const double RepairPrice = 2000d;      // the counter and stands back
        private const double FirstPaintPrice = 1000d;  // the renovation step: the first paint job
        private const double RepaintPrice = 200d;
        private const double TulipsPrice = 3500d;
        private const double HydrangeasPrice = 4500d;
        private const double SunflowersPrice = 5000d;
        private const double FloristHirePrice = 5500d;
        private const double GardenerHirePrice = 7000d;

        // What one flower sells for (FarmSceneBuilder.CreateItems reads these). A bouquet is two to six.
        internal const double RosePrice = 15d;
        internal const double TulipPrice = 18d;
        internal const double SunflowerPrice = 24d;
        internal const double HydrangeaPrice = 27d;

        // ---- growing ---------------------------------------------------------------------
        // Seconds of each of a bed's two growing spells; a harvest puts FlowersPerHarvest in the stock.
        private static readonly float[] SpellSeconds = { 10f, 11f, 13f, 14f };   // rose, tulip, sunflower, hydrangea
        private const int FlowersPerHarvest = 4;
        private const float BedTaskSeconds = 0.9f;   // standing in a bed's square: plant, water, pick
        private const int StockCapacity = 12;        // flowers of one kind waiting behind the counter
        private const int WellCapacity = 6;
        private const float WellSecondsPerCan = 0.4f;
        private const int SeedCapacity = 8;
        private const float SeedSecondsPerPacket = 2.5f;

        // ---- customers -----------------------------------------------------------------------
        private const float CustomerIntervalSeconds = 8f;
        private const float DemandPerFlower = 0.35f;     // each extra flower bought brings customers a third faster
        private const int QueueSlots = 3;

        // ---- the shop (the town's grid; north +z, the street at z -3) ------------------------
        internal const float ShopWest = -13f, ShopEast = -1f, ShopSouth = -0.8f, ShopNorth = 8.2f;
        private const float MidX = (ShopWest + ShopEast) * 0.5f;     // -7
        private const float EntranceFrom = -11f, EntranceTo = -9f;   // bottom left
        private const float ExitFrom = -5f, ExitTo = -3f;            // bottom right
        private const float BackDoorFrom = -6.9f, BackDoorTo = -5.1f; // to the meadow
        private const float WallHeight = 0.7f;

        private static readonly Vector3 CounterAt = new Vector3(MidX, 0f, 4.4f);        // 3.36 x 1.0
        private static readonly Vector3 ServeAt = new Vector3(-7.9f, 0f, 6.0f);          // behind the counter
        private static readonly Vector2 ServeSize = new Vector2(2.0f, 1.8f);
        private static readonly Vector3 CashAt = new Vector3(-10.0f, 0f, 5.8f);
        private static readonly Vector3 FloristHireAt = new Vector3(-4.2f, 0f, 6.0f);
        private static readonly Vector3 CentreStandAt = new Vector3(MidX, 0f, 1.2f);     // 2.06 square

        // ---- the renovation (the same steps as the supermarket's) ---------------------------------
        // The litter heaps: each is a pile of four bags with a collect square on it. Carrying all twelve
        // out to the bin (the player carries eight) is free, so a broke player can always start.
        private static readonly Vector3[] TrashPositions =
        {
            new Vector3(-10.0f, 0f, 1.5f), new Vector3(-3.9f, 0f, 1.9f), new Vector3(-9.6f, 0f, 7.0f),
        };
        private const int BagsPerPile = 4;
        private static readonly Vector2 PileSquare = new Vector2(2.2f, 2.0f);
        // West of the street door, on the pavement beside the shop's west wall; the can stands north of its square
        // (squareToSouth, like the supermarket's) so the way to it from the street is open.
        internal static readonly Vector3 BinAt = new Vector3(-14.4f, 0f, 1.9f);
        private static readonly Vector3 RepairAt = new Vector3(-10.0f, 0f, 1.5f);
        private static readonly Vector3 PaintAt = new Vector3(-11.9f, 0f, 5.4f);
        private static readonly Vector3 OpenAt = new Vector3(-4.1f, 0f, 2.0f);
        private static readonly Vector2 RepairSize = new Vector2(2.2f, 2.0f);
        private static readonly Vector2 PaintSize = new Vector2(1.7f, 1.5f);
        private static readonly Vector2 OpenSize = new Vector2(2.2f, 2.0f);

        // The queue, nearest the counter first, and the way in. A customer's order bubble is wider than the gap
        // between neighbours in a straight line (a three-flower order is ~2.5 m across at the florist's bubble scale,
        // 1.5 m tall), so the slots are staggered: 2.6 m apart sideways, or 2 m apart in depth (which is ~1.5 m up
        // the screen), so no two bubbles overlap at the game camera (the audit measures it). The line runs from the
        // counter's front, round the west side of the central stand, back towards the street door.
        private static readonly Vector3[] Slots =
        {
            new Vector3(MidX, 0f, 3.1f), new Vector3(-9.6f, 0f, 2.4f), new Vector3(-10.6f, 0f, 0.4f),
        };
        private const float BubbleScale = 0.0075f;
        private static readonly Vector3[] EntryRoute =
        {
            new Vector3(-10f, 0f, TownBuilder.RoadZ), new Vector3(-10f, 0f, 0.2f), new Vector3(-9.9f, 0f, 2.9f),
        };
        private static readonly Vector3[] ExitRoute =
        {
            new Vector3(-4.6f, 0f, 3.1f), new Vector3(-4.0f, 0f, 0.6f), new Vector3(-4.0f, 0f, TownBuilder.RoadZ),
        };
        private static readonly Vector3 SpawnPoint = new Vector3(-17.5f, 0f, TownBuilder.RoadZ);
        // Beyond the town's east fence (x 22), where the salon's ground ends: nobody vanishes in plain sight on the road.
        private static readonly Vector3 ExitPoint = new Vector3(24f, 0f, TownBuilder.RoadZ);

        // ---- the meadow ------------------------------------------------------------------------
        internal const float MeadowNorth = 17.2f;
        private static readonly Vector3 WellAt = new Vector3(MidX, 0f, 12.4f);
        private static readonly Vector3 WellSquareAt = new Vector3(MidX, 0f, 10.5f);
        private static readonly Vector2 WellSquareSize = new Vector2(2.0f, 1.8f);
        // Filip's meadow sketch (2026-10-17): the water square lies directly SOUTH of the well, touching it, centred on it
        // (not by the entrance any more, where it was picked up by mistake); the seed box is a long rack against the
        // NORTH fence left of centre, its collect square in front of it (south); the gardener's hire square is
        // against the north fence right of centre. The middle lane between the beds is 3.4 m wide (x -8.7..-5.3).
        private static readonly Vector3 SeedBoxAt = new Vector3(-8.6f, 0f, 16.7f);
        private static readonly Vector3 SeedSquareAt = new Vector3(-7.9f, 0f, 15.35f);
        private static readonly Vector2 SeedSquareSize = new Vector2(1.4f, 1.5f);
        private static readonly Vector3 GardenerHireAt = new Vector3(-6.25f, 0f, 16.2f);
        private static readonly Vector2 BedSquare = new Vector2(1.8f, 2.4f);

        // Two beds per flower: rose south-left, tulip north-left, sunflower south-right, hydrangea north-right.
        private static readonly float[][] BedX =
        {
            new[] { -11.7f, -9.6f }, new[] { -11.7f, -9.6f }, new[] { -4.4f, -2.3f }, new[] { -4.4f, -2.3f },
        };
        private static readonly float[] BedZ = { 10.4f, 14.6f, 10.4f, 14.6f };
        private static readonly string[] FlowerName = { "Roses", "Tulips", "Sunflowers", "Hydrangeas" };

        // Which approved bed model each BedStage shows: (growth stage 0-3, wet soil).
        private static readonly (int stage, bool wet)[] StageLooks =
        {
            (0, false), (1, false), (1, true), (2, false), (2, true), (3, false),
        };

        private class FlowerLine
        {
            public GameObject Group;
            public ItemBuffer Stock;
            public readonly List<FlowerBed> Beds = new List<FlowerBed>();
        }

        /// <summary>The shop's pieces: the permanent building and the sets the renovation swaps.</summary>
        private class Shell
        {
            public Transform Building;                 // ground, fence, colliders: never changes
            public GameObject Grubby, Painted;         // the two coats
            public GameObject Working, Broken;         // counter and stands, and the wrecks they replace
            public readonly List<DecorSurface> Surfaces = new List<DecorSurface>();
        }

        /// <summary>Builds the florist. Returns the gardener's hire square: the last purchase of the chain, which the salon waits on.</summary>
        internal static UnlockStation Build(Transform town, FarmSceneBuilder.Items items)
        {
            var florist = new GameObject("Florist").transform;
            florist.SetParent(town, false);

            var flowers = new[] { items.Rose, items.Tulip, items.Sunflower, items.Hydrangea };
            var shell = BuildShell(florist);

            // ---- the working shop -------------------------------------------------------------
            var shop = new GameObject("Shop");
            shop.transform.SetParent(florist, false);
            LevelBuildKit.Identify(shop, "town.florist");     // the queue's reputation is saved under this

            var lines = new FlowerLine[4];
            for (int f = 0; f < 4; f++) lines[f] = BuildFlower(florist, shell.Working.transform, f, flowers[f], items);

            var counter = BuildCounter(shop.transform, items, flowers, lines);

            // The stock panel in the HUD (Filip, round 3): while the player is in the shop or the meadow it shows the
            // bought flowers' icons with n / capacity, red at 0 (UI/StockPanel.cs, which reads this zone). A stock
            // that is switched off - a flower not yet bought - is simply left out. Shop + meadow, a little generous.
            var zone = florist.gameObject.AddComponent<StockInfoZone>();
            zone.stocks = new[] { lines[0].Stock, lines[1].Stock, lines[2].Stock, lines[3].Stock };
            zone.min = new Vector2(ShopWest - 0.3f, ShopSouth - 0.4f);
            zone.max = new Vector2(ShopEast + 0.3f, MeadowNorth + 0.4f);

            // ---- water and seeds: the well and the seed box --------------------------------------
            var wellGroup = new GameObject("Well");
            wellGroup.transform.SetParent(florist, false);
            var wellModel = FloristModels.Well(wellGroup.transform, WellAt + Vector3.up * 0.04f, 0f);
            wellModel.name = "Stall";
            Solid(wellGroup.transform, "Collision", WellAt + new Vector3(0f, 0.6f, 0f), new Vector3(1.9f, 1.2f, 1.8f));
            var wellWater = LevelBuildKit.Buffer("town.florist.well", "WellWater", wellGroup.transform, WellAt, items.Water,
                WellCapacity, WellCapacity);
            var feed = wellWater.gameObject.AddComponent<SupplyFeed>();
            feed.target = wellWater;
            feed.secondsPerUnit = WellSecondsPerCan;
            var well = LevelBuildKit.Station<CollectStation>("town.florist.well.collect", "FetchWater", wellGroup.transform,
                WellSquareAt, WellSquareSize, "Water", new Color(0.42f, 0.72f, 1f), smallestCard: WellSquareSize);
            well.source = wellWater;

            var seedGroup = new GameObject("SeedBox");
            seedGroup.transform.SetParent(florist, false);
            var seedModel = FloristModels.SeedBox(seedGroup.transform, SeedBoxAt + Vector3.up * 0.0f, 0f);
            seedModel.name = "Stall";
            Solid(seedGroup.transform, "Collision", SeedBoxAt + new Vector3(0.35f, 0.4f, 0f), new Vector3(2.1f, 0.8f, 0.9f));
            var seedStore = LevelBuildKit.Buffer("town.florist.seeds", "Seeds", seedGroup.transform, SeedBoxAt, items.Seed,
                SeedCapacity, SeedCapacity / 2);
            var seedFeed = seedStore.gameObject.AddComponent<SupplyFeed>();
            seedFeed.target = seedStore;
            seedFeed.secondsPerUnit = SeedSecondsPerPacket;
            var seeds = LevelBuildKit.Station<CollectStation>("town.florist.seeds.collect", "FetchSeeds", seedGroup.transform,
                SeedSquareAt, SeedSquareSize, "Seeds", new Color(0.95f, 0.72f, 0.40f), smallestCard: SeedSquareSize);
            seeds.source = seedStore;

            var bedSquares = new List<BedStation>();
            for (int f = 0; f < 4; f++)
                foreach (var bed in lines[f].Beds)
                {
                    bed.wellSquare = well;
                    bed.seedSquare = seeds;
                    bedSquares.Add(bed.square);
                }

            // ---- the staff ---------------------------------------------------------------------
            var florHire = FarmSceneBuilder.BuildHire(florist, "Florist", "florist", FloristHireAt,
                price: FloristHirePrice, pickup: counter, dropoff: counter,
                color: new Color(1.00f, 0.62f, 0.74f), feePerDelivery: 0d, beacon: null,
                idPrefix: "town.hire.", smallestCard: new Vector2(1.8f, 1.8f));
            Dress(florHire.revealOnUnlock[0].transform, "Fl_Florist", new[] { 0, 1, 2, 4, 5 });

            var gardHire = FarmSceneBuilder.BuildHire(florist, "Gardener", "gardener", GardenerHireAt,
                price: GardenerHirePrice, pickup: well, dropoff: bedSquares[0],
                color: new Color(0.46f, 0.78f, 0.42f), feePerDelivery: 0d, beacon: null,
                idPrefix: "town.hire.", smallestCard: new Vector2(1.8f, 1.8f));
            var gardener = gardHire.revealOnUnlock[0].GetComponent<WorkerAgent>();
            Dress(gardener.transform, "Fl_Gardener", new[] { 0, 1, 2 });
            // One seed or can at a time: it takes what a bed wants and nothing else, so a stack of three
            // cans would be left in its arms for a bed that is already wet.
            gardener.GetComponent<Tycoon.Player.CarryStack>().capacity = 1;
            // Per bed three jobs (WorkerAgent.ChooseBed): water it from the well, plant it from the seed
            // box, harvest it (the square is both ends: the work is standing in it).
            var routes = new List<StockRoute>();
            foreach (var square in bedSquares)
            {
                routes.Add(new StockRoute { pickup = well, dropoff = square });
                routes.Add(new StockRoute { pickup = seeds, dropoff = square });
                routes.Add(new StockRoute { pickup = square, dropoff = square });
            }
            gardener.routes = routes.ToArray();
            // The gardener is offered once the florist is hired.
            florHire.revealOnUnlock = new[] { florHire.revealOnUnlock[0], gardHire.gameObject };
            gardHire.gameObject.SetActive(false);

            // ---- the purchases, last first -----------------------------------------------------
            var gateSun = FarmSceneBuilder.Gate(florist, "town.florist.unlock.sunflowers", "UnlockSunflowers",
                GateBetween(2), new Vector2(2.4f, 2.0f), "Sunflowers", SunflowersPrice,
                lines[2].Group, florHire.gameObject);
            var gateHyd = FarmSceneBuilder.Gate(florist, "town.florist.unlock.hydrangeas", "UnlockHydrangeas",
                GateBetween(3), new Vector2(2.4f, 2.0f), "Hydrangeas", HydrangeasPrice,
                lines[3].Group, gateSun.gameObject);
            var gateTulip = FarmSceneBuilder.Gate(florist, "town.florist.unlock.tulips", "UnlockTulips",
                GateBetween(1), new Vector2(2.4f, 2.0f), "Tulips", TulipsPrice,
                lines[1].Group, gateHyd.gameObject);
            foreach (var gate in new[] { gateSun, gateHyd, gateTulip }) gate.icon = SquareIcon.Buy;

            // ---- the renovation: litter, repair + paint, open ------------------------------------
            BuildRenovation(florist, shell, items, shop, lines[0].Group, wellGroup, seedGroup, gateTulip);
            return gardHire;
        }

        /// <summary>Where a flower's purchase square goes: between its two beds.</summary>
        private static Vector3 GateBetween(int flower) =>
            new Vector3((BedX[flower][0] + BedX[flower][1]) * 0.5f, 0f, BedZ[flower]);

        // ---- renovation (the supermarket's, MarketBuilder.BuildRenovation) -----------------------------

        private static void BuildRenovation(Transform florist, Shell shell, FarmSceneBuilder.Items items, GameObject shop,
            GameObject roses, GameObject well, GameObject seedBox, UnlockStation tulips)
        {
            // The bin the litter goes to.
            LevelBuildKit.BuildBin("town.florist.bin", "Bin", florist, BinAt, squareToSouth: true);

            // The heaps: bags of trash in a buffer, a collect square on it, and the litter that shrinks as
            // the buffer empties. Hired hands have no business with it: it is the player's to carry out.
            var heaps = new List<ItemBuffer>();
            for (int i = 0; i < TrashPositions.Length; i++)
            {
                var litter = BuildTrashPile(florist, $"TrashPile_{i + 1}", TrashPositions[i], 41 + i * 7);

                var heap = LevelBuildKit.Buffer($"town.florist.trash.{i + 1}", $"Trash{i + 1}", florist,
                    TrashPositions[i], items.Trash, BagsPerPile, starting: BagsPerPile);
                heaps.Add(heap);

                var take = LevelBuildKit.Station<CollectStation>($"town.florist.trashcollect.{i + 1}",
                    $"CollectTrash{i + 1}", florist, TrashPositions[i], PileSquare, "Trash",
                    new Color(0.7f, 0.74f, 0.78f), smallestCard: PileSquare);
                take.source = heap;
                take.workerCompatible = false;

                var clear = heap.gameObject.AddComponent<ClearablePile>();
                clear.litter = litter;
                clear.square = take.gameObject;
            }

            // Repair puts back everything that does work: an ordinary gate, paid for by standing in it.
            var repair = FarmSceneBuilder.Gate(florist, "town.florist.unlock.repair", "UnlockRepair", RepairAt, RepairSize,
                RepairSize, "Repair", RepairPrice, shell.Working);
            repair.icon = SquareIcon.Fix;
            repair.hideOnUnlock = new[] { shell.Broken };

            // Paint is a menu, not a gate: the first confirmed job is the renovation step and the square
            // stays, so the look can be changed for a small fee whenever the player likes.
            var decorHolder = new GameObject("Decor");
            decorHolder.transform.SetParent(florist, false);
            LevelBuildKit.Identify(decorHolder, "town.florist.decor");
            var decor = decorHolder.AddComponent<MarketDecor>();
            decor.grubby = new[] { shell.Grubby };
            decor.painted = new[] { shell.Painted };
            decor.surfaces = shell.Surfaces.ToArray();
            decor.patterns = PatternFactory.FloristPatterns;
            decor.useStartingLook = true;
            decor.startingLook = new DecorLook
            {
                pattern = PatternFactory.Plain,
                primary = new Color(1.00f, 0.95f, 0.88f),
                secondary = new Color(1.00f, 0.80f, 0.86f),
                tertiary = new Color(1.00f, 0.74f, 0.82f),
                floorKind = PatternFactory.FloorSolid,
                floorColor = new Color(0.90f, 0.76f, 0.56f),
                woodShade = 0.3f,
            };

            var paint = LevelBuildKit.Station<PaintStation>("town.florist.paint", "Paint", florist, PaintAt, PaintSize,
                "Paint", new Color(0.95f, 0.6f, 0.55f), smallestCard: PaintSize);
            paint.mode = Tycoon.Stations.InteractionMode.Task;
            paint.taskDuration = 0.9f;
            paint.decor = decor;
            paint.firstPrice = FarmSceneBuilder.Price(FirstPaintPrice);
            paint.changePrice = FarmSceneBuilder.Price(RepaintPrice);

            // OPEN: a free chore, and what it brings in is the working business - the counter and its
            // queue, the first beds, the well and the seed box, and the offer of the next purchase.
            var open = LevelBuildKit.Station<ChoreStation>("town.florist.chore.open", "Open", florist, OpenAt, OpenSize,
                "Open", new Color(0.55f, 0.92f, 0.55f), smallestCard: OpenSize);
            open.icon = SquareIcon.Open;
            open.completionsNeeded = 1;
            open.taskDuration = 2f;
            open.doneMessage = "OPEN!";
            open.revealOnDone = new[] { shop, roses, well, seedBox, tulips.gameObject };
            foreach (var go in open.revealOnDone) go.SetActive(false);
            decor.openStep = open;

            // The order, in two links. Carry every bag out and the two steps appear; do both of those and
            // the door opens. Clearing is free, so a player with nothing can always start; nothing here
            // can leave them stuck.
            var progress = new GameObject("Progress");
            progress.transform.SetParent(florist, false);

            var afterClearing = progress.AddComponent<RevealWhenAll>();
            afterClearing.emptied = heaps.ToArray();
            afterClearing.reveal = new[] { repair.gameObject, paint.gameObject };
            repair.gameObject.SetActive(false);
            paint.gameObject.SetActive(false);

            var afterWork = progress.AddComponent<RevealWhenAll>();
            afterWork.unlocks = new[] { repair };
            afterWork.painted = new[] { paint };
            afterWork.reveal = new[] { open.gameObject };
            open.gameObject.SetActive(false);
        }

        /// <summary>A heap of bags, boxes and bottles with a few dead flowers in it. Returns the pieces, for ClearablePile.</summary>
        private static GameObject[] BuildTrashPile(Transform parent, string name, Vector3 position, int seed)
        {
            var pile = new GameObject(name);
            pile.transform.SetParent(parent, false);
            pile.transform.localPosition = position;

            var random = new System.Random(seed);
            var materials = new[]
            {
                LevelBuildKit.Mat("Trash_Bag", new Color(0.18f, 0.2f, 0.19f)),
                LevelBuildKit.Mat("Trash_Cardboard", new Color(0.62f, 0.49f, 0.32f)),
                LevelBuildKit.Mat("Trash_Bottle", new Color(0.3f, 0.5f, 0.38f)),
                LevelBuildKit.Mat("Trash_Paper", new Color(0.8f, 0.78f, 0.7f)),
            };

            var pieces = new GameObject[9];
            for (int i = 0; i < pieces.Length; i++)
            {
                float x = ((float)random.NextDouble() - 0.5f) * 1.3f;
                float z = ((float)random.NextDouble() - 0.5f) * 1.1f;
                float size = 0.25f + (float)random.NextDouble() * 0.35f;
                float yaw = (float)random.NextDouble() * 180f;

                if (i % 4 == 3)
                {
                    pieces[i] = FloristModels.DeadFlowers(pile.transform, new Vector3(x, 0.02f, z), yaw, 1.1f);
                    pieces[i].name = $"Litter_{i}";
                    continue;
                }

                pieces[i] = LevelBuildKit.Box($"Litter_{i}", pile.transform,
                    new Vector3(x, size * 0.5f, z), new Vector3(size, size * 0.8f, size * 1.1f),
                    materials[i % materials.Length], castShadow: false, rot: new Vector3(0f, yaw, 0f));
            }
            return pieces;
        }

        // ---- the building and the meadow ---------------------------------------------------------

        /// <summary>
        /// Everything that is there from the day the town opens, in its sets: the PERMANENT building (the
        /// meadow ground and fence, the stepping stones, and the colliders of the walls, counter and stands -
        /// built once, so nothing that blocks anyone blinks in or out), the two COATS (grubby and painted:
        /// floor, low walls with their caps, door mats, grime) and the fittings (the counter and stands that
        /// REPAIR brings back, and the wrecks it replaces). One audit group, so the walls and the stands
        /// standing against them are one building.
        /// </summary>
        private static Shell BuildShell(Transform florist)
        {
            var set = new Shell();
            var shell = new GameObject("Building").transform;
            shell.SetParent(florist, false);
            set.Building = shell;

            set.Grubby = new GameObject("Grubby");
            set.Grubby.transform.SetParent(florist, false);
            set.Painted = new GameObject("Painted");
            set.Painted.transform.SetParent(florist, false);
            set.Working = new GameObject("Working");
            set.Working.transform.SetParent(florist, false);
            set.Broken = new GameObject("Broken");
            set.Broken.transform.SetParent(florist, false);
            var grubby = set.Grubby.transform;
            var painted = set.Painted.transform;
            var working = set.Working.transform;
            var broken = set.Broken.transform;

            var floor = LevelBuildKit.Mat("Florist_Floor", new Color(0.90f, 0.76f, 0.56f), 0.3f);
            var meadow = LevelBuildKit.Mat("Florist_Meadow", new Color(0.50f, 0.76f, 0.34f), 0.05f);
            var wall = LevelBuildKit.Mat("Florist_Wall", new Color(1.00f, 0.95f, 0.88f), 0.4f);
            var cap = LevelBuildKit.Mat("Florist_WallCap", new Color(1.00f, 0.74f, 0.82f), 0.4f);
            var dirtyFloor = LevelBuildKit.Mat("Florist_FloorDirty", new Color(0.33f, 0.31f, 0.28f), 0.1f);
            var dirtyWall = LevelBuildKit.Mat("Florist_WallDirty", new Color(0.52f, 0.47f, 0.41f), 0.1f);
            var dirtyCap = LevelBuildKit.Mat("Florist_WallCapDirty", new Color(0.45f, 0.40f, 0.36f), 0.1f);
            var matIn = LevelBuildKit.Mat("Florist_DoorMat", new Color(0.46f, 0.78f, 0.52f), 0.2f);
            var matOut = LevelBuildKit.Mat("Florist_DoorMatOut", new Color(0.98f, 0.66f, 0.30f), 0.2f);
            var path = LevelBuildKit.Mat("Florist_Path", new Color(0.88f, 0.84f, 0.76f), 0.3f);

            float w = ShopEast - ShopWest, d = ShopNorth - ShopSouth;
            var floorCentre = new Vector3(MidX, 0.03f, (ShopSouth + ShopNorth) * 0.5f);
            var floorSize = new Vector3(w + 0.2f, 0.06f, d + 0.2f);
            // The floor is the painted coat's; the paint menu's FLOOR tab changes it.
            var floorBox = LevelBuildKit.Box("ShopFloor", painted, floorCentre, floorSize, floor, castShadow: false);
            set.Surfaces.Add(new DecorSurface
            {
                renderer = floorBox.GetComponent<Renderer>(),
                kind = DecorSurface.Kind.Floor,
                length = floorSize.x,
                height = floorSize.z,
                origin = new Vector2(ShopWest - 0.1f, ShopSouth - 0.1f),
            });
            LevelBuildKit.Box("DirtyFloor", grubby, floorCentre, floorSize, dirtyFloor, castShadow: false);
            LevelBuildKit.Box("MeadowGround", shell, new Vector3(MidX, 0.02f, (ShopNorth + MeadowNorth) * 0.5f),
                new Vector3(w + 0.2f, 0.04f, MeadowNorth - ShopNorth), meadow, castShadow: false);

            void Wall(float x0, float x1, float z0, float z1)
            {
                var c = new Vector3((x0 + x1) * 0.5f, WallHeight * 0.5f, (z0 + z1) * 0.5f);
                var s = new Vector3(Mathf.Max(0.2f, x1 - x0), WallHeight, Mathf.Max(0.2f, z1 - z0));
                var capAt = c + Vector3.up * (WallHeight * 0.5f + 0.03f);
                var capSize = new Vector3(s.x + 0.04f, 0.07f, s.z + 0.04f);

                // The painted coat: "Wall" is the name the audit measures; the grubby set is named
                // differently because both coats stand on the same ground and must not be counted twice.
                var piece = LevelBuildKit.Box("Wall", painted, c, s, wall, castShadow: false);
                var capPiece = LevelBuildKit.Box("WallCap", painted, capAt, capSize, cap, castShadow: false);
                LevelBuildKit.Box("DirtyWall", grubby, c, s, dirtyWall, castShadow: false);
                LevelBuildKit.Box("DirtyCap", grubby, capAt, capSize, dirtyCap, castShadow: false);

                // The pattern is tiled by the piece's real length and starts where the piece starts in the
                // shop's own grid, so it carries on unbroken from one wall to the next.
                bool alongX = s.x >= s.z;
                set.Surfaces.Add(new DecorSurface
                {
                    renderer = piece.GetComponent<Renderer>(),
                    kind = DecorSurface.Kind.Wall,
                    length = alongX ? s.x : s.z,
                    height = WallHeight,
                    origin = new Vector2(alongX ? c.x - s.x * 0.5f : c.z - s.z * 0.5f, 0f),
                });
                set.Surfaces.Add(new DecorSurface { renderer = capPiece.GetComponent<Renderer>(), kind = DecorSurface.Kind.Trim });

                // Taller than it looks, so nobody steps over a knee-high wall; carved out of the navmesh.
                Solid(shell, "WallCollision", new Vector3(c.x, 0.6f, c.z), new Vector3(s.x, 1.2f, s.z));
            }

            // South: entrance bottom-left, exit bottom-right.
            Wall(ShopWest - 0.1f, EntranceFrom, ShopSouth - 0.1f, ShopSouth + 0.1f);
            Wall(EntranceTo, ExitFrom, ShopSouth - 0.1f, ShopSouth + 0.1f);
            Wall(ExitTo, ShopEast + 0.1f, ShopSouth - 0.1f, ShopSouth + 0.1f);
            // The sides, and the back with the door to the meadow.
            Wall(ShopWest - 0.1f, ShopWest + 0.1f, ShopSouth, ShopNorth);
            Wall(ShopEast - 0.1f, ShopEast + 0.1f, ShopSouth, ShopNorth);
            Wall(ShopWest - 0.1f, BackDoorFrom, ShopNorth - 0.1f, ShopNorth + 0.1f);
            Wall(BackDoorTo, ShopEast + 0.1f, ShopNorth - 0.1f, ShopNorth + 0.1f);

            LevelBuildKit.Box("DoorMatIn", painted, new Vector3((EntranceFrom + EntranceTo) * 0.5f, 0.07f, ShopSouth + 0.6f),
                new Vector3(1.6f, 0.03f, 0.9f), matIn, castShadow: false);
            LevelBuildKit.Box("DoorMatOut", painted, new Vector3((ExitFrom + ExitTo) * 0.5f, 0.07f, ShopSouth + 0.6f),
                new Vector3(1.6f, 0.03f, 0.9f), matOut, castShadow: false);
            LevelBuildKit.Box("BackDoorMat", painted, new Vector3((BackDoorFrom + BackDoorTo) * 0.5f, 0.07f, ShopNorth - 0.5f),
                new Vector3(1.5f, 0.03f, 0.7f), matOut, castShadow: false);

            // The counter across the middle (its staff side north), solid.
            var counter = FloristModels.Counter(working, CounterAt + Vector3.up * 0.06f, 0f);
            counter.name = "Stall";
            Solid(shell, "CounterCollision", CounterAt + new Vector3(0f, 0.6f, 0f), new Vector3(3.36f, 1.2f, 1.0f));
            FloristModels.BrokenCounter(broken, CounterAt + Vector3.up * 0.06f, 0f);

            // The stands: the square one in the middle, one along each side wall, angled ones in the corners.
            var centre = FloristModels.CentreStand(working, CentreStandAt + Vector3.up * 0.06f, 0f);
            centre.name = "Stall";
            Solid(shell, "CentreStandCollision", CentreStandAt + new Vector3(0f, 0.5f, 0f), new Vector3(2.0f, 1.0f, 2.0f));
            FloristModels.BrokenCentreStand(broken, CentreStandAt + Vector3.up * 0.06f, 0f);

            float wallZ = (ShopSouth + ShopNorth) * 0.5f - 0.3f;
            var westStand = FloristModels.WallStand(working, new Vector3(ShopWest + 0.7f, 0.06f, wallZ), -90f);
            westStand.name = "Stall";
            Solid(shell, "WestStandCollision", new Vector3(ShopWest + 0.7f, 0.5f, wallZ), new Vector3(1.3f, 1.0f, 2.2f));
            FloristModels.BrokenWallStand(broken, new Vector3(ShopWest + 0.7f, 0.06f, wallZ), -90f);
            var eastStand = FloristModels.WallStand(working, new Vector3(ShopEast - 0.7f, 0.06f, wallZ), 90f);
            eastStand.name = "Stall";
            Solid(shell, "EastStandCollision", new Vector3(ShopEast - 0.7f, 0.5f, wallZ), new Vector3(1.3f, 1.0f, 2.2f));
            FloristModels.BrokenWallStand(broken, new Vector3(ShopEast - 0.7f, 0.06f, wallZ), 90f);

            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var corner = new Vector3(sx < 0f ? ShopWest : ShopEast, 0f, sz < 0f ? ShopSouth : ShopNorth);
                    var front = new Vector3(-sx * 0.7071f, 0f, -sz * 0.7071f);
                    var at = corner + front * 1.4f;
                    float yaw = Mathf.Atan2(-front.x, -front.z) * Mathf.Rad2Deg;
                    var stand = FloristModels.CornerStand(working, at + Vector3.up * 0.06f, yaw);
                    stand.name = "Stall";
                    var solid = Solid(shell, "CornerStandCollision", at + new Vector3(0f, 0.5f, 0f), new Vector3(1.5f, 1.0f, 1.2f));
                    solid.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    // Two corners are wrecked; the other two only hold rubbish (their heaps).
                    if (sx * sz > 0f) FloristModels.BrokenWallStand(broken, at + Vector3.up * 0.06f, yaw, corner: true);
                }

            BuildGrime(grubby);

            // The meadow: a fence on its three open sides (the shop closes the fourth), the stepping
            // stones from the back door to the well.
            var runs = new[]
            {
                new LevelBuildKit.FenceRun(ShopWest, ShopNorth, ShopWest, MeadowNorth),
                new LevelBuildKit.FenceRun(ShopWest, MeadowNorth, ShopEast, MeadowNorth),
                new LevelBuildKit.FenceRun(ShopEast, MeadowNorth, ShopEast, ShopNorth),
            };
            LevelBuildKit.BuildFence(shell, runs, meshName: "FloristFenceLine", objectName: "MeadowFence");
            Solid(shell, "FenceWest", new Vector3(ShopWest, 0.6f, (ShopNorth + MeadowNorth) * 0.5f), new Vector3(0.25f, 1.2f, MeadowNorth - ShopNorth));
            Solid(shell, "FenceEast", new Vector3(ShopEast, 0.6f, (ShopNorth + MeadowNorth) * 0.5f), new Vector3(0.25f, 1.2f, MeadowNorth - ShopNorth));
            Solid(shell, "FenceNorth", new Vector3(MidX, 0.6f, MeadowNorth), new Vector3(ShopEast - ShopWest, 1.2f, 0.25f));

            for (int i = 0; i < 3; i++)
                LevelBuildKit.Cylinder("Stone", shell, new Vector3((BackDoorFrom + BackDoorTo) * 0.5f - i * 0.35f, 0.045f, ShopNorth + 0.6f + i * 0.6f),
                    new Vector3(0.7f, 0.02f, 0.5f), path, false);

            set.Grubby.SetActive(true);
            set.Painted.SetActive(false);
            set.Working.SetActive(false);
            set.Broken.SetActive(true);
            return set;
        }

        /// <summary>Dirt patches on the floor, damp stains up the walls and weeds by the doors: the grubby coat's extras.</summary>
        private static void BuildGrime(Transform grubby)
        {
            var dirt = LevelBuildKit.Mat("Florist_Dirt", new Color(0.24f, 0.21f, 0.18f), 0.05f);
            var stain = LevelBuildKit.Mat("Florist_Stain", new Color(0.40f, 0.36f, 0.30f), 0.05f);
            var weed = LevelBuildKit.Mat("Florist_Weed", new Color(0.40f, 0.52f, 0.22f), 0.1f);
            var rnd = new System.Random(77);
            float cx = MidX, cz = (ShopSouth + ShopNorth) * 0.5f;

            for (int i = 0; i < 14; i++)
            {
                float x = cx + ((float)rnd.NextDouble() - 0.5f) * 10.5f, z = cz + ((float)rnd.NextDouble() - 0.5f) * 7.8f;
                var o = LevelBuildKit.Cylinder("Dirt", grubby, new Vector3(x, 0.065f, z),
                    new Vector3(0.8f + (float)rnd.NextDouble() * 1.3f, 0.01f, 0.6f + (float)rnd.NextDouble() * 0.9f), dirt, false);
                o.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
            }
            for (int i = 0; i < 4; i++)
                LevelBuildKit.Box("Stain", grubby, new Vector3(cx - 4.5f + i * 3.2f, 0.35f, ShopNorth - 0.115f),
                    new Vector3(0.9f + (0.4f * i) % 1.2f, 0.45f, 0.01f), stain, castShadow: false);
            LevelBuildKit.Box("StainE", grubby, new Vector3(ShopEast - 0.115f, 0.30f, 1.2f), new Vector3(0.01f, 0.4f, 1.6f), stain, castShadow: false);
            LevelBuildKit.Box("StainW", grubby, new Vector3(ShopWest + 0.115f, 0.30f, -0.2f), new Vector3(0.01f, 0.4f, 1.2f), stain, castShadow: false);

            foreach (var wp in new[]
                     {
                         new Vector3(cx - 3f, 0f, ShopSouth + 0.6f), new Vector3(cx + 3f, 0f, ShopSouth + 0.7f),
                         new Vector3(cx - 5.2f, 0f, ShopNorth - 0.8f), new Vector3(cx + 4.6f, 0f, ShopNorth - 0.6f),
                         new Vector3(cx + 0.8f, 0f, ShopNorth - 0.5f),
                     })
                for (int k = 0; k < 5; k++)
                    LevelBuildKit.Box("Weed", grubby, wp + new Vector3((k - 2) * 0.12f, 0.2f, ((k * 37) % 5 - 2) * 0.06f),
                        new Vector3(0.05f, 0.4f + 0.1f * (k % 3), 0.05f), weed, castShadow: false,
                        rot: new Vector3((k % 2) * 14f - 7f, k * 40f, (k % 3) * 10f - 10f));

            FloristModels.DeadFlowers(grubby, new Vector3(cx + 1.8f, 0.06f, 2.9f), 30f, 1.0f);
            FloristModels.DeadFlowers(grubby, new Vector3(cx - 4.2f, 0.06f, ShopSouth + 1.5f), 110f, 1.0f);
            FloristModels.DeadFlowers(grubby, new Vector3(cx + 2.2f, 0.06f, ShopSouth + 1.3f), 200f, 0.9f);
        }

        // ---- one flower: its stock, its two beds, its buckets on the stands --------------------

        private static FlowerLine BuildFlower(Transform florist, Transform fittings, int f, ItemDefinition flower,
            FarmSceneBuilder.Items items)
        {
            var line = new FlowerLine();
            string id = FloristModels.FlowerIds[f];
            var group = new GameObject(FlowerName[f]);
            group.transform.SetParent(florist, false);
            line.Group = group;

            // The stock: behind the counter, unseen (the stands and the beds are what show).
            line.Stock = LevelBuildKit.Buffer($"town.florist.stock.{id}", "Stock", group.transform,
                CounterAt + new Vector3(0f, 0f, 0.4f), flower, StockCapacity);
            // From the moment this flower is bought, customers may ask for it.
            line.Stock.gameObject.AddComponent<ProductOffer>().item = flower;

            for (int b = 0; b < 2; b++)
            {
                var at = new Vector3(BedX[f][b], 0f, BedZ[f]);
                var bedGroup = new GameObject($"Bed_{id}_{b}");
                bedGroup.transform.SetParent(group.transform, false);
                LevelBuildKit.Identify(bedGroup, $"town.florist.bed.{id}{b}");

                // One look per stage, only the current one on: no swapping while a bed grows.
                var looks = new GameObject("Stall");
                looks.transform.SetParent(bedGroup.transform, false);
                var models = new GameObject[StageLooks.Length];
                for (int s = 0; s < models.Length; s++)
                {
                    models[s] = FloristModels.BedStage((Fl)f, StageLooks[s].stage, StageLooks[s].wet, looks.transform,
                        at + Vector3.up * 0.04f, 0f);
                    models[s].SetActive(s == 0);
                }

                var square = LevelBuildKit.Station<BedStation>($"town.florist.bed.{id}{b}.square", "TendBed",
                    bedGroup.transform, at, BedSquare, "Bed", new Color(0.55f, 0.85f, 0.45f), smallestCard: BedSquare);
                square.taskDuration = BedTaskSeconds;

                var bubble = new GameObject("Bubble");
                bubble.transform.SetParent(bedGroup.transform, false);
                bubble.transform.localPosition = at;
                var bubbleComponent = bubble.AddComponent<BedBubble>();

                var bed = bedGroup.AddComponent<FlowerBed>();
                bed.stock = line.Stock;
                bed.flowersPerHarvest = FlowersPerHarvest;
                bed.secondsPerSpell = SpellSeconds[f];
                bed.seed = items.Seed;
                bed.water = items.Water;
                bed.square = square;
                bed.looks = models;
                bed.bubble = bubbleComponent;
                square.bed = bed;
                line.Beds.Add(bed);
            }

            // The stands' buckets of this flower are part of its purchase: hidden until it is bought.
            var display = new GameObject("Display");
            display.transform.SetParent(group.transform, false);
            var fills = new List<Transform>();
            foreach (var t in fittings.GetComponentsInChildren<Transform>(true))
                if (t.name == "Fill_" + id) fills.Add(t);
            foreach (var t in fills) t.SetParent(display.transform, true);

            group.SetActive(false);
            return line;
        }

        // ---- the counter ---------------------------------------------------------------------------

        private static BouquetCounter BuildCounter(Transform shop, FarmSceneBuilder.Items items, ItemDefinition[] flowers,
            FlowerLine[] lines)
        {
            var counter = LevelBuildKit.Station<BouquetCounter>("town.florist.counter", "Counter", shop, ServeAt, ServeSize,
                "Bouquet", new Color(0.45f, 0.85f, 0.6f), smallestCard: ServeSize);
            counter.sells = flowers;
            counter.stock = new[] { lines[0].Stock, lines[1].Stock, lines[2].Stock, lines[3].Stock };
            counter.priceMultiplier = 1f;
            counter.bouquetIcon = items.Bouquet;

            var cash = LevelBuildKit.BuildCashPoint("town.florist", shop, CashAt, new Vector2(1.8f, 1.6f));
            counter.pile = cash.pile;

            var queue = shop.gameObject.AddComponent<CustomerQueue>();
            queue.customerTemplate = LevelBuildKit.BuildCustomerTemplate(shop);
            // Smaller bubbles than the default 0.01: a bouquet order is the widest thing in the game.
            var bubble = queue.customerTemplate.GetComponent<OrderBubble>();
            bubble.worldScale = BubbleScale;
            bubble.height = 2.1f;
            queue.spawnPoint = Marker(shop, "FloristSpawn", SpawnPoint);
            queue.exitPoint = Marker(shop, "FloristExit", ExitPoint);
            queue.counterPoint = Marker(shop, "FloristCounterPoint", CounterAt);
            queue.slots = new Transform[QueueSlots];
            for (int i = 0; i < QueueSlots; i++) queue.slots[i] = Marker(shop, $"FloristSlot_{i}", Slots[i]);
            queue.entryRoute = Markers(shop, "FloristIn", EntryRoute);
            queue.exitRoute = Markers(shop, "FloristOut", ExitRoute);

            queue.register = counter;
            counter.queue = queue;
            queue.bouquets = true;
            queue.maxBouquetLines = 3;
            queue.maxPerFlower = 3;
            queue.maxBouquetFlowers = 6;
            queue.spawnIntervalSeconds = CustomerIntervalSeconds;
            queue.demandPerCapacity = DemandPerFlower;
            queue.maxDemand = 3f;
            return counter;
        }

        // ---- helpers -------------------------------------------------------------------------------

        /// <summary>Puts the approved florist/gardener colours (material ASSETS from the art pass) on a worker.</summary>
        internal static void Dress(Transform worker, string prefix, int[] slots)
        {
            var renderer = worker.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer == null) return;
            var mats = renderer.sharedMaterials;
            foreach (int slot in slots)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/_Project/Materials/{prefix}_{slot}.mat");
                if (m != null && slot < mats.Length) mats[slot] = m;
                else Debug.LogWarning($"[FloristBuilder] no material {prefix}_{slot}");
            }
            renderer.sharedMaterials = mats;
        }

        /// <summary>An invisible solid: a collider the player is stopped by and a navmesh obstacle that carves it.</summary>
        internal static GameObject Solid(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.AddComponent<BoxCollider>().size = size;
            var obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = size;
            obstacle.carving = true;
            return go;
        }

        internal static Transform Marker(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        internal static Transform[] Markers(Transform parent, string prefix, Vector3[] points)
        {
            var result = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++) result[i] = Marker(parent, $"{prefix}_{i}", points[i]);
            return result;
        }
    }
}
