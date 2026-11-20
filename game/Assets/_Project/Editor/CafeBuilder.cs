using System.Collections.Generic;
using Tycoon.Config;
using Tycoon.Customers;
using Tycoon.Player;
using Tycoon.Stations;
using Tycoon.UI;
using Tycoon.Upkeep;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The cafe: the third business on the town street, east of the salon, built from the approved layout
    /// (cafe-layout.png) and art (<see cref="FloristModels"/> cafe models, <see cref="IconFactory.CafeIcon"/>, <see cref="LatteMinigame"/>).
    ///
    ///     north wall   the small FRIDGE (the coffee machine's milk), the coffee MACHINE, the UPGRADE square (premium machine),
    ///                  the display CASE and, below it, TAKE CAKE
    ///     back kitchen east of a low partition with a pass: the EGG FRIDGE (the oven's eggs) and the OVEN, the DISHWASHER, the baker's square
    ///     hall         tables T1..T6, each with a SERVE square (north) and a CLEAR square (south) and a spare chair; the cash pile by the street door
    ///     south        street door (in) .......... exit door (out); two waiting stools and the street garden G1..G4 on the pavement
    ///     east         the back door onto the lane: the milk and egg crates with their UNPACK squares, and the delivery van
    ///
    /// How it works, all from the existing parts:
    ///   - customers (one <see cref="CustomerQueue"/>: seated waiting room = two stools, <c>autoSeat</c>, <c>cafeOrders</c>) arrive from the
    ///     EAST end of the street, sit down on a stool and sit down again at the first free, CLEAN table by themselves (no NEXT square);
    ///     each wants a coffee (or the premium coffee once it is made) and with a chance a slice of cake as well - a two-line order,
    ///     served when both are delivered (<see cref="CustomerAgent.BeginOrder"/>, as a bouquet is);
    ///   - a table's SERVE square is a <see cref="ChairServiceStation"/> with <c>serveFromCarry</c>: the player or the barista hands over the
    ///     goods they CARRY, and the till's price goes to the one cash pile;
    ///   - the coffee machine is a <see cref="ProducerMachine"/> fed by the small fridge (milk only: the beans refill by themselves);
    ///     the oven another, fed by the egg fridge; the case is a buffer (DepositStation / CollectStation) between the baker and the waiter;
    ///   - the milk and eggs arrive free (<see cref="SupplyFeed"/> into two crates) and are UNPACKED by hand into the fridges; a cosmetic
    ///     <see cref="DeliveryVan"/> drives in now and then and touches no buffer (nothing is taken from the farm);
    ///   - a served table keeps a dirty cup (<see cref="HairMess"/>): nobody is sent to it until its CLEAR square has been emptied (free), and
    ///     the cups go to the <see cref="Dishwasher"/> through a DepositStation that takes only dirty cups;
    ///   - the premium upgrade swaps the machine and opens the <see cref="LatteStation"/> (the latte-art minigame, with a tip).
    ///
    /// It starts DERELICT like the florist and the salon (litter -> REPAIR + PAINT -> OPEN) and then one purchase at a time, each revealing
    /// the next: Table 3, Barista, Cakes, Waiter, Table 4, Baker, Premium machine, the four garden tables, Tables 5 and 6.
    /// Everything is a child of the town root; the whole cafe appears when the salon's hairdresser 4 is hired (<see cref="TownBuilder"/>).
    /// New save ids only, all under "town.cafe." (and "town.hire." for the staff).
    /// </summary>
    internal static class CafeBuilder
    {
        // ---- prices (the econ sim, plans/econ/market_sim.py CAFE) -------------------------------------
        private const double RepairPrice = 4000d;
        private const double FirstPaintPrice = 2000d;
        private const double RepaintPrice = 200d;
        private const double Table3Price = 11000d, Table4Price = 15000d, Table5Price = 19000d, Table6Price = 21000d;
        private const double BaristaPrice = 13000d, WaiterPrice = 13000d, BakerPrice = 15000d;
        private const double CakesPrice = 14000d;
        private const double PremiumPrice_ = 18000d;
        private static readonly double[] GardenPrice = { 14000d, 16000d, 19000d, 21000d };

        // What a table sells for (FarmSceneBuilder.CreateItems reads these).
        internal const double CoffeePrice = 55d, PremiumPrice = 110d, CakePrice = 85d;

        // ---- customers ---------------------------------------------------------------------------------
        private const float CustomerIntervalSeconds = 30f;
        private const float DemandPerCapacity = 0.25f;
        private const float MaxDemand = 12f;
        private const int TableDemand = 5;
        private const int StaffDemand = 3;
        private const float CakeChance = 0.35f;
        private const float BubbleScale = 0.0055f;

        // ---- production --------------------------------------------------------------------------------
        private const float CoffeeSeconds = 4f;          // a cup, from the milk in the small fridge
        private const int FridgeCapacity = 24, FridgeStart = 6;
        private const float CakeSeconds = 10f;           // a cake, from two eggs in the egg fridge
        private const int EggsPerCake = 2;
        private const int CaseCapacity = 8;
        private const float MilkSecondsPerUnit = 4f, EggSecondsPerUnit = 5f;     // the free delivery
        private const int CrateCapacity = 24;

        // ---- the shop (the town's grid; north +z, the street at z -3) -----------------------------------
        internal const float ShopWest = 24f, ShopEast = 38f, ShopSouth = 2f, ShopNorth = 13f;
        private const float InFrom = 27f, InTo = 29f, OutFrom = 33f, OutTo = 35f;       // street doors, south wall
        private const float BackFrom = 7.6f, BackTo = 9.6f;                              // the back door, east wall
        private const float PassFrom = 7.4f, PassTo = 9.6f;                              // the kitchen's pass (partition x 33)
        private const float KitchenWest = 33f, KitchenSouth = 6.95f;
        private const float WallHeight = 0.7f, Floor = 0.06f, Pave = 0.05f;

        // The back lane (the delivery van's road, TownBuilder.BuildGround) and its parking bay beside the back door.
        internal const float LaneWest = 42.6f, LaneEast = 45.0f, LaneNorth = 13.6f, BayWest = 40.4f, BayZ0 = 10.4f;

        /// <summary>Everything the cafe stands on, with the garden and the back lane: for the town's grass keep-clear.</summary>
        internal static readonly Rect Bounds = new Rect(23.2f, -1.7f, 45.2f - 23.2f, 14.9f + 1.7f);

        // Squares (the layout picture's numbers).
        private static readonly Vector2 SqStd = new Vector2(1.8f, 1.6f);
        private static readonly Vector3 FridgeAt = new Vector3(25.0f, 0f, 11.0f);
        private static readonly Vector3 MachineAt = new Vector3(27.2f, 0f, 11.0f);
        private static readonly Vector3 UpgradeAt = new Vector3(29.1f, 0f, 11.0f);
        private static readonly Vector3 CaseAt = new Vector3(31.2f, 0f, 11.0f);
        private static readonly Vector3 TakeAt = new Vector3(31.2f, 0f, 9.65f);
        private static readonly Vector3 EggFridgeAt = new Vector3(34.3f, 0f, 11.0f);
        private static readonly Vector3 OvenAt = new Vector3(36.6f, 0f, 11.0f);
        private static readonly Vector3 DishAt = new Vector3(34.6f, 0f, 8.65f);
        private static readonly Vector3 CashAt = new Vector3(31.0f, 0f, 2.7f);
        private static readonly Vector3 BaristaHireAt = new Vector3(29.4f, 0f, 8.7f);
        private static readonly Vector3 WaiterHireAt = new Vector3(31.9f, 0f, 8.0f);
        private static readonly Vector3 BakerHireAt = new Vector3(36.6f, 0f, 8.4f);
        private static readonly Vector3 UnpackMilkAt = new Vector3(40.7f, 0f, 9.4f);
        private static readonly Vector3 UnpackEggAt = new Vector3(40.7f, 0f, 7.6f);
        private static readonly Vector3 CrateMilkAt = new Vector3(38.9f, 0f, 9.4f);
        private static readonly Vector3 CrateEggAt = new Vector3(38.9f, 0f, 7.6f);

        // The renovation (day one only): litter, REPAIR, PAINT and OPEN stand on floor that later holds furniture.
        private static readonly Vector3[] LitterAt = { new Vector3(26.0f, 0f, 6.7f), new Vector3(28.6f, 0f, 6.7f), new Vector3(31.2f, 0f, 6.7f) };
        private static readonly Vector2 PileSquare = new Vector2(2.2f, 2.0f);
        private static readonly Vector3 RepairAt = new Vector3(34.2f, 0f, 3.2f);
        private static readonly Vector3 PaintAt = new Vector3(36.9f, 0f, 3.0f);
        private static readonly Vector3 OpenAt = new Vector3(35.8f, 0f, 5.5f);
        private static readonly Vector2 RepairSize = new Vector2(2.2f, 2.0f), OpenSize = new Vector2(2.2f, 2.0f), PaintSize = new Vector2(1.7f, 1.5f);

        // Waiting stools on the pavement between the doors, facing the street.
        private static readonly Vector3[] StoolAt = { new Vector3(30.4f, 0f, 1.1f), new Vector3(31.7f, 0f, 1.1f) };

        private static readonly Vector2 ServeSize = new Vector2(1.5f, 1.1f), GardenServeSize = new Vector2(1.5f, 1.0f), ClearSize = new Vector2(1.2f, 0.9f);

        // The street: customers arrive on the west-bound lane from the EAST end and leave on the east-bound lane.
        private static readonly Vector3 SpawnPoint = new Vector3(49.0f, 0f, -2.4f);
        private static readonly Vector3 ExitPoint = new Vector3(49.5f, 0f, -3.8f);
        private static readonly Vector3[] EntryRoute = { new Vector3(31.0f, 0f, -2.4f), new Vector3(31.0f, 0f, -0.6f) };
        private static readonly Vector3[] QueueExit = { new Vector3(31.0f, 0f, -1.2f) };
        private static readonly Vector3[] ExitRoute = { new Vector3(34.0f, 0f, -1.2f), new Vector3(34.0f, 0f, -3.8f) };

        // The delivery van: appears on the street east of the cafe, up the back lane to the door.
        private static readonly Vector3[] VanRoute =
        {
            new Vector3(47.5f, 0f, -3.0f), new Vector3(43.8f, 0f, -3.0f), new Vector3(43.8f, 0f, 10.8f), new Vector3(41.9f, 0f, 12.0f),
        };

        private class TableSpec
        {
            public string Id;                 // "T1".. / "G1"..
            public int Number;
            public float X, Z;
            public bool Garden;
            public bool EastSeat;             // the customer sits on the table's east side, facing west (garden table 4, beside the building)
            public bool Open;                 // open at OPEN (T1, T2)
            public Vector3[] Approach, Exit;
            public double Price;
            public string Label;
        }

        private static readonly TableSpec[] Tables = BuildTableSpecs();

        // The front row of tables. Its east end must leave a lane at least two workers wide between the last chair and
        // the corner of the kitchen's south wall (x 33, z 6.85) - the way from the tables' south side to the kitchen pass,
        // and for tables 5 and 6, runs through it (SquareAudit.LaneWidths). At 5.2 (and the wall at 6.7) it was 1.35 m and the staff jammed; it is 1.9 m now.
        private const float RowZ = 4.7f;
        private const float NeckZ = 5.9f;       // the middle of that lane: halfway between the chairs' north edge and the wall

        private static TableSpec[] BuildTableSpecs()
        {
            var list = new List<TableSpec>();
            float[] xs = { 25.15f, 27.45f, 29.75f, 32.05f };
            for (int i = 0; i < 4; i++)
            {
                float cx = xs[i] - 0.78f;
                list.Add(new TableSpec
                {
                    Id = "T" + (i + 1), Number = i + 1, X = xs[i], Z = RowZ, Open = i < 2,
                    Approach = new[] { new Vector3(28f, 0f, 1.6f), new Vector3(28f, 0f, 2.8f), new Vector3(cx, 0f, 2.8f), new Vector3(cx, 0f, RowZ - 1.0f) },
                    Exit = new[] { new Vector3(cx, 0f, RowZ - 1.0f), new Vector3(cx, 0f, 2.8f), new Vector3(34f, 0f, 2.8f), new Vector3(34f, 0f, 1.2f) },
                });
            }
            for (int i = 0; i < 2; i++)
            {
                float cx = xs[i] - 0.78f;
                list.Add(new TableSpec
                {
                    Id = "T" + (i + 5), Number = i + 5, X = xs[i], Z = 8.3f,
                    Approach = new[] { new Vector3(28f, 0f, 1.6f), new Vector3(28f, 0f, 2.8f), new Vector3(34f, 0f, 2.8f), new Vector3(34f, 0f, NeckZ),
                                       new Vector3(cx, 0f, NeckZ), new Vector3(cx, 0f, 7.3f) },
                    Exit = new[] { new Vector3(cx, 0f, 7.3f), new Vector3(cx, 0f, NeckZ), new Vector3(34f, 0f, NeckZ), new Vector3(34f, 0f, 2.8f),
                                   new Vector3(34f, 0f, 2.8f), new Vector3(34f, 0f, 1.2f) },
                });
            }
            float[] gx = { 25.4f, 36.4f, 39.6f, 39.6f };
            float[] gz = { 0.4f, 0.4f, 0.4f, 3.9f };
            for (int i = 0; i < 4; i++)
            {
                float cx = gx[i] - 0.78f;
                var approach = new List<Vector3> { new Vector3(31.0f, 0f, 1.6f) };
                Vector3[] exit;
                bool east = i == 3;
                if (east)
                {
                    approach.Add(new Vector3(41.2f, 0f, 1.6f));
                    approach.Add(new Vector3(41.2f, 0f, gz[i]));
                    exit = new[] { new Vector3(41.2f, 0f, gz[i]), new Vector3(41.2f, 0f, -1.2f) };
                }
                else
                {
                    approach.Add(new Vector3(cx, 0f, 1.6f));
                    approach.Add(new Vector3(cx, 0f, 1.0f));
                    exit = i == 2 ? new[] { new Vector3(37.9f, 0f, gz[i]), new Vector3(37.9f, 0f, -1.2f) } : new[] { new Vector3(cx, 0f, -1.2f) };
                }
                list.Add(new TableSpec
                {
                    Id = "G" + (i + 1), Number = i + 1, X = gx[i], Z = gz[i], Garden = true, EastSeat = east,
                    Approach = approach.ToArray(), Exit = exit, Price = GardenPrice[i],
                });
            }
            list[2].Price = Table3Price; list[3].Price = Table4Price; list[4].Price = Table5Price; list[5].Price = Table6Price;
            foreach (var t in list) t.Label = t.Garden ? "Garden " + t.Number : "Table " + t.Number;
            return list.ToArray();
        }

        private class Shell
        {
            public Transform Building;
            public GameObject Grubby, Painted, Working, Broken, BasicMachine;
            public readonly List<DecorSurface> Surfaces = new List<DecorSurface>();
        }

        private class TableParts
        {
            public TableSpec Spec;
            public GameObject Group;
            public ServiceChair Chair;
            public ChairServiceStation Serve;
            public CollectStation Clear;
        }

        internal static void EnsureGoods() => FloristModels.EnsureCafeGoods();

        // =====================================================================================================
        //  the build
        // =====================================================================================================

        internal static GameObject Build(Transform town, FarmSceneBuilder.Items items)
        {
            var root = new GameObject("Cafe").transform;
            root.SetParent(town, false);

            var shell = BuildShell(root);

            // ---- the working cafe: queue, cash, the machines, the crates ---------------------------------------
            var shop = new GameObject("Shop");
            shop.transform.SetParent(root, false);
            LevelBuildKit.Identify(shop, "town.cafe");          // the queue's reputation is saved under this

            var cash = LevelBuildKit.BuildCashPoint("town.cafe", shop.transform, CashAt, new Vector2(1.8f, 1.1f));

            var menu = new[] { items.Coffee, items.PremiumCoffee, items.Cake };
            var tables = new TableParts[Tables.Length];
            for (int i = 0; i < Tables.Length; i++) tables[i] = BuildTable(root, shell, Tables[i], items, menu, cash.pile);

            var queue = BuildQueue(shop.transform, tables, items);

            // ---- coffee: the small fridge is the machine's milk hopper; the machine makes cups from it -------
            var fridge = LevelBuildKit.Buffer("town.cafe.fridge", "CounterFridge", shop.transform, FridgeAt, items.Milk, FridgeCapacity, FridgeStart);
            var coffeeOut = LevelBuildKit.Buffer("town.cafe.coffee.out", "CoffeeOut", shop.transform, MachineAt, items.Coffee, 8);
            var machineGo = new GameObject("CoffeeMachine");
            machineGo.transform.SetParent(shop.transform, false);
            machineGo.transform.localPosition = MachineAt;
            LevelBuildKit.Identify(machineGo, "town.cafe.coffee.machine");
            var machine = machineGo.AddComponent<ProducerMachine>();
            machine.input = fridge;
            machine.output = coffeeOut;
            machine.inputPerOutput = 1;
            machine.secondsPerOutput = CoffeeSeconds;
            machine.units = 1;
            machine.maxUnits = 1;

            var fridgeSq = LevelBuildKit.Station<DepositStation>("town.cafe.fridge.deposit", "Fridge", shop.transform, FridgeAt, SqStd, "Milk",
                new Color(0.45f, 0.70f, 1f), smallestCard: SqStd);
            fridgeSq.target = fridge;
            fridgeSq.showItem = true;
            var machineSq = LevelBuildKit.Station<CollectStation>("town.cafe.coffee.collect", "Coffee", shop.transform, MachineAt, new Vector2(2.0f, 1.6f),
                "Coffee", new Color(0.95f, 0.62f, 0.25f), smallestCard: new Vector2(2.0f, 1.6f));
            machineSq.source = coffeeOut;

            // ---- the dishwasher ------------------------------------------------------------------------------
            var dishBuffer = LevelBuildKit.Buffer("town.cafe.dish", "Dishes", shop.transform, DishAt, items.DirtyCup, 8);
            var washer = shop.AddComponent<Dishwasher>();
            washer.dirty = dishBuffer;
            var dishSq = LevelBuildKit.Station<DepositStation>("town.cafe.dish.deposit", "Dishwasher", shop.transform, DishAt, new Vector2(1.6f, 1.5f),
                "Wash", new Color(0.30f, 0.76f, 0.72f), smallestCard: new Vector2(1.6f, 1.5f));
            dishSq.target = dishBuffer;
            dishSq.showItem = true;

            // ---- the free delivery: two crates, their UNPACK squares, the van -----------------------------------
            var crateMilk = LevelBuildKit.Buffer("town.cafe.crate.milk", "MilkCrate", shop.transform, CrateMilkAt, items.Milk, CrateCapacity, CrateCapacity / 2);
            var feedMilk = crateMilk.gameObject.AddComponent<SupplyFeed>();
            feedMilk.target = crateMilk;
            feedMilk.secondsPerUnit = MilkSecondsPerUnit;
            var crateEgg = LevelBuildKit.Buffer("town.cafe.crate.eggs", "EggCrate", shop.transform, CrateEggAt, items.Egg, CrateCapacity, CrateCapacity / 2);
            var feedEgg = crateEgg.gameObject.AddComponent<SupplyFeed>();
            feedEgg.target = crateEgg;
            feedEgg.secondsPerUnit = EggSecondsPerUnit;
            var milkModel = FloristModels.SupplyCrate(shop.transform, CrateMilkAt + Vector3.up * Pave, -90f, true);
            var eggModel = FloristModels.SupplyCrate(shop.transform, CrateEggAt + Vector3.up * Pave, -90f, false);
            milkModel.name = eggModel.name = "Stall";
            FloristBuilder.Solid(shop.transform, "MilkCrateCollision", CrateMilkAt + new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 0.9f));
            FloristBuilder.Solid(shop.transform, "EggCrateCollision", CrateEggAt + new Vector3(0f, 0.3f, 0f), new Vector3(0.7f, 0.6f, 0.9f));
            var unpackMilk = LevelBuildKit.Station<CollectStation>("town.cafe.unpack.milk", "UnpackMilk", shop.transform, UnpackMilkAt, new Vector2(1.8f, 1.5f),
                "Unpack", new Color(0.50f, 0.76f, 1f), smallestCard: new Vector2(1.8f, 1.5f));
            unpackMilk.source = crateMilk;
            var unpackEgg = LevelBuildKit.Station<CollectStation>("town.cafe.unpack.eggs", "UnpackEggs", shop.transform, UnpackEggAt, new Vector2(1.8f, 1.5f),
                "Unpack", new Color(0.96f, 0.84f, 0.50f), smallestCard: new Vector2(1.8f, 1.5f));
            unpackEgg.source = crateEgg;
            BuildVan(shop.transform);

            // ---- what the cafe offers: coffee from the day it opens; each table and each hire is more demand -----
            var offers = new GameObject("Offers");
            offers.transform.SetParent(shop.transform, false);
            var coffeeOffer = new GameObject("Offer_coffee");
            coffeeOffer.transform.SetParent(offers.transform, false);
            coffeeOffer.AddComponent<ProductOffer>().item = items.Coffee;
            for (int i = 2; i < tables.Length; i++) AddOffer(tables[i].Group.transform, "TableOffer", items.Coffee, TableDemand);
            // Tables 1 and 2 are open from the start: their demand is the shop's own (two tables' worth).
            coffeeOffer.GetComponent<ProductOffer>().capacity = TableDemand * 2;

            // ---- the bakery: oven and egg fridge, case and TAKE CAKE (switched on by the Cakes purchase) -----------
            var bakery = new GameObject("Bakery");
            bakery.transform.SetParent(root, false);
            var eggFridge = LevelBuildKit.Buffer("town.cafe.eggfridge", "EggFridge", bakery.transform, EggFridgeAt, items.Egg, FridgeCapacity, 0);
            var ovenOut = LevelBuildKit.Buffer("town.cafe.oven.out", "OvenOut", bakery.transform, OvenAt, items.Cake, 6);
            var ovenGo = new GameObject("Oven");
            ovenGo.transform.SetParent(bakery.transform, false);
            ovenGo.transform.localPosition = OvenAt;
            LevelBuildKit.Identify(ovenGo, "town.cafe.oven.machine");
            var oven = ovenGo.AddComponent<ProducerMachine>();
            oven.input = eggFridge;
            oven.output = ovenOut;
            oven.inputPerOutput = EggsPerCake;
            oven.secondsPerOutput = CakeSeconds;
            oven.units = 1;
            oven.maxUnits = 1;
            var caseBuffer = LevelBuildKit.Buffer("town.cafe.case", "CaseCakes", bakery.transform, CaseAt, items.Cake, CaseCapacity);

            var eggSq = LevelBuildKit.Station<DepositStation>("town.cafe.eggfridge.deposit", "EggFridge", bakery.transform, EggFridgeAt, SqStd, "Eggs",
                new Color(0.96f, 0.84f, 0.50f), smallestCard: SqStd);
            eggSq.target = eggFridge;
            eggSq.showItem = true;
            var ovenSq = LevelBuildKit.Station<CollectStation>("town.cafe.oven.collect", "Oven", bakery.transform, OvenAt, SqStd, "Cakes",
                new Color(0.92f, 0.45f, 0.35f), smallestCard: SqStd);
            ovenSq.source = ovenOut;
            var caseSq = LevelBuildKit.Station<DepositStation>("town.cafe.case.deposit", "Case", bakery.transform, CaseAt, SqStd, "Case",
                new Color(0.95f, 0.55f, 0.75f), smallestCard: SqStd);
            caseSq.target = caseBuffer;
            caseSq.showItem = true;
            var takeSq = LevelBuildKit.Station<CollectStation>("town.cafe.case.take", "TakeCake", bakery.transform, TakeAt, new Vector2(1.4f, 1.0f), "Take",
                new Color(0.95f, 0.55f, 0.75f), smallestCard: new Vector2(1.4f, 1.0f));
            takeSq.source = caseBuffer;
            AddOffer(bakery.transform, "CakeOffer", items.Cake, TableDemand);

            // ---- the premium machine: swaps the machine, opens the LATTE square where UPGRADE stood -----------------
            var premium = new GameObject("Premium");
            premium.transform.SetParent(root, false);
            var latte = LevelBuildKit.Station<LatteStation>("town.cafe.latte", "Latte", premium.transform, UpgradeAt, new Vector2(1.6f, 1.6f), "Latte",
                new Color(1f, 0.82f, 0.40f), smallestCard: new Vector2(1.6f, 1.6f));
            latte.milk = fridge;
            latte.premiumItem = items.PremiumCoffee;
            latte.pile = cash.pile;
            AddOffer(premium.transform, "PremiumOffer", items.PremiumCoffee, TableDemand);
            var premiumModel = FloristModels.CoffeeMachine(premium.transform, MachineModelAt + Vector3.up * 1.0f, 0f, premium: true);
            premiumModel.name = "Machine";

            // ---- the staff ---------------------------------------------------------------------------------
            var barista = FarmSceneBuilder.BuildHire(root, "Barista", "barista", BaristaHireAt, BaristaPrice, machineSq, tables[0].Serve,
                new Color(0.86f, 0.42f, 0.24f), 0d, null, "town.hire.", new Vector2(1.8f, 1.8f));
            CafeDress(barista.revealOnUnlock[0].transform, "CafeBarista", new Dictionary<int, Color>
            {
                { CharacterLibrary.ShirtSlot, new Color(0.99f, 0.95f, 0.86f) }, { CharacterLibrary.TrouserSlot, new Color(0.30f, 0.18f, 0.10f) },
                { CharacterLibrary.ShoeSlot, new Color(0.16f, 0.12f, 0.10f) }, { 4, new Color(0.86f, 0.42f, 0.24f) }, { 5, new Color(0.30f, 0.18f, 0.10f) },
            });
            var baristaWorker = barista.revealOnUnlock[0].GetComponent<WorkerAgent>();
            var baristaRoutes = new List<StockRoute>();
            foreach (var t in tables)
            {
                baristaRoutes.Add(new StockRoute { pickup = machineSq, dropoff = t.Serve });
                baristaRoutes.Add(new StockRoute { pickup = latte, dropoff = t.Serve });
            }
            baristaWorker.routes = baristaRoutes.ToArray();
            baristaWorker.deliverPartialLoads = true;
            baristaWorker.GetComponent<CarryStack>().capacity = 1;        // different tables, different orders: one item a trip
            AddOffer(baristaWorker.transform, "BaristaOffer", items.Coffee, StaffDemand);

            var waiter = FarmSceneBuilder.BuildHire(root, "Waiter", "waiter", WaiterHireAt, WaiterPrice, tables[0].Clear, dishSq,
                new Color(0.92f, 0.92f, 0.96f), 0d, null, "town.hire.", new Vector2(1.8f, 1.8f));
            CafeDress(waiter.revealOnUnlock[0].transform, "CafeWaiter", new Dictionary<int, Color>
            {
                { CharacterLibrary.ShirtSlot, new Color(0.97f, 0.97f, 0.98f) }, { CharacterLibrary.TrouserSlot, new Color(0.14f, 0.13f, 0.16f) },
                { CharacterLibrary.ShoeSlot, new Color(0.10f, 0.09f, 0.10f) },
            });
            var waiterWorker = waiter.revealOnUnlock[0].GetComponent<WorkerAgent>();
            var waiterRoutes = new List<StockRoute>();
            foreach (var t in tables) waiterRoutes.Add(new StockRoute { pickup = t.Clear, dropoff = dishSq });
            foreach (var t in tables) waiterRoutes.Add(new StockRoute { pickup = takeSq, dropoff = t.Serve });
            waiterWorker.routes = waiterRoutes.ToArray();
            waiterWorker.deliverPartialLoads = true;
            waiterWorker.GetComponent<CarryStack>().capacity = 1;
            AddOffer(waiterWorker.transform, "WaiterOffer", items.Coffee, StaffDemand);

            var baker = FarmSceneBuilder.BuildHire(root, "Baker", "baker", BakerHireAt, BakerPrice, unpackEgg, eggSq,
                new Color(0.99f, 0.97f, 0.92f), 0d, null, "town.hire.", new Vector2(1.8f, 1.8f));
            CafeDress(baker.revealOnUnlock[0].transform, "CafeBaker", new Dictionary<int, Color>
            {
                { CharacterLibrary.ShirtSlot, new Color(0.99f, 0.97f, 0.92f) }, { CharacterLibrary.TrouserSlot, new Color(0.22f, 0.26f, 0.40f) },
                { CharacterLibrary.ShoeSlot, new Color(0.96f, 0.96f, 0.98f) },
            });
            var bakerWorker = baker.revealOnUnlock[0].GetComponent<WorkerAgent>();
            AttachChefHat(bakerWorker.transform);
            bakerWorker.routes = new[]
            {
                new StockRoute { pickup = unpackEgg, dropoff = eggSq },
                new StockRoute { pickup = ovenSq, dropoff = caseSq },
                new StockRoute { pickup = unpackMilk, dropoff = fridgeSq },      // the milk too: carried six at a time, from the crate to the small fridge
            };
            bakerWorker.deliverPartialLoads = true;
            bakerWorker.onlyFittingLoads = true;
            AddOffer(bakerWorker.transform, "BakerOffer", items.Cake, StaffDemand);

            // ---- the purchases, in the order they are offered, each revealing the next ----------------------------
            var steps = new List<GameObject>();
            var gates = new Dictionary<string, UnlockStation>();
            UnlockStation Gate(string id, string label, double price, Vector3 at, Vector2 size, params GameObject[] reveal)
            {
                var g = FarmSceneBuilder.Gate(root, "town.cafe.unlock." + id, "Unlock" + id, at, size, size, label, price, reveal);
                g.icon = SquareIcon.Buy;
                gates[id] = g;
                return g;
            }

            var gTable3 = Gate("table3", "Table 3", Table3Price, ServeAt(Tables[2]), ServeSz(Tables[2]), tables[2].Group);
            var gTable4 = Gate("table4", "Table 4", Table4Price, ServeAt(Tables[3]), ServeSz(Tables[3]), tables[3].Group);
            var gTable5 = Gate("table5", "Table 5", Table5Price, ServeAt(Tables[4]), ServeSz(Tables[4]), tables[4].Group);
            var gTable6 = Gate("table6", "Table 6", Table6Price, ServeAt(Tables[5]), ServeSz(Tables[5]), tables[5].Group);
            var gCakes = Gate("cakes", "Cakes", CakesPrice, CaseAt, SqStd, bakery);
            var gPremium = Gate("premium", "Premium", PremiumPrice_, UpgradeAt, new Vector2(1.6f, 1.6f), premium);
            gPremium.hideOnUnlock = new[] { shell.BasicMachine };
            var gGarden = new UnlockStation[4];
            for (int i = 0; i < 4; i++) gGarden[i] = Gate("garden" + (i + 1), Tables[6 + i].Label, GardenPrice[i], ServeAt(Tables[6 + i]), ServeSz(Tables[6 + i]), tables[6 + i].Group);

            // The order they are offered in (each purchase reveals the next).
            var order = new List<StationBase>
            {
                gTable3, barista, gCakes, waiter, gTable4, baker, gPremium, gGarden[0], gGarden[1], gGarden[2], gGarden[3], gTable5, gTable6,
            };
            for (int i = 0; i < order.Count; i++)
            {
                if (i > 0) order[i].gameObject.SetActive(false);
                if (i + 1 < order.Count) Chain((UnlockStation)order[i], order[i + 1].gameObject);
            }

            // ---- the renovation: litter, repair + paint, open ---------------------------------------------------------
            var reveal0 = new List<GameObject> { shop, tables[0].Group, tables[1].Group, order[0].gameObject };
            BuildRenovation(root, shell, items, reveal0.ToArray());

            // Everything that is switched on by a purchase or by OPEN is off until then.
            foreach (var t in tables) t.Group.SetActive(false);
            bakery.SetActive(false);
            premium.SetActive(false);
            shop.SetActive(false);

            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        private static readonly Vector3 MachineModelAt = new Vector3(27.2f, 0f, 12.45f);

        private static Vector3 ServeAt(TableSpec t) => new Vector3(t.X, 0f, t.Z + (t.Garden ? 0.95f : 1.05f));
        private static Vector2 ServeSz(TableSpec t) => t.Garden ? GardenServeSize : ServeSize;
        private static Vector3 ClearAt(TableSpec t) => new Vector3(t.X, 0f, t.Z - 0.95f);

        private static void Chain(UnlockStation purchase, GameObject next)
        {
            var list = new List<GameObject>(purchase.revealOnUnlock) { next };
            purchase.revealOnUnlock = list.ToArray();
            next.SetActive(false);
        }

        private static void AddOffer(Transform parent, string name, ItemDefinition item, int capacity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var po = go.AddComponent<ProductOffer>();
            po.item = item;
            po.capacity = capacity;
        }

        // =====================================================================================================
        //  a table: its models, its service chair, SERVE and CLEAR squares and its dirty cup
        // =====================================================================================================

        private static TableParts BuildTable(Transform root, Shell shell, TableSpec spec, FarmSceneBuilder.Items items,
            ItemDefinition[] menu, CashPile pile)
        {
            var p = new TableParts { Spec = spec };
            var group = new GameObject(spec.Id == null ? "Table" : (spec.Garden ? "Garden" : "Table") + spec.Number);
            group.transform.SetParent(root, false);
            p.Group = group;

            float baseY = spec.Garden ? Pave : Floor;
            var at = new Vector3(spec.X, baseY, spec.Z);
            var models = new GameObject("Working").transform;
            models.SetParent(group.transform, false);

            var table = spec.Garden ? FloristModels.GardenTable(models, at, 0f) : FloristModels.CafeTable(models, at, 0f);
            table.name = "Stall";
            var chairA = spec.Garden ? FloristModels.GardenChair(models, at + new Vector3(-0.78f, 0f, 0f), -90f) : FloristModels.CafeChair(models, at + new Vector3(-0.78f, 0f, 0f), -90f);
            var chairB = spec.Garden ? FloristModels.GardenChair(models, at + new Vector3(0.78f, 0f, 0f), 90f) : FloristModels.CafeChair(models, at + new Vector3(0.78f, 0f, 0f), 90f);
            chairA.name = chairB.name = "Stall";
            FloristBuilder.Solid(group.transform, "TableCollision", new Vector3(spec.X, 0.4f, spec.Z), new Vector3(0.9f, 0.8f, 0.9f));
            FloristBuilder.Solid(group.transform, "ChairCollisionA", new Vector3(spec.X - 0.78f, 0.45f, spec.Z), new Vector3(0.5f, 0.9f, 0.5f));
            FloristBuilder.Solid(group.transform, "ChairCollisionB", new Vector3(spec.X + 0.78f, 0.45f, spec.Z), new Vector3(0.5f, 0.9f, 0.5f));

            // The wreck stands at an inside table's place until REPAIR.
            if (!spec.Garden)
            {
                FloristModels.CafeTable(shell.Broken.transform, new Vector3(spec.X, Floor, spec.Z), 0f, true);
                FloristModels.CafeChair(shell.Broken.transform, new Vector3(spec.X - 0.78f, Floor, spec.Z), -90f + (spec.Number % 2 == 0 ? 20f : -12f), true);
                var w2 = FloristModels.CafeChair(shell.Broken.transform, new Vector3(spec.X + 0.78f, Floor, spec.Z), 90f, true);
                if (spec.Number % 3 == 0) { w2.transform.localRotation = Quaternion.Euler(0f, 60f, 82f); w2.transform.localPosition += new Vector3(0.1f, 0.2f, 0f); }
            }

            // The service chair (customer side): the seat, the way in, the way out.
            float seatY = (spec.Garden ? Pave : Floor) + 0.29f;
            var chairObj = new GameObject("ServiceChair");
            chairObj.transform.SetParent(group.transform, false);
            var chair = chairObj.AddComponent<ServiceChair>();
            float seatX = spec.X + (spec.EastSeat ? 0.78f : -0.78f);
            var seat = FloristBuilder.Marker(chairObj.transform, "Seat", new Vector3(seatX, seatY, spec.Z));
            seat.localRotation = Quaternion.Euler(0f, spec.EastSeat ? -90f : 90f, 0f);      // facing the table
            chair.seat = seat;
            chair.approach = FloristBuilder.Markers(chairObj.transform, "Approach", spec.Approach);
            chair.exitRoute = FloristBuilder.Markers(chairObj.transform, "Exit", spec.Exit);
            p.Chair = chair;

            // The SERVE square: hands over what is CARRIED. Its price and pile are the till's.
            var serve = LevelBuildKit.Station<ChairServiceStation>($"town.cafe.{spec.Id.ToLowerInvariant()}.serve", "Serve", group.transform, ServeAt(spec),
                ServeSz(spec), "Serve", new Color(0.50f, 0.85f, 0.45f), smallestCard: ServeSz(spec));
            serve.chair = chair;
            serve.sells = menu;
            serve.pile = pile;
            serve.priceMultiplier = 1f;
            serve.serveFromCarry = true;
            serve.tickInterval = 0.2f;
            p.Serve = serve;

            // The dirty cup: a one-cup buffer, its picture on the table, the CLEAR square, and the marker that keeps the chair out of use.
            var cupObj = new GameObject("DirtyCup");
            cupObj.transform.SetParent(group.transform, false);
            cupObj.transform.localPosition = new Vector3(spec.X, 0f, spec.Z);
            LevelBuildKit.Identify(cupObj, $"town.cafe.{spec.Id.ToLowerInvariant()}.cup");
            var buffer = cupObj.AddComponent<ItemBuffer>();
            buffer.item = items.DirtyCup;
            buffer.capacity = 1;
            var cup = FloristModels.CafeGood("dirty_cup", cupObj.transform, new Vector3(0.06f, baseY + (spec.Garden ? 0.725f : 0.76f) + 0.058f, -0.08f), 40f);
            cup.transform.localScale = Vector3.one * 0.3f;
            cup.name = "Heap0";
            var clear = LevelBuildKit.Station<CollectStation>($"town.cafe.{spec.Id.ToLowerInvariant()}.clear", "Clear", group.transform, ClearAt(spec),
                ClearSize, "Clear", new Color(0.62f, 0.62f, 0.62f), smallestCard: ClearSize);
            clear.source = buffer;
            var dirty = new GameObject("Dirty");
            dirty.transform.SetParent(cupObj.transform, false);

            var mess = cupObj.AddComponent<HairMess>();
            mess.chair = chair;
            mess.bagsPerService = 1;
            mess.heap = new[] { cup };
            mess.square = clear.gameObject;
            mess.dirty = dirty;
            chair.dirtyWhen = dirty;
            clear.gameObject.SetActive(false);
            dirty.SetActive(false);
            cup.SetActive(false);
            p.Clear = clear;
            return p;
        }

        // =====================================================================================================
        //  the customers
        // =====================================================================================================

        private static CustomerQueue BuildQueue(Transform shop, TableParts[] tables, FarmSceneBuilder.Items items)
        {
            var queue = shop.gameObject.AddComponent<CustomerQueue>();
            queue.customerTemplate = LevelBuildKit.BuildCustomerTemplate(shop);
            var bubble = queue.customerTemplate.GetComponent<OrderBubble>();
            bubble.worldScale = BubbleScale;
            bubble.height = 1.9f;

            queue.spawnPoint = FloristBuilder.Marker(shop, "CafeSpawn", SpawnPoint);
            queue.exitPoint = FloristBuilder.Marker(shop, "CafeExit", ExitPoint);
            queue.counterPoint = FloristBuilder.Marker(shop, "CafeCounterPoint", new Vector3(30.4f, 0f, 2.4f));
            queue.slots = new Transform[StoolAt.Length];
            for (int i = 0; i < StoolAt.Length; i++)
            {
                var stool = FloristModels.CafeStool(shop, StoolAt[i] + Vector3.up * Pave, 0f);
                stool.name = "Stall";
                FloristBuilder.Solid(shop, "StoolCollision", StoolAt[i] + new Vector3(0f, 0.25f, 0f), new Vector3(0.45f, 0.5f, 0.45f));
                var slot = FloristBuilder.Marker(shop, $"CafeSeat_{i}", new Vector3(StoolAt[i].x, Pave + 0.295f, StoolAt[i].z));
                slot.localRotation = Quaternion.Euler(0f, 180f, 0f);        // facing the street
                queue.slots[i] = slot;
            }
            queue.entryRoute = FloristBuilder.Markers(shop, "CafeIn", EntryRoute);
            queue.queueExit = FloristBuilder.Markers(shop, "CafeGaveUp", QueueExit);
            queue.exitRoute = FloristBuilder.Markers(shop, "CafeOut", ExitRoute);

            queue.seatedWaiting = true;
            queue.autoSeat = true;
            queue.cafeOrders = true;
            queue.cakeItem = items.Cake;
            queue.cakeChance = CakeChance;
            queue.chairs = new ServiceChair[tables.Length];
            for (int i = 0; i < tables.Length; i++)
            {
                queue.chairs[i] = tables[i].Chair;
                tables[i].Serve.queue = queue;
            }
            queue.register = tables[0].Serve;

            queue.minOrder = 1;
            queue.maxOrder = 1;
            queue.spawnIntervalSeconds = CustomerIntervalSeconds;
            queue.demandPerCapacity = DemandPerCapacity;
            queue.maxDemand = MaxDemand;
            return queue;
        }

        private static void BuildVan(Transform shop)
        {
            var vanRoot = new GameObject("DeliveryVan");
            vanRoot.transform.SetParent(shop, false);
            vanRoot.transform.localPosition = VanRoute[0];
            var van = vanRoot.AddComponent<DeliveryVan>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(vanRoot.transform, false);
            visual.localRotation = Quaternion.Euler(0f, 180f, 0f);        // the model's front is -z; the root drives +z
            FloristModels.DeliveryVan(visual, new Vector3(0f, 0.03f, 0f), 0f);
            van.visual = visual.gameObject;
            van.route = FloristBuilder.Markers(shop, "VanRoute", VanRoute);
            van.speed = 5.5f;
            van.stopSeconds = 7f;
            van.intervalSeconds = 70f;
        }

        // =====================================================================================================
        //  the renovation
        // =====================================================================================================

        private static void BuildRenovation(Transform root, Shell shell, FarmSceneBuilder.Items items, GameObject[] openReveals)
        {
            var heaps = new List<ItemBuffer>();
            for (int i = 0; i < LitterAt.Length; i++)
            {
                var litter = BuildLitter(root, $"Litter_{i + 1}", LitterAt[i], 71 + i * 9);
                var heap = LevelBuildKit.Buffer($"town.cafe.trash.{i + 1}", $"Trash{i + 1}", root, LitterAt[i], items.Trash, 4, starting: 4);
                heaps.Add(heap);

                var take = LevelBuildKit.Station<CollectStation>($"town.cafe.trashcollect.{i + 1}", $"CollectTrash{i + 1}", root,
                    LitterAt[i], PileSquare, "Trash", new Color(0.7f, 0.74f, 0.78f), smallestCard: PileSquare);
                take.source = heap;
                take.workerCompatible = false;

                var clear = heap.gameObject.AddComponent<ClearablePile>();
                clear.litter = litter;
                clear.square = take.gameObject;
            }

            var repair = FarmSceneBuilder.Gate(root, "town.cafe.unlock.repair", "UnlockRepair", RepairAt, RepairSize, RepairSize,
                "Repair", RepairPrice, shell.Working);
            repair.icon = SquareIcon.Fix;
            repair.hideOnUnlock = new[] { shell.Broken };

            var decorHolder = new GameObject("Decor");
            decorHolder.transform.SetParent(root, false);
            LevelBuildKit.Identify(decorHolder, "town.cafe.decor");
            var decor = decorHolder.AddComponent<MarketDecor>();
            decor.grubby = new[] { shell.Grubby };
            decor.painted = new[] { shell.Painted };
            decor.surfaces = shell.Surfaces.ToArray();
            decor.patterns = PatternFactory.CafePatterns;
            decor.useStartingLook = true;
            decor.startingLook = new DecorLook
            {
                pattern = PatternFactory.Plain,
                primary = new Color(0.99f, 0.96f, 0.90f),
                secondary = new Color(0.96f, 0.76f, 0.62f),
                tertiary = new Color(0.86f, 0.42f, 0.24f),
                floorKind = PatternFactory.FloorTiles,
                floorColor = new Color(0.97f, 0.90f, 0.80f),
                woodShade = 0.3f,
            };

            var paint = LevelBuildKit.Station<PaintStation>("town.cafe.paint", "Paint", root, PaintAt, PaintSize, "Paint",
                new Color(0.95f, 0.6f, 0.55f), smallestCard: PaintSize);
            paint.mode = Tycoon.Stations.InteractionMode.Task;
            paint.taskDuration = 0.9f;
            paint.decor = decor;
            paint.firstPrice = FarmSceneBuilder.Price(FirstPaintPrice);
            paint.changePrice = FarmSceneBuilder.Price(RepaintPrice);

            // OPEN: a free chore; what it brings in is the working business (the queue, the cash pile, the machines, tables 1 and 2)
            // and the offer of the next purchase.
            var open = LevelBuildKit.Station<ChoreStation>("town.cafe.chore.open", "Open", root, OpenAt, OpenSize, "Open",
                new Color(0.55f, 0.92f, 0.55f), smallestCard: OpenSize);
            open.icon = SquareIcon.Open;
            open.completionsNeeded = 1;
            open.taskDuration = 2f;
            open.doneMessage = "OPEN!";
            open.revealOnDone = openReveals;
            foreach (var go in open.revealOnDone) go.SetActive(false);
            decor.openStep = open;

            var progress = new GameObject("Progress");
            progress.transform.SetParent(root, false);
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

        /// <summary>A heap of bags, boxes, bean sacks and broken cups. Returns the pieces, for ClearablePile.</summary>
        private static GameObject[] BuildLitter(Transform parent, string name, Vector3 position, int seed)
        {
            var pile = new GameObject(name);
            pile.transform.SetParent(parent, false);
            pile.transform.localPosition = position;

            var random = new System.Random(seed);
            var materials = new[]
            {
                LevelBuildKit.Mat("Trash_Bag", new Color(0.18f, 0.2f, 0.19f)),
                LevelBuildKit.Mat("Trash_Cardboard", new Color(0.62f, 0.49f, 0.32f)),
                LevelBuildKit.Mat("Cafe_TrashSack", new Color(0.66f, 0.58f, 0.40f)),
                LevelBuildKit.Mat("Trash_Bottle", new Color(0.3f, 0.5f, 0.38f)),
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
                    pieces[i] = FloristModels.CafeGood("dirty_cup", pile.transform, new Vector3(x, 0.12f, z), yaw);
                    pieces[i].transform.localScale = Vector3.one * 0.35f;
                    pieces[i].name = $"Litter_{i}";
                    continue;
                }

                pieces[i] = LevelBuildKit.Box($"Litter_{i}", pile.transform, new Vector3(x, size * 0.5f, z),
                    new Vector3(size, size * 0.8f, size * 1.1f), materials[i % materials.Length], castShadow: false,
                    rot: new Vector3(0f, yaw, 0f));
            }
            return pieces;
        }

        // =====================================================================================================
        //  the building
        // =====================================================================================================

        private static Shell BuildShell(Transform cafe)
        {
            var set = new Shell();
            var shell = new GameObject("Building").transform;
            shell.SetParent(cafe, false);
            set.Building = shell;

            set.Grubby = new GameObject("Grubby");
            set.Grubby.transform.SetParent(cafe, false);
            set.Painted = new GameObject("Painted");
            set.Painted.transform.SetParent(cafe, false);
            set.Working = new GameObject("Working");
            set.Working.transform.SetParent(cafe, false);
            set.Broken = new GameObject("Broken");
            set.Broken.transform.SetParent(cafe, false);
            var grubby = set.Grubby.transform;
            var painted = set.Painted.transform;
            var working = set.Working.transform;
            var broken = set.Broken.transform;

            var floor = LevelBuildKit.Mat("Cafe_Floor", new Color(0.97f, 0.90f, 0.80f), 0.35f);
            var kitchenFloor = LevelBuildKit.Mat("Cafe_KitchenFloor", new Color(0.78f, 0.92f, 0.88f), 0.4f);
            var wall = LevelBuildKit.Mat("Cafe_Wall", new Color(0.99f, 0.96f, 0.90f), 0.4f);
            var cap = LevelBuildKit.Mat("Cafe_WallCap", new Color(0.86f, 0.42f, 0.24f), 0.4f);
            var dirtyFloor = LevelBuildKit.Mat("Cafe_FloorDirty", new Color(0.34f, 0.30f, 0.27f), 0.1f);
            var dirtyWall = LevelBuildKit.Mat("Cafe_WallDirty", new Color(0.54f, 0.48f, 0.41f), 0.1f);
            var dirtyCap = LevelBuildKit.Mat("Cafe_WallCapDirty", new Color(0.44f, 0.39f, 0.35f), 0.1f);
            var matIn = LevelBuildKit.Mat("Cafe_DoorMat", new Color(0.46f, 0.78f, 0.52f), 0.2f);
            var matOut = LevelBuildKit.Mat("Cafe_DoorMatOut", new Color(0.98f, 0.66f, 0.30f), 0.2f);
            var matBack = LevelBuildKit.Mat("Cafe_DoorMatBack", new Color(0.52f, 0.68f, 0.92f), 0.2f);

            float midX = (ShopWest + ShopEast) * 0.5f, midZ = (ShopSouth + ShopNorth) * 0.5f;
            float w = ShopEast - ShopWest, d = ShopNorth - ShopSouth;
            var floorCentre = new Vector3(midX, 0.03f, midZ);
            var floorSize = new Vector3(w + 0.2f, 0.06f, d + 0.2f);
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
            // The kitchen's tiles: a coat of their own on the painted floor (not repainted).
            LevelBuildKit.Box("KitchenFloor", painted, new Vector3((KitchenWest + ShopEast) * 0.5f, 0.035f, (KitchenSouth + ShopNorth) * 0.5f),
                new Vector3(ShopEast - KitchenWest - 0.2f, 0.06f, ShopNorth - KitchenSouth - 0.2f), kitchenFloor, castShadow: false);

            void Wall(float x0, float x1, float z0, float z1)
            {
                var c = new Vector3((x0 + x1) * 0.5f, WallHeight * 0.5f, (z0 + z1) * 0.5f);
                var s = new Vector3(Mathf.Max(0.2f, x1 - x0), WallHeight, Mathf.Max(0.2f, z1 - z0));
                var capAt = c + Vector3.up * (WallHeight * 0.5f + 0.03f);
                var capSize = new Vector3(s.x + 0.04f, 0.07f, s.z + 0.04f);

                var piece = LevelBuildKit.Box("Wall", painted, c, s, wall, castShadow: false);
                var capPiece = LevelBuildKit.Box("WallCap", painted, capAt, capSize, cap, castShadow: false);
                LevelBuildKit.Box("DirtyWall", grubby, c, s, dirtyWall, castShadow: false);
                LevelBuildKit.Box("DirtyCap", grubby, capAt, capSize, dirtyCap, castShadow: false);

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

                FloristBuilder.Solid(shell, "WallCollision", new Vector3(c.x, 0.6f, c.z), new Vector3(s.x, 1.2f, s.z));
            }

            // South: the street door bottom left, the exit door bottom right.
            Wall(ShopWest - 0.1f, InFrom, ShopSouth - 0.1f, ShopSouth + 0.1f);
            Wall(InTo, OutFrom, ShopSouth - 0.1f, ShopSouth + 0.1f);
            Wall(OutTo, ShopEast + 0.1f, ShopSouth - 0.1f, ShopSouth + 0.1f);
            // West, north, and the east wall with its back door.
            Wall(ShopWest - 0.1f, ShopWest + 0.1f, ShopSouth, ShopNorth);
            Wall(ShopWest - 0.1f, ShopEast + 0.1f, ShopNorth - 0.1f, ShopNorth + 0.1f);
            Wall(ShopEast - 0.1f, ShopEast + 0.1f, ShopSouth, BackFrom);
            Wall(ShopEast - 0.1f, ShopEast + 0.1f, BackTo, ShopNorth);
            // The kitchen: a partition with a pass, and its south wall.
            Wall(KitchenWest - 0.1f, KitchenWest + 0.1f, KitchenSouth, PassFrom);
            Wall(KitchenWest - 0.1f, KitchenWest + 0.1f, PassTo, ShopNorth);
            Wall(KitchenWest - 0.1f, ShopEast, KitchenSouth - 0.1f, KitchenSouth + 0.1f);

            LevelBuildKit.Box("DoorMatIn", painted, new Vector3((InFrom + InTo) * 0.5f, 0.07f, ShopSouth + 0.6f), new Vector3(1.6f, 0.03f, 0.9f), matIn, castShadow: false);
            LevelBuildKit.Box("DoorMatOut", painted, new Vector3((OutFrom + OutTo) * 0.5f, 0.07f, ShopSouth + 0.6f), new Vector3(1.6f, 0.03f, 0.9f), matOut, castShadow: false);
            LevelBuildKit.Box("BackMat", painted, new Vector3(ShopEast - 0.6f, 0.07f, (BackFrom + BackTo) * 0.5f), new Vector3(0.9f, 0.03f, 1.6f), matBack, castShadow: false);

            // The counter, the machine, the small fridge, the display case, the egg fridge, the oven, the dishwasher: what REPAIR brings back;
            // their wrecks stand in the same places until then.
            var counter = FloristModels.CafeCounter(working, new Vector3(27.9f, Floor, 12.45f), 0f);
            counter.name = "Stall";
            FloristBuilder.Solid(shell, "CounterCollision", new Vector3(27.9f, 0.5f, 12.45f), new Vector3(4.0f, 1.0f, 0.9f));
            FloristModels.CafeCounter(broken, new Vector3(27.9f, Floor, 12.45f), 0f, true);

            set.BasicMachine = FloristModels.CoffeeMachine(working, MachineModelAt + Vector3.up * 1.0f, 0f);
            set.BasicMachine.name = "BasicMachine";
            FloristModels.CoffeeMachine(broken, MachineModelAt + Vector3.up * 1.0f, 0f, broken: true);

            var fridgeAt = new Vector3(25.0f, Floor, 12.45f);
            var smallFridge = FloristModels.CafeFridge(working, fridgeAt, 0f);
            smallFridge.transform.localScale = new Vector3(0.9f, 0.62f, 0.9f);
            smallFridge.name = "Stall";
            FloristBuilder.Solid(shell, "SmallFridgeCollision", new Vector3(25.0f, 0.6f, 12.45f), new Vector3(0.9f, 1.2f, 0.75f));
            var smallWreck = FloristModels.CafeFridge(broken, fridgeAt, 0f, true);
            smallWreck.transform.localScale = new Vector3(0.9f, 0.62f, 0.9f);

            AddFitting(working, broken, set, FloristModels.CafeCase, new Vector3(31.2f, Floor, 12.5f), 0f, new Vector3(1.6f, 1.2f, 0.7f));
            AddFitting(working, broken, set, FloristModels.CafeFridge, new Vector3(34.3f, Floor, 12.5f), 0f, new Vector3(0.95f, 1.9f, 0.75f));
            AddFitting(working, broken, set, FloristModels.CafeOven, new Vector3(36.6f, Floor, 12.45f), 0f, new Vector3(1.15f, 1.55f, 0.85f));
            AddFitting(working, broken, set, FloristModels.CafeDishwasher, new Vector3(34.6f, Floor, 7.45f), 180f, new Vector3(0.95f, 1.0f, 0.75f));

            // Plants by the doors.
            var pot = LevelBuildKit.Mat("Cafe_Pot", new Color(0.86f, 0.42f, 0.24f), 0.4f);
            var leaf = LevelBuildKit.Mat("Cafe_Leaf", new Color(0.30f, 0.62f, 0.22f), 0.3f);
            foreach (float px in new[] { 26.4f, 29.6f, 32.4f, 35.6f })
            {
                LevelBuildKit.Cylinder("Pot", painted, new Vector3(px, 0.2f, 1.6f), new Vector3(0.42f, 0.2f, 0.42f), pot);
                for (int k = 0; k < 6; k++)
                    LevelBuildKit.Box("Leaf", painted, new Vector3(px, 0.55f, 1.6f), new Vector3(0.06f, 0.5f, 0.06f), leaf, castShadow: false, rot: new Vector3(20f, k * 60f, 0f));
            }

            BuildGrime(grubby);

            set.Grubby.SetActive(true);
            set.Painted.SetActive(false);
            set.Working.SetActive(false);
            set.Broken.SetActive(true);
            return set;
        }

        private static void AddFitting(Transform working, Transform broken, Shell shell,
            System.Func<Transform, Vector3, float, bool, GameObject> make, Vector3 at, float yaw, Vector3 size)
        {
            var model = make(working, at, yaw, false);
            model.name = "Stall";
            FloristBuilder.Solid(shell.Building, "FittingCollision", new Vector3(at.x, size.y * 0.5f, at.z), size);
            make(broken, at, yaw, true);
        }

        private static void BuildGrime(Transform grubby)
        {
            var dirt = LevelBuildKit.Mat("Cafe_Dirt", new Color(0.24f, 0.21f, 0.18f), 0.05f);
            var stain = LevelBuildKit.Mat("Cafe_Stain", new Color(0.40f, 0.36f, 0.30f), 0.05f);
            var rnd = new System.Random(83);
            float cx = (ShopWest + ShopEast) * 0.5f, cz = (ShopSouth + ShopNorth) * 0.5f;

            for (int i = 0; i < 18; i++)
            {
                float x = cx + ((float)rnd.NextDouble() - 0.5f) * 13.0f, z = cz + ((float)rnd.NextDouble() - 0.5f) * 9.8f;
                var o = LevelBuildKit.Cylinder("Dirt", grubby, new Vector3(x, 0.065f, z),
                    new Vector3(0.8f + (float)rnd.NextDouble() * 1.3f, 0.01f, 0.6f + (float)rnd.NextDouble() * 0.9f), dirt, false);
                o.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
            }
            for (int i = 0; i < 5; i++)
                LevelBuildKit.Box("Stain", grubby, new Vector3(ShopWest + 2.5f + i * 2.6f, 0.35f, ShopNorth - 0.115f),
                    new Vector3(0.9f + (0.4f * i) % 1.2f, 0.45f, 0.01f), stain, castShadow: false);
        }

        // =====================================================================================================
        //  dressing a worker
        // =====================================================================================================

        /// <summary>
        /// The baker's chef hat, hung on the rig's Head bone (the rig is Generic with 20 named bones and no bone is optimised away, so Head is a real
        /// Transform): as a child of it the hat follows the head through Idle, Walk and Sit, the way the skinned hats of the other roles are weighted
        /// to Head. It is placed once, at the top of the bare head in the rest pose, and parented keeping that world pose.
        /// </summary>
        internal static void AttachChefHat(Transform worker)
        {
            Transform head = null;
            foreach (var t in worker.GetComponentsInChildren<Transform>(true)) if (t.name == "Head") { head = t; break; }
            var skin = worker.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (head == null || skin == null) { Debug.LogWarning("[CafeBuilder] no Head bone: the chef hat is not attached"); return; }

            bool was = worker.gameObject.activeSelf;
            worker.gameObject.SetActive(true);
            skin.updateWhenOffscreen = true;
            float top = skin.bounds.max.y;
            var hat = FloristModels.ChefHat(worker);
            hat.name = "ChefHat";
            hat.transform.position = new Vector3(head.position.x, top - 0.035f, head.position.z);
            hat.transform.rotation = Quaternion.identity;
            hat.transform.SetParent(head, true);
            worker.gameObject.SetActive(was);
        }

        /// <summary>Recolours a villager with material ASSETS (never runtime copies, which render magenta under URP), as the florist's staff are.</summary>
        private static void CafeDress(Transform worker, string tag, Dictionary<int, Color> tints)
        {
            var r = worker.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (r == null) return;
            var mats = r.sharedMaterials;
            foreach (var kv in tints)
            {
                if (kv.Key >= mats.Length) continue;
                string path = $"Assets/_Project/Materials/Fl_{tag}_{kv.Key}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = Object.Instantiate(mats[kv.Key]);
                    m.name = $"Fl_{tag}_{kv.Key}";
                    AssetDatabase.CreateAsset(m, path);
                }
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", kv.Value);
                if (m.HasProperty("_Color")) m.SetColor("_Color", kv.Value);
                EditorUtility.SetDirty(m);
                mats[kv.Key] = m;
            }
            r.sharedMaterials = mats;
        }
    }
}
