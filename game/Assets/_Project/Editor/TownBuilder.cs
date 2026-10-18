using Tycoon.Core;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Generates "Town", the third location: an EMPTY, LOCKED skeleton. What the town will hold has
    /// not been decided, so this file builds only the frame every location needs and nothing in it:
    ///
    ///     a walled patch of ground with a fence, a road stub, a paved square, grass,
    ///     an arrival point, and the travel squares in and out
    ///     the florist (FloristBuilder) and the hairdresser salon (SalonBuilder) on the street
    ///     (EdgeEast is 22; the ATM stands at (1.7, 3.0); the old COMING SOON square is gone)
    ///
    /// It is written the way <see cref="MarketBuilder"/> is - a second root in the same scene, turned
    /// the same 45 degrees, every number a commented constant - and reuses the same parts: Location,
    /// TravelStation, UnlockStation, RevealWhenAll, LevelBuildKit's boundary, fence and grass.
    ///
    /// How it is reached:
    ///
    ///     the butcher is complete (every butcher purchase, sausages last)   RevealWhenAll
    ///       -> the town gate appears on the supermarket's forecourt         "town.unlock", Price()
    ///          buy it -> the travel square beside it, and the whole town root, switch on
    ///             travel square (market) -> Town;   travel square (town) -> back to the supermarket
    ///
    /// The town root is saved INACTIVE, like the market's: it does not tick, save or exist until the
    /// gate is paid. Nothing inside it saves state, so it adds no save entries beyond the gate.
    /// Save ids (all new): town.unlock, market.travel.town, town.travel.market, town.locked.soon.
    /// </summary>
    internal static class TownBuilder
    {
        // ---- where the town stands ---------------------------------------------------
        // Offsets in the FARM's grid. The ground the player can stand on is the 160 m plane, and the
        // market already reaches world x 78 along the road axis, so a third location cannot go
        // further down that axis. It goes south of the farm instead: centred 56 m below the farm's
        // origin, which puts its fence ~14 m clear of the farm's southern one and everything of
        // it well inside the plane (the layout audit's world-edge check measures the margin).
        internal static readonly Vector3 Offset = new Vector3(0f, 0f, -56f);

        // ---- where the world ends ------------------------------------------------------
        // Small on purpose: an empty town only has to look like a place. The fence, the invisible
        // wall and the grass all read these.
        private const float EdgeWest = -16f;
        // East grew from 16 to 22 for the hairdresser (SalonBuilder: the shop stands at x 4.8..18.2, its bin at 20).
        private const float EdgeEast = 22f;
        private const float EdgeSouth = -12f;
        // North grew from 13 to 19.5 for the florist's meadow (FloristBuilder.MeadowNorth 17.2 + room for the fence).
        private const float EdgeNorth = 19.5f;

        /// <summary>The road stub's centre line, in the town's grid. The road box is 2.8 m deep.</summary>
        internal const float RoadZ = -3f;

        // ---- the paved square ----------------------------------------------------------
        // North of the road, east of the florist: where the next businesses of the street will stand.
        private const float PlazaWest = 1f, PlazaEast = 21f;
        private const float PlazaSouth = -1.4f, PlazaNorth = 10.4f;

        // ---- travel --------------------------------------------------------------------
        // The way back sits on the west end of the road, and the player lands a few metres inside
        // it, clear of its square (landing inside one would send them straight back).
        private static readonly Vector3 ToMarketPosition = new Vector3(-12f, 0f, RoadZ);
        private static readonly Vector3 ArrivalPoint = new Vector3(-6f, 0f, RoadZ);

        /// <summary>
        /// The supermarket end: on the forecourt east of the shop's front, off the shoppers' road
        /// (z -9) and clear of the bin and the farm square. The gate and the travel square share it,
        /// as the farm's gate and its travel square do.
        /// </summary>
        private static readonly Vector3 MarketSquarePosition = new Vector3(14.2f, 0f, -3.8f);

        /// <summary>The cash machine on the paving east of the florist; its squares lie south of it.</summary>
        // (Moved 2.6 -> 1.7 for the hairdresser: its deposit square stands 1.9 m east of the machine and must clear the salon's west wall.)
        private static readonly Vector3 AtmPosition = new Vector3(1.7f, 0f, 3.0f);

        // ---- price ---------------------------------------------------------------------
        // From the pacing simulation (plans/econ/market_sim.py, proposal B): the butcher's last
        // purchase leaves the account earning about $2,050 a minute, and every step before it takes
        // 2 to 4.5 minutes of that. A whole new location is a bigger milestone than any shelf or
        // hire, so it is priced at about six minutes of full income. Goes through Price().
        private const double TownPrice = 12000d;

        /// <summary>
        /// Builds the town as a root beside the farm, and wires the way to it into the supermarket.
        /// <paramref name="market"/> supplies the supermarket location, the root the gate and the
        /// travel square are children of, and the butcher purchases the gate waits on.
        /// </summary>
        internal static Location Build(Transform farmRoot, MarketBuilder.Result market, FarmSceneBuilder.Items items)
        {
            var go = new GameObject("Town");
            go.transform.rotation = farmRoot.rotation;
            go.transform.position = farmRoot.position + farmRoot.rotation * Offset;
            var root = go.transform;

            var location = go.AddComponent<Location>();
            location.locationId = "town";
            location.displayName = "Town";

            var arrival = new GameObject("Spawn");
            arrival.transform.SetParent(root, false);
            arrival.transform.localPosition = ArrivalPoint;
            location.spawnPoint = arrival.transform;

            BuildGround(root);
            LevelBuildKit.BuildBoundary(root, EdgeWest, EdgeEast, EdgeSouth, EdgeNorth);
            LevelBuildKit.BuildFenceAround(root, EdgeWest, EdgeEast, EdgeSouth, EdgeNorth, RoadZ,
                meshName: "TownFenceLine");

            var back = LevelBuildKit.Station<TravelStation>("town.travel.market", "TravelToMarket",
                root, ToMarketPosition, new Vector2(2.5f, 2.1f), "Supermarket", new Color(0.55f, 0.8f, 1f));
            back.destination = market.Location;
            back.taskDuration = 1.2f;

            // The first business of the street, and the second east of it: the hairdresser's derelict shop appears
            // when the florist's last purchase (the gardener) is made.
            var gardenerHire = FloristBuilder.Build(root, items);
            var salon = SalonBuilder.Build(root, items);
            var salonWatcher = new GameObject("SalonReveal");
            salonWatcher.transform.SetParent(root, false);
            var salonReveal = salonWatcher.AddComponent<RevealWhenAll>();
            salonReveal.unlocks = new[] { gardenerHire };
            salonReveal.reveal = new[] { salon };

            // ---- the way in, from the supermarket ----------------------------------------
            var marketRoot = market.Root.transform;
            var toTown = LevelBuildKit.Station<TravelStation>("market.travel.town", "TravelToTown",
                marketRoot, MarketSquarePosition, new Vector2(2.5f, 2.1f), "Town", new Color(0.55f, 0.8f, 1f));
            toTown.destination = location;
            toTown.taskDuration = 1.2f;

            // The gate sits exactly where the travel square will be, as the farm's does. Paying the
            // last of it leaves the player standing in the square, so the ring fills and they go.
            var gate = FarmSceneBuilder.Gate(marketRoot, "town.unlock", "UnlockTown", MarketSquarePosition,
                new Vector2(3.0f, 2.4f), "Town", TownPrice, go, toTown.gameObject);
            gate.icon = SquareIcon.Buy;

            // Offered only when the butcher is complete: the extension, every step inside it and the
            // pigs at their maximum. The watcher lives on the market root, which is always active
            // once the supermarket is bought, and reads the saved purchases, so it holds after a reload.
            // Once the town is bought the supermarket is let go the way the farm was when the supermarket
            // was bought (FarmSceneBuilder, parts.Counters): its takings go straight to the account at half
            // value - the same RegisterStation rule, the same 0.5 - so the florist, not a bigger shop, is
            // where the money is made. The market has no wear to switch off. Both read the saved purchase,
            // so they hold after a reload, and the florist's own tills are untouched.
            foreach (var checkout in marketRoot.GetComponentsInChildren<CheckoutStation>(true))
            {
                if (checkout.pile == null) continue;
                checkout.accountWhenUnlocked = gate;
                checkout.accountShare = 0.5f;
            }
            foreach (var counter in marketRoot.GetComponentsInChildren<RegisterStation>(true))
            {
                if (counter.pile == null) continue;
                counter.accountWhenUnlocked = gate;
                counter.accountShare = 0.5f;
            }

            // The town's cash machine, open from the moment the town is bought: the renovation is paid in
            // cash and the account is where the farm's and the shop's takings now land.
            LevelBuildKit.BuildAtm("town.atm", "Atm", root, AtmPosition, new Vector2(1.6f, 1.6f), new Vector2(1.3f, 1.5f), 1.9f);

            var watcher = new GameObject("TownUnlock");
            watcher.transform.SetParent(marketRoot, false);
            var reveal = watcher.AddComponent<RevealWhenAll>();
            var needed = new System.Collections.Generic.List<UnlockStation> { market.ButcherExtension };
            needed.AddRange(market.ButcherUnlocks);
            reveal.unlocks = needed.ToArray();
            reveal.maxed = market.ButcherMaxed;
            reveal.reveal = new[] { gate.gameObject };
            gate.gameObject.SetActive(false);

            return location;
        }

        // ---- ground, road, grass -------------------------------------------------------

        private static void BuildGround(Transform root)
        {
            var tarmac = LevelBuildKit.Mat("Road", new Color(0.36f, 0.36f, 0.39f));
            var markings = LevelBuildKit.Mat("RoadLine", new Color(0.88f, 0.88f, 0.8f));
            var paving = LevelBuildKit.Mat("Town_Paving", new Color(0.70f, 0.69f, 0.66f));

            // The road stub runs right through the fence gaps at both ends, as the market's does.
            const float roadWest = EdgeWest - 1f;
            const float roadEast = EdgeEast + 4f;
            LevelBuildKit.Box("Road", root, new Vector3((roadWest + roadEast) * 0.5f, 0.02f, RoadZ),
                new Vector3(roadEast - roadWest, 0.04f, 2.8f), tarmac, castShadow: false);

            for (int x = (int)roadWest; x <= (int)roadEast; x += 2)
            {
                LevelBuildKit.Box($"Line_{x}", root, new Vector3(x, 0.05f, RoadZ),
                    new Vector3(0.9f, 0.04f, 0.14f), markings, castShadow: false);
            }

            LevelBuildKit.Box("Plaza", root,
                new Vector3((PlazaWest + PlazaEast) * 0.5f, 0.025f, (PlazaSouth + PlazaNorth) * 0.5f),
                new Vector3(PlazaEast - PlazaWest, 0.05f, PlazaNorth - PlazaSouth), paving, castShadow: false);

            // Grass wherever the road and the paving are not. Kept clear of every square, which is
            // painted 9 cm up (a tuft is up to 40 cm tall).
            const float beyond = 8f;
            var area = new Rect(EdgeWest - beyond, EdgeSouth - beyond,
                                (EdgeEast - EdgeWest) + beyond * 2f,
                                (EdgeNorth - EdgeSouth) + beyond * 2f);

            var keepClear = new[]
            {
                // The road and the strip either side of it, out to the end of the tarmac.
                new Rect(roadWest - 1f, RoadZ - 2.2f, (roadEast - roadWest) + 2f, 4.4f),
                // The paved square and the margin round it.
                new Rect(PlazaWest - 1f, PlazaSouth - 1f, (PlazaEast - PlazaWest) + 2f,
                    (PlazaNorth - PlazaSouth) + 2f),
                // The florist: the shop and its fenced meadow, which has its own lawn.
                new Rect(FloristBuilder.ShopWest - 0.8f, FloristBuilder.ShopSouth - 0.8f,
                    (FloristBuilder.ShopEast - FloristBuilder.ShopWest) + 1.6f,
                    (FloristBuilder.MeadowNorth - FloristBuilder.ShopSouth) + 1.6f),
                // The florist's bin on the pavement west of the street door: its pad, its square and the walk to it.
                new Rect(FloristBuilder.BinAt.x - 1.8f, FloristBuilder.BinAt.z - 2.9f, 3.6f, 5.3f),
                // The hairdresser: the shop and its bin outside the side door.
                SalonBuilder.Bounds,
            };

            LevelBuildKit.BuildGrass(root, area, keepClear, tufts: 420, seed: 20261014,
                meshName: "TownGrassCover", objectName: "Grass");
        }
    }
}
