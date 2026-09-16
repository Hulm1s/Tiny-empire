using System.Collections.Generic;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Measures the built farm and reports anything standing on top of anything else.
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
    /// </summary>
    public static class SquareAudit
    {
        private const string ScenePath = "Assets/_Project/Scenes/Farm.unity";

        /// <summary>Mirrors InteractionSquare: the drawn outline is never smaller than this.</summary>
        private static readonly Vector2 MinCard = new Vector2(2.5f, 2.1f);

        private class Entry
        {
            public string Name;
            public string Owner;    // the building it belongs to, or itself
            public Rect Trigger;    // where the action actually works
            public Rect Card;       // what the player sees
            public bool Active;

            /// <summary>
            /// True once every purchase has been made: gates delete themselves, and everything
            /// they were hiding is on. That is the fullest the farm ever gets, and so the state
            /// most likely to have two things fighting over the same patch of grass.
            /// </summary>
            public bool ActiveWhenBuilt;
        }

        /// <summary>A piece of scenery with a footprint: a building shell, a pen, a stall.</summary>
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

            var root = GameObject.Find("Farm");
            if (root == null)
            {
                Debug.LogError("[Audit] No Farm root in the scene.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            var entries = new List<Entry>();

            foreach (var station in Object.FindObjectsByType<StationBase>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var box = station.GetComponent<BoxCollider>();
                var square = station.GetComponent<InteractionSquare>();
                if (box == null) continue;

                Vector3 local = root.transform.InverseTransformPoint(station.transform.position);
                Vector2 triggerSize = new Vector2(box.size.x, box.size.z);
                Vector2 cardSize = square != null
                    ? new Vector2(Mathf.Max(square.size.x, MinCard.x),
                                  Mathf.Max(square.size.y, MinCard.y))
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
                    Active = station.gameObject.activeInHierarchy,
                    // Every UnlockStation hides itself once paid off (hideSelfOnUnlock), and
                    // everything else in the scene is either on already or revealed by one.
                    ActiveWhenBuilt = !(station is UnlockStation),
                });
            }

            var solids = CollectSolids(root.transform);

            Debug.Log($"[Audit] {entries.Count} squares, {solids.Count} building footprints");

            // Checked twice: as the game opens, and as it ends. An overlap that only appears
            // once the second coop has been bought is still an overlap the player will meet.
            int clashes = Overlaps(entries, "day one", e => e.Active)
                        + Overlaps(entries, "fully built", e => e.ActiveWhenBuilt)
                        + SolidOverlaps(solids)
                        + SquaresOnSolids(entries, solids);

            // A trigger noticeably bigger than its outline means the player can act from
            // somewhere the game never told them about, and vice versa.
            foreach (var e in entries)
            {
                float dx = Mathf.Abs(e.Trigger.width - e.Card.width);
                float dy = Mathf.Abs(e.Trigger.height - e.Card.height);
                if (dx > 0.8f || dy > 0.8f)
                    Debug.LogWarning($"[Audit] MISMATCH {e.Name}: trigger " +
                                     $"{e.Trigger.width:0.0}x{e.Trigger.height:0.0} vs outline " +
                                     $"{e.Card.width:0.0}x{e.Card.height:0.0}");
            }

            clashes += EdgeOfWorld(root.transform);
            clashes += Progression();
            ReportLayout(root.transform, solids);

            Debug.Log(clashes == 0 ? "[Audit] Nothing overlaps." : $"[Audit] {clashes} faults.");
            Debug.Log("AUDIT_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        /// <summary>
        /// Reports every pair of squares that share ground in one particular state of the farm.
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

        /// <summary>
        /// Every building shell, pen, pasture and stall in the level, with the ground it covers.
        ///
        /// Found by name rather than by component, because these are plain boxes with no script
        /// on them - which is precisely why nothing was checking them.
        /// </summary>
        private static List<Solid> CollectSolids(Transform root)
        {
            var found = new List<Solid>();
            var wanted = new[] { "Shell", "Pen", "Pasture", "Stall" };

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
        private static int SolidOverlaps(List<Solid> solids)
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
                        $"[Audit] BUILDINGS {a.Name} x {b.Name} " +
                        $"by {shared.width:0.00} x {shared.height:0.00} m");
                }
            }

            Debug.Log($"[Audit] buildings: {clashes} overlapping pairs.");
            return clashes;
        }

        /// <summary>
        /// Squares painted across somebody else's building. A building's own squares sit right
        /// against it on purpose, so only other buildings count.
        ///
        /// Measured on the fully-built farm, because that is when the most is standing.
        /// </summary>
        private static int SquaresOnSolids(List<Entry> entries, List<Solid> solids)
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
                        $"[Audit] SQUARE-ON-BUILDING {square.Name} x {solid.Name} " +
                        $"by {shared.width:0.00} x {shared.height:0.00} m");
                }
            }

            Debug.Log($"[Audit] squares on buildings: {clashes} overlapping pairs.");
            return clashes;
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
        private static int EdgeOfWorld(Transform root)
        {
            var rig = Object.FindFirstObjectByType<Tycoon.Core.IsometricCameraRig>();

            // By name alone this finds a pen's dirt patch just as happily as the ground plane -
            // every animal pen has a child called "Ground" - and measuring the world against a
            // three metre patch of dirt reported the edge as visible from everywhere. The real
            // one is the scene root object, so insist on that.
            GameObject ground = null;
            foreach (var go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (go.name != "Ground" || go.transform.parent != null) continue;
                ground = go;
                break;
            }

            if (rig == null || ground == null)
            {
                Debug.LogWarning("[Audit] No camera rig or ground plane; cannot check the world edge.");
                return 0;
            }

            // A Unity plane primitive is 10 x 10 at scale 1.
            float groundHalf = ground.transform.localScale.x * 5f;
            float groundHalfZ = ground.transform.localScale.z * 5f;

            // Where the invisible walls are, pulled in by half a wall and a player radius -
            // the closest the player can actually stand to the edge.
            var walls = root.Find("Boundary");
            if (walls == null) { Debug.LogWarning("[Audit] No boundary."); return 0; }

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

            const float standoff = 1.4f;   // half a wall plus the player's own radius
            west += standoff; east -= standoff; south += standoff; north -= standoff;

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
                Debug.LogWarning($"[Audit] EDGE OF WORLD visible by {worst:0.0} m - {worstWhere}");
            }
            else
            {
                Debug.Log($"[Audit] world edge: never visible. Closest the camera gets to " +
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
        /// </summary>
        private static int Progression()
        {
            // What reveals what, and at what price.
            var revealedBy = new Dictionary<GameObject, UnlockStation>();

            foreach (var gate in Object.FindObjectsByType<UnlockStation>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (gate.revealOnUnlock == null) continue;
                foreach (var go in gate.revealOnUnlock)
                    if (go != null) revealedBy[go] = gate;
            }

            int faults = 0;
            var lines = new List<string>();

            foreach (var worker in Object.FindObjectsByType<Tycoon.Upkeep.WorkerAgent>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (worker.pickup == null || worker.dropoff == null)
                {
                    faults++;
                    Debug.LogWarning($"[Audit] ROUTE {worker.name} has no pickup or no dropoff.");
                    continue;
                }

                double hireCost = CostToReach(worker.gameObject, revealedBy);
                double pickupCost = CostToReach(worker.pickup.gameObject, revealedBy);
                double dropCost = CostToReach(worker.dropoff.gameObject, revealedBy);

                if (pickupCost > hireCost || dropCost > hireCost)
                {
                    faults++;
                    Debug.LogWarning(
                        $"[Audit] ROUTE {worker.name} can be hired for {hireCost:0} but its " +
                        $"route costs {Mathf.Max((float)pickupCost, (float)dropCost):0} to " +
                        $"complete - it would stand idle after being paid for.");
                }

                lines.Add($"{worker.name} hire@{hireCost:0} route@{Mathf.Max((float)pickupCost, (float)dropCost):0}");
            }

            lines.Sort();
            Debug.Log("[Audit] routes: " + string.Join(" | ", lines));
            Debug.Log($"[Audit] progression: {faults} hires that outrun their own route.");
            return faults;
        }

        /// <summary>
        /// What the player must have paid before this object exists. Zero if it is standing
        /// from the start. Walks back through gates, so a thing behind two purchases costs
        /// both.
        /// </summary>
        private static double CostToReach(GameObject go,
            Dictionary<GameObject, UnlockStation> revealedBy)
        {
            double total = 0d;
            var seen = new HashSet<GameObject>();

            // A square is usually a child of the thing that gets revealed, so climb until a
            // gate is found or the level root runs out.
            Transform at = go.transform;
            while (at != null)
            {
                if (revealedBy.TryGetValue(at.gameObject, out var gate))
                {
                    if (!seen.Add(at.gameObject)) break;
                    total += gate.price;
                    at = gate.transform;   // and whatever that gate itself sits behind
                    go = gate.gameObject;
                    continue;
                }
                at = at.parent;
            }

            return total;
        }

        /// <summary>Prints where everything ended up, so the layout can be read off the log.</summary>
        private static void ReportLayout(Transform root, List<Solid> solids)
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
            Debug.Log("[Audit] layout: " + string.Join(" | ", named));
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
