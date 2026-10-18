using System.Collections.Generic;
using Tycoon.Config;
using Tycoon.Customers;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEngine;
using Tycoon.Upkeep;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// The hairdresser: the second business on the town street, east of the florist, built from the approved layout
    /// (the phase-1 pictures) and art (<see cref="FloristModels"/> salon models, <see cref="HaircutArt"/>).
    ///
    ///     north   mirrors on the wall, a salon chair in front of each (C1, C2 on the north wall; C3, C4 on the east
    ///             wall), a STYLE square behind each chair and a SWEEP square beside it
    ///     west    the waiting room: four chairs along the wall, the NEXT square beside them
    ///     middle  the reception desk with its cash pile and the receptionist's square behind it
    ///     south   street door (in, bottom left) .......... exit door (out, bottom right); a side door in the east
    ///             wall to the hair bin
    ///
    /// How it works, all from the existing parts:
    ///   - customers (one <see cref="CustomerQueue"/>, seated waiting room) walk in, sit on a waiting chair and want one of
    ///     four hairstyles; the NEXT square (<see cref="CallNextStation"/>) sends the head of the room to a free, clean
    ///     chair (<see cref="ServiceChair"/>), where they sit and wait for their haircut;
    ///   - each chair has a STYLE square (<see cref="SalonChairStation"/>): the player cuts the hair in the haircut
    ///     minigame (price + tip), a hired hairdresser (a worker, pickup = dropoff = the square) at the base price;
    ///   - a served customer leaves hair on the floor (<see cref="HairMess"/>): the chair is not used until it is
    ///     swept; the SWEEP square gives hair bags, the player (or the cleaner, a multi-route worker from each heap) takes
    ///     them out of the side door to the bin (<see cref="WorkerBinStation"/>);
    ///   - takings go onto one cash pile at the reception desk, as at every shop.
    ///
    /// It starts DERELICT and is brought back like the florist (litter heaps -> REPAIR + PAINT -> OPEN, the same parts
    /// and the same <see cref="MarketDecor"/> two-coat swap). Then one purchase at a time, each revealing the next:
    ///     Chair 2 -> Receptionist -> Hairdresser 1 -> Chair 3 -> Cleaner -> Hairdresser 2 -> Chair 4 ->
    ///     Hairdresser 3 -> Hairdresser 4
    /// The hairdresser hires share one square (only one is ever on offer). Everything is a child of the town root;
    /// the whole salon appears when the florist's gardener is hired (<see cref="TownBuilder"/>). New save ids only,
    /// all under "town.salon." (and "town.hire." for the staff).
    /// </summary>
    internal static class SalonBuilder
    {
        // ---- prices (plans/econ/market_sim.py, SALON, 2026-10-18) ----------------------------------------
        private const double RepairPrice = 2800d;
        private const double FirstPaintPrice = 1400d;
        private const double RepaintPrice = 200d;
        private const double Chair2Price = 4500d;
        private const double Chair3Price = 7000d;
        private const double Chair4Price = 12000d;
        private const double ReceptionistPrice = 5500d;
        private const double CleanerPrice = 6000d;
        private static readonly double[] HairdresserPrice = { 6500d, 8000d, 12000d, 15000d };

        // What a haircut sells for (FarmSceneBuilder.CreateItems reads these).
        internal const double ShortPrice = 80d, BobPrice = 110d, LongPrice = 160d, QuiffPrice = 120d;

        // ---- customers -----------------------------------------------------------------------------------
        // Tuned in the sim (market_sim.py SALON): interval / (demand x reputation), demand = 1 + 0.25 x (capacity - 1) up to MaxDemand.
        // The four styles are capacity 4 (one chair's worth of customers); each chair after the first adds ChairDemand and
        // each hairdresser hired adds HairdresserDemand, so customers keep up with the chairs and staff that are bought
        // (the same CustomerQueue.DemandMultiplier the farm and market scale with). A hairdresser turns a chair over in about
        // 12 s, so a full salon (4 chairs, 4 hairdressers) needs about 16 arrivals a minute to keep its chairs busy.
        private const float CustomerIntervalSeconds = 40f;
        private const float DemandPerCapacity = 0.25f;
        private const float MaxDemand = 11f;
        private const int ChairDemand = 7;
        private const int HairdresserDemand = 4;
        private const float ServiceSeconds = 10f;      // a hairdresser's whole haircut (the player's minigame takes about 20 s)
        private const int WaitingSeats = 4;
        private const int BagsPerService = 3;
        private const float BubbleScale = 0.0055f;

        // ---- the shop (the town's grid; north +z, the street at z -3) ---------------------------------------
        internal const float ShopWest = 4.8f, ShopEast = 18.2f, ShopSouth = -0.8f, ShopNorth = 9.6f;
        private const float EntranceFrom = 6.0f, EntranceTo = 8.0f;       // bottom left
        private const float ExitFrom = 14.0f, ExitTo = 16.0f;             // bottom right
        private const float SideFrom = 3.4f, SideTo = 4.8f;               // the east wall's door to the bin
        private const float WallHeight = 0.7f;
        private const float Floor = 0.06f;

        /// <summary>Everything the salon stands on, with the bin outside: for the town's grass keep-clear.</summary>
        internal static readonly Rect Bounds = new Rect(ShopWest - 0.8f, ShopSouth - 0.8f, 22.4f - ShopWest + 0.4f, ShopNorth - ShopSouth + 1.6f);

        // Squares (the layout picture's numbers).
        private static readonly Vector3[] LitterAt = { new Vector3(7.6f, 0f, 2.0f), new Vector3(13.4f, 0f, 4.8f), new Vector3(7.6f, 0f, 6.4f) };
        private static readonly Vector2 PileSquare = new Vector2(2.2f, 2.0f);
        private static readonly Vector3 RepairAt = new Vector3(13.4f, 0f, 4.8f);
        private static readonly Vector3 PaintAt = new Vector3(6.0f, 0f, 8.6f);
        private static readonly Vector3 OpenAt = new Vector3(10.4f, 0f, 4.8f);
        private static readonly Vector2 RepairSize = new Vector2(2.2f, 2.0f), OpenSize = new Vector2(2.2f, 2.0f), PaintSize = new Vector2(1.7f, 1.5f);

        private static readonly Vector3 DeskAt = new Vector3(10.8f, 0f, 2.05f);          // 3.0 x 0.9, staff side north
        private static readonly Vector3 CashAt = new Vector3(13.2f, 0f, 3.6f);
        private static readonly Vector3 ReceptionistHireAt = new Vector3(10.8f, 0f, 3.7f);
        private static readonly Vector3 NextAt = new Vector3(7.7f, 0f, 4.6f);
        private static readonly Vector3 HairdresserHireAt = new Vector3(7.9f, 0f, 8.5f);   // shared by the four hairdressers
        private static readonly Vector3 CleanerHireAt = new Vector3(17.0f, 0f, 0.2f);
        private static readonly Vector3 BinAt = new Vector3(20.2f, 0f, 2.3f);              // the can; its square is 1.7 north of it, at the door

        private static readonly Vector2 StyleSize = new Vector2(2.0f, 1.8f);

        private class ChairSpec
        {
            public Vector3 Chair;          // where the salon chair stands
            public float Yaw;              // the chair model's yaw (the sitter faces the mirror)
            public Vector3 Mirror;         // where its mirror station stands
            public float MirrorYaw;
            public Vector3 Style;
            public Vector3 Sweep;
            public Vector2 SweepSize;
            public Vector3[] Approach;     // head of the waiting room -> the seat
            public Vector3[] Exit;         // out of the seat -> the shop's exit route
        }

        // C1, C2 on the north wall (the sitter faces north), C3, C4 on the east wall (the sitter faces east).
        private static readonly ChairSpec[] Chairs =
        {
            new ChairSpec
            {
                Chair = new Vector3(10.0f, 0f, 8.5f), Yaw = 180f, Mirror = new Vector3(10.0f, 0f, ShopNorth - 0.34f), MirrorYaw = 0f,
                Style = new Vector3(10.0f, 0f, 6.9f), Sweep = new Vector3(11.75f, 0f, 8.7f), SweepSize = new Vector2(1.5f, 1.5f),
                Approach = new[] { new Vector3(8.2f, 0f, 3.4f), new Vector3(10.0f, 0f, 6.9f) },
                Exit = new[] { new Vector3(10.0f, 0f, 7.0f), new Vector3(12.4f, 0f, 6.0f), new Vector3(14.6f, 0f, 4.6f) },
            },
            new ChairSpec
            {
                Chair = new Vector3(13.4f, 0f, 8.5f), Yaw = 180f, Mirror = new Vector3(13.4f, 0f, ShopNorth - 0.34f), MirrorYaw = 0f,
                Style = new Vector3(13.4f, 0f, 6.9f), Sweep = new Vector3(15.15f, 0f, 8.7f), SweepSize = new Vector2(1.5f, 1.5f),
                Approach = new[] { new Vector3(8.2f, 0f, 3.4f), new Vector3(13.4f, 0f, 6.9f) },
                Exit = new[] { new Vector3(13.4f, 0f, 7.0f), new Vector3(14.6f, 0f, 4.6f) },
            },
            new ChairSpec
            {
                Chair = new Vector3(17.0f, 0f, 5.9f), Yaw = -90f, Mirror = new Vector3(ShopEast - 0.34f, 0f, 5.9f), MirrorYaw = 90f,
                Style = new Vector3(15.5f, 0f, 5.9f), Sweep = new Vector3(16.95f, 0f, 7.6f), SweepSize = new Vector2(1.3f, 1.3f),
                Approach = new[] { new Vector3(8.2f, 0f, 3.4f), new Vector3(14.6f, 0f, 5.9f) },
                Exit = new[] { new Vector3(15.2f, 0f, 5.9f), new Vector3(14.6f, 0f, 4.6f) },
            },
            new ChairSpec
            {
                Chair = new Vector3(17.0f, 0f, 2.4f), Yaw = -90f, Mirror = new Vector3(ShopEast - 0.34f, 0f, 2.4f), MirrorYaw = 90f,
                Style = new Vector3(15.5f, 0f, 2.4f), Sweep = new Vector3(16.95f, 0f, 4.05f), SweepSize = new Vector2(1.3f, 1.3f),
                Approach = new[] { new Vector3(8.2f, 0f, 3.4f), new Vector3(14.6f, 0f, 3.4f), new Vector3(15.2f, 0f, 2.4f) },
                Exit = new[] { new Vector3(15.2f, 0f, 2.4f), new Vector3(15.0f, 0f, 1.0f) },
            },
        };

        // The seat is the middle of the seat in plan, at (seat top - 0.16 m): the salon chair's top is 0.575 above its
        // floor, the waiting chair's 0.445 (Sit clip, ServiceChair.seat).
        private const float ChairSeatY = Floor + 0.415f, WaitSeatY = Floor + 0.285f;

        // The waiting room: four chairs along the west wall facing east, slot 0 (the head) nearest the door.
        private const float WaitX = 5.45f;
        private static readonly float[] WaitZ = { 2.2f, 3.8f, 5.4f, 7.0f };

        private static readonly Vector3[] EntryRoute =
        {
            new Vector3(7.0f, 0f, TownBuilder.RoadZ), new Vector3(7.0f, 0f, 0.5f), new Vector3(8.4f, 0f, 1.4f), new Vector3(8.8f, 0f, 4.6f),
        };
        private static readonly Vector3[] QueueExit = { new Vector3(8.8f, 0f, 4.6f), new Vector3(14.4f, 0f, 3.4f) };
        private static readonly Vector3[] ExitRoute =
        {
            new Vector3(15.0f, 0f, 0.2f), new Vector3(15.0f, 0f, TownBuilder.RoadZ),
        };
        private static readonly Vector3 SpawnPoint = new Vector3(-17.5f, 0f, TownBuilder.RoadZ);
        private static readonly Vector3 ExitPoint = new Vector3(24f, 0f, TownBuilder.RoadZ);

        // The styles, in the order the minigame knows them.
        private static readonly string[] StyleIds = { "hair_short", "hair_bob", "hair_long", "hair_quiff" };

        private class Shell
        {
            public Transform Building;
            public GameObject Grubby, Painted, Working, Broken;
            public readonly List<DecorSurface> Surfaces = new List<DecorSurface>();
        }

        private class ChairParts
        {
            public GameObject Group;
            public ServiceChair Chair;
            public SalonChairStation Station;
            public CollectStation Sweep;
            public HairMess Mess;
            public Transform Models;        // where this chair's mirror and chair models stand
        }

        internal static void EnsureGoods() => FloristModels.EnsureSalonGoods();

        /// <summary>
        /// Builds the salon (switched off: it appears when the florist's gardener is hired) and returns its root.
        /// </summary>
        internal static GameObject Build(Transform town, FarmSceneBuilder.Items items)
        {
            var root = new GameObject("Salon").transform;
            root.SetParent(town, false);

            var styles = new[] { items.HairShort, items.HairBob, items.HairLong, items.HairQuiff };
            var shell = BuildShell(root);

            // ---- the working salon: queue, cash, NEXT, chair 1 ----------------------------------------------------
            var shop = new GameObject("Shop");
            shop.transform.SetParent(root, false);
            LevelBuildKit.Identify(shop, "town.salon");      // the queue's reputation is saved under this

            var cash = LevelBuildKit.BuildCashPoint("town.salon", shop.transform, CashAt, new Vector2(1.8f, 1.6f));

            var parts = new ChairParts[Chairs.Length];
            for (int i = 0; i < Chairs.Length; i++) parts[i] = BuildChair(root, shell, i, styles, items, cash.pile);

            var queue = BuildQueue(shop.transform, parts, styles);

            var next = LevelBuildKit.Station<CallNextStation>("town.salon.next", "CallNext", shop.transform, NextAt,
                new Vector2(1.8f, 1.6f), "Next", new Color(1.00f, 0.67f, 0.24f), smallestCard: new Vector2(1.8f, 1.6f));
            next.queue = queue;
            next.taskDuration = 0.8f;

            // What the shop offers: every style from the day it opens; each extra chair is a little more demand.
            var offers = new GameObject("Offers");
            offers.transform.SetParent(shop.transform, false);
            for (int i = 0; i < styles.Length; i++)
            {
                var offer = new GameObject("Offer_" + StyleIds[i]);
                offer.transform.SetParent(offers.transform, false);
                var po = offer.AddComponent<ProductOffer>();
                po.item = styles[i];
            }
            for (int c = 1; c < parts.Length; c++)
            {
                var offer = new GameObject("ChairOffer");
                offer.transform.SetParent(parts[c].Group.transform, false);
                var po = offer.AddComponent<ProductOffer>();
                po.item = styles[c];
                po.capacity = ChairDemand;
            }

            // ---- the hair bin, and the bin's square: always there (the litter goes into it too) ------------------
            var bin = LevelBuildKit.BuildBin("town.salon.bin", "HairBin", root, BinAt, squareToSouth: false, workers: true);

            // ---- the staff -------------------------------------------------------------------------------------
            var hairdresserHires = new UnlockStation[4];
            for (int i = 0; i < 4; i++)
            {
                hairdresserHires[i] = FarmSceneBuilder.BuildHire(root, "Hairdresser", "hairdresser" + (i + 1), HairdresserHireAt,
                    price: HairdresserPrice[i], pickup: parts[i].Station, dropoff: parts[i].Station,
                    color: new Color(0.20f, 0.74f, 0.68f), feePerDelivery: 0d, beacon: null,
                    idPrefix: "town.hire.", smallestCard: new Vector2(1.8f, 1.8f));
                FloristBuilder.Dress(hairdresserHires[i].revealOnUnlock[0].transform, "Fl_SalonHairdresser", new[] { 0, 1, 2 });
                hairdresserHires[i].label = "Hairdresser " + (i + 1);
                // A hired hairdresser is more demand for the shop, like another chair (announced while the worker exists).
                var hdOffer = new GameObject("HairdresserOffer");
                hdOffer.transform.SetParent(hairdresserHires[i].revealOnUnlock[0].transform, false);
                var hdPo = hdOffer.AddComponent<ProductOffer>();
                hdPo.item = styles[i];
                hdPo.capacity = HairdresserDemand;
            }

            var receptionistHire = FarmSceneBuilder.BuildHire(root, "Receptionist", "receptionist", ReceptionistHireAt,
                price: ReceptionistPrice, pickup: next, dropoff: next,
                color: new Color(0.74f, 0.94f, 0.89f), feePerDelivery: 0d, beacon: null,
                idPrefix: "town.hire.", smallestCard: new Vector2(1.8f, 1.8f));
            FloristBuilder.Dress(receptionistHire.revealOnUnlock[0].transform, "Fl_SalonReceptionist", new[] { 0, 1, 2, 4, 5 });

            // The cleaner works each heap in turn: its square (SWEEP) to the bin. Hair bags only, so nothing else can be
            // in its arms for the bin to destroy.
            var cleanerHire = FarmSceneBuilder.BuildHire(root, "Cleaner", "cleaner", CleanerHireAt,
                price: CleanerPrice, pickup: parts[0].Sweep, dropoff: bin,
                color: new Color(0.30f, 0.36f, 0.54f), feePerDelivery: 0d, beacon: null,
                idPrefix: "town.hire.", smallestCard: new Vector2(1.8f, 1.8f));
            var cleaner = cleanerHire.revealOnUnlock[0].GetComponent<WorkerAgent>();
            FloristBuilder.Dress(cleaner.transform, "Fl_SalonCleaner", new[] { 0, 1, 2, 4, 5 });
            var routes = new List<StockRoute>();
            foreach (var p in parts) routes.Add(new StockRoute { pickup = p.Sweep, dropoff = bin });
            cleaner.routes = routes.ToArray();
            // Takes what there is to the bin rather than waiting at an empty heap for a full load.
            cleaner.deliverPartialLoads = true;

            // ---- the purchases, in the order they are offered, each revealing the next ---------------------------------
            var gate4 = FarmSceneBuilder.Gate(root, "town.salon.unlock.chair4", "UnlockChair4", Chairs[3].Style, StyleSize, StyleSize,
                "Chair 4", Chair4Price, parts[3].Group, hairdresserHires[2].gameObject);
            var gate3 = FarmSceneBuilder.Gate(root, "town.salon.unlock.chair3", "UnlockChair3", Chairs[2].Style, StyleSize, StyleSize,
                "Chair 3", Chair3Price, parts[2].Group, cleanerHire.gameObject);
            var gate2 = FarmSceneBuilder.Gate(root, "town.salon.unlock.chair2", "UnlockChair2", Chairs[1].Style, StyleSize, StyleSize,
                "Chair 2", Chair2Price, parts[1].Group, receptionistHire.gameObject);
            foreach (var g in new[] { gate2, gate3, gate4 }) g.icon = SquareIcon.Buy;

            // Offered only when the one before is paid (the chain above): switched off until then.
            gate3.gameObject.SetActive(false);
            gate4.gameObject.SetActive(false);
            Chain(receptionistHire, hairdresserHires[0].gameObject);
            Chain(hairdresserHires[0], gate3.gameObject);
            Chain(cleanerHire, hairdresserHires[1].gameObject);
            Chain(hairdresserHires[1], gate4.gameObject);
            Chain(hairdresserHires[2], hairdresserHires[3].gameObject);
            foreach (var h in hairdresserHires) h.gameObject.SetActive(false);
            receptionistHire.gameObject.SetActive(false);
            cleanerHire.gameObject.SetActive(false);

            // ---- the renovation: litter, repair + paint, open ---------------------------------------------------------
            BuildRenovation(root, shell, items, shop, parts[0].Group, gate2);

            root.gameObject.SetActive(false);
            return root.gameObject;
        }

        /// <summary>The hire reveals its worker (BuildHire's) and the next purchase.</summary>
        private static void Chain(UnlockStation hire, GameObject next)
        {
            var list = new List<GameObject>(hire.revealOnUnlock) { next };
            hire.revealOnUnlock = list.ToArray();
        }

        // ---- one chair: its group, models, service chair, STYLE and SWEEP squares and its hair ------------------------

        private static ChairParts BuildChair(Transform root, Shell shell, int n, ItemDefinition[] styles,
            FarmSceneBuilder.Items items, CashPile pile)
        {
            var spec = Chairs[n];
            var p = new ChairParts();
            var group = new GameObject("Chair" + (n + 1));
            group.transform.SetParent(root, false);
            p.Group = group;

            // Chair 1's models belong to the fittings REPAIR brings back; the others come with their purchase, and stand in a
            // group named like the fittings so the audit counts them as part of the building (their mirror touches its wall).
            Transform models;
            if (n == 0) models = shell.Working.transform;
            else
            {
                var m = new GameObject("Working");
                m.transform.SetParent(group.transform, false);
                models = m.transform;
            }
            p.Models = models;

            var mirror = FloristModels.MirrorStation(models, spec.Mirror + Vector3.up * Floor, spec.MirrorYaw);
            mirror.name = "Stall";
            var chairModel = FloristModels.SalonChair(models, spec.Chair + Vector3.up * Floor, spec.Yaw);
            chairModel.name = "Stall";
            // Solid: the player and the workers walk round them. (Chair 1's are built once with the building.)
            var solidParent = n == 0 ? shell.Building : group.transform;
            bool north = spec.MirrorYaw == 0f;
            FloristBuilder.Solid(solidParent, "MirrorCollision", spec.Mirror + new Vector3(0f, 0.6f, 0f),
                north ? new Vector3(1.8f, 1.2f, 0.5f) : new Vector3(0.5f, 1.2f, 1.8f));
            FloristBuilder.Solid(solidParent, "ChairCollision", spec.Chair + new Vector3(0f, 0.5f, 0f), new Vector3(0.95f, 1.0f, 0.95f));
            // The wrecks stand at every chair's place until REPAIR.
            FloristModels.MirrorStation(shell.Broken.transform, spec.Mirror + Vector3.up * Floor, spec.MirrorYaw, true);
            FloristModels.SalonChair(shell.Broken.transform, spec.Chair + Vector3.up * Floor, spec.Yaw + (n % 2 == 0 ? -12f : 14f), true);

            // The service chair (customer side): the seat, the way in, the way out.
            var chairObj = new GameObject("ServiceChair");
            chairObj.transform.SetParent(group.transform, false);
            var chair = chairObj.AddComponent<ServiceChair>();
            var seat = FloristBuilder.Marker(chairObj.transform, "Seat", new Vector3(spec.Chair.x, ChairSeatY, spec.Chair.z));
            seat.localRotation = Quaternion.Euler(0f, spec.MirrorYaw == 0f ? 0f : 90f, 0f);
            chair.seat = seat;
            chair.approach = FloristBuilder.Markers(chairObj.transform, "Approach", spec.Approach);
            chair.exitRoute = FloristBuilder.Markers(chairObj.transform, "Exit", spec.Exit);
            p.Chair = chair;

            // The STYLE square.
            var style = LevelBuildKit.Station<SalonChairStation>($"town.salon.chair{n + 1}.style", "Style", group.transform, spec.Style,
                StyleSize, "Style", new Color(0.91f, 0.41f, 0.66f), smallestCard: StyleSize);
            style.chair = chair;
            style.sells = styles;
            style.pile = pile;
            style.priceMultiplier = 1f;
            style.serviceSeconds = ServiceSeconds;
            p.Station = style;

            // The hair: a buffer of bags, the heap's pictures, the SWEEP square, and the marker that keeps the chair out of use.
            var hairObj = new GameObject("Hair");
            hairObj.transform.SetParent(group.transform, false);
            hairObj.transform.localPosition = spec.Sweep;
            LevelBuildKit.Identify(hairObj, $"town.salon.chair{n + 1}.hair");
            var buffer = hairObj.AddComponent<ItemBuffer>();
            buffer.item = items.HairBag;
            buffer.capacity = BagsPerService;
            var heap = new GameObject[3];
            for (int i = 0; i < heap.Length; i++)
            {
                heap[i] = FloristModels.HairHeap(hairObj.transform, new Vector3(0f, Floor, 0f), 25f + i * 70f);
                heap[i].transform.localScale = Vector3.one * (0.55f + 0.25f * i);
                heap[i].name = "Heap" + i;
            }
            var sweep = LevelBuildKit.Station<CollectStation>($"town.salon.chair{n + 1}.sweep", "Sweep", group.transform, spec.Sweep,
                spec.SweepSize, "Sweep", new Color(0.43f, 0.82f, 0.80f), smallestCard: spec.SweepSize);
            sweep.source = buffer;
            var dirty = new GameObject("Dirty");
            dirty.transform.SetParent(hairObj.transform, false);

            var mess = hairObj.AddComponent<HairMess>();
            mess.chair = chair;
            mess.bagsPerService = BagsPerService;
            mess.heap = heap;
            mess.square = sweep.gameObject;
            mess.dirty = dirty;
            chair.dirtyWhen = dirty;
            sweep.gameObject.SetActive(false);
            dirty.SetActive(false);
            p.Sweep = sweep;
            p.Mess = mess;

            if (n > 0) group.SetActive(false);
            return p;
        }

        // ---- the customers ------------------------------------------------------------------------------------------

        private static CustomerQueue BuildQueue(Transform shop, ChairParts[] parts, ItemDefinition[] styles)
        {
            var queue = shop.gameObject.AddComponent<CustomerQueue>();
            queue.customerTemplate = LevelBuildKit.BuildCustomerTemplate(shop);
            var bubble = queue.customerTemplate.GetComponent<OrderBubble>();
            bubble.worldScale = BubbleScale;
            bubble.height = 1.9f;

            queue.spawnPoint = FloristBuilder.Marker(shop, "SalonSpawn", SpawnPoint);
            queue.exitPoint = FloristBuilder.Marker(shop, "SalonExit", ExitPoint);
            queue.counterPoint = FloristBuilder.Marker(shop, "SalonCounterPoint", DeskAt);
            queue.slots = new Transform[WaitingSeats];
            for (int i = 0; i < WaitingSeats; i++)
            {
                var slot = FloristBuilder.Marker(shop, $"SalonSeat_{i}", new Vector3(WaitX, WaitSeatY, WaitZ[i]));
                slot.localRotation = Quaternion.Euler(0f, 90f, 0f);     // facing east, into the room
                queue.slots[i] = slot;
            }
            queue.entryRoute = FloristBuilder.Markers(shop, "SalonIn", EntryRoute);
            queue.queueExit = FloristBuilder.Markers(shop, "SalonGaveUp", QueueExit);
            queue.exitRoute = FloristBuilder.Markers(shop, "SalonOut", ExitRoute);

            queue.seatedWaiting = true;
            queue.chairs = new ServiceChair[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                queue.chairs[i] = parts[i].Chair;
                parts[i].Station.queue = queue;
            }
            queue.register = parts[0].Station;
            parts[0].Station.queue = queue;

            queue.minOrder = 1;
            queue.maxOrder = 1;
            queue.spawnIntervalSeconds = CustomerIntervalSeconds;
            queue.demandPerCapacity = DemandPerCapacity;
            queue.maxDemand = MaxDemand;
            return queue;
        }

        // ---- renovation (the florist's, FloristBuilder.BuildRenovation) -------------------------------------------------

        private static void BuildRenovation(Transform root, Shell shell, FarmSceneBuilder.Items items, GameObject shop,
            GameObject chair1, UnlockStation chair2Gate)
        {
            // The heaps: each a pile of bags with a collect square on it. Carrying all twelve to the bin is free.
            var heaps = new List<ItemBuffer>();
            for (int i = 0; i < LitterAt.Length; i++)
            {
                var litter = BuildLitter(root, $"Litter_{i + 1}", LitterAt[i], 61 + i * 9);
                var heap = LevelBuildKit.Buffer($"town.salon.trash.{i + 1}", $"Trash{i + 1}", root, LitterAt[i], items.Trash, 4, starting: 4);
                heaps.Add(heap);

                var take = LevelBuildKit.Station<CollectStation>($"town.salon.trashcollect.{i + 1}", $"CollectTrash{i + 1}", root,
                    LitterAt[i], PileSquare, "Trash", new Color(0.7f, 0.74f, 0.78f), smallestCard: PileSquare);
                take.source = heap;
                take.workerCompatible = false;

                var clear = heap.gameObject.AddComponent<ClearablePile>();
                clear.litter = litter;
                clear.square = take.gameObject;
            }

            var repair = FarmSceneBuilder.Gate(root, "town.salon.unlock.repair", "UnlockRepair", RepairAt, RepairSize, RepairSize,
                "Repair", RepairPrice, shell.Working);
            repair.icon = SquareIcon.Fix;
            repair.hideOnUnlock = new[] { shell.Broken };

            var decorHolder = new GameObject("Decor");
            decorHolder.transform.SetParent(root, false);
            LevelBuildKit.Identify(decorHolder, "town.salon.decor");
            var decor = decorHolder.AddComponent<MarketDecor>();
            decor.grubby = new[] { shell.Grubby };
            decor.painted = new[] { shell.Painted };
            decor.surfaces = shell.Surfaces.ToArray();
            decor.patterns = PatternFactory.SalonPatterns;
            decor.useStartingLook = true;
            decor.startingLook = new DecorLook
            {
                pattern = PatternFactory.Plain,
                primary = new Color(0.99f, 0.97f, 0.93f),
                secondary = new Color(0.74f, 0.94f, 0.89f),
                tertiary = new Color(0.20f, 0.74f, 0.68f),
                floorKind = PatternFactory.FloorTiles,
                floorColor = new Color(0.80f, 0.89f, 0.90f),
                woodShade = 0.3f,
            };

            var paint = LevelBuildKit.Station<PaintStation>("town.salon.paint", "Paint", root, PaintAt, PaintSize, "Paint",
                new Color(0.95f, 0.6f, 0.55f), smallestCard: PaintSize);
            paint.mode = Tycoon.Stations.InteractionMode.Task;
            paint.taskDuration = 0.9f;
            paint.decor = decor;
            paint.firstPrice = FarmSceneBuilder.Price(FirstPaintPrice);
            paint.changePrice = FarmSceneBuilder.Price(RepaintPrice);

            // OPEN: a free chore; what it brings in is the working business (the queue, the cash pile, NEXT, chair 1) and
            // the offer of the next purchase.
            var open = LevelBuildKit.Station<ChoreStation>("town.salon.chore.open", "Open", root, OpenAt, OpenSize, "Open",
                new Color(0.55f, 0.92f, 0.55f), smallestCard: OpenSize);
            open.icon = SquareIcon.Open;
            open.completionsNeeded = 1;
            open.taskDuration = 2f;
            open.doneMessage = "OPEN!";
            open.revealOnDone = new[] { shop, chair1, chair2Gate.gameObject };
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

        /// <summary>A heap of bags, boxes and towels with hair in it. Returns the pieces, for ClearablePile.</summary>
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
                LevelBuildKit.Mat("Trash_Towel", new Color(0.50f, 0.66f, 0.64f)),
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
                    pieces[i] = FloristModels.HairHeap(pile.transform, new Vector3(x, 0.02f, z), yaw);
                    pieces[i].transform.localScale = Vector3.one * 0.55f;
                    pieces[i].name = $"Litter_{i}";
                    continue;
                }

                pieces[i] = LevelBuildKit.Box($"Litter_{i}", pile.transform, new Vector3(x, size * 0.5f, z),
                    new Vector3(size, size * 0.8f, size * 1.1f), materials[i % materials.Length], castShadow: false,
                    rot: new Vector3(0f, yaw, 0f));
            }
            return pieces;
        }

        // ---- the building ---------------------------------------------------------------------------------------------

        private static Shell BuildShell(Transform salon)
        {
            var set = new Shell();
            var shell = new GameObject("Building").transform;
            shell.SetParent(salon, false);
            set.Building = shell;

            set.Grubby = new GameObject("Grubby");
            set.Grubby.transform.SetParent(salon, false);
            set.Painted = new GameObject("Painted");
            set.Painted.transform.SetParent(salon, false);
            set.Working = new GameObject("Working");
            set.Working.transform.SetParent(salon, false);
            set.Broken = new GameObject("Broken");
            set.Broken.transform.SetParent(salon, false);
            var grubby = set.Grubby.transform;
            var painted = set.Painted.transform;
            var working = set.Working.transform;
            var broken = set.Broken.transform;

            var floor = LevelBuildKit.Mat("Salon_Floor", new Color(0.80f, 0.89f, 0.90f), 0.35f);
            var wall = LevelBuildKit.Mat("Salon_Wall", new Color(0.99f, 0.97f, 0.93f), 0.4f);
            var cap = LevelBuildKit.Mat("Salon_WallCap", new Color(0.20f, 0.74f, 0.68f), 0.4f);
            var dirtyFloor = LevelBuildKit.Mat("Salon_FloorDirty", new Color(0.33f, 0.31f, 0.28f), 0.1f);
            var dirtyWall = LevelBuildKit.Mat("Salon_WallDirty", new Color(0.52f, 0.47f, 0.41f), 0.1f);
            var dirtyCap = LevelBuildKit.Mat("Salon_WallCapDirty", new Color(0.45f, 0.40f, 0.36f), 0.1f);
            var matIn = LevelBuildKit.Mat("Salon_DoorMat", new Color(0.46f, 0.78f, 0.52f), 0.2f);
            var matOut = LevelBuildKit.Mat("Salon_DoorMatOut", new Color(0.98f, 0.66f, 0.30f), 0.2f);

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
            Wall(ShopWest - 0.1f, EntranceFrom, ShopSouth - 0.1f, ShopSouth + 0.1f);
            Wall(EntranceTo, ExitFrom, ShopSouth - 0.1f, ShopSouth + 0.1f);
            Wall(ExitTo, ShopEast + 0.1f, ShopSouth - 0.1f, ShopSouth + 0.1f);
            // West and north, and the east wall with its side door.
            Wall(ShopWest - 0.1f, ShopWest + 0.1f, ShopSouth, ShopNorth);
            Wall(ShopWest - 0.1f, ShopEast + 0.1f, ShopNorth - 0.1f, ShopNorth + 0.1f);
            Wall(ShopEast - 0.1f, ShopEast + 0.1f, ShopSouth, SideFrom);
            Wall(ShopEast - 0.1f, ShopEast + 0.1f, SideTo, ShopNorth);

            LevelBuildKit.Box("DoorMatIn", painted, new Vector3((EntranceFrom + EntranceTo) * 0.5f, 0.07f, ShopSouth + 0.6f),
                new Vector3(1.6f, 0.03f, 0.9f), matIn, castShadow: false);
            LevelBuildKit.Box("DoorMatOut", painted, new Vector3((ExitFrom + ExitTo) * 0.5f, 0.07f, ShopSouth + 0.6f),
                new Vector3(1.6f, 0.03f, 0.9f), matOut, castShadow: false);
            LevelBuildKit.Box("SideMat", painted, new Vector3(ShopEast - 0.6f, 0.07f, (SideFrom + SideTo) * 0.5f),
                new Vector3(0.9f, 0.03f, 1.2f), matOut, castShadow: false);

            // The reception desk, the waiting chairs and the barber pole by the street door, and their wrecks.
            var desk = FloristModels.ReceptionDesk(working, DeskAt + Vector3.up * Floor, 0f);
            desk.name = "Stall";
            FloristBuilder.Solid(shell, "DeskCollision", DeskAt + new Vector3(0f, 0.6f, 0f), new Vector3(3.1f, 1.2f, 1.0f));
            FloristModels.ReceptionDesk(broken, DeskAt + Vector3.up * Floor, 0f, true);

            for (int i = 0; i < WaitZ.Length; i++)
            {
                var at = new Vector3(WaitX, 0f, WaitZ[i]);
                var chair = FloristModels.WaitingChair(working, at + Vector3.up * Floor, -90f);
                chair.name = "Stall";
                FloristBuilder.Solid(shell, "WaitingChairCollision", at + new Vector3(0f, 0.45f, 0f), new Vector3(0.75f, 0.9f, 0.95f));
                var wreck = FloristModels.WaitingChair(broken, at + Vector3.up * Floor, -90f);
                // The wrecked room: one chair tipped over, one shoved aside.
                if (i == 1) wreck.transform.localRotation = Quaternion.Euler(0f, -60f, 82f);
                if (i == 2) wreck.transform.localPosition += new Vector3(0.3f, 0f, 0.1f);
            }

            var pole = FloristModels.BarberPole(painted, new Vector3(5.4f, 0.04f, -1.15f), 0f);
            pole.name = "Stall";
            FloristBuilder.Solid(shell, "PoleCollision", new Vector3(5.4f, 0.8f, -1.15f), new Vector3(0.45f, 1.6f, 0.45f));

            BuildGrime(grubby);

            set.Grubby.SetActive(true);
            set.Painted.SetActive(false);
            set.Working.SetActive(false);
            set.Broken.SetActive(true);
            return set;
        }

        /// <summary>Dirt patches on the floor and damp stains up the walls: the grubby coat's extras.</summary>
        private static void BuildGrime(Transform grubby)
        {
            var dirt = LevelBuildKit.Mat("Salon_Dirt", new Color(0.24f, 0.21f, 0.18f), 0.05f);
            var stain = LevelBuildKit.Mat("Salon_Stain", new Color(0.40f, 0.36f, 0.30f), 0.05f);
            var rnd = new System.Random(81);
            float cx = (ShopWest + ShopEast) * 0.5f, cz = (ShopSouth + ShopNorth) * 0.5f;

            for (int i = 0; i < 16; i++)
            {
                float x = cx + ((float)rnd.NextDouble() - 0.5f) * 12.5f, z = cz + ((float)rnd.NextDouble() - 0.5f) * 8.8f;
                var o = LevelBuildKit.Cylinder("Dirt", grubby, new Vector3(x, 0.065f, z),
                    new Vector3(0.8f + (float)rnd.NextDouble() * 1.3f, 0.01f, 0.6f + (float)rnd.NextDouble() * 0.9f), dirt, false);
                o.transform.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
            }
            for (int i = 0; i < 5; i++)
                LevelBuildKit.Box("Stain", grubby, new Vector3(ShopWest + 2.5f + i * 2.6f, 0.35f, ShopNorth - 0.115f),
                    new Vector3(0.9f + (0.4f * i) % 1.2f, 0.45f, 0.01f), stain, castShadow: false);
            LevelBuildKit.Box("StainW", grubby, new Vector3(ShopWest + 0.115f, 0.30f, 6.2f), new Vector3(0.01f, 0.4f, 1.6f), stain, castShadow: false);
        }
    }
}
