using Tycoon.Config;
using Tycoon.Core;
using Tycoon.Customers;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Fills the butcher extension that <see cref="MarketBuilder"/> builds as an empty shell.
    ///
    /// Written the way the market is: every layout and economy number is a commented constant, and
    /// everything is the farm's own parts - gates, buffers, deposit and collect squares, the register
    /// and customer queue, UpgradeStation, WorkerAgent - with three small new pieces where nothing fit:
    /// <see cref="CutStation"/> (the cutting table), <see cref="MeatVan"/> (the van that really drives)
    /// and <see cref="ProductOffer"/> (what shoppers may ask for).
    ///
    /// Everything here is a child of the shell, so it does not exist until the extension is bought,
    /// and from then on the butcher is brought in one purchase at a time, each revealing the next:
    ///
    ///     extension ($5,500, in MarketBuilder)
    ///       Pig Farm $5,000   at the drop square   -> the pig column on the FARM (pen, 1 pig, corn field
    ///                                                 E1, the pig hand, the van), the drop pile
    ///         Cutting Table $3,000  at the cut square  -> the table
    ///           Steak Counter $4,000  at the counter    -> display counter, cash pile, shoppers;
    ///                                                      pigs 2-4, corn field E2
    ///             Pigs ($3,500 / $5,000 / $7,000) and Corn Field E2 ($6,000)
    ///               both done -> Butcher $8,000 (hire) -> Sausages $9,000
    ///
    /// The goods move by hand and by van, never by teleport: pigs make raw meat, the van brings it to
    /// the drop pile, the cutting table turns it into steaks (or sausages) in the cutter's arms, and
    /// the counter takes them from the arms into its case and serves shoppers out of the case.
    /// </summary>
    internal static class ButcherBuilder
    {
        // ---- prices --------------------------------------------------------------------
        // From the pacing simulation (plans/econ/market_sim.py, "butcher" block): about 34 minutes from
        // the extension to the last purchase. Every one goes through Price().
        private const double PigFarmPrice = 5000d;     // pen + 1 pig + corn field E1 + the pig hand + the van
        private const double TablePrice = 3000d;
        private const double SteakCounterPrice = 4000d;
        private static readonly double[] PigPrices = { 3500d, 5000d, 7000d };   // pigs 2, 3 and 4
        private const double FieldTwoPrice = 6000d;
        private const double ButcherHirePrice = 8000d;
        private const double SausagePrice = 9000d;

        // ---- demand --------------------------------------------------------------------
        // A shopper every 5 s at full reputation, one to three units each; sausages open a second
        // line, which counts as one more "producer" (ProductOffer) and so brings shoppers about half
        // again as fast (demandPerCapacity 0.5), the +50% in the simulation.
        private const float ShopperIntervalSeconds = 5f;
        private const int QueueSlots = 4;
        private const int CaseCapacity = 12;       // units in each glass case
        private const int DropCapacity = 24;       // raw meat on the drop pile

        // ---- where things stand (the market's grid; see MarketBuilder for the shell) --------
        private static readonly Vector3 PigBuyPosition = new Vector3(-19.3f, 0f, -1.2f);        // customers' side, west
        private static readonly Vector3 FieldTwoPosition = new Vector3(-19.3f, 0f, -3.5f);

        // The counter: two display cases end to end, each stretched to 4.2 m, wall to wall. The cases
        // are drawn a little in from the walls (the ends round off) and one box of collision seals the
        // gap, so neither a person nor a navmesh agent can slip round either end.
        private static readonly float[] CasePieceX = { -18.65f, -14.55f };
        private const float CaseStretch = 1.3f;
        private const float CaseLineZ = 1.2f;

        /// <summary>The way in from the street, through the door, to the queue; and the way out again.</summary>
        private static readonly Vector3[] EntryRoute =
        {
            new Vector3(-16.5f, 0f, MarketBuilder.RoadZ),
            new Vector3(-16.5f, 0f, -6.6f),
        };
        private static readonly Vector3[] ExitRoute =
        {
            new Vector3(-16.5f, 0f, -6.6f),
            new Vector3(-16.5f, 0f, MarketBuilder.RoadZ),
        };

        /// <summary>Where shoppers appear and vanish: on the road just west of the shop's front, east of the van's lane.</summary>
        private static readonly Vector3 StreetPoint = new Vector3(-22.0f, 0f, MarketBuilder.RoadZ);

        /// <summary>Everything the butcher sells, for whoever waits on it being complete (the town gate).</summary>
        internal class Purchases
        {
            public UnlockStation[] Unlocks;
            public UpgradeStation[] Maxed;
        }

        internal static Purchases Build(Transform root, Transform shell, FarmSceneBuilder.Items items,
            FarmSceneBuilder.PigFarm pigs)
        {
            var drop = BuildDrop(shell, items);
            var counter = BuildCounter(shell, items);
            var cut = BuildCut(shell, items, drop.Buffer, counter);
            var grind = BuildGrinder(shell, items, drop.Buffer, counter);

            // ---- the staff -----------------------------------------------------------------
            // One butcher: it cuts until its arms are full, takes the load to the counter (which
            // stocks the case and serves whoever is waiting on the way), and goes back to cutting.
            // deliverPartialLoads is what keeps it from waiting at an empty table for a full load.
            var hire = FarmSceneBuilder.BuildHire(shell, "Butcher", "butcher", MarketBuilder.ButcherHire,
                price: ButcherHirePrice, pickup: cut.Cut, dropoff: counter.Register,
                color: new Color(0.9f, 0.45f, 0.45f), feePerDelivery: 0d, beacon: null,
                idPrefix: "market.hire.", smallestCard: new Vector2(1.8f, 1.8f));
            var butcher = hire.revealOnUnlock[0].GetComponent<Tycoon.Upkeep.WorkerAgent>();
            butcher.deliverPartialLoads = true;
            // Two benches, one counter: the butcher picks the one whose case is emptier each time its arms
            // are empty (WorkerAgent.ChooseBench). Route 0 is also pickup/dropoff.
            butcher.routes = new[]
            {
                new Tycoon.Upkeep.StockRoute { pickup = cut.Cut, dropoff = counter.Register },
                new Tycoon.Upkeep.StockRoute { pickup = grind.Cut, dropoff = counter.Register },
            };
            hire.gameObject.SetActive(false);

            // ---- the purchases -------------------------------------------------------------
            var pigBuy = LevelBuildKit.Station<UpgradeStation>("market.butcher.pigs", "BuyPig", shell,
                PigBuyPosition, new Vector2(1.8f, 1.8f), "Pig", new Color(0.55f, 0.85f, 0.45f));
            pigBuy.target = pigs.Pen.Machine;
            pigBuy.unitName = "Pig";
            pigBuy.tierPrices = new[]
            {
                FarmSceneBuilder.Price(PigPrices[0]),
                FarmSceneBuilder.Price(PigPrices[1]),
                FarmSceneBuilder.Price(PigPrices[2]),
            };
            pigBuy.basePrice = pigBuy.tierPrices[0];
            pigBuy.gameObject.SetActive(false);

            // Sits where the grinder will stand, and brings the grinder and the sausage case.
            var gateSausage = FarmSceneBuilder.Gate(shell, "market.unlock.sausages", "UnlockSausages",
                MarketBuilder.ButcherGrindSquare, new Vector2(2.4f, 2.4f), new Vector2(2.4f, 2.4f),
                "Sausages", SausagePrice, counter.SausageLine, grind.Root);
            gateSausage.icon = SquareIcon.Buy;
            gateSausage.gameObject.SetActive(false);

            var gateField = FarmSceneBuilder.Gate(shell, "market.unlock.pigfield2", "UnlockCornE2",
                FieldTwoPosition, new Vector2(2.4f, 2.0f), new Vector2(2.4f, 2.0f), "Corn Field", FieldTwoPrice,
                pigs.FieldTwo);
            gateField.icon = SquareIcon.Buy;
            gateField.gameObject.SetActive(false);

            var gateSteaks = FarmSceneBuilder.Gate(shell, "market.unlock.steaks", "UnlockSteaks",
                MarketBuilder.ButcherCounterSquare, new Vector2(1.8f, 2.0f), new Vector2(1.8f, 2.0f),
                "Steak Counter", SteakCounterPrice,
                counter.Root, pigBuy.gameObject, gateField.gameObject);
            gateSteaks.icon = SquareIcon.Buy;
            gateSteaks.gameObject.SetActive(false);

            var gateTable = FarmSceneBuilder.Gate(shell, "market.unlock.table", "UnlockTable",
                MarketBuilder.ButcherCutSquare, new Vector2(2.4f, 2.4f), new Vector2(2.4f, 2.4f),
                "Cutting Table", TablePrice, cut.Root, gateSteaks.gameObject);
            gateTable.icon = SquareIcon.Buy;
            gateTable.gameObject.SetActive(false);

            // The first step: buying the farm. Sits where the drop square will be, and brings the pig
            // column on the farm, the drop pile, and the next purchase.
            var gatePig = FarmSceneBuilder.Gate(shell, "market.unlock.pigfarm", "UnlockPigFarm",
                MarketBuilder.ButcherDropSquare, new Vector2(2.2f, 2.2f), new Vector2(2.2f, 2.2f),
                "Pig Farm", PigFarmPrice, pigs.Group, drop.Root, gateTable.gameObject);
            gatePig.icon = SquareIcon.Buy;

            // Pigs and the second corn field first, then the butcher, then sausages. A hire is only ever
            // offered once everything it works with is standing; the counter, the table and the pigs
            // are all there by then, and sausages need somebody to cut them.
            var progress = new GameObject("ButcherProgress");
            progress.transform.SetParent(shell, false);

            var afterGrowth = progress.AddComponent<RevealWhenAll>();
            // The whole chain the butcher works in, not just the last link: everything it cuts, carries and serves
            // through has been bought by the time it is offered (and the audit prices the route off this list).
            afterGrowth.unlocks = new[] { gatePig, gateTable, gateSteaks, gateField };
            afterGrowth.maxed = new[] { pigBuy };
            afterGrowth.reveal = new[] { hire.gameObject };

            var afterHire = progress.AddComponent<RevealWhenAll>();
            afterHire.unlocks = new[] { hire };
            afterHire.reveal = new[] { gateSausage.gameObject };

            // The van drops onto the pile this purchase brings.
            pigs.Van.destination = drop.Buffer;

            return new Purchases
            {
                Unlocks = new[] { gatePig, gateTable, gateSteaks, gateField, hire, gateSausage },
                Maxed = new[] { pigBuy },
            };
        }

        // ---- the drop pile -----------------------------------------------------------------

        private class Drop
        {
            public GameObject Root;
            public ItemBuffer Buffer;
        }

        /// <summary>
        /// Where the van unloads: a pallet with the raw meat stacked on it (the buffer, shown as one
        /// joint per unit), and a deposit square so a player who carries meat in by hand from the pen
        /// can put it down too. The cutting table takes from this pile directly.
        /// </summary>
        private static Drop BuildDrop(Transform shell, FarmSceneBuilder.Items items)
        {
            var group = new GameObject("DropPile");
            group.transform.SetParent(shell, false);
            Vector3 at = MarketBuilder.ButcherDropSquare;

            LevelBuildKit.Box("Pallet", group.transform, new Vector3(at.x, 0.06f, at.z),
                new Vector3(1.9f, 0.12f, 1.9f), LevelBuildKit.Mat("Butcher_Pallet", new Color(0.55f, 0.38f, 0.22f)),
                castShadow: false);

            var buffer = LevelBuildKit.Buffer("market.butcher.drop", "DropBuffer", group.transform, at,
                items.Meat, DropCapacity);

            // Two layers of joints on the pallet, three across and four deep.
            var display = buffer.gameObject.AddComponent<ShelfDisplay>();
            display.buffer = buffer;
            display.columns = 3;
            display.rowsPerBoard = 4;
            display.pitchX = 0.6f;
            display.pitchZ = 0.34f;
            display.boardHeights = new[] { 0.12f, 0.36f };
            display.maxUnitHeight = 0.5f;

            var deposit = LevelBuildKit.Station<DepositStation>("market.butcher.drop.deposit", "DropOff",
                group.transform, at, new Vector2(2.2f, 2.2f), "Raw Meat", new Color(0.42f, 0.72f, 1f),
                smallestCard: new Vector2(2.2f, 2.2f));
            deposit.target = buffer;
            deposit.icon = SquareIcon.Feed;

            group.SetActive(false);
            return new Drop { Root = group, Buffer = buffer };
        }

        // ---- the cutting table ---------------------------------------------------------------

        private class Cutting
        {
            public GameObject Root;
            public CutStation Cut;
        }

        private static Cutting BuildCut(Transform shell, FarmSceneBuilder.Items items, ItemBuffer meat,
            Counter counter)
        {
            var group = new GameObject("CutTable");
            group.transform.SetParent(shell, false);

            // Against the market's wall, beside the cut square; turned a quarter so its long side runs
            // north-south. "Stall" is the name the layout audit measures counters and tables by.
            var table = ButcherModels.CuttingTable(group.transform, MarketBuilder.ButcherTable, 90f);
            table.name = "Stall";
            Solid(group.transform, "Collision", MarketBuilder.ButcherTable + new Vector3(0f, 0.55f, 0f),
                new Vector3(1.3f, 1.1f, 2.0f));
            Report("CuttingTable", table);

            var cut = LevelBuildKit.Station<CutStation>("market.butcher.cut", "Cut", group.transform,
                MarketBuilder.ButcherCutSquare, new Vector2(2.4f, 2.4f), "Cut", new Color(0.95f, 0.55f, 0.5f),
                smallestCard: new Vector2(2.4f, 2.4f));
            cut.meat = meat;
            cut.output = items.Steak;
            cut.makes = 1;
            cut.outputCase = counter.SteakCase;
            // 2 s a joint by hand: the cutter fills its arms (6-8 units) in about 15 s, and the pigs make
            // at most one joint every 2.5 s, so the table is never the bottleneck once the meat is there.
            cut.mode = InteractionMode.Task;
            cut.taskDuration = 2f;

            group.SetActive(false);
            return new Cutting { Root = group, Cut = cut };
        }

        /// <summary>
        /// The sausage grinder: a bench north of the cutting table, with its own GRIND square, making three
        /// sausages from one raw joint. Brought by the Sausages purchase (which sits on the GRIND square).
        /// One place, one product: the table makes steaks and this makes sausages.
        /// </summary>
        private static Cutting BuildGrinder(Transform shell, FarmSceneBuilder.Items items, ItemBuffer meat,
            Counter counter)
        {
            var group = new GameObject("Grinder");
            group.transform.SetParent(shell, false);

            // Turned a quarter like the table, so the nozzle (the model's -z) faces west, at the GRIND square.
            var bench = ButcherModels.Grinder(group.transform, MarketBuilder.ButcherGrinder, 90f);
            bench.name = "Stall";
            Solid(group.transform, "Collision", MarketBuilder.ButcherGrinder + new Vector3(0f, 0.55f, 0f),
                new Vector3(1.2f, 1.1f, 1.4f));
            Report("Grinder", bench);

            var grind = LevelBuildKit.Station<CutStation>("market.butcher.grind", "Grind", group.transform,
                MarketBuilder.ButcherGrindSquare, new Vector2(2.4f, 2.4f), "Grind", new Color(0.95f, 0.55f, 0.5f),
                smallestCard: new Vector2(2.4f, 2.4f));
            grind.meat = meat;
            grind.output = items.Sausage;
            grind.makes = 3;
            grind.outputCase = counter.SausageCase;
            grind.mode = InteractionMode.Task;
            grind.taskDuration = 2f;

            group.SetActive(false);
            return new Cutting { Root = group, Cut = grind };
        }

        // ---- the counter ---------------------------------------------------------------------

        private class Counter
        {
            public GameObject Root;
            public RegisterStation Register;
            public ItemBuffer SteakCase;
            public ItemBuffer SausageCase;
            public GameObject SausageLine;
        }

        /// <summary>
        /// The display counter with its glass cases, the square behind it that serves and restocks, the
        /// cash pile, and the queue of shoppers who come in from the street. One group, switched on by
        /// the steak counter purchase; the sausage case and its product wait in a child for their own.
        /// </summary>
        private static Counter BuildCounter(Transform shell, FarmSceneBuilder.Items items)
        {
            var counter = new Counter();

            var group = new GameObject("ButcherCounter");
            group.transform.SetParent(shell, false);
            LevelBuildKit.Identify(group, "market.butcher");   // the queue's reputation is saved under this
            counter.Root = group;

            // Two display cases end to end. Stretched along the counter's length only, so the glass,
            // the stripe and the rounded ends stretch with it and the case stays the approved height.
            foreach (float x in CasePieceX)
            {
                var piece = ButcherModels.DisplayCounter(group.transform, new Vector3(x, 0f, CaseLineZ), 0f);
                piece.name = "Stall";
                piece.transform.localScale = new Vector3(CaseStretch, 1f, 1f);
                Report("DisplayCounter", piece);
            }

            // One solid line across the whole room, wall to wall (the cases above are drawn a little in
            // from the walls), so shoppers' side and staff side are properly separate.
            Solid(group.transform, "Collision", new Vector3(-16.5f, 0.6f, CaseLineZ), new Vector3(8.6f, 1.2f, 1.0f));

            // The two cases: steaks in the west one, sausages in the east. Units stand on the tray, two
            // rows of six, exactly the cells the approved preview used.
            counter.SteakCase = BuildCase(group.transform, "SteakCase", "market.butcher.steak",
                CasePieceX[0], items.Steak, rowPitch: 0.40f);

            // The sausage case and its product are a child, off until the sausage line is bought.
            var line = new GameObject("SausageLine");
            line.transform.SetParent(group.transform, false);
            counter.SausageCase = BuildCase(line.transform, "SausageCase", "market.butcher.sausage",
                CasePieceX[1], items.Sausage, rowPitch: 0.38f);
            counter.SausageCase.gameObject.AddComponent<ProductOffer>().item = items.Sausage;
            line.SetActive(false);
            counter.SausageLine = line;

            // What shoppers may ask for: steak now, sausage once its line is on.
            counter.SteakCase.gameObject.AddComponent<ProductOffer>().item = items.Steak;

            // The square behind the counter: where the player (or the butcher) restocks the cases and
            // serves the shopper at the front out of them. Smaller than the usual card on purpose: the
            // strip behind the counter is only 2.6 m deep and 5.8 m wide for three squares.
            Vector2 serveSize = new Vector2(1.8f, 2.0f);
            counter.Register = LevelBuildKit.Station<RegisterStation>("market.butcher.counter", "Counter",
                group.transform, MarketBuilder.ButcherCounterSquare, serveSize, "Counter",
                new Color(0.45f, 0.85f, 0.6f), smallestCard: serveSize);
            counter.Register.sells = new[] { items.Steak, items.Sausage };
            counter.Register.stock = new[] { counter.SteakCase, counter.SausageCase };
            counter.Register.priceMultiplier = 1f;

            // The takings lie on the cash pile at the west end; only the player's square gathers them.
            var cash = LevelBuildKit.BuildCashPoint("market.butcher", group.transform,
                MarketBuilder.ButcherCashPile, new Vector2(1.8f, 1.5f));
            counter.Register.pile = cash.pile;

            // ---- the shoppers -------------------------------------------------------------
            var queue = group.AddComponent<CustomerQueue>();
            queue.customerTemplate = LevelBuildKit.BuildCustomerTemplate(group.transform);
            queue.spawnPoint = Marker(group.transform, "ButcherSpawn", StreetPoint);
            queue.exitPoint = Marker(group.transform, "ButcherExit", StreetPoint);
            queue.counterPoint = Marker(group.transform, "ButcherCounterPoint",
                new Vector3(MarketBuilder.ButcherQueueFront.x, 0f, CaseLineZ));

            // The queue runs south from the counter's front, nearest first, in line with the door.
            queue.slots = new Transform[QueueSlots];
            for (int i = 0; i < QueueSlots; i++)
                queue.slots[i] = Marker(group.transform, $"ButcherSlot_{i}",
                    MarketBuilder.ButcherQueueFront + new Vector3(0f, 0f, -1.35f * i));

            queue.entryRoute = Markers(group.transform, "ButcherIn", EntryRoute);
            queue.exitRoute = Markers(group.transform, "ButcherOut", ExitRoute);

            queue.register = counter.Register;
            counter.Register.queue = queue;
            queue.spawnIntervalSeconds = ShopperIntervalSeconds;
            queue.minOrder = 1;
            queue.maxOrder = 3;
            queue.demandPerCapacity = 0.5f;
            queue.maxDemand = 3f;

            group.SetActive(false);
            return counter;
        }

        /// <summary>One glass case: a buffer on the tray with its goods shown unit by unit.</summary>
        private static ItemBuffer BuildCase(Transform parent, string name, string id, float x,
            ItemDefinition item, float rowPitch)
        {
            var buffer = LevelBuildKit.Buffer(id, name, parent,
                new Vector3(x, ButcherModels.CounterTrayY, CaseLineZ + 0.05f), item, CaseCapacity);

            var display = buffer.gameObject.AddComponent<ShelfDisplay>();
            display.buffer = buffer;
            display.columns = 6;
            display.rowsPerBoard = 2;
            display.pitchX = 0.62f;
            display.pitchZ = rowPitch;
            display.boardHeights = new[] { 0f };
            display.maxUnitHeight = 0.5f;
            return buffer;
        }

        // ---- small helpers -------------------------------------------------------------------

        /// <summary>An invisible solid: a collider the player is stopped by and a navmesh obstacle that carves it.</summary>
        private static void Solid(Transform parent, string name, Vector3 centre, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = centre;
            go.AddComponent<BoxCollider>().size = size;
            var obstacle = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = size;
            obstacle.carving = true;
        }

        private static Transform Marker(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go.transform;
        }

        private static Transform[] Markers(Transform parent, string prefix, Vector3[] points)
        {
            var result = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++) result[i] = Marker(parent, $"{prefix}_{i}", points[i]);
            return result;
        }

        /// <summary>Logs where a placed model really stands, in the market's grid, so the layout can be checked off the build log.</summary>
        private static void Report(string label, GameObject model)
        {
            var filter = model.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return;
            var b = filter.sharedMesh.bounds;
            var up = model.transform.parent;
            Vector3 lo = up.InverseTransformPoint(model.transform.TransformPoint(b.min));
            Vector3 hi = up.InverseTransformPoint(model.transform.TransformPoint(b.max));
            Debug.Log($"[ButcherBuilder] {label} at x {Mathf.Min(lo.x, hi.x):0.00}..{Mathf.Max(lo.x, hi.x):0.00}, " +
                      $"z {Mathf.Min(lo.z, hi.z):0.00}..{Mathf.Max(lo.z, hi.z):0.00} (the market's grid)");
        }
    }
}
