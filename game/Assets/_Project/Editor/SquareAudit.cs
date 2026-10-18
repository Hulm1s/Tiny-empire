using System.Collections.Generic;
using Tycoon.Core;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Measures every built location and reports anything standing on top of anything else.
    ///
    /// Two squares on top of each other means the player stands in both at once - buying a
    /// worker while harvesting, or paying into two purchases. Eyeballing the farm does not
    /// catch it reliably, and the numbers in the builder are easy to get subtly wrong, so the
    /// scene itself gets asked.
    ///
    /// It measures the BUILDINGS too - shells, roofs, and the pens and pastures beside them.
    /// Squares alone were not enough. A cow shed's repair square could sit squarely on the
    /// neighbouring coop's chicken pen, and a second shed's squares on the first shed's
    /// pasture, and the audit reported a clean bill of health because a pen is scenery rather
    /// than a station. That is exactly the "the dairy is on top of the chickens" that the eye
    /// sees immediately and the old check could not see at all.
    ///
    /// Every <see cref="Location"/> in the scene is audited in its own grid: the farm, the
    /// supermarket, and whatever comes next. Each gets the same checks, plus a staged replay of
    /// its progression (see <see cref="StagedOverlaps"/>) because a location that is brought
    /// back in steps has states in between "day one" and "fully built" that nobody has looked at.
    /// </summary>
    public static class SquareAudit
    {
        private const string ScenePath = "Assets/_Project/Scenes/Farm.unity";

        private class Entry
        {
            public string Name;
            public string Owner;    // the building it belongs to, or itself
            public Rect Trigger;    // where the action actually works
            public Rect Card;       // what the player sees
            public bool Active;

            /// <summary>
            /// True once every purchase has been made: gates delete themselves, and everything
            /// they were hiding is on. That is the fullest the location ever gets, and so the
            /// state most likely to have two things fighting over the same patch of grass.
            /// </summary>
            public bool ActiveWhenBuilt;
        }

        /// <summary>A piece of scenery with a footprint: a building shell, a pen, a stall, a wall.</summary>
        private class Solid
        {
            public string Name;
            public string Owner;
            public Rect Area;
        }

        [MenuItem("Tycoon/Audit Interaction Squares")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var locations = new List<Location>(Object.FindObjectsByType<Location>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));
            // Stable order, so the log reads the same every time.
            locations.Sort((a, b) => string.CompareOrdinal(a.locationId, b.locationId));

            if (locations.Count == 0)
            {
                Debug.LogError("[Audit] No Location in the scene.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            int total = 0;
            foreach (var location in locations)
            {
                int faults = AuditLocation(location);
                total += faults;

                Debug.Log(faults == 0
                    ? $"[Audit] Nothing overlaps. ({location.locationId})"
                    : $"[Audit] {location.locationId}: {faults} faults.");
            }

            total += Links(locations);
            total += SaveIds();

            Debug.Log(total == 0 ? "[Audit] Nothing overlaps." : $"[Audit] {total} faults.");
            Debug.Log("AUDIT_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static int AuditLocation(Location location)
        {
            var root = location.transform;
            string tag = $"[{location.locationId}]";

            var entries = new List<Entry>();

            foreach (var station in Object.FindObjectsByType<StationBase>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!station.transform.IsChildOf(root)) continue;

                var box = station.GetComponent<BoxCollider>();
                var square = station.GetComponent<InteractionSquare>();
                if (box == null) continue;

                Vector3 local = root.InverseTransformPoint(station.transform.position);
                Vector2 triggerSize = new Vector2(box.size.x, box.size.z);
                // Read off the square rather than mirrored here, so a square deliberately
                // drawn smaller than the usual minimum - the bin - is measured as it really is.
                Vector2 cardSize = square != null
                    ? new Vector2(Mathf.Max(square.size.x, square.minCard.x),
                                  Mathf.Max(square.size.y, square.minCard.y))
                    : triggerSize;

                entries.Add(new Entry
                {
                    Name = station.name + " (" + station.GetType().Name + ")",
                    // A workshop parents its own squares, so the parent names the building they
                    // serve. A square that belongs to nothing - a gate, a hire, the bin - owns
                    // itself and so can never be excused for sitting on someone else's shed.
                    Owner = station.transform.parent != null
                        ? station.transform.parent.name
                        : station.name,
                    Trigger = Centred(local, triggerSize),
                    Card = Centred(local, cardSize),
                    // As the player would meet it the moment the location opens: the root itself
                    // counts as switched on, because for a location that is bought (the market)
                    // it is saved off until the gate is paid, and "everything in it is off" would
                    // make the day-one check vacuous.
                    Active = ActiveIn(root, station.gameObject, null),
                    // Every UnlockStation and ChoreStation hides itself once done, and so
                    // does the collect square of a pile of rubbish once the pile is carried
                    // away; everything else in the scene is either on already or revealed by one.
                    ActiveWhenBuilt = !(station is UnlockStation) && !(station is ChoreStation)
                                      && !IsRubbishSquare(station),
                });
            }

            var solids = CollectSolids(root);

            Debug.Log($"[Audit] {tag} {entries.Count} squares, {solids.Count} building footprints");

            // Checked three ways: as the location opens, as it ends, and at every step between.
            // An overlap that only appears once the second coop has been bought, or while the
            // shop is half painted, is still an overlap the player will meet.
            int clashes = Overlaps(entries, $"{tag} day one", e => e.Active)
                        + Overlaps(entries, $"{tag} fully built", e => e.ActiveWhenBuilt)
                        + StagedOverlaps(root, tag, out var firstLive, out var firstBuffer)
                        + SolidOverlaps(solids, tag)
                        + SquaresOnSolids(entries, solids, tag);

            // A trigger noticeably bigger than its outline means the player can act from
            // somewhere the game never told them about, and vice versa.
            foreach (var e in entries)
            {
                float dx = Mathf.Abs(e.Trigger.width - e.Card.width);
                float dy = Mathf.Abs(e.Trigger.height - e.Card.height);
                if (dx > 0.8f || dy > 0.8f)
                    Debug.LogWarning($"[Audit] {tag} MISMATCH {e.Name}: trigger " +
                                     $"{e.Trigger.width:0.0}x{e.Trigger.height:0.0} vs outline " +
                                     $"{e.Card.width:0.0}x{e.Card.height:0.0}");
            }

            clashes += SpawnClear(location, entries, solids, tag);
            clashes += ShopperPaths(root, solids, tag);
            clashes += ChairPaths(root, solids, entries, tag);
            clashes += ButcherShell(root, solids, tag);
            clashes += EdgeOfWorld(root, tag);
            clashes += Progression(root, firstLive, tag);
            clashes += GrowthRules(root, firstLive, firstBuffer, tag);
            clashes += ButcherChain(root, solids, entries, firstLive, tag);
            clashes += FloristChain(root, solids, entries, firstLive, tag);
            clashes += SalonChain(root, entries, firstLive, tag);
            clashes += LetGoRules(location, tag);
            ReportLayout(root, solids, tag);

            return clashes;
        }

        /// <summary>
        /// Reports every pair of squares that share ground in one particular state of a location.
        ///
        /// A gate sits exactly on top of whatever it unlocks, by design, so a pair is only a
        /// fault when both halves are live at the same moment - hence taking the state as a
        /// predicate rather than reading it off the scene.
        /// </summary>
        private static int Overlaps(List<Entry> entries, string state, System.Func<Entry, bool> live)
        {
            int clashes = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                for (int j = i + 1; j < entries.Count; j++)
                {
                    var a = entries[i];
                    var b = entries[j];
                    if (!live(a) || !live(b)) continue;
                    if (!a.Card.Overlaps(b.Card)) continue;

                    clashes++;
                    var shared = Intersection(a.Card, b.Card);
                    Debug.LogWarning(
                        $"[Audit] OVERLAP ({state}) {a.Name} x {b.Name} " +
                        $"by {shared.width:0.00} x {shared.height:0.00} m");
                }
            }

            Debug.Log($"[Audit] {state}: {clashes} overlapping pairs.");
            return clashes;
        }

        // ---------------------------------------------------------------- staged replay

        /// <summary>
        /// Is this object on, if everything inside the location root is taken as reachable and
        /// the given switches have been thrown? The root's own flag is ignored on purpose.
        /// </summary>
        private static bool ActiveIn(Transform root, GameObject go, Dictionary<GameObject, bool> switched)
        {
            for (Transform at = go.transform; at != null && at != root; at = at.parent)
            {
                bool on = at.gameObject.activeSelf;
                if (switched != null && switched.TryGetValue(at.gameObject, out bool forced)) on = forced;
                if (!on) return false;
            }
            return true;
        }

        /// <summary>
        /// Plays the location's progression forward in rounds and checks the squares live in
        /// each one.
        ///
        /// Every round: whatever squares are on right now are checked against one another;
        /// then everything completable among them is completed at once - every gate paid, every
        /// chore done, every upgrade maxed - and whatever that reveals or hides is applied, and
        /// the "all of these" watchers are asked whether they are satisfied yet. It stops when a
        /// round changes nothing.
        ///
        /// This is the check that sees the shop half-renovated. "Day one" and "fully built"
        /// are the two ends; the repair and paint squares only ever coexist with the chores'
        /// aftermath in the middle, and so does every gate on the farm with whatever is already
        /// standing beside it. It also reports anything that is NEVER live - a square nothing
        /// reveals is a square the player will never meet - and records the round each square
        /// first appears, which the progression check uses to confirm a hire is not sold before
        /// its route exists.
        /// </summary>
        private static int StagedOverlaps(Transform root, string tag,
            out Dictionary<StationBase, int> firstLive, out Dictionary<ItemBuffer, int> firstBuffer)
        {
            firstLive = new Dictionary<StationBase, int>();
            firstBuffer = new Dictionary<ItemBuffer, int>();

            var buffers = new List<ItemBuffer>(root.GetComponentsInChildren<ItemBuffer>(true));

            var stations = new List<StationBase>();
            foreach (var station in Object.FindObjectsByType<StationBase>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (station.transform.IsChildOf(root) && station.GetComponent<BoxCollider>() != null)
                    stations.Add(station);
            }

            var watchers = new List<RevealWhenAll>();
            foreach (var w in Object.FindObjectsByType<RevealWhenAll>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (w.transform.IsChildOf(root)) watchers.Add(w);
            }

            var switched = new Dictionary<GameObject, bool>();
            var done = new HashSet<StationBase>();
            var fired = new HashSet<RevealWhenAll>();

            // A purchase made in ANOTHER location that switches something on in this one - the butcher's
            // pig farm reveals the farm's pig column - is taken as made from the start here: that
            // location's own replay is what proves the purchase itself is reachable, and without this the
            // pig column would be reported as never switched on.
            foreach (var gate in Object.FindObjectsByType<UnlockStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gate.revealOnUnlock == null || gate.transform.IsChildOf(root)) continue;
                foreach (var go in gate.revealOnUnlock)
                    if (go != null && go.transform.IsChildOf(root)) switched[go] = true;
            }

            int clashes = 0;
            int round = 0;
            const int maxRounds = 24;

            for (; round < maxRounds; round++)
            {
                var live = new List<StationBase>();
                foreach (var station in stations)
                {
                    if (done.Contains(station)) continue;
                    if (!ActiveIn(root, station.gameObject, switched)) continue;
                    live.Add(station);
                    if (!firstLive.ContainsKey(station)) firstLive[station] = round;
                }

                // When each shelf and crate first stands, for the product-before-shelf rule.
                foreach (var buffer in buffers)
                    if (!firstBuffer.ContainsKey(buffer) && ActiveIn(root, buffer.gameObject, switched))
                        firstBuffer[buffer] = round;

                // Only one product may be open for ordering at a time: never two order
                // squares live together, or a shelf's goods could be bought before the last
                // product has its place.
                int openOrders = 0;
                foreach (var station in live)
                    if (station is UnlockStation gate && RevealsCrate(gate)) openOrders++;
                if (openOrders > 1)
                {
                    clashes++;
                    Debug.LogWarning($"[Audit] ORDERS {tag} stage {round}: {openOrders} goods orders " +
                                     "are on offer at once; the market sells them one at a time.");
                }

                // Pairs among what is live this round.
                int roundClashes = 0;
                for (int i = 0; i < live.Count; i++)
                {
                    for (int j = i + 1; j < live.Count; j++)
                    {
                        Rect a = CardOf(root, live[i]);
                        Rect b = CardOf(root, live[j]);
                        if (!a.Overlaps(b)) continue;

                        roundClashes++;
                        var shared = Intersection(a, b);
                        Debug.LogWarning(
                            $"[Audit] OVERLAP ({tag} stage {round}) {live[i].name} x {live[j].name} " +
                            $"by {shared.width:0.00} x {shared.height:0.00} m");
                    }
                }
                clashes += roundClashes;

                bool changed = false;

                foreach (var station in live)
                {
                    switch (station)
                    {
                        case UnlockStation gate:
                            done.Add(gate);
                            Throw(switched, gate.revealOnUnlock, true);
                            Throw(switched, gate.hideOnUnlock, false);
                            changed = true;
                            break;

                        case ChoreStation chore:
                            done.Add(chore);
                            Throw(switched, chore.revealOnDone, true);
                            Throw(switched, chore.hideOnDone, false);
                            changed = true;
                            break;

                        // Carrying a pile away empties it, and the square goes with it.
                        case CollectStation take when IsRubbishSquare(take):
                            done.Add(take);
                            changed = true;
                            break;
                    }
                }

                foreach (var watcher in watchers)
                {
                    if (fired.Contains(watcher)) continue;
                    if (!ActiveIn(root, watcher.gameObject, switched)) continue;
                    if (!Met(watcher, done, live)) continue;

                    fired.Add(watcher);
                    Throw(switched, watcher.reveal, true);
                    changed = true;
                }

                Debug.Log($"[Audit] {tag} stage {round}: {live.Count} squares live, " +
                          $"{roundClashes} overlapping pairs.");

                if (!changed) break;
            }

            // The salon's SWEEP squares are switched on by the hair itself (HairMess), at play time, not by a purchase.
            var messSquares = new HashSet<GameObject>();
            foreach (var mess in root.GetComponentsInChildren<HairMess>(true))
                if (mess.square != null) messSquares.Add(mess.square);

            // A square nothing ever switches on cannot be used by anyone.
            foreach (var station in stations)
            {
                if (firstLive.ContainsKey(station)) continue;
                if (messSquares.Contains(station.gameObject)) continue;
                clashes++;
                Debug.LogWarning($"[Audit] UNREACHABLE {tag} {station.name} ({station.GetType().Name}) " +
                                 "is never switched on by anything.");
            }

            return clashes;
        }

        /// <summary>
        /// Are all of a watcher's conditions satisfied? Upgrades count as maxed once they are
        /// live, because buying them is just paying, and every square that is live gets paid.
        /// </summary>
        private static bool Met(RevealWhenAll watcher, HashSet<StationBase> done, List<StationBase> live)
        {
            if (watcher.unlocks != null)
                foreach (var u in watcher.unlocks)
                    if (u == null || !done.Contains(u)) return false;

            if (watcher.chores != null)
                foreach (var c in watcher.chores)
                    if (c == null || !done.Contains(c)) return false;

            if (watcher.maxed != null)
                foreach (var m in watcher.maxed)
                    if (m == null || !live.Contains(m)) return false;

            // A pile is empty once its collect square has been worked through.
            if (watcher.emptied != null)
            {
                foreach (var heap in watcher.emptied)
                {
                    var pile = heap != null ? heap.GetComponent<ClearablePile>() : null;
                    var square = pile != null && pile.square != null
                        ? pile.square.GetComponent<StationBase>() : null;
                    if (square == null || !done.Contains(square)) return false;
                }
            }

            // The first paint job is completable as soon as the square is there to stand in.
            if (watcher.painted != null)
                foreach (var p in watcher.painted)
                    if (p == null || !live.Contains(p)) return false;

            return true;
        }

        /// <summary>True for a purchase that brings in a supplied crate: the market's goods orders.</summary>
        private static bool RevealsCrate(UnlockStation gate)
        {
            if (gate.revealOnUnlock == null) return false;
            foreach (var go in gate.revealOnUnlock)
                if (go != null && go.GetComponent<SupplyFeed>() != null) return true;
            return false;
        }

        /// <summary>
        /// The supermarket's growth rules, which the generic checks cannot see.
        ///
        /// Nothing may be orderable before the shelf that sells it exists (otherwise the player
        /// can fill their arms with goods that have nowhere to go, and carried goods cannot be
        /// put down anywhere but a shelf or the bin outside); every stocker route has to be
        /// something that turns up in the scene and carries one product from its own crate to
        /// that product's own shelf; and a second checkout has to be wired to share the first's
        /// shoppers and reputation.
        /// </summary>
        private static int GrowthRules(Transform root, Dictionary<StationBase, int> firstLive,
            Dictionary<ItemBuffer, int> firstBuffer, string tag)
        {
            int faults = 0;

            // The shelves, as shoppers see them.
            var shelves = new List<Tycoon.Customers.StoreShelf>();
            foreach (var queue in root.GetComponentsInChildren<Tycoon.Customers.CustomerQueue>(true))
                if (queue.IsBrowsing) shelves.AddRange(queue.shelves);

            if (shelves.Count == 0) return 0;   // the farm: nothing here applies

            // Product before shelf: when does each supplied crate (and the order for it) first
            // exist, against the first moment its goods have a shelf?
            var orderFor = new Dictionary<GameObject, UnlockStation>();
            foreach (var gate in root.GetComponentsInChildren<UnlockStation>(true))
                if (gate.revealOnUnlock != null)
                    foreach (var go in gate.revealOnUnlock)
                        if (go != null && go.GetComponent<SupplyFeed>() != null) orderFor[go] = gate;

            foreach (var collect in root.GetComponentsInChildren<CollectStation>(true))
            {
                var crate = collect.source;
                if (crate == null || crate.GetComponent<SupplyFeed>() == null) continue;

                int soldOn = 0;
                foreach (var shelf in shelves)
                {
                    if (shelf.buffer == null || shelf.buffer.item != crate.item) continue;
                    soldOn++;

                    if (!firstBuffer.TryGetValue(shelf.buffer, out int shelfRound)) continue;   // reported as unreachable elsewhere

                    // The collect square, and the order that brings the crate in.
                    if (firstLive.TryGetValue(collect, out int collectRound) && collectRound < shelfRound)
                    {
                        faults++;
                        Debug.LogWarning($"[Audit] PRODUCT-BEFORE-SHELF {tag} {collect.name} is live from " +
                                         $"stage {collectRound} but {shelf.buffer.item.displayName}'s shelf " +
                                         $"only stands from stage {shelfRound}.");
                    }

                    if (orderFor.TryGetValue(crate.gameObject, out var order) &&
                        firstLive.TryGetValue(order, out int orderRound) && orderRound < shelfRound)
                    {
                        faults++;
                        Debug.LogWarning($"[Audit] PRODUCT-BEFORE-SHELF {tag} {order.name} is on offer from " +
                                         $"stage {orderRound} but {shelf.buffer.item.displayName}'s shelf " +
                                         $"only stands from stage {shelfRound}.");
                    }
                }

                if (soldOn == 0)
                {
                    faults++;
                    Debug.LogWarning($"[Audit] PRODUCT-BEFORE-SHELF {tag} {crate.item.displayName} is supplied " +
                                     "but no shelf sells it.");
                }
            }

            // Stocker routes: every pair reachable, one product per pair, and a log of what each
            // stocker can carry so a room's stockers can be read off at a glance.
            var summaries = new List<string>();
            foreach (var worker in root.GetComponentsInChildren<Tycoon.Upkeep.WorkerAgent>(true))
            {
                if (worker.routes == null || worker.routes.Length == 0) continue;
                if (worker.routes[0] != null && worker.routes[0].pickup is CutStation) continue;   // the butcher: see ButcherChain
                if (worker.routes[0] != null && worker.routes[0].dropoff is BedStation) continue;   // the gardener: see FloristChain

                var carried = new List<string>();
                foreach (var route in worker.routes)
                {
                    var from = route != null ? route.pickup as CollectStation : null;
                    var to = route != null ? route.dropoff as DepositStation : null;
                    if (from == null || to == null || from.source == null || to.target == null)
                    {
                        faults++;
                        Debug.LogWarning($"[Audit] STOCKER {tag} {worker.name} has a route that is not " +
                                         "a collect square to a stocking square.");
                        continue;
                    }

                    if (from.source.item != to.target.item)
                    {
                        faults++;
                        Debug.LogWarning($"[Audit] STOCKER {tag} {worker.name} carries " +
                                         $"{from.source.item.displayName} from {from.name} to {to.name}, which " +
                                         $"stocks {to.target.item.displayName}.");
                    }

                    foreach (StationBase end in new StationBase[] { from, to })
                    {
                        if (firstLive.ContainsKey(end)) continue;
                        faults++;
                        Debug.LogWarning($"[Audit] STOCKER {tag} {worker.name}'s route uses {end.name}, " +
                                         "which is never switched on.");
                    }

                    carried.Add(from.source.item.displayName);
                }

                // The first route is also pickup/dropoff, which is what the generic route check
                // and the hire's own sale price are measured against.
                if (worker.routes[0].pickup != worker.pickup || worker.routes[0].dropoff != worker.dropoff)
                {
                    faults++;
                    Debug.LogWarning($"[Audit] STOCKER {tag} {worker.name}'s pickup/dropoff is not its first route.");
                }

                summaries.Add($"{worker.name}: {string.Join("/", carried)}");
            }

            summaries.Sort();
            if (summaries.Count > 0) Debug.Log($"[Audit] {tag} stockers: " + string.Join(" | ", summaries));

            // Two checkouts: the second must hand its shoppers and reputation to the first's.
            foreach (var queue in root.GetComponentsInChildren<Tycoon.Customers.CustomerQueue>(true))
            {
                if (!queue.IsBrowsing || queue.alternates == null) continue;

                foreach (var other in queue.alternates)
                {
                    if (other != null && other.sharesReputationWith == queue) continue;
                    faults++;
                    Debug.LogWarning($"[Audit] CHECKOUT {tag} {(other != null ? other.name : "(missing)")} " +
                                     "is an alternate till that does not share the shop's reputation.");
                }
            }

            Debug.Log($"[Audit] {tag} growth rules: {faults} faults.");
            return faults;
        }

        /// <summary>The collect square of a pile of rubbish: it disappears when the pile is gone.</summary>
        private static bool IsRubbishSquare(StationBase station) =>
            station is CollectStation take && take.source != null &&
            take.source.GetComponent<ClearablePile>() != null;

        private static void Throw(Dictionary<GameObject, bool> switched, GameObject[] targets, bool on)
        {
            if (targets == null) return;
            foreach (var go in targets)
                if (go != null) switched[go] = on;
        }

        private static Rect CardOf(Transform root, StationBase station)
        {
            var box = station.GetComponent<BoxCollider>();
            var square = station.GetComponent<InteractionSquare>();
            Vector3 local = root.InverseTransformPoint(station.transform.position);
            Vector2 size = square != null
                ? new Vector2(Mathf.Max(square.size.x, square.minCard.x),
                              Mathf.Max(square.size.y, square.minCard.y))
                : new Vector2(box.size.x, box.size.z);
            return Centred(local, size);
        }

        // ---------------------------------------------------------------- footprints

        /// <summary>
        /// Every building shell, pen, pasture, stall and wall in the location, with the ground
        /// it covers.
        ///
        /// Found by name rather than by component, because these are plain boxes with no script
        /// on them - which is precisely why nothing was checking them.
        /// </summary>
        private static List<Solid> CollectSolids(Transform root)
        {
            var found = new List<Solid>();
            var wanted = new[] { "Shell", "Pen", "Pasture", "Stall", "Wall", "Crate" };

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                bool match = false;
                for (int i = 0; i < wanted.Length; i++)
                    if (t.name == wanted[i]) { match = true; break; }
                if (!match) continue;

                if (!TryFootprint(root, t, out Rect area)) continue;

                string owner = t.parent != null ? t.parent.name : t.name;
                // The florist's walls (painted coat) and its counter and stands (the fittings that REPAIR brings
                // back) are separate groups so each can be switched, but they are one building.
                if (owner == "Painted" || owner == "Working") owner = "Building";
                found.Add(new Solid { Name = (t.parent != null ? t.parent.name : "") + "/" + t.name, Owner = owner, Area = area });
            }

            return found;
        }

        /// <summary>
        /// The ground a group of meshes covers, in the level's own grid.
        ///
        /// Worked out from each mesh's own corners rather than from Renderer.bounds, because
        /// those are axis-aligned in WORLD space and the whole level is turned 45 degrees - so
        /// a three metre shed would measure well over four, and every building on the farm
        /// would be reported as overlapping its neighbour.
        /// </summary>
        private static bool TryFootprint(Transform root, Transform group, out Rect area)
        {
            area = new Rect();
            bool any = false;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var renderer in group.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Bounds local = filter.sharedMesh.bounds;

                for (int corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z);

                    Vector3 levelLocal = root.InverseTransformPoint(
                        renderer.transform.TransformPoint(point));

                    if (levelLocal.x < minX) minX = levelLocal.x;
                    if (levelLocal.x > maxX) maxX = levelLocal.x;
                    if (levelLocal.z < minZ) minZ = levelLocal.z;
                    if (levelLocal.z > maxZ) maxZ = levelLocal.z;
                    any = true;
                }
            }

            if (!any) return false;
            area = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            return true;
        }

        /// <summary>
        /// Buildings standing in one another. A pen touches its own shed by design, so parts of
        /// the same building are skipped; anything else sharing ground is a fault.
        /// </summary>
        private static int SolidOverlaps(List<Solid> solids, string tag)
        {
            int clashes = 0;

            for (int i = 0; i < solids.Count; i++)
            {
                for (int j = i + 1; j < solids.Count; j++)
                {
                    var a = solids[i];
                    var b = solids[j];
                    if (a.Owner == b.Owner) continue;
                    if (!a.Area.Overlaps(b.Area)) continue;

                    clashes++;
                    var shared = Intersection(a.Area, b.Area);
                    Debug.LogWarning(
                        $"[Audit] BUILDINGS {tag} {a.Name} x {b.Name} " +
                        $"by {shared.width:0.00} x {shared.height:0.00} m");
                }
            }

            Debug.Log($"[Audit] {tag} buildings: {clashes} overlapping pairs.");
            return clashes;
        }

        /// <summary>
        /// Squares painted across somebody else's building. A building's own squares sit right
        /// against it on purpose, so only other buildings count.
        ///
        /// Measured on the fully-built location, because that is when the most is standing.
        /// </summary>
        private static int SquaresOnSolids(List<Entry> entries, List<Solid> solids, string tag)
        {
            int clashes = 0;

            foreach (var square in entries)
            {
                if (!square.ActiveWhenBuilt) continue;

                foreach (var solid in solids)
                {
                    if (solid.Owner == square.Owner) continue;
                    if (!square.Card.Overlaps(solid.Area)) continue;

                    clashes++;
                    var shared = Intersection(square.Card, solid.Area);
                    Debug.LogWarning(
                        $"[Audit] SQUARE-ON-BUILDING {tag} {square.Name} x {solid.Name} " +
                        $"by {shared.width:0.00} x {shared.height:0.00} m");
                }
            }

            Debug.Log($"[Audit] {tag} squares on buildings: {clashes} overlapping pairs.");
            return clashes;
        }

        // ---------------------------------------------------------------- arrival and paths

        /// <summary>
        /// Where the player lands must be inside the location, clear of every square (landing
        /// inside the travel square that brought them would send them straight back) and
        /// clear of every wall and fitting.
        /// </summary>
        private static int SpawnClear(Location location, List<Entry> entries, List<Solid> solids, string tag)
        {
            if (location.spawnPoint == null)
            {
                Debug.LogWarning($"[Audit] SPAWN {tag} has no spawn point.");
                return 1;
            }

            var root = location.transform;
            Vector3 at3 = root.InverseTransformPoint(location.spawnPoint.position);
            var at = new Vector2(at3.x, at3.z);
            const float margin = 0.6f;   // the player's radius and a little more

            int faults = 0;

            foreach (var e in entries)
            {
                if (!Grow(e.Card, margin).Contains(at)) continue;
                faults++;
                Debug.LogWarning($"[Audit] SPAWN {tag} the arrival point is inside {e.Name}.");
            }

            foreach (var s in solids)
            {
                if (!Grow(s.Area, margin).Contains(at)) continue;
                faults++;
                Debug.LogWarning($"[Audit] SPAWN {tag} the arrival point is inside {s.Name}.");
            }

            var walls = root.Find("Boundary");
            if (walls != null)
            {
                Rect inside = InnerBoundary(walls);
                if (!inside.Contains(at))
                {
                    faults++;
                    Debug.LogWarning($"[Audit] SPAWN {tag} the arrival point is outside the boundary.");
                }
            }

            Debug.Log($"[Audit] {tag} arrival point: {faults} faults.");
            return faults;
        }

        /// <summary>
        /// Shoppers steer in straight lines and cannot see walls, so every leg they can walk -
        /// in, between the shelves, to the queue, out - must stay clear of anything solid.
        /// Only supermarket queues (the ones with shelves) are checked; a farm customer walks
        /// the road.
        /// </summary>
        private static int ShopperPaths(Transform root, List<Solid> solids, string tag)
        {
            int faults = 0;

            foreach (var queue in root.GetComponentsInChildren<Tycoon.Customers.CustomerQueue>(true))
            {
                if (!queue.IsBrowsing) continue;

                // Every till a shopper can end up at: this one and its alternates.
                var tills = new List<Tycoon.Customers.CustomerQueue> { queue };
                if (queue.alternates != null)
                    foreach (var other in queue.alternates)
                        if (other != null) tills.Add(other);

                var entry = new List<Vector2> { Flat(root, queue.spawnPoint) };
                foreach (var t in queue.entryRoute) entry.Add(Flat(root, t));

                var legs = new List<Leg>();
                for (int i = 0; i + 1 < entry.Count; i++) legs.Add(new Leg(entry[i], entry[i + 1], "in"));
                Vector2 inside = entry[entry.Count - 1];

                // Every till's way out: from each place in its queue down its own queue exit,
                // then the shared route to the exit.
                foreach (var till in tills)
                {
                    var leave = new List<Vector2>();
                    foreach (var t in till.exitRoute) leave.Add(Flat(root, t));
                    leave.Add(Flat(root, till.exitPoint));
                    for (int i = 0; i + 1 < leave.Count; i++) legs.Add(new Leg(leave[i], leave[i + 1], "out"));

                    var fromQueue = new List<Vector2>();
                    if (till.queueExit != null)
                        foreach (var t in till.queueExit) if (t != null) fromQueue.Add(Flat(root, t));
                    fromQueue.Add(leave[0]);
                    for (int i = 0; i + 1 < fromQueue.Count; i++)
                        legs.Add(new Leg(fromQueue[i], fromQueue[i + 1], "queue exit"));

                    foreach (var slot in till.slots)
                        legs.Add(new Leg(Flat(root, slot), fromQueue[0], "queue to door"));

                    // A till's approach is only ever walked to that till's own queue.
                    var tillApproach = Approach(root, till);
                    for (int i = 0; i + 1 < tillApproach.Count; i++)
                        legs.Add(new Leg(tillApproach[i], tillApproach[i + 1], "queue approach"));
                }

                Vector2 firstLeave = Flat(root, queue.exitRoute[0]);

                // The walk through the shop, shelf by shelf, exactly as a shopper takes it:
                // StoreShelf.Connect is the same call the shopper makes, so a waypoint set that
                // fails here fails in the shop and vice versa.
                foreach (var a in queue.shelves)
                {
                    if (a == null || a.standPoint == null) continue;
                    Vector2 standA = Flat(root, a.standPoint);

                    // Door to this shelf.
                    var trail = new List<Transform>();
                    AddWalk(root, legs, inside, trail, a.via, standA, "door to shelf");

                    // This shelf to every other.
                    foreach (var b in queue.shelves)
                    {
                        if (b == null || b == a || b.standPoint == null) continue;
                        var from = new List<Transform>(a.via ?? new Transform[0]);
                        AddWalk(root, legs, standA, from, b.via, Flat(root, b.standPoint), "shelf to shelf");
                    }

                    // This shelf, then out of the shop empty-handed.
                    var toDoor = new List<Transform>(a.via ?? new Transform[0]);
                    AddWalk(root, legs, standA, toDoor, null, firstLeave, "shelf to door (leaving empty-handed)");

                    // This shelf, then to a place in whichever queue they join, by that till's
                    // own approach.
                    foreach (var till in tills)
                    {
                        var approach = Approach(root, till);
                        foreach (var slot in till.slots)
                        {
                            var unwind = new List<Transform>(a.via ?? new Transform[0]);
                            Vector2 first = approach.Count > 0 ? approach[0] : Flat(root, slot);
                            AddWalk(root, legs, standA, unwind, null, first, "shelf to queue");
                            if (approach.Count > 0)
                                legs.Add(new Leg(approach[approach.Count - 1], Flat(root, slot), "approach to queue"));
                        }
                    }
                }

                foreach (var leg in legs)
                {
                    foreach (var solid in solids)
                    {
                        if (!SegmentHits(leg.A, leg.B, solid.Area)) continue;
                        faults++;
                        Debug.LogWarning($"[Audit] SHOPPER {tag} walking {leg.Label} " +
                                         $"({leg.A.x:0.0},{leg.A.y:0.0}) to ({leg.B.x:0.0},{leg.B.y:0.0}) " +
                                         $"goes through {solid.Name}.");
                    }
                }
            }

            Debug.Log($"[Audit] {tag} shopper paths: {faults} faults.");
            return faults;
        }

        /// <summary>
        /// Every walk a customer of a queue WITH CHAIRS (the salon) can take, measured the way they take it:
        /// in from the road along the entry route to each waiting place, up the waiting room place by place,
        /// from the head place along each chair's approach to its seat, from each seat along the chair's way
        /// out and the queue's exit route to the exit point, and from any waiting place out when patience runs
        /// out. Customers steer in straight lines and cannot see walls, so none of it may pass through anything
        /// solid. A leg is excused only the solid it sits down in: a waiting place or a seat may stand inside its
        /// own chair's footprint (a solid that contains the seat itself).
        ///
        /// Also: no seat may be on an interaction square that is live once the place is built - a customer
        /// sitting there for a whole haircut covers the square's card, which is how the player reads it.
        ///
        /// Queues without chairs - every till before the salon - are not looked at, so this adds nothing to
        /// the farm, the market or the florist.
        /// </summary>
        private static int ChairPaths(Transform root, List<Solid> solids, List<Entry> entries, string tag)
        {
            int faults = 0;
            void Fault(string what)
            {
                faults++;
                Debug.LogWarning($"[Audit] CHAIRS {tag} {what}");
            }

            foreach (var queue in root.GetComponentsInChildren<Tycoon.Customers.CustomerQueue>(true))
            {
                if (queue.chairs == null || queue.chairs.Length == 0) continue;
                if (queue.spawnPoint == null || queue.exitPoint == null || queue.slots == null || queue.slots.Length == 0)
                {
                    Fault($"{queue.name} has chairs but no spawn point, exit point or waiting places.");
                    continue;
                }

                var seats = new List<Vector2>();
                var legs = new List<Leg>();

                Vector2 at = Flat(root, queue.spawnPoint);
                if (queue.entryRoute != null)
                    foreach (var t in queue.entryRoute) if (t != null) { var p = Flat(root, t); legs.Add(new Leg(at, p, "in")); at = p; }
                foreach (var slot in queue.slots)
                {
                    if (slot == null) continue;
                    legs.Add(new Leg(at, Flat(root, slot), "in to a waiting place"));
                    if (queue.seatedWaiting) seats.Add(Flat(root, slot));
                }
                for (int i = 0; i + 1 < queue.slots.Length; i++)
                    if (queue.slots[i] != null && queue.slots[i + 1] != null)
                        legs.Add(new Leg(Flat(root, queue.slots[i + 1]), Flat(root, queue.slots[i]), "up the waiting room"));

                var shopExit = new List<Vector2>();
                if (queue.exitRoute != null)
                    foreach (var t in queue.exitRoute) if (t != null) shopExit.Add(Flat(root, t));
                shopExit.Add(Flat(root, queue.exitPoint));
                for (int i = 0; i + 1 < shopExit.Count; i++) legs.Add(new Leg(shopExit[i], shopExit[i + 1], "out"));

                // Out of patience in the waiting room: the queue's own exit, then the shop's way out.
                var fromQueue = new List<Vector2>();
                if (queue.queueExit != null)
                    foreach (var t in queue.queueExit) if (t != null) fromQueue.Add(Flat(root, t));
                fromQueue.Add(shopExit[0]);
                for (int i = 0; i + 1 < fromQueue.Count; i++) legs.Add(new Leg(fromQueue[i], fromQueue[i + 1], "queue exit"));
                foreach (var slot in queue.slots)
                    if (slot != null) legs.Add(new Leg(Flat(root, slot), fromQueue[0], "waiting place to the way out (gave up)"));

                Vector2 head = Flat(root, queue.slots[0]);
                foreach (var chair in queue.chairs)
                {
                    if (chair == null) { Fault($"{queue.name} lists an empty chair."); continue; }
                    Vector2 seat = Flat(root, chair.Seat);
                    seats.Add(seat);

                    Vector2 from = head;
                    if (chair.approach != null)
                        foreach (var t in chair.approach) if (t != null) { var p = Flat(root, t); legs.Add(new Leg(from, p, $"to {chair.name}")); from = p; }
                    legs.Add(new Leg(from, seat, $"to {chair.name}"));

                    from = seat;
                    if (chair.exitRoute != null)
                        foreach (var t in chair.exitRoute) if (t != null) { var p = Flat(root, t); legs.Add(new Leg(from, p, $"out of {chair.name}")); from = p; }
                    legs.Add(new Leg(from, shopExit[0], $"out of {chair.name}"));
                }

                foreach (var leg in legs)
                    foreach (var solid in solids)
                    {
                        if (solid.Area.Contains(leg.A) || solid.Area.Contains(leg.B))
                        {
                            // Sitting down in (or getting up out of) a chair is not walking through it.
                            bool own = false;
                            foreach (var s in seats) if (solid.Area.Contains(s) && (s == leg.A || s == leg.B)) own = true;
                            if (own) continue;
                        }
                        if (!SegmentHits(leg.A, leg.B, Grow(solid.Area, 0.25f))) continue;
                        Fault($"a customer walking {leg.Label} ({leg.A.x:0.0},{leg.A.y:0.0}) to ({leg.B.x:0.0},{leg.B.y:0.0}) " +
                              $"goes through {solid.Name}.");
                    }

                foreach (var seat in seats)
                    foreach (var e in entries)
                        if (e.ActiveWhenBuilt && e.Card.Contains(seat))
                            Fault($"a seat at ({seat.x:0.0},{seat.y:0.0}) is on {e.Name}.");
            }

            Debug.Log($"[Audit] {tag} chair paths: {faults} faults.");
            return faults;
        }

        // ---------------------------------------------------------------- the butcher's chain

        /// <summary>
        /// The butcher as a whole, which the generic checks cannot see: that the goods can actually go from
        /// the pigs to a paying shopper, that the purchases come in the order the design wants, and that
        /// every walk and drive it relies on is clear.
        ///
        /// Market only (it needs a cutting table to start from). It checks, in order: the chain is wired
        /// (pen -> van -> drop pile -> cutting table -> counter -> shoppers, and the butcher worker's two
        /// ends); the pig pen really has no repair or buy square and no wear; the purchases are live in
        /// the intended order (extension, pig farm, table, steak counter, pigs and corn field E2, butcher,
        /// sausages); the pig farm is not something the supermarket asks to see owned; the shoppers' way in
        /// and out is clear of walls and of every square; and the van's route stays on the road and clear
        /// of anything solid in either location.
        /// </summary>
        private static int ButcherChain(Transform root, List<Solid> solids, List<Entry> entries,
            Dictionary<StationBase, int> firstLive, string tag)
        {
            // The cutting table (1 meat -> 1 steak) and the grinder (1 meat -> 3 sausages).
            CutStation cut = null, grinder = null;
            foreach (var bench in root.GetComponentsInChildren<CutStation>(true))
            {
                if (bench.makes > 1) grinder = bench;
                else cut = bench;
            }
            if (cut == null) return 0;

            int faults = 0;
            void Fault(string what)
            {
                faults++;
                Debug.LogWarning($"[Audit] BUTCHER {tag} {what}");
            }

            // ---- the counter and what is cut for it --------------------------------------
            RegisterStation counter = null;
            foreach (var r in root.GetComponentsInChildren<RegisterStation>(true))
                if (r.HasDisplay) counter = r;
            if (counter == null) { Fault("there is no display counter"); return faults; }

            if (cut.meat == null || cut.meat.item == null) Fault("the cutting table has no drop pile to cut from.");
            if (grinder == null) Fault("there is no sausage grinder.");

            foreach (var bench in new[] { cut, grinder })
            {
                if (bench == null) continue;
                if (bench.output == null || bench.outputCase == null)
                {
                    Fault($"{bench.name} does not know what it makes or which case it is for.");
                    continue;
                }

                var product = bench.output;
                if (bench.meat != cut.meat) Fault($"{bench.name} cuts from a different pile than the table.");
                if (bench.makes != (product.id == "sausage" ? 3 : 1))
                    Fault($"{bench.name} makes {bench.makes} {product.displayName} from a joint.");

                bool cased = false;
                foreach (var shelf in counter.stock) if (shelf == bench.outputCase && shelf.item == product) cased = true;
                if (!cased) Fault($"{bench.name} makes {product.displayName} but the counter has no case for it.");
                if (!counter.CanFulfill(product)) Fault($"shoppers may not ask for {product.displayName}, which the counter stocks.");

                bool offered = false;
                foreach (var offer in root.GetComponentsInChildren<ProductOffer>(true))
                    if (offer.item == product) offered = true;
                if (!offered) Fault($"nothing advertises {product.displayName}, so shoppers would never ask for it.");
            }

            // The sausage line: the grinder and the sausage case are both brought by ONE purchase, and are
            // saved off until then.
            if (grinder != null && grinder.outputCase != null)
            {
                var benchRoot = grinder.transform.parent != null ? grinder.transform.parent.gameObject : null;
                GameObject line = null;
                foreach (var gate in root.GetComponentsInChildren<UnlockStation>(true))
                {
                    if (gate.revealOnUnlock == null || benchRoot == null) continue;
                    if (System.Array.IndexOf(gate.revealOnUnlock, benchRoot) < 0) continue;
                    foreach (var go in gate.revealOnUnlock)
                        if (go != null && grinder.outputCase.transform.IsChildOf(go.transform)) line = go;
                }

                if (benchRoot != null && benchRoot.activeSelf) Fault("the grinder must be saved inactive.");
                if (line == null) Fault("no single purchase brings both the grinder and the sausage case.");
                else if (line.activeSelf) Fault("the sausage line must be saved inactive.");
            }

            var queue = counter.queue;
            if (queue == null || queue.register != counter) Fault("the counter and its shopper queue do not point at each other.");

            // ---- the butcher worker -------------------------------------------------------
            Tycoon.Upkeep.WorkerAgent butcher = null;
            foreach (var worker in root.GetComponentsInChildren<Tycoon.Upkeep.WorkerAgent>(true))
                if (worker.pickup is CutStation) butcher = worker;
            if (butcher == null) Fault("no worker cuts at the table.");
            else
            {
                if (butcher.dropoff != counter) Fault("the butcher does not take what it cuts to the counter.");
                if (!butcher.deliverPartialLoads) Fault("the butcher would wait at an empty table for a full load.");

                // Both benches, each to the counter; the first route is also pickup/dropoff.
                bool hasCut = false, hasGrind = false;
                if (butcher.routes != null)
                    foreach (var route in butcher.routes)
                    {
                        if (route == null || route.dropoff != counter) continue;
                        if (route.pickup == cut) hasCut = true;
                        if (route.pickup == grinder && grinder != null) hasGrind = true;
                    }
                if (!hasCut || !hasGrind) Fault("the butcher does not run both the cutting table and the grinder to the counter.");
                if (butcher.routes == null || butcher.routes.Length == 0 ||
                    butcher.routes[0].pickup != butcher.pickup || butcher.routes[0].dropoff != butcher.dropoff)
                    Fault("the butcher's pickup/dropoff is not its first route.");
            }

            // ---- the pig farm side ---------------------------------------------------------
            var van = Object.FindFirstObjectByType<MeatVan>(FindObjectsInactive.Include);
            if (van == null) { Fault("there is no delivery van."); return faults; }

            if (van.destination != cut.meat) Fault("the van unloads somewhere the cutting table does not cut from.");
            if (van.route == null || van.route.Length < 2) Fault("the van has no route.");

            ProducerMachine pen = null;
            foreach (var machine in Object.FindObjectsByType<ProducerMachine>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (machine.output == van.source) pen = machine;
            if (pen == null) Fault("the van collects from something no pig pen makes.");
            else
            {
                if (pen.output.item != cut.meat.item) Fault($"the pen makes {pen.output.item.displayName}, the table cuts {cut.meat.item.displayName}.");
                if (pen.input == null || pen.input.item == null || pen.input.item.id != "corn") Fault("the pigs are not fed corn.");
                if (pen.durability != null || pen.GetComponentInChildren<RepairStation>(true) != null ||
                    pen.GetComponentInChildren<UpgradeStation>(true) != null)
                    Fault("the pig pen has wear, a repair square or a buy square; it should have none.");

                var pigBuy = root.GetComponentInChildren<UpgradeStation>(true);
                if (pigBuy == null || pigBuy.target != pen) Fault("pigs cannot be bought for the pen.");
                else if (pigBuy.tierPrices == null || pigBuy.tierPrices.Length != pen.maxUnits - pen.units)
                    Fault($"the pig purchase has {(pigBuy.tierPrices == null ? 0 : pigBuy.tierPrices.Length)} prices " +
                          $"for {pen.maxUnits - pen.units} pigs to buy.");

                // The pig hand: it feeds the pen from corn.
                Tycoon.Upkeep.WorkerAgent hand = null;
                foreach (var worker in Object.FindObjectsByType<Tycoon.Upkeep.WorkerAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (worker.dropoff is DepositStation feed && feed.target == pen.input) hand = worker;
                if (hand == null) Fault("nobody feeds the pig pen.");

                // The pig farm is not part of the farm the supermarket asks to see owned.
                var group = van.transform.parent;
                foreach (var watcher in Object.FindObjectsByType<RevealWhenAll>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    // The town gate is the one thing that SHOULD wait on the whole butcher, pig farm included.
                    if (watcher.unlocks == null || watcher.name == "TownUnlock" || watcher.name == "ButcherProgress") continue;
                    foreach (var gate in watcher.unlocks)
                    {
                        if (gate == null || gate.revealOnUnlock == null) continue;
                        foreach (var go in gate.revealOnUnlock)
                            if (go != null && group != null && (go.transform == group || go.transform.IsChildOf(group)))
                                Fault($"{watcher.name} waits on {gate.name}, which brings the pig farm: the supermarket must not ask for it.");
                    }
                }
            }

            // ---- the order the purchases come in ----------------------------------------
            var named = new Dictionary<string, StationBase>();
            foreach (var station in root.GetComponentsInChildren<StationBase>(true))
                if (!named.ContainsKey(station.name)) named[station.name] = station;

            int Round(string name)
            {
                if (named.TryGetValue(name, out var s) && firstLive.TryGetValue(s, out int r)) return r;
                Fault($"{name} is never live.");
                return -1;
            }

            int extension = Round("UnlockButcher"), pigFarm = Round("UnlockPigFarm"), table = Round("UnlockTable"),
                steaks = Round("UnlockSteaks"), pigs = Round("BuyPig"), field = Round("UnlockCornE2"),
                hire = Round("Hirebutcher"), sausages = Round("UnlockSausages");

            void Order(string earlier, int a, string later, int b)
            {
                if (a >= 0 && b >= 0 && a >= b)
                    Fault($"{later} (stage {b}) is on offer no later than {earlier} (stage {a}); one step at a time.");
            }

            Order("the extension", extension, "the pig farm", pigFarm);
            Order("the pig farm", pigFarm, "the cutting table", table);
            Order("the cutting table", table, "the steak counter", steaks);
            Order("the steak counter", steaks, "pigs", pigs);
            Order("the steak counter", steaks, "corn field E2", field);
            Order("pigs", pigs, "the butcher", hire);
            Order("corn field E2", field, "the butcher", hire);
            Order("the butcher", hire, "sausages", sausages);

            // ---- shoppers: the way in, the queue, the way out ------------------------------
            if (queue != null && queue.spawnPoint != null && queue.slots != null && queue.slots.Length > 0)
            {
                var legs = new List<Leg>();
                Vector2 at = Flat(root, queue.spawnPoint);
                if (queue.entryRoute != null)
                    foreach (var t in queue.entryRoute) { var p = Flat(root, t); legs.Add(new Leg(at, p, "in")); at = p; }
                legs.Add(new Leg(at, Flat(root, queue.slots[0]), "in to the counter"));

                foreach (var slot in queue.slots)
                {
                    Vector2 from = Flat(root, slot);
                    if (queue.exitRoute != null)
                        foreach (var t in queue.exitRoute) { var p = Flat(root, t); legs.Add(new Leg(from, p, "out")); from = p; }
                    legs.Add(new Leg(from, Flat(root, queue.exitPoint), "out to the street"));
                }

                foreach (var leg in legs)
                    foreach (var solid in solids)
                    {
                        if (!SegmentHits(leg.A, leg.B, Grow(solid.Area, 0.25f))) continue;
                        Fault($"a shopper walking {leg.Label} ({leg.A.x:0.0},{leg.A.y:0.0}) to ({leg.B.x:0.0},{leg.B.y:0.0}) " +
                              $"goes through {solid.Name}.");
                    }

                // And the lane they queue in is not somebody's square, or the player would pay for a
                // purchase by standing among the shoppers. The extension's own gate is on the lane by
                // design and gone once the extension is bought, so only squares that stay are counted.
                var lane = new List<Vector2>();
                foreach (var slot in queue.slots) lane.Add(Flat(root, slot));
                if (queue.entryRoute != null) foreach (var t in queue.entryRoute) lane.Add(Flat(root, t));
                foreach (var e in entries)
                {
                    if (!e.ActiveWhenBuilt || e.Name.Contains("UnlockButcher")) continue;
                    foreach (var p in lane)
                        if (e.Card.Contains(p))
                        {
                            Fault($"the shoppers' queue passes through {e.Name}.");
                            break;
                        }
                }
            }

            // ---- the van's drive --------------------------------------------------------
            faults += VanRoute(van, tag);

            Debug.Log($"[Audit] {tag} butcher chain: {faults} faults.");
            return faults;
        }

        // ---------------------------------------------------------------- letting a business go

        /// <summary>
        /// The farm is let go when the supermarket is bought and the supermarket when the town is: every till
        /// with a cash pile there pays the account at a share (never nothing, never more than a sale) once
        /// that purchase is owned, and the purchase is a real gate. A till with no pile already pays the
        /// account in full and is left alone. The farm's share is the one the supermarket's copies.
        /// </summary>
        private static int LetGoRules(Location location, string tag)
        {
            if (location.locationId != "farm" && location.locationId != "market") return 0;

            int faults = 0;
            var root = location.transform;
            string gateName = location.locationId == "farm" ? "UnlockMarket" : "UnlockTown";
            void Check(string name, CashPile pile, UnlockStation gate, float share)
            {
                if (pile == null) return;
                if (gate == null)
                {
                    faults++;
                    Debug.LogWarning($"[Audit] LET-GO {tag} {name} has a cash pile but is never let go ({gateName} does not switch it).");
                    return;
                }
                if (share <= 0f || share > 1f)
                {
                    faults++;
                    Debug.LogWarning($"[Audit] LET-GO {tag} {name} pays the account {share:0.00} of a sale once let go.");
                }
            }

            if (location.locationId == "market")
            {
                foreach (var till in root.GetComponentsInChildren<CheckoutStation>(true))
                    Check(till.name, till.pile, till.accountWhenUnlocked, till.accountShare);
                foreach (var register in root.GetComponentsInChildren<RegisterStation>(true))
                    Check(register.name, register.pile, register.accountWhenUnlocked, register.accountShare);
            }
            else
            {
                foreach (var register in root.GetComponentsInChildren<RegisterStation>(true))
                    Check(register.name, register.pile, register.accountWhenUnlocked, register.accountShare);
            }

            Debug.Log($"[Audit] {tag} let-go rules: {faults} faults.");
            return faults;
        }

        // ---------------------------------------------------------------- the florist

        /// <summary>
        /// The florist as a whole (town only; it starts from the BouquetCounter): that the derelict shop can
        /// be renovated in order, that the flowers can go from the beds to a paying customer, that every bed
        /// can be planted, watered and harvested (and by the gardener), that the purchases come one at a time
        /// in the intended order, and that every walk it relies on is clear.
        ///
        /// Checks, in order: the renovation (every litter heap has a pile and a square, REPAIR brings the
        /// counter and stands back and removes the wrecks, PAINT is the florist's own menu without the
        /// two-band wall, OPEN brings in the shop, the garden, the well and the seed box, and the steps are
        /// live in the order litter, repair and paint, open); every flower the counter sells has a stock case,
        /// is advertised (ProductOffer) and has two beds that fill that very stock; every bed has its one
        /// square, six looks, a seed it takes from a seed box that supplies it and water it takes from a well
        /// that supplies it, and those squares are live only once the shop is open; the gardener has three
        /// routes per bed (water, seed, harvest) and carries one thing at a time; the purchases are live in
        /// the order Tulips, Hydrangeas, Sunflowers, the florist hire, the gardener hire; customers' way in
        /// (west of the central stand), along the queue and out (east of it) clears every wall, stand and
        /// the counter by 0.25 m, and the lane they queue in is nobody's square; and the player's walk from
        /// the street round the counter, through the back door to the well, the seed box and every bed (and
        /// to the bin by the street door) is clear of walls, stands, the fence and the well by 0.3 m.
        /// </summary>
        private static int FloristChain(Transform root, List<Solid> solids, List<Entry> entries,
            Dictionary<StationBase, int> firstLive, string tag)
        {
            // The florist's own building and meadow: the salon beside it has a repair, a paint and an open of its own.
            Transform scope = root.Find("Florist") != null ? root.Find("Florist") : root;
            BouquetCounter counter = null;
            foreach (var c in scope.GetComponentsInChildren<BouquetCounter>(true)) counter = c;
            if (counter == null) return 0;

            int faults = 0;
            void Fault(string what)
            {
                faults++;
                Debug.LogWarning($"[Audit] FLORIST {tag} {what}");
            }

            var beds = scope.GetComponentsInChildren<FlowerBed>(true);
            var offers = scope.GetComponentsInChildren<ProductOffer>(true);

            int LiveAt(StationBase st) => st != null && firstLive.TryGetValue(st, out int r) ? r : -1;

            // ---- the renovation ----------------------------------------------------------------------
            var named = new Dictionary<string, StationBase>();
            foreach (var station in scope.GetComponentsInChildren<StationBase>(true))
                if (!named.ContainsKey(station.name)) named[station.name] = station;

            var piles = new List<CollectStation>();
            foreach (var take in scope.GetComponentsInChildren<CollectStation>(true))
                if (IsRubbishSquare(take)) piles.Add(take);
            if (piles.Count == 0) Fault("there is no litter to clear: the shop does not start derelict.");
            foreach (var take in piles)
            {
                if (take.workerCompatible) Fault($"{take.name} may be used by a worker; the litter is the player's to carry out.");
                if (take.source == null || take.source.item == null || take.source.item.basePrice > 0d)
                    Fault($"{take.name} does not hold worthless rubbish.");
            }
            bool hasBin = false;
            foreach (var bin in scope.GetComponentsInChildren<DiscardStation>(true)) hasBin |= LiveAt(bin) == 0;
            if (!hasBin) Fault("there is no bin standing from the start for the litter.");

            UnlockStation repair = named.TryGetValue("UnlockRepair", out var rs) ? rs as UnlockStation : null;
            PaintStation paint = named.TryGetValue("Paint", out var ps) ? ps as PaintStation : null;
            ChoreStation open = named.TryGetValue("Open", out var os) ? os as ChoreStation : null;
            if (repair == null || paint == null || open == null) Fault("the renovation steps (repair, paint, open) are missing.");
            else
            {
                if (repair.revealOnUnlock == null || repair.revealOnUnlock.Length == 0 ||
                    repair.hideOnUnlock == null || repair.hideOnUnlock.Length == 0)
                    Fault("repair does not bring the fittings back and take the wrecks away.");
                if (paint.decor == null) Fault("the paint square has no decor.");
                else
                {
                    var d = paint.decor;
                    if (d.surfaces == null || d.surfaces.Length == 0) Fault("the decor has nothing to paint.");
                    else
                        foreach (var surface in d.surfaces)
                            if (surface == null || surface.renderer == null) { Fault("the decor has a surface with no renderer."); break; }
                    if (d.grubby == null || d.painted == null || d.grubby.Length == 0 || d.painted.Length == 0)
                        Fault("the decor has no grubby and painted coats.");
                    if (d.openStep != open) Fault("the decor does not know the OPEN step.");
                    if (d.patterns == null || d.patterns.Length == 0)
                        Fault("the florist decor has no pattern list of its own (it would offer the supermarket's).");
                    else
                    {
                        foreach (int pattern in d.patterns)
                            if (pattern < 0 || pattern >= Tycoon.UI.PatternFactory.PatternCount || Tycoon.UI.PatternFactory.IsBanded(pattern))
                                Fault($"the florist offers pattern {pattern}, which does not exist or is a whole-wall pattern (its walls are 0.7 m).");
                        if (d.patterns.Length > 10) Fault("the florist lists more patterns than the paint menu has room for.");
                    }
                }

                bool Reveals(Component target) =>
                    open.revealOnDone != null && System.Array.Exists(open.revealOnDone, go => go != null && target.transform.IsChildOf(go.transform));
                if (!Reveals(counter)) Fault("OPEN does not bring in the counter and its queue.");
                foreach (var bed in beds)
                    if (bed.Flower != null && bed.Flower.id == "rose" && !Reveals(bed)) Fault("OPEN does not bring in the rose beds.");
                foreach (var name in new[] { "FetchWater", "FetchSeeds" })
                    if (!named.TryGetValue(name, out var supply) || !Reveals(supply)) Fault($"OPEN does not bring in {name}.");

                // Litter, then repair and paint together, then open.
                int trash = -1;
                foreach (var take in piles) trash = Mathf.Max(trash, LiveAt(take));
                int r = LiveAt(repair), pn = LiveAt(paint), op = LiveAt(open);
                if (trash < 0 || r < 0 || pn < 0 || op < 0) Fault("a renovation step is never live.");
                else
                {
                    if (r <= trash || pn <= trash) Fault($"repair (stage {r}) or paint (stage {pn}) is on offer before the litter is gone (stage {trash}).");
                    if (op <= Mathf.Max(r, pn)) Fault($"OPEN (stage {op}) is on offer before repair (stage {r}) and paint (stage {pn}).");
                }
            }

            // ---- flowers: bed -> stock -> counter -> customer -------------------------------------
            if (counter.queue == null || counter.queue.register != counter || !counter.queue.bouquets)
                Fault("the counter and its bouquet queue do not point at each other.");
            if (counter.sells == null || counter.sells.Length != 4) Fault("the counter does not sell the four flowers.");
            else
                foreach (var flower in counter.sells)
                {
                    var stock = counter.stock != null ? System.Array.Find(counter.stock, b => b != null && b.item == flower) : null;
                    if (stock == null) { Fault($"no stock case for {flower.displayName}."); continue; }
                    if (System.Array.Find(offers, o => o.item == flower && o.transform == stock.transform) == null)
                        Fault($"nothing advertises {flower.displayName} with its stock, so customers would never ask for it.");
                    int feeding = 0;
                    foreach (var bed in beds) if (bed.stock == stock) feeding++;
                    if (feeding != 2) Fault($"{flower.displayName} has {feeding} beds filling its stock, not 2.");
                }

            // ---- the beds: plant, water, harvest -------------------------------------------------------
            int openRound = open != null ? LiveAt(open) : -1;
            foreach (var bed in beds)
            {
                if (bed.square == null || bed.square.bed != bed) { Fault($"{bed.name} has no square that points back at it."); continue; }
                if (bed.looks == null || bed.looks.Length != 6 || System.Array.Exists(bed.looks, l => l == null))
                    Fault($"{bed.name} does not have its six looks.");
                if (bed.bubble == null) Fault($"{bed.name} has no bubble.");
                if (bed.seed == null || bed.water == null || bed.stock == null) { Fault($"{bed.name} does not know its seed, water and stock."); continue; }
                if (bed.flowersPerHarvest > bed.stock.capacity)
                    Fault($"{bed.name} harvests {bed.flowersPerHarvest} flowers into a stock of {bed.stock.capacity}: it could never be picked.");

                var seedSource = bed.seedSquare != null ? bed.seedSquare.source : null;
                if (seedSource == null || seedSource.item != bed.seed || seedSource.GetComponent<SupplyFeed>() == null)
                    Fault($"{bed.name}'s seed box does not supply the seed it plants.");
                var wellSource = bed.wellSquare != null ? bed.wellSquare.source : null;
                if (wellSource == null || wellSource.item != bed.water || wellSource.GetComponent<SupplyFeed>() == null)
                    Fault($"{bed.name}'s well does not supply the water it drinks.");

                int at = LiveAt(bed.square);
                if (at < 0) Fault($"{bed.name}'s square is never live.");
                else if (openRound >= 0 && at <= openRound)
                    Fault($"{bed.name}'s square is live (stage {at}) before the shop is open (stage {openRound}).");
                foreach (var supply in new StationBase[] { bed.seedSquare, bed.wellSquare })
                    if (supply != null && openRound >= 0 && LiveAt(supply) >= 0 && LiveAt(supply) <= openRound)
                        Fault($"{supply.name} is live before the shop is open.");
            }

            Tycoon.Upkeep.WorkerAgent gardener = null, florist = null;
            foreach (var worker in scope.GetComponentsInChildren<Tycoon.Upkeep.WorkerAgent>(true))
            {
                if (worker.dropoff == counter) florist = worker;
                else if (worker.routes != null && worker.routes.Length > 0 && worker.routes[0].dropoff is BedStation) gardener = worker;
            }
            if (florist == null) Fault("no worker serves at the counter.");
            else if (florist.routes != null && florist.routes.Length > 0) Fault("the florist worker has routes: it should only serve the counter.");
            if (gardener == null) Fault("there is no gardener.");
            else
            {
                if (gardener.routes.Length != beds.Length * 3) Fault($"the gardener has {gardener.routes.Length} routes for {beds.Length} beds (three each).");
                foreach (var bed in beds)
                {
                    if (bed.square == null) continue;
                    if (System.Array.Find(gardener.routes, r => r.dropoff == bed.square && r.pickup == bed.wellSquare) == null)
                        Fault($"the gardener never waters {bed.name}.");
                    if (System.Array.Find(gardener.routes, r => r.dropoff == bed.square && r.pickup == bed.seedSquare) == null)
                        Fault($"the gardener never plants {bed.name}.");
                    if (System.Array.Find(gardener.routes, r => r.dropoff == bed.square && r.pickup == bed.square) == null)
                        Fault($"the gardener never harvests {bed.name}.");
                }
                if (gardener.GetComponent<Tycoon.Player.CarryStack>().capacity != 1)
                    Fault("the gardener must carry one seed or can at a time (a left-over would strand it).");
            }

            // ---- one purchase at a time ------------------------------------------------------------
            int Round(string name)
            {
                if (named.TryGetValue(name, out var st) && firstLive.TryGetValue(st, out int r)) return r;
                Fault($"{name} is never live.");
                return -1;
            }
            string[] order = { "Open", "UnlockTulips", "UnlockHydrangeas", "UnlockSunflowers", "Hireflorist", "Hiregardener" };
            int previous = -1;
            for (int i = 0; i < order.Length; i++)
            {
                int r = Round(order[i]);
                if (i > 0 && r >= 0 && previous >= 0 && r <= previous)
                    Fault($"{order[i]} (stage {r}) is on offer no later than {order[i - 1]} (stage {previous}); one step at a time.");
                previous = r;
            }

            // ---- customers: in west of the stand, along the counter, out east of it --------------------
            var queue = counter.queue;
            if (queue != null && queue.spawnPoint != null && queue.slots != null && queue.slots.Length > 0)
            {
                var legs = new List<Leg>();
                Vector2 at = Flat(root, queue.spawnPoint);
                if (queue.entryRoute != null)
                    foreach (var t in queue.entryRoute) { var p = Flat(root, t); legs.Add(new Leg(at, p, "in")); at = p; }
                foreach (var slot in queue.slots) legs.Add(new Leg(at, Flat(root, slot), "in to the queue"));
                for (int i = 0; i + 1 < queue.slots.Length; i++)
                    legs.Add(new Leg(Flat(root, queue.slots[i]), Flat(root, queue.slots[i + 1]), "along the queue"));
                // Only the customer at the front is served and leaves; the others advance to the front first (the
                // "along the queue" legs above), so the way out is measured from slot 0.
                {
                    Vector2 from = Flat(root, queue.slots[0]);
                    if (queue.exitRoute != null)
                        foreach (var t in queue.exitRoute) { var p = Flat(root, t); legs.Add(new Leg(from, p, "out")); from = p; }
                    legs.Add(new Leg(from, Flat(root, queue.exitPoint), "out to the street"));
                }

                // Order bubbles: the widest one (every line of a bouquet) of two queued customers must not overlap
                // at the game camera. The town's grid is turned with the camera, so x is screen-right and z is
                // screen-up foreshortened by sin(pitch 50).
                var bubble = queue.customerTemplate != null ? queue.customerTemplate.GetComponent<OrderBubble>() : null;
                if (bubble != null)
                {
                    Vector2 big = OrderBubble.CanvasSize(Mathf.Max(1, queue.maxBouquetLines)) * bubble.worldScale;
                    for (int i = 0; i < queue.slots.Length; i++)
                        for (int j = i + 1; j < queue.slots.Length; j++)
                        {
                            Vector2 a = Flat(root, queue.slots[i]), b = Flat(root, queue.slots[j]);
                            float dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y) * 0.766f;
                            if (dx < big.x && dy < big.y)
                                Fault($"the order bubbles of queue slots {i} and {j} overlap at the game camera: " +
                                      $"{dx:0.00} m across and {dy:0.00} m up the screen apart, a {queue.maxBouquetLines}-line bubble is {big.x:0.00} x {big.y:0.00} m.");
                        }
                }

                for (int i = 0; i + 1 < queue.slots.Length; i++)
                    legs.Add(new Leg(Flat(root, queue.slots[i]), Flat(root, queue.slots[i + 1]), "along the queue"));

                // Customers are not solid, but they are drawn: no walking through walls or the counter.
                foreach (var leg in legs)
                    foreach (var solid in solids)
                    {
                        if (!SegmentHits(leg.A, leg.B, Grow(solid.Area, 0.25f))) continue;
                        Fault($"a customer walking {leg.Label} ({leg.A.x:0.0},{leg.A.y:0.0}) to ({leg.B.x:0.0},{leg.B.y:0.0}) " +
                              $"goes through {solid.Name}.");
                    }

                // "Round the stand", as the sketch has it: the way in passes the central stand on its
                // west side and the way out on its east side, and both stay south of the counter.
                Rect stand = default;
                bool found = false;
                float front = Flat(root, queue.slots[0]).y;
                foreach (var solid in solids)
                    if (solid.Name.EndsWith("/Stall") && solid.Area.width > 1.9f && solid.Area.width < 2.2f &&
                        Mathf.Abs(solid.Area.width - solid.Area.height) < 0.05f && solid.Area.center.y < front &&
                        Mathf.Abs(solid.Area.center.x - Flat(root, queue.slots[0]).x) < 0.5f)
                    { stand = solid.Area; found = true; }
                if (!found) Fault("cannot find the central stand in front of the counter.");
                else
                {
                    bool westOk = false, eastOk = false;
                    foreach (var t in queue.entryRoute)
                    {
                        var p = Flat(root, t);
                        if (p.x < stand.xMin && p.y > stand.yMin - 0.5f) westOk = true;
                    }
                    foreach (var t in queue.exitRoute)
                    {
                        var p = Flat(root, t);
                        if (p.x > stand.xMax && p.y < stand.yMax + 1f && p.y > stand.yMin - 0.5f) eastOk = true;
                    }
                    if (!westOk) Fault("the way in does not go round the west side of the central stand.");
                    if (!eastOk) Fault("the way out does not go round the east side of the central stand.");
                }

                // Nobody pays for a purchase by standing in the queue.
                var lane = new List<Vector2>();
                foreach (var slot in queue.slots) lane.Add(Flat(root, slot));
                foreach (var t in queue.entryRoute) lane.Add(Flat(root, t));
                foreach (var t in queue.exitRoute) lane.Add(Flat(root, t));
                foreach (var e in entries)
                {
                    if (!e.ActiveWhenBuilt) continue;
                    foreach (var p in lane)
                        if (e.Card.Contains(p)) { Fault($"the customers' lane passes through {e.Name}."); break; }
                }
            }

            // ---- the player's walks -----------------------------------------------------------------
            Vector2 V(Component c) => Flat(root, c.transform);
            var walks = new List<Leg>();
            var street = new Vector2(-10f, TownBuilder.RoadZ);
            var inside = new Vector2(-10f, 2.2f);
            var westEnd = new Vector2(-10.2f, 4.9f);
            var behind = new Vector2(-9.6f, 7.0f);
            var door = new Vector2(-6.0f, 7.2f);
            var meadow = new Vector2(-6.0f, 9.0f);
            walks.Add(new Leg(street, inside, "street into the shop"));
            walks.Add(new Leg(inside, westEnd, "past the west end of the counter"));
            walks.Add(new Leg(westEnd, behind, "behind the counter"));
            walks.Add(new Leg(behind, V(counter), "to the serve square"));
            walks.Add(new Leg(behind, door, "to the back door"));
            walks.Add(new Leg(door, meadow, "through the back door"));
            if (beds.Length > 0 && beds[0].wellSquare != null) walks.Add(new Leg(meadow, V(beds[0].wellSquare), "to the well"));
            if (beds.Length > 0 && beds[0].seedSquare != null)
            {
                // Up the east side of the lane, past the well, then across to the seed box under the north fence.
                var seed = V(beds[0].seedSquare);
                walks.Add(new Leg(new Vector2(-5.65f, meadow.y), new Vector2(-5.65f, seed.y), "up the meadow to the seed box"));
                walks.Add(new Leg(new Vector2(-5.65f, seed.y), seed, "to the seed box"));
            }
            foreach (var bin in scope.GetComponentsInChildren<DiscardStation>(true))
            {
                if (LiveAt(bin) != 0) continue;
                // Along the pavement outside the south wall and up to the square, which lies south of the can.
                var pavement = new Vector2(-10f, -1.3f);
                var corner = new Vector2(-14.0f, -1.3f);
                walks.Add(new Leg(street, pavement, "from the street to the pavement"));
                walks.Add(new Leg(pavement, corner, "along the pavement to the bin"));
                walks.Add(new Leg(corner, V(bin), "up to the bin"));
                walks.Add(new Leg(pavement, new Vector2(-10f, 0.2f), "from the pavement through the door"));
            }
            foreach (var bed in beds)
            {
                if (bed.square == null) continue;
                var target = V(bed.square);
                // Up the open strip beside the well, then across along the bed's own row.
                float lane = target.x < meadow.x ? -8.35f : -5.65f;
                walks.Add(new Leg(meadow, new Vector2(lane, meadow.y), "into the meadow"));
                walks.Add(new Leg(new Vector2(lane, meadow.y), new Vector2(lane, target.y), "up the meadow"));
                walks.Add(new Leg(new Vector2(lane, target.y), target, $"to {bed.name}"));
            }
            foreach (var leg in walks)
                foreach (var solid in solids)
                {
                    // The beds are walked on (that is how they are watered); everything else is solid.
                    if (solid.Name.StartsWith("Bed_") || !SegmentHits(leg.A, leg.B, Grow(solid.Area, 0.3f))) continue;
                    Fault($"the player walking {leg.Label} ({leg.A.x:0.0},{leg.A.y:0.0}) to ({leg.B.x:0.0},{leg.B.y:0.0}) " +
                          $"is blocked by {solid.Name}.");
                }

            Debug.Log($"[Audit] {tag} florist chain: {faults} faults.");
            return faults;
        }

        /// <summary>
        /// The hairdresser as a whole (town only; it starts from the seated queue): that the customer system is wired the
        /// way ChairServiceStation expects (every chair station names the queue, its chair and the one cash pile; the
        /// queue's register is one of them; every haircut is on the menu and advertised), that hair and the broom close
        /// the loop (each chair's hair heap is its dirtyWhen, its SWEEP square takes the heap's bags, the bin it ends in
        /// may be used by the cleaner), that every hire can do its job (receptionist on NEXT, a hairdresser on his own
        /// chair's STYLE square, the cleaner from every heap to the bin), that the renovation runs litter, repair and
        /// paint, open, and that after OPEN the purchases are offered one at a time. The customers' walks are
        /// ChairPaths' (every queue with chairs).
        /// </summary>
        private static int SalonChain(Transform root, List<Entry> entries, Dictionary<StationBase, int> firstLive, string tag)
        {
            var salon = root.Find("Salon");
            if (salon == null) return 0;

            int faults = 0;
            void Fault(string what)
            {
                faults++;
                Debug.LogWarning($"[Audit] SALON {tag} {what}");
            }
            int LiveAt(StationBase st) => st != null && firstLive.TryGetValue(st, out int r) ? r : -1;

            var queue = salon.GetComponentInChildren<Tycoon.Customers.CustomerQueue>(true);
            var stations = salon.GetComponentsInChildren<SalonChairStation>(true);
            if (queue == null || stations.Length == 0) { Fault("there is no seated queue or no chair station."); return faults; }

            if (!queue.seatedWaiting) Fault("the queue is not a seated waiting room.");
            if (queue.chairs == null || queue.chairs.Length != stations.Length)
                Fault($"the queue lists {(queue.chairs == null ? 0 : queue.chairs.Length)} chairs for {stations.Length} chair stations.");
            if (queue.minOrder != 1 || queue.maxOrder != 1) Fault("a customer must want exactly one haircut (minOrder = maxOrder = 1).");
            if (queue.register == null || System.Array.IndexOf(stations, queue.register as SalonChairStation) < 0)
                Fault("the queue's register is not one of the chair stations (its menu would be empty).");

            Tycoon.Stations.CashPile pile = null;
            foreach (var st in stations)
            {
                if (st.queue != queue) Fault($"{st.name} (chair {st.chair?.name}) does not name the salon's queue: its haircuts would be free.");
                if (st.chair == null || System.Array.IndexOf(queue.chairs, st.chair) < 0) Fault($"{st.name}'s chair is not one of the queue's chairs.");
                if (st.pile == null) Fault($"{st.name} has no cash pile.");
                else if (pile != null && st.pile != pile) Fault("the chairs do not all pay into the one reception cash pile.");
                pile = st.pile;
                if (st.sells == null || st.sells.Length != 4) Fault($"{st.name} does not sell the four haircuts.");
                if (st.chair != null && (st.chair.seat == null || st.chair.dirtyWhen == null)) Fault($"{st.name}'s chair has no seat or no hair marker.");
            }

            // Every haircut is a service, on the menu, and advertised while the salon is open.
            var offers = salon.GetComponentsInChildren<ProductOffer>(true);
            var menu = stations[0].sells ?? new Tycoon.Config.ItemDefinition[0];
            foreach (var item in menu)
            {
                if (item == null) { Fault("the menu has an empty entry."); continue; }
                if (!item.service) Fault($"{item.displayName} is on the menu but is not a service (the order bubble would count it).");
                bool advertised = false;
                foreach (var o in offers) if (o.item == item && o.transform.IsChildOf(queue.transform)) advertised = true;
                if (!advertised) Fault($"nothing advertises {item.displayName} with the open shop, so customers would never ask for it.");
            }

            // The hair and the broom.
            foreach (var mess in salon.GetComponentsInChildren<HairMess>(true))
            {
                var buffer = mess.GetComponent<ItemBuffer>();
                var take = mess.square != null ? mess.square.GetComponent<CollectStation>() : null;
                if (mess.chair == null || mess.chair.dirtyWhen != mess.dirty) Fault($"{mess.name}: its marker is not its chair's dirtyWhen.");
                if (take == null || take.source != buffer) Fault($"{mess.name}: its SWEEP square does not take the heap's bags.");
                else if (!take.workerCompatible) Fault($"{mess.name}: the cleaner may not use the SWEEP square.");
                if (buffer == null || buffer.item == null || buffer.item.basePrice > 0d || buffer.capacity < mess.bagsPerService)
                    Fault($"{mess.name}: hair must be worthless bags and the heap must hold a haircut's worth.");
                if (mess.heap == null || mess.heap.Length == 0) Fault($"{mess.name} has no heap pictures.");
                if (mess.dirty != null && mess.dirty.activeSelf) Fault($"{mess.name}: the chair starts out dirty.");
            }
            var binSquare = salon.GetComponentInChildren<WorkerBinStation>(true);
            if (binSquare == null) Fault("there is no bin the cleaner may use.");
            else
            {
                // The whole salon appears at once (when the florist is complete): the bin must stand from then on.
                int litterAt = int.MaxValue;
                foreach (var take in salon.GetComponentsInChildren<CollectStation>(true))
                    if (IsRubbishSquare(take) && LiveAt(take) >= 0) litterAt = Mathf.Min(litterAt, LiveAt(take));
                if (LiveAt(binSquare) < 0 || LiveAt(binSquare) > litterAt) Fault("the hair bin is not there from the start (the litter goes into it).");
            }

            // The staff.
            var next = salon.GetComponentInChildren<CallNextStation>(true);
            if (next == null || next.queue != queue) Fault("the NEXT square does not call this queue.");
            bool receptionist = false, cleaner = false;
            int hairdressers = 0;
            foreach (var worker in salon.GetComponentsInChildren<Tycoon.Upkeep.WorkerAgent>(true))
            {
                if (worker.name == "receptionist")
                {
                    receptionist = true;
                    if (worker.pickup != next || worker.dropoff != next) Fault("the receptionist's route is not the NEXT square.");
                }
                else if (worker.name == "cleaner")
                {
                    cleaner = true;
                    if (worker.routes == null || worker.routes.Length != stations.Length) Fault("the cleaner does not have a route from every heap.");
                    else
                        foreach (var r in worker.routes)
                            if (r == null || !(r.pickup is CollectStation c) || !(c.source != null && c.source.GetComponent<HairMess>() != null) || r.dropoff != binSquare)
                                Fault("a cleaner route is not (a hair heap's SWEEP square) to (the hair bin).");
                    if (!worker.deliverPartialLoads) Fault("the cleaner would wait at an empty heap for a full load.");
                }
                else if (worker.name.StartsWith("hairdresser"))
                {
                    hairdressers++;
                    if (!(worker.pickup is SalonChairStation chairStation) || worker.pickup != worker.dropoff)
                        Fault($"{worker.name}'s route is not one chair's STYLE square.");
                }
            }
            if (!receptionist) Fault("there is no receptionist.");
            if (!cleaner) Fault("there is no cleaner.");
            if (hairdressers != stations.Length) Fault($"{hairdressers} hairdressers for {stations.Length} chairs.");

            // Renovation, then one purchase at a time: from OPEN on, never two of the salon's gates and hires on offer together
            // (the four hairdresser hires share one square on purpose).
            var open = salon.GetComponentInChildren<ChoreStation>(true);
            int openRound = LiveAt(open);
            if (open == null || openRound < 0) Fault("the OPEN step is never live.");
            else
            {
                var perRound = new Dictionary<int, int>();
                foreach (var u in salon.GetComponentsInChildren<UnlockStation>(true))
                {
                    int r = LiveAt(u);
                    if (r < 0) { Fault($"{u.name} is never on offer."); continue; }
                    if (u.name == "UnlockRepair") continue;
                    if (r <= openRound) Fault($"{u.name} is on offer (stage {r}) before the shop is open (stage {openRound}).");
                    perRound[r] = perRound.TryGetValue(r, out int n) ? n + 1 : 1;
                }
                foreach (var kv in perRound)
                    if (kv.Value > 1) Fault($"{kv.Value} purchases are on offer together at stage {kv.Key}; the salon sells them one at a time.");
            }

            Debug.Log($"[Audit] {tag} salon chain: {faults} faults.");
            return faults;
        }

        /// <summary>
        /// The van's route, in whichever location each leg is in: every stop on the tarmac (the bay and
        /// the door, where it parks, excepted), and no leg through a wall, a pen or a counter (grown by the
        /// van's half width and a little more). The van has no collider and cannot block anyone; this is
        /// about it looking like it drives, not like it cuts across a field.
        /// </summary>
        private static int VanRoute(MeatVan van, string tag)
        {
            if (van.route == null || van.route.Length < 2) return 0;

            int faults = 0;
            var locations = new List<Location>(Object.FindObjectsByType<Location>(
                FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (locations.Count == 0) return 0;
            locations.Sort((a, b) => string.CompareOrdinal(a.locationId, b.locationId));
            Transform frame = locations.Find(l => l.locationId == "farm")?.transform ?? locations[0].transform;

            // The tarmac, in the farm's grid. Both roads, the link between them and the driveway.
            var tarmac = new List<Rect>();
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string n = renderer.name;
                if (!(n == "Road" || n.StartsWith("RoadLink") || n.StartsWith("Driveway"))) continue;
                if (TryFootprint(frame, renderer.transform, out Rect r)) tarmac.Add(r);
            }

            for (int i = 0; i < van.route.Length; i++)
            {
                var stop = van.route[i];
                if (stop.point == null) { faults++; Debug.LogWarning($"[Audit] BUTCHER {tag} a van stop is missing."); continue; }

                // The two ends are where it parks, not where it drives.
                if (i == 0 || i == van.route.Length - 1) continue;

                Vector3 local = frame.InverseTransformPoint(stop.point.position);
                bool onRoad = false;
                foreach (var r in tarmac) if (r.Contains(new Vector2(local.x, local.z))) { onRoad = true; break; }
                if (!onRoad)
                {
                    faults++;
                    Debug.LogWarning($"[Audit] BUTCHER {tag} the van's stop {stop.point.name} " +
                                     $"({local.x:0.0},{local.z:0.0}) is not on a road.");
                }
            }

            foreach (var location in locations)
            {
                var solids = CollectSolids(location.transform);
                for (int i = 0; i + 1 < van.route.Length; i++)
                {
                    if (van.route[i].point == null || van.route[i + 1].point == null) continue;
                    Vector3 a = location.transform.InverseTransformPoint(van.route[i].point.position);
                    Vector3 b = location.transform.InverseTransformPoint(van.route[i + 1].point.position);

                    foreach (var solid in solids)
                    {
                        if (!SegmentHits(new Vector2(a.x, a.z), new Vector2(b.x, b.z), Grow(solid.Area, 0.9f))) continue;
                        faults++;
                        Debug.LogWarning($"[Audit] BUTCHER {tag} the van driving {van.route[i].point.name} to " +
                                         $"{van.route[i + 1].point.name} goes through {location.locationId}'s {solid.Name}.");
                    }
                }
            }

            return faults;
        }

        /// <summary>
        /// The butcher extension's shell (market only). It is saved inactive and bought with one
        /// gate, so the audit checks the three things that can go quietly wrong: the gate really
        /// hides the staff door's plug (otherwise the shop's back door is a wall for ever), and,
        /// with the plug gone, every opening the butcher needs is wide enough for a person to
        /// walk through with room to spare. Walls are grown by 0.35 m, a body's radius, and the
        /// walks are the ones the butcher will use: street to counter, staff door to back room,
        /// back room to the drop square, and the driveway to the van door.
        /// </summary>
        private static int ButcherShell(Transform root, List<Solid> solids, string tag)
        {
            UnlockStation gate = null;
            foreach (var g in root.GetComponentsInChildren<UnlockStation>(true))
            {
                if (g.revealOnUnlock == null) continue;
                foreach (var go in g.revealOnUnlock)
                    if (go != null && go.name == "Butcher") gate = g;
            }
            if (gate == null) return 0;

            int faults = 0;
            var shell = root.Find("Butcher");
            if (shell == null || shell.gameObject.activeSelf)
            {
                faults++;
                Debug.LogWarning($"[Audit] BUTCHER {tag} the shell must be saved inactive.");
            }

            // The plug: what the gate hides, measured as the solids it takes away.
            var plugAreas = new List<Rect>();
            if (gate.hideOnUnlock != null)
                foreach (var go in gate.hideOnUnlock)
                    if (go != null && go.name == "Wall" && TryFootprint(root, go.transform, out Rect r))
                        plugAreas.Add(r);

            if (plugAreas.Count == 0)
            {
                faults++;
                Debug.LogWarning($"[Audit] BUTCHER {tag} the extension gate hides no wall: the staff door is never opened.");
            }

            var open = new List<Rect>();
            foreach (var solid in solids)
            {
                bool plug = false;
                foreach (var area in plugAreas)
                    if (Mathf.Approximately(area.xMin, solid.Area.xMin) && Mathf.Approximately(area.yMin, solid.Area.yMin) &&
                        Mathf.Approximately(area.xMax, solid.Area.xMax) && Mathf.Approximately(area.yMax, solid.Area.yMax))
                        plug = true;
                if (!plug) open.Add(Grow(solid.Area, 0.35f));
            }

            float doorMid = (MarketBuilder.StaffDoorFrom + MarketBuilder.StaffDoorTo) * 0.5f;
            float gapMid = (MarketBuilder.BackGapFrom + MarketBuilder.BackGapTo) * 0.5f;
            float shopDoorMid = (MarketBuilder.ShopDoorFrom + MarketBuilder.ShopDoorTo) * 0.5f;
            float vanMid = (MarketBuilder.VanDoorFrom + MarketBuilder.VanDoorTo) * 0.5f;
            float stripZ = doorMid;   // the staff strip behind the counter, level with the door

            // The back room: in through the opening, along the entry row south of the first square, then up
            // the walkway along the west wall; every square is a short step east of it.
            float northX = MarketBuilder.BackGapFrom + 0.6f;
            float walkX = MarketBuilder.BackWalkX, entryZ = MarketBuilder.BackEntryZ;

            Vector2 V(Vector3 p) => new Vector2(p.x, p.z);
            var walks = new List<Leg>
            {
                // Street in through the customers' gap to the counter.
                new Leg(new Vector2(shopDoorMid, -8f), new Vector2(shopDoorMid, V(MarketBuilder.ButcherQueueFront).y), "street to counter"),
                // Market floor through the staff door, along the strip, up through the gap into the back room.
                new Leg(new Vector2(MarketBuilder.ShopWest + 1.5f, doorMid), new Vector2(northX, stripZ), "staff door to strip"),
                new Leg(new Vector2(northX, stripZ), new Vector2(northX, entryZ), "strip to back room"),
                new Leg(new Vector2(northX, entryZ), new Vector2(walkX, entryZ), "back room entry row"),
                new Leg(new Vector2(walkX, entryZ), new Vector2(walkX, V(MarketBuilder.ButcherDropSquare).y), "back room walkway"),
                new Leg(new Vector2(walkX, V(MarketBuilder.ButcherCutSquare).y), V(MarketBuilder.ButcherCutSquare), "walkway to the cutting table"),
                new Leg(new Vector2(walkX, V(MarketBuilder.ButcherGrindSquare).y), V(MarketBuilder.ButcherGrindSquare), "walkway to the grinder"),
                new Leg(new Vector2(walkX, V(MarketBuilder.ButcherDropSquare).y), V(MarketBuilder.ButcherDropSquare), "walkway to the drop square"),
                // The butcher's round: each bench to the counter and back.
                new Leg(V(MarketBuilder.ButcherCutSquare), new Vector2(northX, entryZ), "cutting table to the opening"),
                new Leg(V(MarketBuilder.ButcherGrindSquare), new Vector2(walkX, V(MarketBuilder.ButcherGrindSquare).y), "grinder to the walkway"),
                // The strip itself, end to end (cash pile, hire, counter service).
                new Leg(new Vector2(northX, stripZ), V(MarketBuilder.ButcherCashPile), "strip to the cash pile"),
                new Leg(new Vector2(northX, stripZ), V(MarketBuilder.ButcherHire), "strip to the butcher's hire square"),
                new Leg(new Vector2(northX, stripZ), V(MarketBuilder.ButcherCounterSquare), "strip to the counter"),
                // The customers' side: the door to the pig purchase and the second corn field's.
                new Leg(new Vector2(shopDoorMid, -6.5f), new Vector2(shopDoorMid, -2.5f), "street to the middle of the shop"),
                new Leg(new Vector2(shopDoorMid, -2.5f), new Vector2(-19.3f, -1.2f), "shop to the pig purchase"),
                new Leg(new Vector2(shopDoorMid, -2.5f), new Vector2(-19.3f, -3.5f), "shop to the corn field purchase"),
                // The van's lane to its door and the drop square.
                new Leg(new Vector2((MarketBuilder.LaneWest + MarketBuilder.LaneEast) * 0.5f, -8.5f),
                        new Vector2((MarketBuilder.LaneWest + MarketBuilder.LaneEast) * 0.5f, vanMid), "driveway up"),
                new Leg(new Vector2((MarketBuilder.LaneWest + MarketBuilder.LaneEast) * 0.5f, vanMid),
                        new Vector2(walkX, vanMid), "driveway through the van door"),
                new Leg(new Vector2(walkX, vanMid), V(MarketBuilder.ButcherDropSquare), "van door to the drop square"),
            };

            foreach (var leg in walks)
            {
                foreach (var wall in open)
                {
                    if (!SegmentHits(leg.A, leg.B, wall)) continue;
                    faults++;
                    Debug.LogWarning($"[Audit] BUTCHER {tag} walking {leg.Label} " +
                                     $"({leg.A.x:0.0},{leg.A.y:0.0}) to ({leg.B.x:0.0},{leg.B.y:0.0}) " +
                                     $"is blocked by a wall (plug removed).");
                    break;
                }
            }

            Debug.Log($"[Audit] {tag} butcher shell: {faults} faults.");
            return faults;
        }

        /// <summary>
        /// Adds the legs of one walk: from a point, through the aisle nodes a shopper would take
        /// to get from where <paramref name="trail"/> leaves them to the end of
        /// <paramref name="via"/>, then to the destination.
        /// </summary>
        private static void AddWalk(Transform root, List<Leg> legs, Vector2 from, List<Transform> trail,
            Transform[] via, Vector2 to, string label)
        {
            var nodes = new List<Vector3>();
            Tycoon.Customers.StoreShelf.Connect(trail, via, nodes);

            Vector2 at = from;
            foreach (var node in nodes)
            {
                Vector3 local = root.InverseTransformPoint(node);
                var next = new Vector2(local.x, local.z);
                legs.Add(new Leg(at, next, label));
                at = next;
            }

            legs.Add(new Leg(at, to, label));
        }

        private struct Leg
        {
            public readonly Vector2 A, B;
            public readonly string Label;
            public Leg(Vector2 a, Vector2 b, string label) { A = a; B = b; Label = label; }
        }

        private static List<Vector2> Approach(Transform root, Tycoon.Customers.CustomerQueue till)
        {
            var points = new List<Vector2>();
            if (till.queueApproach != null)
                foreach (var t in till.queueApproach) if (t != null) points.Add(Flat(root, t));
            return points;
        }

        private static Vector2 Flat(Transform root, Transform t)
        {
            Vector3 local = root.InverseTransformPoint(t.position);
            return new Vector2(local.x, local.z);
        }

        private static bool SegmentHits(Vector2 from, Vector2 to, Rect rect)
        {
            float length = Vector2.Distance(from, to);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.1f));
            for (int i = 0; i <= steps; i++)
                if (rect.Contains(Vector2.Lerp(from, to, i / (float)steps))) return true;
            return false;
        }

        private static Rect Grow(Rect r, float by) =>
            Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        /// <summary>The walkable rectangle inside a location's invisible boundary walls.</summary>
        private static Rect InnerBoundary(Transform walls)
        {
            float west = float.MaxValue, east = float.MinValue;
            float south = float.MaxValue, north = float.MinValue;
            foreach (Transform wall in walls)
            {
                Vector3 at = wall.localPosition;
                if (at.x < west) west = at.x;
                if (at.x > east) east = at.x;
                if (at.z < south) south = at.z;
                if (at.z > north) north = at.z;
            }

            // Pulled in by half a wall and a player radius: the closest the player can stand.
            const float standoff = 1.4f;
            return Rect.MinMaxRect(west + standoff, south + standoff, east - standoff, north - standoff);
        }

        // ---------------------------------------------------------------- the whole scene

        /// <summary>
        /// Checks that bind the locations together: the locations stand apart (so the only way
        /// between them is the travel squares), every travel square leads somewhere real, and
        /// every location that is bought is actually revealed by something.
        /// </summary>
        private static int Links(List<Location> locations)
        {
            int faults = 0;

            // Their walls, in one frame, so "do they touch" is a plain rectangle test.
            var frame = locations[0].transform;
            var rects = new Dictionary<Location, Rect>();
            foreach (var location in locations)
            {
                var walls = location.transform.Find("Boundary");
                if (walls == null) continue;
                rects[location] = WorldBoundary(walls, frame);
            }

            for (int i = 0; i < locations.Count; i++)
            {
                for (int j = i + 1; j < locations.Count; j++)
                {
                    if (!rects.TryGetValue(locations[i], out var a) || !rects.TryGetValue(locations[j], out var b))
                        continue;

                    if (a.Overlaps(b))
                    {
                        faults++;
                        Debug.LogWarning($"[Audit] LOCATIONS {locations[i].locationId} and " +
                                         $"{locations[j].locationId} overlap - the player could walk between them.");
                    }
                    else
                    {
                        float gapX = Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax);
                        float gapZ = Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax);
                        Debug.Log($"[Audit] locations {locations[i].locationId} and {locations[j].locationId} " +
                                  $"stand {Mathf.Max(gapX, gapZ):0.0} m apart.");
                    }
                }
            }

            foreach (var travel in Object.FindObjectsByType<TravelStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Location home = locations.Find(l => travel.transform.IsChildOf(l.transform));
                if (travel.destination == null || travel.destination == home)
                {
                    faults++;
                    Debug.LogWarning($"[Audit] TRAVEL {travel.name} does not lead to another location.");
                }
            }

            // Every location but the first is bought: something must switch its root on.
            var reveals = new HashSet<GameObject>();
            foreach (var gate in Object.FindObjectsByType<UnlockStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (gate.revealOnUnlock != null)
                    foreach (var go in gate.revealOnUnlock) if (go != null) reveals.Add(go);

            foreach (var location in locations)
            {
                if (location.gameObject.activeSelf) continue;
                if (reveals.Contains(location.gameObject)) continue;

                faults++;
                Debug.LogWarning($"[Audit] LOCATION {location.locationId} is saved off and nothing reveals it.");
            }

            Debug.Log($"[Audit] links: {faults} faults.");
            return faults;
        }

        /// <summary>
        /// Every object that saves state must have a stable id of its own, and no two may share
        /// one. A missing id falls back to the hierarchy path (rename it and the progress is
        /// gone); a duplicate makes two objects overwrite each other's save.
        /// </summary>
        private static int SaveIds()
        {
            int faults = 0;
            var seen = new Dictionary<string, string>();
            int count = 0;

            foreach (var behaviour in Object.FindObjectsByType<MonoBehaviour>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(behaviour is ISaveable saveable)) continue;
                count++;

                var identity = behaviour.GetComponent<SaveIdentity>();
                if (identity == null || !identity.HasId)
                {
                    faults++;
                    Debug.LogWarning($"[Audit] SAVE {behaviour.name} ({behaviour.GetType().Name}) has no SaveIdentity.");
                    continue;
                }

                string key = saveable.SaveKey;
                if (seen.TryGetValue(key, out string other))
                {
                    faults++;
                    Debug.LogWarning($"[Audit] SAVE duplicate key {key} ({behaviour.name} and {other}).");
                    continue;
                }
                seen[key] = behaviour.name;
            }

            Debug.Log($"[Audit] save ids: {count} saveable components, {faults} faults.");
            return faults;
        }

        private static Rect WorldBoundary(Transform walls, Transform frame)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var box in walls.GetComponentsInChildren<BoxCollider>(true))
            {
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = box.center + Vector3.Scale(box.size * 0.5f, new Vector3(
                        (corner & 1) == 0 ? -1f : 1f,
                        (corner & 2) == 0 ? -1f : 1f,
                        (corner & 4) == 0 ? -1f : 1f));

                    Vector3 local = frame.InverseTransformPoint(box.transform.TransformPoint(point));
                    minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
                    minZ = Mathf.Min(minZ, local.z); maxZ = Mathf.Max(maxZ, local.z);
                }
            }

            return Rect.MinMaxRect(minX, minZ, maxX, maxZ);
        }

        /// <summary>
        /// Can the player ever walk somewhere the camera shows the edge of the ground?
        ///
        /// This is the question behind "the map looks cut off", and it is one of arithmetic
        /// rather than of opinion: the camera is orthographic and rigidly fixed to the player,
        /// so the patch of ground it shows is a known quad at a known offset. Walk the inside
        /// of the boundary wall, project the four frustum corners onto the ground, and check
        /// every one of them lands on the ground plane.
        ///
        /// Checked at the narrowest and the widest screen the game can be played on, because a
        /// wide window shows more to the sides and a tall one shows more up and down the slope.
        /// </summary>
        private static int EdgeOfWorld(Transform root, string tag)
        {
            var rig = Object.FindFirstObjectByType<Tycoon.Core.IsometricCameraRig>();

            // By name alone this finds a pen's dirt patch just as happily as the ground plane -
            // every animal pen has a child called "Ground" - and measuring the world against a
            // three metre patch of dirt reported the edge as visible from everywhere. The real
            // ones are scene root objects, so insist on that. There can be more than one: the
            // plane that can be walked on, and a much larger one beyond it that only exists to
            // be looked at. The widest of them is what the camera can reach.
            GameObject ground = null;
            foreach (var go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!go.name.StartsWith("Ground") || go.transform.parent != null) continue;
                if (ground == null || go.transform.localScale.x > ground.transform.localScale.x)
                    ground = go;
            }

            if (rig == null || ground == null)
            {
                Debug.LogWarning($"[Audit] {tag} No camera rig or ground plane; cannot check the world edge.");
                return 0;
            }

            // A Unity plane primitive is 10 x 10 at scale 1.
            float groundHalf = ground.transform.localScale.x * 5f;
            float groundHalfZ = ground.transform.localScale.z * 5f;

            var walls = root.Find("Boundary");
            if (walls == null) { Debug.LogWarning($"[Audit] {tag} No boundary."); return 0; }

            Rect inside = InnerBoundary(walls);
            float west = inside.xMin, east = inside.xMax;
            float south = inside.yMin, north = inside.yMax;

            var rotation = Quaternion.Euler(rig.pitchYaw.x, rig.pitchYaw.y, 0f);
            Vector3 forward = rotation * Vector3.forward;
            Vector3 right = rotation * Vector3.right;
            Vector3 up = rotation * Vector3.up;

            // Signed: positive means ground ran out that far short, negative is the margin
            // still in hand. Started below anything real so the margin gets reported too.
            float worst = float.MinValue;
            string worstWhere = "";
            int faults = 0;

            // 0.46 is a tall phone held upright; 2.4 is about as wide as a desktop window gets.
            foreach (float aspect in new[] { 0.46f, 1.0f, 1.8f, 2.4f })
            {
                float halfH = rig.orthographicSize;
                float halfW = rig.orthographicSize * aspect;

                for (float x = west; x <= east + 0.01f; x += 1f)
                {
                    for (float z = south; z <= north + 0.01f; z += 1f)
                    {
                        // Only the rim matters: standing in the middle can never see further
                        // out than standing at the edge does.
                        bool onRim = x <= west + 1f || x >= east - 1f ||
                                     z <= south + 1f || z >= north - 1f;
                        if (!onRim) continue;

                        Vector3 focus = root.TransformPoint(new Vector3(x, 0f, z)) + rig.lookOffset;

                        for (int corner = 0; corner < 4; corner++)
                        {
                            Vector3 at = focus
                                + right * ((corner & 1) == 0 ? -halfW : halfW)
                                + up * ((corner & 2) == 0 ? -halfH : halfH);

                            // Slide along the view direction until it meets the ground.
                            if (Mathf.Abs(forward.y) < 0.0001f) continue;
                            Vector3 hit = at + forward * (-at.y / forward.y);

                            float overX = Mathf.Abs(hit.x) - groundHalf;
                            float overZ = Mathf.Abs(hit.z) - groundHalfZ;
                            float over = Mathf.Max(overX, overZ);

                            if (over > worst)
                            {
                                worst = over;
                                worstWhere = $"standing at ({x:0.0}, {z:0.0}) on a {aspect:0.00} screen";
                            }
                        }
                    }
                }
            }

            if (worst > 0f)
            {
                faults++;
                Debug.LogWarning($"[Audit] {tag} EDGE OF WORLD visible by {worst:0.0} m - {worstWhere}");
            }
            else
            {
                Debug.Log($"[Audit] {tag} world edge: never visible. Closest the camera gets to " +
                          $"running out of ground is {-worst:0.0} m short of it, " +
                          $"{worstWhere}. Ground plane is {groundHalf * 2f:0} m across.");
            }

            return faults;
        }

        /// <summary>
        /// Checks that nothing can be bought before the thing that makes it useful.
        ///
        /// A hired hand runs between two squares. If either end has not been paid for yet the
        /// worker has nothing to do, and the player has spent real money on it - so for every
        /// hire, both ends of its route must already be standing by the time the hire itself
        /// can be reached in the intended order. "Intended order" is price order, because that
        /// is the order these are actually bought in.
        ///
        /// This is the check that would have caught the farm hand who was sold with the corn
        /// field but delivered into a coop that had not been built, and stood at the empty
        /// plot with his arms full for the rest of the game.
        ///
        /// Cost is followed through every kind of switch: a gate costs its price, a chore costs
        /// nothing, and an "all of these" watcher costs the gates it waits for. The staged
        /// replay adds the same question in rounds - a route square must be live no later than
        /// the hire that uses it.
        /// </summary>
        private static int Progression(Transform root, Dictionary<StationBase, int> firstLive, string tag)
        {
            // What reveals what, and at what price.
            var revealedBy = new Dictionary<GameObject, Source>();
            var hiredBy = new Dictionary<GameObject, UnlockStation>();

            foreach (var gate in Object.FindObjectsByType<UnlockStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gate.revealOnUnlock == null) continue;
                foreach (var go in gate.revealOnUnlock)
                {
                    if (go == null) continue;
                    revealedBy[go] = new Source { Cost = gate.price, At = gate.transform };
                    hiredBy[go] = gate;
                }
            }

            foreach (var chore in Object.FindObjectsByType<ChoreStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (chore.revealOnDone == null) continue;
                foreach (var go in chore.revealOnDone)
                    if (go != null) revealedBy[go] = new Source { Cost = 0d, At = chore.transform };
            }

            foreach (var watcher in Object.FindObjectsByType<RevealWhenAll>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (watcher.reveal == null) continue;

                double cost = 0d;
                if (watcher.unlocks != null)
                    foreach (var u in watcher.unlocks) if (u != null) cost += u.price;
                if (watcher.painted != null)
                    foreach (var p in watcher.painted) if (p != null) cost += p.firstPrice;

                foreach (var go in watcher.reveal)
                    if (go != null) revealedBy[go] = new Source { Cost = cost, At = watcher.transform };
            }

            int faults = 0;
            var lines = new List<string>();

            foreach (var worker in Object.FindObjectsByType<Tycoon.Upkeep.WorkerAgent>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!worker.transform.IsChildOf(root)) continue;

                if (worker.pickup == null || worker.dropoff == null)
                {
                    faults++;
                    Debug.LogWarning($"[Audit] ROUTE {tag} {worker.name} has no pickup or no dropoff.");
                    continue;
                }

                double hireCost = CostToReach(worker.gameObject, revealedBy);
                double pickupCost = CostToReach(worker.pickup.gameObject, revealedBy);
                double dropCost = CostToReach(worker.dropoff.gameObject, revealedBy);

                if (pickupCost > hireCost || dropCost > hireCost)
                {
                    faults++;
                    Debug.LogWarning(
                        $"[Audit] ROUTE {tag} {worker.name} can be hired for {hireCost:0} but its " +
                        $"route costs {Mathf.Max((float)pickupCost, (float)dropCost):0} to " +
                        $"complete - it would stand idle after being paid for.");
                }

                // The same question in stages: when does the hire first appear, against when
                // do the two ends of its route?
                if (hiredBy.TryGetValue(worker.gameObject, out var hire) &&
                    firstLive.TryGetValue(hire, out int hireRound))
                {
                    foreach (var end in new[] { worker.pickup, worker.dropoff })
                    {
                        if (!firstLive.TryGetValue(end, out int endRound) || endRound <= hireRound) continue;

                        faults++;
                        Debug.LogWarning(
                            $"[Audit] ROUTE {tag} {worker.name} is on sale from stage {hireRound} but " +
                            $"{end.name} on its route only exists from stage {endRound}.");
                    }
                }

                // A field hand with several fields: every field must be reachable at some stage,
                // carry the crop the shared feed square takes, and the first must be pickup/dropoff.
                if (worker.routes != null && worker.routes.Length > 0 &&
                    worker.routes[0] != null && worker.routes[0].pickup is HarvestStation)
                {
                    if (worker.routes[0].pickup != worker.pickup || worker.routes[0].dropoff != worker.dropoff)
                    {
                        faults++;
                        Debug.LogWarning($"[Audit] FIELDS {tag} {worker.name}'s pickup/dropoff is not its first field route.");
                    }

                    foreach (var route in worker.routes)
                    {
                        var field = route != null ? route.pickup as HarvestStation : null;
                        var feed = route != null ? route.dropoff as DepositStation : null;
                        if (field == null || feed == null || feed.target == null || feed.target.item != field.crop)
                        {
                            faults++;
                            Debug.LogWarning($"[Audit] FIELDS {tag} {worker.name} has a field route that is not a " +
                                             "field to a feed square taking its crop.");
                            continue;
                        }

                        if (!firstLive.ContainsKey(field) || !firstLive.ContainsKey(feed))
                        {
                            faults++;
                            Debug.LogWarning($"[Audit] FIELDS {tag} {worker.name}'s route {field.name} -> {feed.name} " +
                                             "is never switched on.");
                        }
                    }
                }

                lines.Add($"{worker.name} hire@{hireCost:0} route@{Mathf.Max((float)pickupCost, (float)dropCost):0}");
            }

            lines.Sort();
            Debug.Log($"[Audit] {tag} routes: " + string.Join(" | ", lines));
            Debug.Log($"[Audit] {tag} progression: {faults} hires that outrun their own route.");
            return faults;
        }

        private struct Source
        {
            public double Cost;
            public Transform At;
        }

        /// <summary>
        /// What the player must have paid before this object exists. Zero if it is standing
        /// from the start. Walks back through gates, chores and watchers, so a thing behind two
        /// purchases costs both.
        /// </summary>
        private static double CostToReach(GameObject go, Dictionary<GameObject, Source> revealedBy)
        {
            double total = 0d;
            var seen = new HashSet<GameObject>();

            // A square is usually a child of the thing that gets revealed, so climb until a
            // switch is found or the scene root runs out.
            Transform at = go.transform;
            while (at != null)
            {
                if (revealedBy.TryGetValue(at.gameObject, out var source))
                {
                    if (!seen.Add(at.gameObject)) break;
                    total += source.Cost;
                    at = source.At;   // and whatever that switch itself sits behind
                    continue;
                }
                at = at.parent;
            }

            return total;
        }

        /// <summary>Prints where everything ended up, so the layout can be read off the log.</summary>
        private static void ReportLayout(Transform root, List<Solid> solids, string tag)
        {
            var named = new List<string>();

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                bool interesting =
                    t.GetComponent<Tycoon.Stations.ProducerMachine>() != null ||
                    t.GetComponent<Tycoon.Stations.HarvestStation>() != null;
                if (!interesting || t.parent != root) continue;

                Vector3 at = root.InverseTransformPoint(t.position);
                named.Add($"{t.name} at ({at.x:0.0}, {at.z:0.0})");
            }

            named.Sort();
            if (named.Count > 0) Debug.Log($"[Audit] {tag} layout: " + string.Join(" | ", named));
        }

        private static Rect Centred(Vector3 centre, Vector2 size) =>
            new Rect(centre.x - size.x * 0.5f, centre.z - size.y * 0.5f, size.x, size.y);

        private static Rect Intersection(Rect a, Rect b)
        {
            float x = Mathf.Max(a.xMin, b.xMin);
            float y = Mathf.Max(a.yMin, b.yMin);
            return new Rect(x, y, Mathf.Min(a.xMax, b.xMax) - x, Mathf.Min(a.yMax, b.yMax) - y);
        }
    }
}
