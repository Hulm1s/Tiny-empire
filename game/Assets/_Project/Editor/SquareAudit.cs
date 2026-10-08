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
            clashes += EdgeOfWorld(root, tag);
            clashes += Progression(root, firstLive, tag);
            clashes += GrowthRules(root, firstLive, firstBuffer, tag);
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

            // A square nothing ever switches on cannot be used by anyone.
            foreach (var station in stations)
            {
                if (firstLive.ContainsKey(station)) continue;
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
                found.Add(new Solid { Name = owner + "/" + t.name, Owner = owner, Area = area });
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
