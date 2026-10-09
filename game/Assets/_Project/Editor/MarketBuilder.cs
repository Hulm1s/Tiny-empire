using System.Collections.Generic;
using Tycoon.Config;
using Tycoon.Core;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Generates the supermarket, "Tiny Market": the second location.
    ///
    /// The same idea as <see cref="FarmSceneBuilder"/> and written the same way - every layout
    /// and economy number is a commented constant in this one file. It reuses the farm's
    /// parts (stations, buffers, workers, the customer queue) rather than growing its own, so
    /// the market is mostly a new arrangement of things that already work.
    ///
    /// The market is a second root object in the same scene, turned the same 45 degrees as the
    /// farm so that its local axes read off the screen the same way, and standing seventy-odd
    /// metres down the farm's road axis behind its own walls and fence. The only way between
    /// the two is the pair of travel squares.
    ///
    /// Everything in it is saved under a "market." id, and the whole root is saved inactive:
    /// it does not tick, save or exist for the player until the farm's supermarket gate is paid.
    ///
    /// The shop is a derelict when the player arrives and is brought back in stages, each of
    /// which reveals the next:
    ///
    ///     carry the rubbish out to the bin (free)
    ///        then REPAIR, which costs money, and the first PAINT job, which costs more
    ///           then OPEN, which is free
    ///              then the shop works: supply, shelves, shoppers, checkout, hires
    ///                 then it grows, one product at a time (see BuildGrowth):
    ///                    a shelf, then that shelf's goods ordered, then the next shelf ...
    ///                    with a second checkout arriving alongside the first new product
    ///
    /// Two rows of three shelves on the shop floor and two storage rooms up the east wall:
    /// sklad1 (north) supplies the first row, sklad2 (south) the second. The checkouts stand
    /// side by side in the south-west, below the second row; the paint corner is under the
    /// south room and COMING SOON just inside the front door.
    /// </summary>
    internal static class MarketBuilder
    {
        internal class Result
        {
            public GameObject Root;
            public Location Location;
        }

        // ---- where the world ends --------------------------------------------------
        // The fence, the invisible wall and the grass all read these. The shop itself spans
        // x -12..12, z -5.4..15.6; the edge is held three metres clear of it, with the road
        // running through the south end exactly as it does on the farm.
        private const float EdgeWest = -17f;
        private const float EdgeEast = 17f;
        private const float EdgeNorth = 18.6f;
        private const float EdgeSouth = -13f;

        /// <summary>Where the tarmac lies, in the market's grid. The road box is 2.8 m deep.</summary>
        private const float RoadZ = -9f;

        // ---- the building ----------------------------------------------------------
        // No roof: the camera looks down into the shop, so the interior is always visible.
        // That is the whole reason for the next few numbers.
        //
        // The camera looks up the screen (local +Z) and a little to the east (+X), so the
        // north and east walls are BEHIND the shop floor from where it sits and can be full
        // height without hiding anything, while the south and west walls are in FRONT of it
        // and are cut down to a low kerb. Stood at full height they would hide the checkout and
        // the whole west end of the floor.
        private const float ShopWest = -12f;
        private const float ShopEast = 12f;
        private const float ShopSouth = -5.4f;
        private const float ShopNorth = 15.6f;

        private const float WallThickness = 0.4f;
        private const float TallWall = 3.4f;
        private const float LowWall = 0.9f;

        /// <summary>Half the width of the doorway in the south wall. The shoppers' only way in.</summary>
        private const float DoorHalf = 1.8f;

        // ---- the storage rooms ------------------------------------------------------
        // Two rooms stacked up the east wall, walled off from the shop floor: sklad1 in the
        // north holds the products the shop opens with (egg, milk, corn), sklad2 in the south
        // the ones it adds later (bread, apples, yogurt). Their north and east walls are the
        // shop's own tall ones; the partitions that face the camera are low kerbs like the
        // shop's own front walls, so the crates inside stay in view.
        //
        //     z 15.4  -----------------------------------------------   shop's north wall
        //             hire B | .. | crates            (sklad1)
        //             door 1 |    | collect squares
        //             hire A | .. |
        //     z  8.0  ------ partition ------------------------------
        //     z  7.6  door 2 ... |  crates            (sklad2)
        //             hire B | collect squares
        //             hire A |
        //     z  0.2  ------ partition ------------------------------   paint corner below
        //
        // Each doorway has to pass a NavMeshAgent of radius 0.3 with the walls carving 0.3
        // either side of it: 2.6 m of opening leaves 2.0 m of walkable ground, where the agent
        // needs 0.6. The stockers' hire squares are on the room side, BESIDE the doorway (above
        // and below it along the west partition), so the player walking in and out of the room
        // all day never crosses one by accident; the collect squares are in front of the crates.
        private const float RoomWest = 5f;        // centre line of the west partition
        private const float RoomMid = 7.8f;       // centre line of the partition between the rooms
        private const float RoomSouth = 0f;       // centre line of sklad2's south partition

        private const float Door1From = 10.4f;    // sklad1's opening, along the west partition (z)
        private const float Door1To = 13.0f;

        // Sklad2's opening runs right up to the partition between the rooms (7.8 less half its
        // thickness), so there is no wall piece above it.
        private const float Door2From = 4.8f;
        private const float Door2To = 7.6f;

        // ---- travel ----------------------------------------------------------------
        // The way back to the farm sits on the east end of the road, level with it, and the
        // player lands a few metres inside it. Shoppers come in from the west and leave short
        // of the square so none of them walk across it.
        private static readonly Vector3 ToFarmPosition = new Vector3(13.5f, 0f, RoadZ);
        private static readonly Vector3 ArrivalPoint = new Vector3(8f, 0f, -6.3f);

        // ---- the fittings ----------------------------------------------------------
        // Six shelves in two rows of three, in three columns. The first row stands against the
        // north wall and is what the shop opens with; the second is a free-standing row a little
        // over five metres south of it, bought one shelf at a time. Both face south, so a
        // shopper stands on the south side of either.
        //
        // The columns are 4.8 m apart with 1.2 m of walkway between shelves, and the middle gap
        // lines up with the front door, so there is a straight lane from the door to the north
        // aisle. West of the first column is a bay against the north wall left empty on purpose:
        // the COMING SOON square goes there.
        //
        // Order, west to east: corn, milk, egg in the north row (storage is on the east wall,
        // so the dearest product has the shortest carry); bread, apples, yogurt in the south row
        // in the order they are bought.
        private static readonly float[] ShelfX = { -7.2f, -2.4f, 2.4f };
        private const float Row1Z = 14.9f;      // against the north wall (face at 15.4)
        private const float Row2Z = 9.6f;       // 1.8 m of aisle between its back and row 1's squares

        // The stocking square lies right up against its shelf: its far edge is a few
        // centimetres short of where shoppers stand and the shelf is a short step beyond that.
        // Measured from the shelf's own z, so both rows share the same arithmetic.
        private const float StockOffset = 2.0f;
        private const float StockSquareDepth = 1.8f;
        private const float BrowseOffset = 0.75f;

        /// <summary>
        /// Where the butcher will go: a locked bay just inside the front door on the east side,
        /// south-west of the PAINT square, where every shopper and the player walk past it.
        /// </summary>
        private static readonly Vector3 ComingSoonPosition = new Vector3(5.2f, 0f, -3.2f);

        // The checkouts: two counters side by side in the west half of the shop, below the second
        // shelf row, each running north-south. Each has its serving square on the counter's WEST
        // side at the south end, and its cashier's hire square just north of that. Shoppers come
        // down from the aisles and queue southward along the counter's EAST side (the front of
        // the queue at the south end, level with the serving square), pay, then turn east along
        // the bottom of the shop (z = ExitLaneZ) to the door. No leg of that crosses a counter.
        private const float CounterZ = 0.2f;            // centre of the counter (3.4 m long, N-S)
        private const float Counter1X = -8.5f;
        private const float Counter2X = -3.9f;          // 4.6 m pitch: room for a queue between units
        private const float ServeDX = -1.9f;            // serving square, west of the counter
        private const float ServeZ = -1.05f;            // south end of the counter
        // Smaller than the default card minimum on purpose: the cashier only has to stand in it.
        private static readonly Vector2 ServeSize = new Vector2(1.8f, 1.6f);
        private const float HireZ = 1.2f;              // cashier hire, just north of the serving square
        private const float CounterHalfLength = 1.7f;
        private const float QueueApproachClearance = 1.2f;  // north of the counters' far end
        private const float QueueExitDrop = 1.0f;            // below the counters' near end
        private const float QueueDX = 0.85f;            // queue line, east of the counter
        private const float QueuePitch = 1.35f;         // slots run north from the serving square
        private const float ExitLaneZ = -4.7f;          // the walk east along the south wall

        // ---- stock and supply --------------------------------------------------------
        // Goods reach the shop by themselves: each product has a crate in a back room that
        // a supplier tops up on a fixed clock, taking nothing from the farm. The player (or a
        // stocker) carries from the crate to the shelf; shoppers carry from the shelf to the
        // checkout. The shelf is the only place goods are sold from.
        //
        // 4 s a unit is 15 a minute a product. A shopper basket averages about three units
        // every six seconds across all the shelves, so one product sells roughly ten a
        // minute: supply is comfortably ahead of a kept-up shelf, and short of it only when
        // the carrying stops.
        private const float SupplySecondsPerUnit = 4f;
        private const int ShelfCapacity = 12;
        private const int StorageCapacity = 20;

        /// <summary>
        /// Crates stand against the rooms' east wall, in a column; their collect squares are
        /// in front of them, inside the room.
        /// </summary>
        private const float CrateX = 11.1f;
        private const float CollectX = 9.1f;

        // Crate z by product. Pitch 2.4 m: a crate is 1.8 long and a collect card 2.1, so the
        // cards sit 0.3 m apart. In sklad1 corn is level with the doorway; in sklad2 the
        // first product bought (bread) is the crate nearest the doorway.
        private const float CrateCornZ = 11.6f;
        private const float CrateMilkZ = 9.2f;
        private const float CrateEggZ = 14.0f;
        private const float CrateBreadZ = 6.3f;
        private const float CrateApplesZ = 3.9f;
        private const float CrateYogurtZ = 1.5f;

        // ---- shoppers and the checkouts -----------------------------------------------
        // Shoppers come along the road from the west, go in through the door, visit the shelves
        // they want, then queue at whichever checkout is shorter and leave the way they came.
        // They steer in straight lines (there is no navmesh for shoppers), so every leg is a
        // waypoint that keeps the line clear of a wall or a shelf: they only ever cross the
        // south wall at the door, at x = 0.
        //
        // The shelves are not all in sight of one another, so the shop has a small tree of
        // aisle nodes (see CustomerQueue's StoreShelf.via): a hub in the hall below the second
        // row, a lane up the gap between the middle and east columns, and a node on each
        // shelf's own aisle. A shopper backs out only as far as two shelves differ.
        //
        // One shopper every six seconds at full reputation with the three opening lines, and
        // a quarter faster for each line added after that (capped at double): see
        // CustomerQueue.LineDemand. A basket is one or two products and one to three of each.
        private const float ShopperIntervalSeconds = 6f;
        private const int QueueSlots = 3;
        private const int MaxShoppers = 8;

        private static readonly Vector3 ShopperSpawn = new Vector3(-12f, 0f, RoadZ);
        private static readonly Vector3 ShopperExit = new Vector3(11f, 0f, RoadZ);

        /// <summary>Outside the door, then just inside it.</summary>
        private static readonly Vector3[] EntryRoute =
        {
            new Vector3(0f, 0f, -7.2f),
            new Vector3(0f, 0f, -4.7f),
        };

        /// <summary>The hub: the hall's centre, where every route through the shop starts.</summary>
        private static readonly Vector3 HubPoint = new Vector3(0f, 0f, 5f);

        /// <summary>The south row's aisle, on the hall side of its stocking squares.</summary>
        private const float HallAisleZ = 5f;

        /// <summary>
        /// The north aisle: between the second row's backs (10.05) and the first row's
        /// stocking squares (11.85). The lane joins it to the hub up the middle gap.
        /// </summary>
        private const float NorthAisleZ = 10.95f;

        /// <summary>
        /// None: the queues run north from the counters' south ends and shoppers come at them from the
        /// north, so the straight line from the hub to every place is already clear of the
        /// counters. The audit checks every one of those legs against the counters and the walls.
        /// </summary>
        private static readonly Vector3[] QueueApproach = { };

        /// <summary>
        /// Each till prepends its own first leg (front of its queue down to the exit lane, see
        /// BuildTill); then east along the lane to the door, out of it and down to the road. Down to the road centre line
        /// (z = -9) before turning east, so the walk to the exit stays below the bin's square
        /// on the forecourt.
        /// </summary>
        private static readonly Vector3[] ExitRoute =
        {
            new Vector3(0f, 0f, ExitLaneZ),
            new Vector3(0f, 0f, -7.2f),
            new Vector3(0f, 0f, RoadZ),
        };

        // ---- staff -------------------------------------------------------------------
        // Two stockers per storage room, hired beside the room's doorway, and a cashier at each
        // checkout - the same "taken on where the job happens" rule as the farm.
        //
        // A stocker does exactly what the player does between a crate and a shelf, so it is the
        // existing WorkerAgent, now with a list of routes: every crate in its room to that
        // crate's shelf. Whenever it is empty-handed it picks the shelf that needs stock most
        // and that the other stocker is not already doing (see WorkerAgent.routes). Sklad1's
        // stockers only ever carry egg, milk and corn, sklad2's bread, apples and yogurt.
        // The cashier has no journey at all: pickup and drop are both the checkout, its arms
        // never fill, and it simply stands there scanning (see CheckoutStation for how its fee
        // is charged). It steps aside whenever the player serves, as every worker does.
        //
        // Prices sit between a farm hand (250-700) and a dairy hand (1500-2400): a stocker
        // pays back in a few shelves of sales, the cashier is the bigger step because it
        // frees the player entirely from the till.
        private const double StockerPrice = 800d;
        private const double StockerFee = 0d;   // staff cost only their hire price
        private const double CashierPrice = 1500d;
        private const double CashierFee = 0d;

        /// <summary>Stocker hire squares: beside each doorway, on the room side, not in it.</summary>
        private static readonly Vector3[] Stocker1Hires =
        {
            new Vector3(6.5f, 0f, 9.2f),    // below sklad1's doorway
            new Vector3(6.5f, 0f, 14.25f),  // above it
        };

        private static readonly Vector3[] Stocker2Hires =
        {
            new Vector3(6.5f, 0f, 1.35f),   // both below sklad2's doorway
            new Vector3(6.5f, 0f, 3.55f),
        };

        /// <summary>
        /// Just north of each serving square, so the cashier is hired where they will work.
        /// Level with the paint corner on the east side, and clear of the partition above.
        /// </summary>
        private static readonly Vector3 Cashier1HirePosition = new Vector3(Counter1X + ServeDX, 0f, HireZ);
        private static readonly Vector3 Cashier2HirePosition = new Vector3(Counter2X + ServeDX, 0f, HireZ);

        // ---- growth ------------------------------------------------------------------
        // After the shop is staffed it grows one product at a time, in the order bread, apples,
        // yogurt. For each: its shelf is bought first (the second row), and only then is its
        // goods' order offered; ordering reveals its crate, collect square, stocking square and
        // the NEXT shelf. So there is never a product without a shelf to stand on, and only one
        // order is ever open at a time. The second checkout arrives with the first order, and
        // its cashier with it. COMING SOON appears once all three are in.
        //
        // Prices, in order bread / apples / yogurt. Each product costs more than the last, and
        // each shelf-and-order pair roughly pays for itself in a few minutes of its own sales:
        // bread sells for 10, apples for 6, yogurt for 15.
        private static readonly double[] GrowthShelfPrices = { 1000d, 1500d, 2000d };
        private static readonly double[] GrowthOrderPrices = { 1500d, 2500d, 3500d };
        private const double Checkout2Price = 3000d;

        /// <summary>
        /// The bin is OUTSIDE the shop, on the forecourt at the east end, because the shop's
        /// rubbish is carried out to it. It is mirrored (square to the south) because the ground
        /// its square needs is the forecourt, south of the can.
        /// </summary>
        private static readonly Vector3 BinPosition = new Vector3(10.5f, 0f, -6.4f);

        // ---- renovation ------------------------------------------------------------
        // Where each step of the renovation is done. Spread across the floor so the player
        // walks the shop rather than standing in one spot, and held clear of every square the
        // working shop will use (the audit checks both states).
        //
        // The rubbish is CARRIED, not swept: each pile is a heap of bags the player picks up
        // like any other goods and walks out to the bin, so cleaning takes trips and not just
        // standing still. Four piles of four bags is sixteen bags and the player carries eight,
        // so it takes at least two trips to the bin, and a player who is not paying attention
        // to the carry limit takes three. It costs nothing, so a broke player can always start.
        private static readonly Vector3[] TrashPositions =
        {
            new Vector3(-9.2f, 0f, 1.5f),
            new Vector3(-4.8f, 0f, 4.6f),
            new Vector3(2.2f, 0f, 4.6f),
            new Vector3(3.4f, 0f, 1.0f),
        };

        private const int BagsPerPile = 4;

        /// <summary>
        /// What the money steps cost. Repair is dearer than the first paint job because it
        /// replaces the shelves and the checkout, which is what makes the shop able to trade.
        /// The first confirmed paint job is the renovation step; every repaint after it is cheap
        /// enough to try a different look.
        /// </summary>
        private const double RepairPrice = 1500d;
        private const double FirstPaintPrice = 1000d;
        private const double RepaintPrice = 200d;

        private static readonly Vector3 RepairPosition = new Vector3(-4.0f, 0f, 1.2f);
        // The paint corner: south-east, under the south storage room and off every route.
        private static readonly Vector3 PaintPosition = new Vector3(9.6f, 0f, -1.55f);
        private static readonly Vector3 OpenPosition = new Vector3(0f, 0f, 1.2f);

        internal static Result Build(Transform farmRoot, FarmSceneBuilder.Items items,
            Location farm, float offsetX)
        {
            // Same yaw as the farm, so "up the screen" is local +Z here too. Placed by the
            // farm's own rotation, because offsetX is measured along the farm's road axis.
            var go = new GameObject("Market");
            go.transform.rotation = farmRoot.rotation;
            go.transform.position = farmRoot.position + farmRoot.rotation * new Vector3(offsetX, 0f, 0f);
            var root = go.transform;

            var location = go.AddComponent<Location>();
            location.locationId = "market";
            location.displayName = "Tiny Market";

            var arrival = new GameObject("Spawn");
            arrival.transform.SetParent(root, false);
            arrival.transform.localPosition = ArrivalPoint;
            location.spawnPoint = arrival.transform;

            BuildGround(root);
            BuildEdges(root);

            var toFarm = LevelBuildKit.Station<TravelStation>("market.travel.farm", "TravelToFarm",
                root, ToFarmPosition, new Vector2(2.5f, 2.1f), "Farm", new Color(0.55f, 0.8f, 1f));
            toFarm.destination = farm;
            toFarm.taskDuration = 1.2f;

            // The bin, outside on the forecourt. It is where the renovation's rubbish ends up,
            // and also the way out for any other goods a player is left holding: every place
            // with a carry stack has a way to empty it.
            LevelBuildKit.BuildBin("market.bin", "Bin", root, BinPosition, squareToSouth: true);

            var shop = BuildShop(root);
            BuildStock(root, shop, items);
            BuildCheckout(root, shop);
            BuildStaff(root, shop);
            BuildRenovation(root, shop, items);
            BuildGrowth(root, shop);

            return new Result { Root = go, Location = location };
        }

        // ---- ground, road, edges -----------------------------------------------------

        private static void BuildGround(Transform root)
        {
            var tarmac = LevelBuildKit.Mat("Road", new Color(0.36f, 0.36f, 0.39f));
            var markings = LevelBuildKit.Mat("RoadLine", new Color(0.88f, 0.88f, 0.8f));
            var paving = LevelBuildKit.Mat("Market_Paving", new Color(0.72f, 0.71f, 0.68f));

            // The farm's road comes out of the shopfront helper, whose dashes run twice as far
            // as the tarmac does; this one is drawn to its own length so the market's road
            // ends where it says it does.
            const float roadHalfLength = 24f;
            LevelBuildKit.Box("Road", root, new Vector3(0f, 0.02f, RoadZ),
                new Vector3(roadHalfLength * 2f, 0.04f, 2.8f), tarmac, castShadow: false);

            for (int x = -(int)roadHalfLength; x <= (int)roadHalfLength; x += 2)
            {
                LevelBuildKit.Box($"Line_{x}", root, new Vector3(x, 0.05f, RoadZ),
                    new Vector3(0.9f, 0.04f, 0.14f), markings, castShadow: false);
            }

            // The forecourt between the road and the door.
            LevelBuildKit.Box("Forecourt", root, new Vector3(0f, 0.025f, -6.3f),
                new Vector3(28f, 0.05f, 2.6f), paving, castShadow: false);

            // Grass everywhere the shop, the road and the squares are not. Kept clear of a
            // tuft's 40 cm height poking through any interaction square painted 9 cm up.
            const float beyond = 11f;
            var area = new Rect(EdgeWest - beyond, EdgeSouth - beyond,
                                (EdgeEast - EdgeWest) + beyond * 2f,
                                (EdgeNorth - EdgeSouth) + beyond * 2f);

            var keepClear = new[]
            {
                // The shop and everything standing inside it.
                new Rect(-14f, -7f, 28f, 26f),
                // The road, the forecourt between it and the shop, and the travel square.
                new Rect(EdgeWest - 1f, -11.5f, (EdgeEast - EdgeWest) + 2f, 5f),
            };

            LevelBuildKit.BuildGrass(root, area, keepClear, tufts: 900, seed: 20261007,
                meshName: "MarketGrassCover", objectName: "Grass");
        }

        private static void BuildEdges(Transform root)
        {
            LevelBuildKit.BuildBoundary(root, EdgeWest, EdgeEast, EdgeSouth, EdgeNorth);
            LevelBuildKit.BuildFenceAround(root, EdgeWest, EdgeEast, EdgeSouth, EdgeNorth, RoadZ,
                meshName: "MarketFenceLine");
        }

        // ---- the shop ----------------------------------------------------------------

        /// <summary>
        /// The pieces of the shop that change as it is renovated. Each is a set of objects the
        /// stages switch on and off, so a stage is one list rather than a pile of special cases.
        /// </summary>
        private class Shop
        {
            // Present while it is a ruin, gone once repaired.
            public readonly List<GameObject> Broken = new List<GameObject>();
            // Appear when repaired.
            public readonly List<GameObject> Working = new List<GameObject>();
            // Grubby until painted.
            public readonly List<GameObject> Dirty = new List<GameObject>();
            // Fresh once painted.
            public readonly List<GameObject> Painted = new List<GameObject>();
            // The sign over the shelves, in its three states.
            public GameObject SignDirty, SignClean, SignLit;
            // Switched on by OPEN: everything that only exists once the shop trades.
            public readonly List<GameObject> Opening = new List<GameObject>();

            // Every wall piece, the skirting and the floor of the painted coat: what the paint
            // menu changes.
            public readonly List<DecorSurface> Surfaces = new List<DecorSurface>();

            // All six product lines: sklad1's first (corn, milk, egg), then sklad2's.
            public readonly List<ShelfParts> Shelves = new List<ShelfParts>();

            // The two checkouts: the first opens with the shop, the second is bought.
            public readonly List<Till> Tills = new List<Till>();

            // The hires the growth steps wait on or reveal.
            public readonly List<UnlockStation> Stock1Hires = new List<UnlockStation>();
            public readonly List<UnlockStation> Stock2Hires = new List<UnlockStation>();
            public UnlockStation Cashier1Hire, Cashier2Hire;
        }

        /// <summary>One product line, end to end: crate, collect square, shelf, stocking square.</summary>
        private class ShelfParts
        {
            public string Key;
            public string Name;
            public ItemDefinition Item;

            /// <summary>The id the shelf is saved under: "market.shelf." + this.</summary>
            public string ShelfId;
            public float X, Z, CrateZ;

            /// <summary>
            /// True for the three products added after the shop opens: their shelf, crate, collect
            /// square and stocking square are revealed by purchases rather than by repairing and
            /// opening the shop.
            /// </summary>
            public bool Growth;

            public ItemBuffer Shelf;
            public ItemBuffer Storage;
            public CollectStation Collect;
            public DepositStation Stock;
            public Transform Stand;
            public GameObject ShelfObject;
            public GameObject CrateObject;
            public Transform[] Via;
        }

        /// <summary>One checkout: the counter, the serving square and the queue that goes with it.</summary>
        private class Till
        {
            public GameObject Root;
            public GameObject Desk;
            public CheckoutStation Serve;
            public Tycoon.Customers.CustomerQueue Queue;
        }

        /// <summary>
        /// Walls, floor, sign and the derelict fittings. The collision is built once and always
        /// present; what the player SEES comes in two coats (grubby and painted) so that the
        /// paint gate is a swap of two sets and nothing that blocks anyone ever blinks out.
        /// </summary>
        private static Shop BuildShop(Transform root)
        {
            var shop = new Shop();

            // ---- collision: always there, never drawn --------------------------------
            var colliders = new GameObject("WallColliders");
            colliders.transform.SetParent(root, false);
            foreach (var wall in WallSegments())
            {
                var piece = new GameObject("Collider");
                piece.transform.SetParent(colliders.transform, false);
                piece.transform.localPosition = new Vector3(wall.Centre.x, TallWall * 0.5f, wall.Centre.z);

                // Full height even where the wall is drawn low: a 90 cm kerb the player can
                // simply be stopped by is a kerb, and one they could climb is a bug.
                var size = new Vector3(wall.Size.x, TallWall, wall.Size.z);
                var box = piece.AddComponent<BoxCollider>();
                box.size = size;

                // Carves its own hole in the navmesh. The market is saved inactive, so the
                // runtime bake never sees these walls; the obstacle cuts them in the moment
                // the root switches on, which is how the farm's later buildings do it too.
                var obstacle = piece.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
                obstacle.size = size;
                obstacle.carving = true;
            }

            // ---- the two coats of paint ---------------------------------------------
            var grubby = new GameObject("Grubby");
            grubby.transform.SetParent(root, false);
            var fresh = new GameObject("Painted");
            fresh.transform.SetParent(root, false);

            var dirtyWall = LevelBuildKit.Mat("Market_WallDirty", new Color(0.52f, 0.47f, 0.41f));
            var cleanWall = LevelBuildKit.Mat("Market_WallClean", new Color(0.96f, 0.92f, 0.82f));
            var dirtyFloor = LevelBuildKit.Mat("Market_FloorDirty", new Color(0.33f, 0.31f, 0.28f));
            var cleanFloor = LevelBuildKit.Mat("Market_FloorClean", new Color(0.84f, 0.82f, 0.77f));
            var trim = LevelBuildKit.Mat("Market_Trim", new Color(0.82f, 0.28f, 0.28f));

            float floorDepth = ShopNorth - ShopSouth;
            float floorCentreZ = (ShopNorth + ShopSouth) * 0.5f;
            Vector3 floorSize = new Vector3(ShopEast - ShopWest, 0.04f, floorDepth);
            LevelBuildKit.Box("DirtyFloor", grubby.transform, new Vector3(0f, 0.02f, floorCentreZ),
                floorSize, dirtyFloor, castShadow: false);
            var floorBox = LevelBuildKit.Box("CleanFloor", fresh.transform, new Vector3(0f, 0.02f, floorCentreZ),
                floorSize, cleanFloor, castShadow: false);
            shop.Surfaces.Add(new DecorSurface
            {
                renderer = floorBox.GetComponent<Renderer>(),
                kind = DecorSurface.Kind.Floor,
                length = floorSize.x,
                height = floorSize.z,
                origin = new Vector2(ShopWest, ShopSouth),
            });

            foreach (var wall in WallSegments())
            {
                // "Wall" is the name the layout audit looks for when it measures what the
                // squares must keep clear of. The grubby set is deliberately named differently:
                // both coats occupy the same ground and must not be counted twice.
                LevelBuildKit.Box("DirtyWall", grubby.transform,
                    new Vector3(wall.Centre.x, wall.Height * 0.5f, wall.Centre.z),
                    new Vector3(wall.Size.x, wall.Height, wall.Size.z), dirtyWall,
                    castShadow: wall.Height > LowWall);

                var face = LevelBuildKit.Box("Wall", fresh.transform,
                    new Vector3(wall.Centre.x, wall.Height * 0.5f, wall.Centre.z),
                    new Vector3(wall.Size.x, wall.Height, wall.Size.z), cleanWall,
                    castShadow: wall.Height > LowWall);

                // The long face of a wall is the one the pattern is measured over, so a brick is
                // the same size on every piece: the pattern is tiled by the piece's real length
                // and height, and starts where the piece starts in the shop's own grid, so it
                // carries on unbroken from one piece to the next.
                bool alongX = wall.Size.x >= wall.Size.z;
                shop.Surfaces.Add(new DecorSurface
                {
                    renderer = face.GetComponent<Renderer>(),
                    kind = DecorSurface.Kind.Wall,
                    length = alongX ? wall.Size.x : wall.Size.z,
                    height = wall.Height,
                    origin = new Vector2(alongX ? wall.Centre.x - wall.Size.x * 0.5f
                                                : wall.Centre.z - wall.Size.z * 0.5f, 0f),
                });

                // A skirting on the painted walls, in the third colour.
                var skirting = LevelBuildKit.Box("Skirting", fresh.transform,
                    new Vector3(wall.Centre.x, 0.18f, wall.Centre.z),
                    new Vector3(wall.Size.x + 0.04f, 0.36f, wall.Size.z + 0.04f), trim,
                    castShadow: false);
                shop.Surfaces.Add(new DecorSurface
                {
                    renderer = skirting.GetComponent<Renderer>(),
                    kind = DecorSurface.Kind.Trim,
                });
            }

            shop.Dirty.Add(grubby);
            shop.Painted.Add(fresh);
            grubby.SetActive(true);
            fresh.SetActive(false);

            // ---- the sign over the shelves -------------------------------------------
            // On the north wall, because that is the one the camera faces. Three states: a
            // dull crooked board, a painted one, and a lit one once the shop is open.
            shop.SignDirty = BuildSign(root, "SignDirty",
                LevelBuildKit.Mat("Market_SignDirty", new Color(0.38f, 0.35f, 0.32f)), 4f);
            shop.SignClean = BuildSign(root, "SignClean",
                LevelBuildKit.Mat("Market_SignClean", new Color(0.86f, 0.3f, 0.3f)), 0f);
            shop.SignLit = BuildSign(root, "SignLit",
                LevelBuildKit.Mat("Market_SignLit", new Color(1f, 0.86f, 0.3f)), 0f);
            shop.SignDirty.SetActive(true);
            shop.SignClean.SetActive(false);
            shop.SignLit.SetActive(false);

            // ---- what is wrecked until it is repaired ---------------------------------
            for (int i = 0; i < ShelfX.Length; i++)
                shop.Broken.Add(BuildBrokenShelf(root, $"BrokenShelf_{i + 1}",
                    new Vector3(ShelfX[i], 0f, Row1Z), i));

            shop.Broken.Add(BuildBrokenDesk(root, new Vector3(Counter1X, 0f, CounterZ)));

            return shop;
        }

        private struct Wall
        {
            public Vector3 Centre;   // at ground level
            public Vector3 Size;     // footprint; the height is separate
            public float Height;
        }

        /// <summary>
        /// The runs of wall: north, east, west, the south wall in two halves either side of the
        /// door, and the storage rooms' partitions: two across (the south end of sklad2 and the
        /// one between the rooms) and the west one up the side in three pieces, round the two
        /// doorways.
        /// </summary>
        private static List<Wall> WallSegments()
        {
            float half = WallThickness * 0.5f;
            float spanX = (ShopEast - ShopWest) + WallThickness;
            float spanZ = (ShopNorth - ShopSouth) + WallThickness;
            float midZ = (ShopNorth + ShopSouth) * 0.5f;

            // Each half of the south wall runs from the outer corner to the edge of the door.
            float cornerX = ShopWest - half;
            float runLength = -DoorHalf - cornerX;
            float southCentre = (cornerX - DoorHalf) * 0.5f;   // negative: the west half

            // The rooms' partitions run between the outer faces of the walls they meet.
            float westFace = RoomWest - half;
            float eastInner = ShopEast - half;
            float northInner = ShopNorth - half;
            float partitionRun = eastInner - westFace;
            float partitionCentreX = westFace + partitionRun * 0.5f;

            var walls = new List<Wall>
            {
                new Wall { Centre = new Vector3(partitionCentreX, 0f, RoomSouth), Size = new Vector3(partitionRun, 0f, WallThickness), Height = LowWall },
                new Wall { Centre = new Vector3(partitionCentreX, 0f, RoomMid), Size = new Vector3(partitionRun, 0f, WallThickness), Height = LowWall },
                new Wall { Centre = new Vector3(0f, 0f, ShopNorth), Size = new Vector3(spanX, 0f, WallThickness), Height = TallWall },
                new Wall { Centre = new Vector3(ShopEast, 0f, midZ), Size = new Vector3(WallThickness, 0f, spanZ), Height = TallWall },
                new Wall { Centre = new Vector3(ShopWest, 0f, midZ), Size = new Vector3(WallThickness, 0f, spanZ), Height = LowWall },
                new Wall { Centre = new Vector3(southCentre, 0f, ShopSouth), Size = new Vector3(runLength, 0f, WallThickness), Height = LowWall },
                new Wall { Centre = new Vector3(-southCentre, 0f, ShopSouth), Size = new Vector3(runLength, 0f, WallThickness), Height = LowWall },
            };

            // The west partition: sklad2 below its doorway, then sklad1 below and above its own.
            // Between the doorways it is the partition between the rooms, already above.
            void Upright(float from, float to) => walls.Add(new Wall
            {
                Centre = new Vector3(RoomWest, 0f, (from + to) * 0.5f),
                Size = new Vector3(WallThickness, 0f, to - from),
                Height = LowWall
            });

            Upright(RoomSouth + half, Door2From);
            Upright(RoomMid + half, Door1From);
            Upright(Door1To, northInner);

            return walls;
        }

        private static GameObject BuildSign(Transform root, string name, Material material, float tilt)
        {
            var sign = new GameObject(name);
            sign.transform.SetParent(root, false);

            // A strip along the top of the north wall, above the shelves (which stop at 2.05 m)
            // and their count boards. The wall face is at ShopNorth - 0.2.
            float z = ShopNorth - WallThickness * 0.5f - 0.1f;
            LevelBuildKit.Box("Board", sign.transform, new Vector3(0f, 3.1f, z),
                new Vector3(7f, 0.5f, 0.12f), material, castShadow: false, rot: new Vector3(0f, 0f, tilt));
            return sign;
        }

        /// <summary>
        /// A shelf that has come apart: one upright leaning, a board hanging by a corner, another
        /// on the floor. Plain dark boxes; it only has to read as "not usable".
        /// </summary>
        private static GameObject BuildBrokenShelf(Transform root, string name, Vector3 position, int variant)
        {
            var wreck = new GameObject(name);
            wreck.transform.SetParent(root, false);
            wreck.transform.localPosition = position;

            var wood = LevelBuildKit.Mat("Market_BrokenWood", new Color(0.36f, 0.28f, 0.21f));
            var plank = LevelBuildKit.Mat("Market_BrokenPlank", new Color(0.46f, 0.36f, 0.26f));

            // Mirrored on alternate shelves so a row of three is not three copies.
            float flip = variant % 2 == 0 ? 1f : -1f;

            LevelBuildKit.Box("UprightL", wreck.transform, new Vector3(-1.6f * flip, 0.85f, 0f),
                new Vector3(0.12f, 1.7f, 0.6f), wood, castShadow: false, rot: new Vector3(0f, 0f, 14f * flip));
            LevelBuildKit.Box("UprightR", wreck.transform, new Vector3(1.6f * flip, 0.6f, 0f),
                new Vector3(0.12f, 1.2f, 0.6f), wood, castShadow: false, rot: new Vector3(0f, 0f, -6f * flip));
            LevelBuildKit.Box("BoardHanging", wreck.transform, new Vector3(0f, 0.75f, 0f),
                new Vector3(3.2f, 0.08f, 0.7f), plank, castShadow: false, rot: new Vector3(0f, 0f, -11f * flip));
            LevelBuildKit.Box("BoardFallen", wreck.transform, new Vector3(0.3f * flip, 0.07f, -0.9f),
                new Vector3(2.8f, 0.08f, 0.6f), plank, castShadow: false, rot: new Vector3(0f, 14f * flip, 0f));
            return wreck;
        }

        private static GameObject BuildBrokenDesk(Transform root, Vector3 position)
        {
            var wreck = new GameObject("BrokenDesk");
            wreck.transform.SetParent(root, false);
            wreck.transform.localPosition = position;
            wreck.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var wood = LevelBuildKit.Mat("Market_BrokenWood", new Color(0.36f, 0.28f, 0.21f));
            var metal = LevelBuildKit.Mat("Market_BrokenTill", new Color(0.25f, 0.26f, 0.28f));

            LevelBuildKit.Box("Counter", wreck.transform, new Vector3(0f, 0.4f, 0f),
                new Vector3(3.4f, 0.8f, 0.5f), wood, castShadow: false, rot: new Vector3(0f, 3f, 3f));
            LevelBuildKit.Box("Till", wreck.transform, new Vector3(-0.6f, 0.95f, 0f),
                new Vector3(0.6f, 0.3f, 0.45f), metal, castShadow: false, rot: new Vector3(0f, 20f, 16f));
            return wreck;
        }

        // ---- shelves, crates and the squares that join them ---------------------------

        private static void BuildStock(Transform root, Shop shop, FarmSceneBuilder.Items items)
        {
            var lines = new[]
            {
                // Sklad1, first row: what the shop opens with.
                new ShelfParts { Key = "corn", Name = "Corn", Item = items.Corn, ShelfId = "corn",
                    X = ShelfX[0], Z = Row1Z, CrateZ = CrateCornZ },
                new ShelfParts { Key = "milk", Name = "Milk", Item = items.Milk, ShelfId = "milk",
                    X = ShelfX[1], Z = Row1Z, CrateZ = CrateMilkZ },
                new ShelfParts { Key = "egg", Name = "Egg", Item = items.Egg, ShelfId = "egg",
                    X = ShelfX[2], Z = Row1Z, CrateZ = CrateEggZ },

                // Sklad2, second row: bought one at a time (see BuildGrowth). The shelf ids are
                // the shelves' own names on the plan: S21, S22, S23.
                new ShelfParts { Key = "bread", Name = "Bread", Item = items.Bread, ShelfId = "s21",
                    X = ShelfX[0], Z = Row2Z, CrateZ = CrateBreadZ, Growth = true },
                new ShelfParts { Key = "apples", Name = "Apples", Item = items.Apples, ShelfId = "s22",
                    X = ShelfX[1], Z = Row2Z, CrateZ = CrateApplesZ, Growth = true },
                new ShelfParts { Key = "yogurt", Name = "Yogurt", Item = items.Yogurt, ShelfId = "s23",
                    X = ShelfX[2], Z = Row2Z, CrateZ = CrateYogurtZ, Growth = true },
            };

            foreach (var line in lines)
            {
                BuildShelf(root, shop, line);
                BuildCrate(root, shop, line);

                line.Stock = LevelBuildKit.Station<DepositStation>($"market.stock.{line.Key}",
                    $"Stock{line.Name}", root, new Vector3(line.X, 0f, line.Z - StockOffset),
                    new Vector2(3.0f, StockSquareDepth), line.Name, new Color(0.42f, 0.72f, 1f));
                line.Stock.target = line.Shelf;
                line.Stock.icon = SquareIcon.Stock;
                line.Stock.gameObject.SetActive(false);

                line.Collect = LevelBuildKit.Station<CollectStation>($"market.collect.{line.Key}",
                    $"Collect{line.Name}", root, new Vector3(CollectX, 0f, line.CrateZ),
                    new Vector2(2.4f, 2.1f), "Collect", new Color(0.55f, 0.9f, 0.5f));
                line.Collect.source = line.Storage;
                line.Collect.gameObject.SetActive(false);

                // The opening three come on with the shop; the others wait for their order.
                if (!line.Growth)
                {
                    shop.Opening.Add(line.Stock.gameObject);
                    shop.Opening.Add(line.Collect.gameObject);
                }

                shop.Shelves.Add(line);
            }
        }

        /// <summary>
        /// A working shelf: two boards of goods under a coloured header, its stock shown as
        /// cubes. Saved inactive - repair switches the first row on, a shelf purchase the rest.
        /// </summary>
        private static void BuildShelf(Transform root, Shop shop, ShelfParts line)
        {
            var shelf = new GameObject($"Shelf_{line.Name}");
            shelf.transform.SetParent(root, false);
            shelf.transform.localPosition = new Vector3(line.X, 0f, line.Z);

            // "Shell" is the name the layout audit measures building footprints by.
            var shell = new GameObject("Shell");
            shell.transform.SetParent(shelf.transform, false);

            var wood = LevelBuildKit.Mat("Market_ShelfWood", new Color(0.72f, 0.54f, 0.34f));
            var dark = LevelBuildKit.Mat("Market_ShelfBack", new Color(0.55f, 0.4f, 0.26f));
            var header = LevelBuildKit.Mat($"Market_Header_{line.Key}", line.Item.color);

            LevelBuildKit.Box("Back", shell.transform, new Vector3(0f, 0.9f, 0.35f),
                new Vector3(3.6f, 1.8f, 0.1f), dark, castShadow: false);
            LevelBuildKit.Box("SideL", shell.transform, new Vector3(-1.76f, 0.85f, 0f),
                new Vector3(0.1f, 1.7f, 0.8f), wood, castShadow: false);
            LevelBuildKit.Box("SideR", shell.transform, new Vector3(1.76f, 0.85f, 0f),
                new Vector3(0.1f, 1.7f, 0.8f), wood, castShadow: false);
            foreach (float y in new[] { 0.1f, 0.62f, 1.14f })
                LevelBuildKit.Box($"Board_{y:0.00}", shell.transform, new Vector3(0f, y, 0f),
                    new Vector3(3.6f, 0.08f, 0.8f), wood, castShadow: false);
            LevelBuildKit.Box("Header", shell.transform, new Vector3(0f, 1.9f, 0.02f),
                new Vector3(3.6f, 0.3f, 0.86f), header, castShadow: false);

            // Solid, and cut out of the navmesh, so nobody walks through the goods.
            var body = shelf.AddComponent<BoxCollider>();
            body.size = new Vector3(3.6f, 2.0f, 0.8f);
            body.center = new Vector3(0f, 1f, 0f);
            var obstacle = shelf.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = body.size;
            obstacle.center = body.center;
            obstacle.carving = true;

            line.Shelf = LevelBuildKit.Buffer($"market.shelf.{line.ShelfId}", "ShelfBuffer",
                shelf.transform, Vector3.zero, line.Item, ShelfCapacity);

            var display = shelf.AddComponent<ShelfDisplay>();
            display.buffer = line.Shelf;
            display.columns = 6;
            display.rowsPerBoard = 1;
            display.pitchX = 0.55f;
            display.boardHeights = new[] { 0.66f, 1.18f };

            // No count board over the shelf. The stocking square in front of it already says
            // "8 / 12", on the ground where the player is standing when they act on it, and the
            // goods on the shelf show it too; a second readout above it only said it again.

            var stand = new GameObject($"Stand_{line.Name}");
            stand.transform.SetParent(root, false);
            stand.transform.localPosition = new Vector3(line.X, 0f, line.Z - BrowseOffset);
            line.Stand = stand.transform;

            shelf.SetActive(false);
            if (line.Growth) line.ShelfObject = shelf;
            else shop.Working.Add(shelf);
        }

        /// <summary>
        /// A supply crate in a back room. It is the item buffer, the supplier that fills it
        /// and the cubes that show it, all on one object so one save id covers them.
        /// </summary>
        private static void BuildCrate(Transform root, Shop shop, ShelfParts line)
        {
            line.Storage = LevelBuildKit.Buffer($"market.storage.{line.Key}", $"Storage{line.Name}",
                root, new Vector3(CrateX, 0f, line.CrateZ), line.Item, StorageCapacity);
            var crate = line.Storage.gameObject;

            var wood = LevelBuildKit.Mat("Market_CrateWood", new Color(0.66f, 0.5f, 0.32f));
            var band = LevelBuildKit.Mat($"Market_Band_{line.Key}", line.Item.color);

            var box = LevelBuildKit.Box("Crate", crate.transform, new Vector3(0f, 0.4f, 0f),
                new Vector3(1.3f, 0.8f, 1.8f), wood, collider: true, castShadow: false);
            var obstacle = box.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;
            LevelBuildKit.Box("Band", crate.transform, new Vector3(0f, 0.55f, 0f),
                new Vector3(1.34f, 0.16f, 1.84f), band, castShadow: false);

            var display = crate.AddComponent<ShelfDisplay>();
            display.buffer = line.Storage;
            display.columns = 3;
            display.rowsPerBoard = 4;
            display.pitchX = 0.4f;
            display.pitchZ = 0.42f;
            display.boardHeights = new[] { 0.8f, 1.1f };
            display.cubeFootprint = new Vector2(0.34f, 0.38f);

            var feed = crate.AddComponent<SupplyFeed>();
            feed.target = line.Storage;
            feed.secondsPerUnit = SupplySecondsPerUnit;

            crate.SetActive(false);
            if (line.Growth) line.CrateObject = crate;
            else shop.Opening.Add(crate);
        }

        // ---- staff -------------------------------------------------------------------

        private static void BuildStaff(Transform root, Shop shop)
        {
            var sklad1 = shop.Shelves.GetRange(0, 3);
            var sklad2 = shop.Shelves.GetRange(3, 3);

            // Sklad1's stockers are on sale from the day the shop opens.
            string[] names = { "a", "b" };
            for (int i = 0; i < Stocker1Hires.Length; i++)
            {
                var hire = HireStocker(root, $"stock1{names[i]}", Stocker1Hires[i], sklad1,
                    new Color(0.42f, 0.72f, 1f));
                hire.gameObject.SetActive(false);
                shop.Opening.Add(hire.gameObject);
                shop.Stock1Hires.Add(hire);
            }

            // Sklad2's wait for the first order, which is when they have something to carry.
            for (int i = 0; i < Stocker2Hires.Length; i++)
            {
                var hire = HireStocker(root, $"stock2{names[i]}", Stocker2Hires[i], sklad2,
                    new Color(0.98f, 0.66f, 0.34f));
                hire.gameObject.SetActive(false);
                shop.Stock2Hires.Add(hire);
            }

            shop.Cashier1Hire = HireCashier(root, "cashier", Cashier1HirePosition, shop.Tills[0]);
            shop.Opening.Add(shop.Cashier1Hire.gameObject);

            // The second cashier is sold with the second checkout (see BuildGrowth).
            shop.Cashier2Hire = HireCashier(root, "cashier2", Cashier2HirePosition, shop.Tills[1]);
        }

        /// <summary>
        /// A stocker for one storage room: it can run any crate in the room to its shelf, so its
        /// first route is the room's first line and the rest are listed on the worker.
        /// </summary>
        private static UnlockStation HireStocker(Transform root, string id, Vector3 at,
            List<ShelfParts> room, Color color)
        {
            var first = room[0];
            var hire = FarmSceneBuilder.BuildHire(root, "Stocker", id, at,
                price: StockerPrice, pickup: first.Collect, dropoff: first.Stock,
                color: color, feePerDelivery: StockerFee, beacon: null,
                idPrefix: "market.hire.");

            var worker = hire.revealOnUnlock[0].GetComponent<Tycoon.Upkeep.WorkerAgent>();
            worker.routes = room.ConvertAll(line => new Tycoon.Upkeep.StockRoute
            {
                pickup = line.Collect,
                dropoff = line.Stock
            }).ToArray();

            return hire;
        }

        private static UnlockStation HireCashier(Transform root, string id, Vector3 at, Till till)
        {
            var hire = FarmSceneBuilder.BuildHire(root, "Cashier", id, at,
                price: CashierPrice, pickup: till.Serve, dropoff: till.Serve,
                color: new Color(0.35f, 0.75f, 0.55f), feePerDelivery: CashierFee, beacon: null,
                idPrefix: "market.hire.");
            hire.gameObject.SetActive(false);
            return hire;
        }

        // ---- the checkouts -----------------------------------------------------------

        private static void BuildCheckout(Transform root, Shop shop)
        {
            // Two tills side by side in the south-west. The first opens with the shop and the
            // desk is a fitting, so it is repaired like the shelves; the second is bought
            // (BuildGrowth) and brings its own desk and queue with it.
            var west = BuildTill(root, "market.checkout", "Checkout", "", Counter1X);
            var east = BuildTill(root, "market.checkout2", "Checkout2", "2", Counter2X);
            shop.Tills.Add(west);
            shop.Tills.Add(east);

            shop.Working.Add(west.Desk);
            shop.Opening.Add(west.Root);

            // Everything that spawns and routes shoppers lives on the first till: the second only
            // queues them, and shares the first's reputation.
            var queue = west.Queue;
            var template = LevelBuildKit.BuildCustomerTemplate(west.Root.transform);

            queue.customerTemplate = template;
            queue.spawnPoint = Child(root, "ShopperSpawn", ShopperSpawn);
            queue.spawnIntervalSeconds = ShopperIntervalSeconds;
            queue.maxShoppers = MaxShoppers;
            queue.entryRoute = Markers(root, "Entry", EntryRoute);

            queue.alternates = new[] { east.Queue };
            east.Queue.sharesReputationWith = queue;

            // The tree of aisle nodes. Shelves share nodes by sharing the same Transform, which
            // is what lets a shopper walking from one shelf to the next back out only as far
            // as they differ (see StoreShelf.Connect).
            var hub = Child(root, "Aisle_Hub", HubPoint);
            var lane = Child(root, "Aisle_Lane", new Vector3(0f, 0f, NorthAisleZ));
            var north = new Transform[ShelfX.Length];
            var hall = new Transform[ShelfX.Length];
            for (int i = 0; i < ShelfX.Length; i++)
            {
                north[i] = Child(root, $"Aisle_North_{i}", new Vector3(ShelfX[i], 0f, NorthAisleZ));
                hall[i] = Child(root, $"Aisle_Hall_{i}", new Vector3(ShelfX[i], 0f, HallAisleZ));
            }

            // Each aisle is a chain out from the middle column, so a shopper working along it
            // walks on from one shelf to the next and does not go back to the middle between.
            // First row, off the north aisle: lane, then the middle column, then the west one.
            shop.Shelves[2].Via = new[] { hub, lane, north[2] };
            shop.Shelves[1].Via = new[] { hub, lane, north[1] };
            shop.Shelves[0].Via = new[] { hub, lane, north[1], north[0] };

            // Second row, off the hall: the same shape, without the lane.
            shop.Shelves[5].Via = new[] { hub, hall[2] };
            shop.Shelves[4].Via = new[] { hub, hall[1] };
            shop.Shelves[3].Via = new[] { hub, hall[1], hall[0] };

            // A tour of the shop: the second row west to east, up the lane, then the first row
            // east to west. A shopper's list is walked in this order, so they never double back.
            int[] tour = { 3, 4, 5, 2, 1, 0 };
            queue.shelves = new Tycoon.Customers.StoreShelf[tour.Length];
            for (int i = 0; i < tour.Length; i++)
            {
                var line = shop.Shelves[tour[i]];
                queue.shelves[i] = new Tycoon.Customers.StoreShelf
                {
                    buffer = line.Shelf,
                    standPoint = line.Stand,
                    via = line.Via,
                    // A bought shelf is not trading until its goods have been ordered.
                    openWhen = line.Growth ? line.CrateObject : null
                };
            }

            template.SetActive(false);
        }

        /// <summary>
        /// One checkout: a north-south counter at <paramref name="counterX"/>, its serving square
        /// on the west side at the south end, the queue running north along the east side.
        /// Built switched off; the caller says what switches it on.
        /// </summary>
        private static Till BuildTill(Transform root, string id, string name, string suffix, float counterX)
        {
            var till = new Till();
            till.Desk = BuildDesk(root, new Vector3(counterX, 0f, CounterZ));

            var checkout = new GameObject(name);
            checkout.transform.SetParent(root, false);
            checkout.transform.localPosition = new Vector3(counterX + ServeDX, 0f, ServeZ);
            LevelBuildKit.Identify(checkout, id);
            till.Root = checkout;

            till.Serve = LevelBuildKit.Station<CheckoutStation>($"{id}.serve", "Serve",
                checkout.transform, Vector3.zero, ServeSize, "Serve",
                new Color(0.45f, 0.85f, 0.6f), smallestCard: ServeSize);

            // What the queue faces: the counter, from the east side, level with the serving square.
            var counterPoint = Child(checkout.transform, "CounterPoint", new Vector3(-ServeDX, 0f, 0f));

            // The queue runs north from the front of the counter, nearest first.
            float queueX = counterX + QueueDX;
            var slots = new Transform[QueueSlots];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = Child(root, $"Slot{suffix}_{i}",
                    new Vector3(queueX, 0f, ServeZ + QueuePitch * i));

            var queue = checkout.AddComponent<Tycoon.Customers.CustomerQueue>();
            queue.counterPoint = counterPoint;
            queue.slots = slots;
            queue.exitPoint = Child(root, $"ShopperExit{suffix}", ShopperExit);
            // Paid: straight south from the front of the queue to the exit lane, then the shared
            // way east to the door.
            // Out of the queue: down its own line to below the counters, then the shared way
            // to the door. Shoppers leaving empty-handed from the aisles skip the first part.
            queue.queueExit = Markers(root, $"QueueExit{suffix}",
                new[] { new Vector3(queueX, 0f, ServeZ - QueueExitDrop) });
            queue.exitRoute = Markers(root, $"Leave{suffix}", ExitRoute);

            // In over the top of the counters: the hall's centre is north-east of both tills, and
            // a straight line from it to the west till's queue crosses the east till's counter.
            var approach = new List<Vector3>(QueueApproach)
                { new Vector3(queueX, 0f, CounterZ + CounterHalfLength + QueueApproachClearance) };
            queue.queueApproach = Markers(root, $"Queue{suffix}", approach.ToArray());
            till.Queue = queue;
            till.Serve.queue = queue;

            checkout.SetActive(false);
            return till;
        }

        /// <summary>
        /// The counter the shoppers queue at: solid, with the till on top. Turned a quarter turn
        /// so its long axis runs north-south; the till and scanner are at the south end, by the
        /// serving square.
        /// </summary>
        private static GameObject BuildDesk(Transform root, Vector3 centre)
        {
            // "Stall" is the name the layout audit measures counters by.
            var stall = new GameObject("Stall");
            stall.transform.SetParent(root, false);
            stall.transform.localPosition = centre;
            // Local +X then points south.
            stall.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var wood = LevelBuildKit.Mat("Market_DeskWood", new Color(0.55f, 0.38f, 0.24f));
            var top = LevelBuildKit.Mat("Market_DeskTop", new Color(0.86f, 0.82f, 0.74f));
            var till = LevelBuildKit.Mat("Market_Till", new Color(0.25f, 0.3f, 0.34f));

            var counter = LevelBuildKit.Box("Counter", stall.transform, new Vector3(0f, 0.5f, 0f),
                new Vector3(3.4f, 1f, 0.5f), wood, collider: true);
            var obstacle = counter.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;
            obstacle.carving = true;

            LevelBuildKit.Box("Top", stall.transform, new Vector3(0f, 1.03f, 0f),
                new Vector3(3.5f, 0.06f, 0.6f), top, castShadow: false);
            // The till and scanner at the end by the serving square.
            const float toServe = 1f;
            LevelBuildKit.Box("Till", stall.transform, new Vector3(0.9f * toServe, 1.25f, 0.05f),
                new Vector3(0.55f, 0.36f, 0.4f), till, castShadow: false);
            LevelBuildKit.Box("Scanner", stall.transform, new Vector3(-0.5f * toServe, 1.08f, 0.05f),
                new Vector3(0.7f, 0.05f, 0.35f), till, castShadow: false);

            stall.SetActive(false);
            return stall;
        }

        // ---- growth ------------------------------------------------------------------

        /// <summary>
        /// Wires the progression that follows the opening: staff the shop, then add bread,
        /// apples and yogurt one at a time, with a second checkout alongside the first of them.
        ///
        ///   cashier + both sklad1 stockers hired  (RevealWhenAll)
        ///     -> shelf S21 offered
        ///        buy it -> "Order Bread" offered
        ///           order it -> bread's crate, collect and stocking squares, sklad2's two
        ///                       stockers, the SECOND CHECKOUT offered, and shelf S22 offered
        ///                          checkout 2 bought -> its desk, queue, and its cashier's hire
        ///        S22 -> "Order Apples" -> S23 -> "Order Yogurt"
        ///     all six purchases made (RevealWhenAll) -> COMING SOON
        ///
        /// Every step is an ordinary UnlockStation, and a gate's reveal list is what keeps the
        /// order: a product is only ever offered once its shelf is standing, and the next shelf
        /// only once the last product's goods are in.
        /// </summary>
        private static void BuildGrowth(Transform root, Shop shop)
        {
            var lines = shop.Shelves.FindAll(l => l.Growth);
            var shelfGates = new UnlockStation[lines.Count];
            var orderGates = new UnlockStation[lines.Count];

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];

                // The shelf gate sits where the stocking square will be, as every gate sits on
                // what it sells; the square itself only appears with the order.
                shelfGates[i] = FarmSceneBuilder.Gate(root, $"market.unlock.shelf.{line.ShelfId}",
                    $"UnlockShelf{line.Name}", new Vector3(line.X, 0f, line.Z - StockOffset),
                    new Vector2(3.0f, StockSquareDepth), "Shelf", GrowthShelfPrices[i],
                    line.ShelfObject);
                shelfGates[i].icon = SquareIcon.Buy;

                // The order sits where the collect square will be.
                orderGates[i] = FarmSceneBuilder.Gate(root, $"market.order.{line.Key}",
                    $"Order{line.Name}", new Vector3(CollectX, 0f, line.CrateZ),
                    new Vector2(2.4f, 2.1f), $"Order {line.Name}", GrowthOrderPrices[i],
                    line.CrateObject, line.Collect.gameObject, line.Stock.gameObject);
                orderGates[i].icon = SquareIcon.Buy;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                // Buying a shelf offers the order for what goes on it...
                Reveal(shelfGates[i], orderGates[i].gameObject);

                // ...and ordering it offers the next shelf, so only one product is ever open.
                if (i + 1 < lines.Count) Reveal(orderGates[i], shelfGates[i + 1].gameObject);
            }

            // The first order brings sklad2's stockers, and the second checkout with them.
            var second = shop.Tills[1];
            var checkout2 = FarmSceneBuilder.Gate(root, "market.unlock.checkout2", "UnlockCheckout2",
                second.Root.transform.localPosition, new Vector2(2.4f, 2.2f), "Checkout 2",
                Checkout2Price, second.Root, second.Desk, shop.Cashier2Hire.gameObject);
            checkout2.icon = SquareIcon.Buy;

            foreach (var hire in shop.Stock2Hires) Reveal(orderGates[0], hire.gameObject);
            Reveal(orderGates[0], checkout2.gameObject);

            // COMING SOON: locked, not for sale, and last.
            var soon = LevelBuildKit.Station<LockedStation>("market.comingsoon", "ComingSoon", root,
                ComingSoonPosition, new Vector2(3.0f, 2.6f), "Coming soon", new Color(0.75f, 0.75f, 0.8f));
            soon.gameObject.SetActive(false);

            var progress = new GameObject("Growth");
            progress.transform.SetParent(root, false);

            // Staffed: the cashier and both of sklad1's stockers. Then the first new shelf.
            var staffed = progress.AddComponent<RevealWhenAll>();
            var staff = new List<UnlockStation> { shop.Cashier1Hire };
            staff.AddRange(shop.Stock1Hires);
            staffed.unlocks = staff.ToArray();
            staffed.reveal = new[] { shelfGates[0].gameObject };
            shelfGates[0].gameObject.SetActive(false);

            // Everything added: all three shelves and all three orders.
            var complete = progress.AddComponent<RevealWhenAll>();
            var everything = new List<UnlockStation>(shelfGates);
            everything.AddRange(orderGates);
            complete.unlocks = everything.ToArray();
            complete.reveal = new[] { soon.gameObject };
        }

        /// <summary>Adds objects to what a purchase switches on, keeping them off until it does.</summary>
        private static void Reveal(UnlockStation gate, params GameObject[] more)
        {
            var all = new List<GameObject>();
            if (gate.revealOnUnlock != null) all.AddRange(gate.revealOnUnlock);

            foreach (var go in more)
            {
                if (go == null) continue;
                go.SetActive(false);
                all.Add(go);
            }

            gate.revealOnUnlock = all.ToArray();
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static Transform[] Markers(Transform root, string prefix, Vector3[] points)
        {
            var result = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
                result[i] = Child(root, $"{prefix}_{i}", points[i]);
            return result;
        }

        // ---- renovation --------------------------------------------------------------

        private static void BuildRenovation(Transform root, Shop shop, FarmSceneBuilder.Items items)
        {
            var heaps = new List<ItemBuffer>();

            for (int i = 0; i < TrashPositions.Length; i++)
            {
                // The heap: bags of trash in a buffer, a collect square beside it, and the litter
                // that shrinks as the buffer empties. Taking the bags is the whole job.
                var pile = BuildTrashPile(root, $"TrashPile_{i + 1}", TrashPositions[i], seed: 31 + i * 7,
                    out GameObject[] litter);

                var heap = LevelBuildKit.Buffer($"market.trash.{i + 1}", $"Trash{i + 1}", root,
                    TrashPositions[i], items.Trash, BagsPerPile, starting: BagsPerPile);
                heaps.Add(heap);

                var take = LevelBuildKit.Station<CollectStation>($"market.trashcollect.{i + 1}",
                    $"CollectTrash{i + 1}", root, TrashPositions[i], new Vector2(2.2f, 2.0f), "Trash",
                    new Color(0.7f, 0.74f, 0.78f));
                take.source = heap;
                // Hired hands have no business with it: the rubbish is the player's to carry out.
                take.workerCompatible = false;

                var clear = heap.gameObject.AddComponent<ClearablePile>();
                clear.litter = litter;
                clear.square = take.gameObject;
                pile.transform.SetParent(root, false);
            }

            // Repair puts back everything that does work. An ordinary gate, paid for by standing
            // in it like any other.
            var repair = FarmSceneBuilder.Gate(root, "market.unlock.repair", "UnlockRepair",
                RepairPosition, new Vector2(3.4f, 2.6f), "Repair", RepairPrice,
                shop.Working.ToArray());
            repair.icon = Tycoon.UI.SquareIcon.Fix;
            repair.hideOnUnlock = shop.Broken.ToArray();

            // Paint is a menu, not a gate: standing in the square opens it. The first confirmed
            // job is the renovation step; the square stays after that, so the look can be
            // changed for a smaller fee whenever the player likes.
            var decorHolder = new GameObject("Decor");
            decorHolder.transform.SetParent(root, false);
            LevelBuildKit.Identify(decorHolder, "market.decor");
            var decor = decorHolder.AddComponent<MarketDecor>();
            decor.grubby = shop.Dirty.ToArray();
            decor.painted = shop.Painted.ToArray();
            decor.signDirty = shop.SignDirty;
            decor.signClean = shop.SignClean;
            decor.surfaces = shop.Surfaces.ToArray();

            var paint = LevelBuildKit.Station<PaintStation>("market.paint", "Paint", root,
                PaintPosition, new Vector2(3.0f, 2.4f), "Paint", new Color(0.95f, 0.6f, 0.55f));
            paint.mode = InteractionMode.Task;
            paint.taskDuration = 0.9f;
            paint.decor = decor;
            paint.firstPrice = FarmSceneBuilder.Price(FirstPaintPrice);
            paint.changePrice = FarmSceneBuilder.Price(RepaintPrice);

            var open = LevelBuildKit.Station<ChoreStation>("market.open", "Open", root,
                OpenPosition, new Vector2(2.8f, 2.2f), "Open", new Color(0.55f, 0.92f, 0.55f));
            open.icon = Tycoon.UI.SquareIcon.Open;
            open.completionsNeeded = 1;
            open.taskDuration = 2f;
            open.doneMessage = "OPEN!";
            open.hideOnDone = new[] { shop.SignClean };
            open.revealOnDone = Concat(shop.Opening, shop.SignLit);
            decor.openStep = open;

            // The order, in two links. Carry every bag out and the two paid steps appear; do both
            // of those, and the door opens. Clearing is free, so a player with nothing can
            // always start; nothing here can leave them stuck.
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

        private static GameObject[] Concat(List<GameObject> list, GameObject extra)
        {
            var all = new List<GameObject>(list) { extra };
            return all.ToArray();
        }

        /// <summary>A heap of bags, boxes and bottles. Dark and lumpy; it only has to read as mess.</summary>
        private static GameObject BuildTrashPile(Transform root, string name, Vector3 position, int seed,
            out GameObject[] litter)
        {
            var pile = new GameObject(name);
            pile.transform.SetParent(root, false);
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

                pieces[i] = LevelBuildKit.Box($"Litter_{i}", pile.transform,
                    new Vector3(x, size * 0.5f, z), new Vector3(size, size * 0.8f, size * 1.1f),
                    materials[i % materials.Length], castShadow: false,
                    rot: new Vector3(0f, yaw, 0f));
            }

            litter = pieces;
            return pile;
        }
    }
}
