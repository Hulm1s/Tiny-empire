using Tycoon.Core;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEditor;
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
        // East grew again from 22 to 46 for the cafe (CafeBuilder: the shop stands at x 24..38, its back lane and the van at 40..44).
        private const float EdgeEast = 46f;
        private const float EdgeSouth = -12f;
        // North grew from 13 to 19.5 for the florist's meadow (FloristBuilder.MeadowNorth 17.2 + room for the fence).
        private const float EdgeNorth = 19.5f;

        /// <summary>The road stub's centre line, in the town's grid. The road box is 2.8 m deep.</summary>
        internal const float RoadZ = -3f;

        // ---- the paved square ----------------------------------------------------------
        // North of the road, east of the florist: where the next businesses of the street will stand.
        private const float PlazaWest = 1f, PlazaEast = 45f;
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
            var salon = SalonBuilder.Build(root, items, out var salonLast);
            var salonWatcher = new GameObject("SalonReveal");
            salonWatcher.transform.SetParent(root, false);
            var salonReveal = salonWatcher.AddComponent<RevealWhenAll>();
            salonReveal.unlocks = new[] { gardenerHire };
            salonReveal.reveal = new[] { salon };

            // The third, east of the salon: the cafe's derelict shop appears when the salon's last purchase (hairdresser 4) is made.
            var cafe = CafeBuilder.Build(root, items);
            var cafeWatcher = new GameObject("CafeReveal");
            cafeWatcher.transform.SetParent(root, false);
            var cafeReveal = cafeWatcher.AddComponent<RevealWhenAll>();
            cafeReveal.unlocks = new[] { salonLast };
            cafeReveal.reveal = new[] { cafe };

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

        // ---- ground, road, paving, park -----------------------------------------------------------

        private const float PaveTop = 0.05f;          // the sidewalk's top: what the cafe's garden, the stools and every pad stand on
        private const float RoadTop = 0.06f;          // the road stands a hair proud of it, with a kerb between

        /// <summary>The town park, opposite the shops across the street (south of the road): a lawn with a path, trees, benches and a low hedge.</summary>
        private static readonly Rect ParkLawn = new Rect(-12.5f, -10.8f, 29f, 4.6f);

        /// <summary>Everything in <paramref name="area"/> that is not in any of <paramref name="holes"/>, as rectangles.</summary>
        private static System.Collections.Generic.List<Rect> Subtract(Rect area, Rect[] holes)
        {
            var parts = new System.Collections.Generic.List<Rect> { area };
            foreach (var h in holes)
            {
                var next = new System.Collections.Generic.List<Rect>();
                foreach (var r in parts)
                {
                    if (!r.Overlaps(h)) { next.Add(r); continue; }
                    if (h.yMin > r.yMin) next.Add(Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, h.yMin));
                    if (h.yMax < r.yMax) next.Add(Rect.MinMaxRect(r.xMin, h.yMax, r.xMax, r.yMax));
                    float y0 = Mathf.Max(r.yMin, h.yMin), y1 = Mathf.Min(r.yMax, h.yMax);
                    if (h.xMin > r.xMin) next.Add(Rect.MinMaxRect(r.xMin, y0, h.xMin, y1));
                    if (h.xMax < r.xMax) next.Add(Rect.MinMaxRect(h.xMax, y0, r.xMax, y1));
                }
                parts = next;
            }
            return parts;
        }

        /// <summary>
        /// The sidewalk: ONE mesh (saved as an asset, like the grass cover) of flat quads whose UVs are metres, so one small tile texture
        /// repeats as continuous paving slabs over the whole town floor; one material, no runtime Material. Left out: the road, the lane,
        /// the florist's meadow and the park, which have their own ground.
        /// </summary>
        private static void BuildPaving(Transform root, Rect[] holes)
        {
            var tex = EnsurePavingTexture();
            var paving = LevelBuildKit.Mat("Town_Paving", Color.white, 0.05f);
            paving.mainTexture = tex;
            if (paving.HasProperty("_BaseMap")) paving.SetTexture("_BaseMap", tex);
            if (paving.HasProperty("_BaseColor")) paving.SetColor("_BaseColor", Color.white);
            if (paving.HasProperty("_Color")) paving.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(paving);

            var area = Rect.MinMaxRect(EdgeWest, EdgeSouth, EdgeEast, EdgeNorth);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();
            const float repeat = 2f;                     // metres per texture repeat (2 x 2 slabs of 1 m)
            foreach (var r in Subtract(area, holes))
            {
                if (r.width < 0.01f || r.height < 0.01f) continue;
                int b = vertices.Count;
                vertices.Add(new Vector3(r.xMin, PaveTop, r.yMin)); uvs.Add(new Vector2(r.xMin / repeat, r.yMin / repeat));
                vertices.Add(new Vector3(r.xMin, PaveTop, r.yMax)); uvs.Add(new Vector2(r.xMin / repeat, r.yMax / repeat));
                vertices.Add(new Vector3(r.xMax, PaveTop, r.yMax)); uvs.Add(new Vector2(r.xMax / repeat, r.yMax / repeat));
                vertices.Add(new Vector3(r.xMax, PaveTop, r.yMin)); uvs.Add(new Vector2(r.xMax / repeat, r.yMin / repeat));
                triangles.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }

            var mesh = new Mesh { name = "TownPaving" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            const string path = "Assets/_Project/Meshes/TownPaving.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(mesh); }
            else AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject("Paving");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = paving;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>A small slab texture (2 x 2 one-metre slabs, a joint between, slightly different greys), written once and imported repeating.</summary>
        private static Texture2D EnsurePavingTexture()
        {
            const string path = "Assets/_Project/Textures/Town_Paving.png";
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Textures")) AssetDatabase.CreateFolder("Assets/_Project", "Textures");
            if (!System.IO.File.Exists(System.IO.Path.GetFullPath(path)))
            {
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                var grey = new[] { 0.80f, 0.76f, 0.73f, 0.78f };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int sx = x / 64, sy = y / 64;
                        float g = grey[sx + 2 * sy];
                        // A dark joint on each slab's first and last pixels, a worn edge inside it.
                        int lx = x % 64, ly = y % 64;
                        int edge = Mathf.Min(Mathf.Min(lx, 63 - lx), Mathf.Min(ly, 63 - ly));
                        if (edge < 2) g *= 0.72f; else if (edge < 4) g *= 0.93f;
                        g += ((x * 7 + y * 13) % 11 - 5) * 0.003f;
                        tex.SetPixel(x, y, new Color(g, g * 0.985f, g * 0.96f, 1f));
                    }
                tex.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && (importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled))
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 128;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void BuildGround(Transform root)
        {
            var tarmac = LevelBuildKit.Mat("Road", new Color(0.36f, 0.36f, 0.39f));
            var markings = LevelBuildKit.Mat("RoadLine", new Color(0.88f, 0.88f, 0.8f));
            var kerb = LevelBuildKit.Mat("Town_Kerb", new Color(0.86f, 0.85f, 0.82f));

            // The road runs right through the fence gaps at both ends, as the market's does. It stands a hair above the sidewalk.
            const float roadWest = EdgeWest - 1f;
            const float roadEast = EdgeEast + 4f;
            LevelBuildKit.Box("Road", root, new Vector3((roadWest + roadEast) * 0.5f, RoadTop * 0.5f, RoadZ),
                new Vector3(roadEast - roadWest, RoadTop, 2.8f), tarmac, castShadow: false);
            for (int x = (int)roadWest; x <= (int)roadEast; x += 2)
            {
                LevelBuildKit.Box($"Line_{x}", root, new Vector3(x, RoadTop, RoadZ),
                    new Vector3(0.9f, 0.03f, 0.14f), markings, castShadow: false);
            }
            // The kerbs, either side.
            foreach (float side in new[] { -1.5f, 1.5f })
                LevelBuildKit.Box("Kerb", root, new Vector3((roadWest + roadEast) * 0.5f, 0.075f, RoadZ + side),
                    new Vector3(roadEast - roadWest, 0.05f, 0.12f), kerb, castShadow: false);

            // The back lane: the delivery van's road, off the street at x 43.8 up behind the cafe to its back door, widening to a bay where it parks.
            var laneRect = new Rect(CafeBuilder.LaneWest, RoadZ + 1.4f, CafeBuilder.LaneEast - CafeBuilder.LaneWest, CafeBuilder.LaneNorth - (RoadZ + 1.4f));
            var bayRect = new Rect(CafeBuilder.BayWest, CafeBuilder.BayZ0, CafeBuilder.LaneWest - CafeBuilder.BayWest, CafeBuilder.LaneNorth - CafeBuilder.BayZ0);
            foreach (var r in new[] { laneRect, bayRect })
                LevelBuildKit.Box("Lane", root, new Vector3(r.center.x, RoadTop * 0.5f, r.center.y), new Vector3(r.width, RoadTop, r.height), tarmac, castShadow: false);
            for (float z = RoadZ + 2.0f; z < CafeBuilder.LaneNorth - 0.8f; z += 2f)
                LevelBuildKit.Box("LaneLine", root, new Vector3((CafeBuilder.LaneWest + CafeBuilder.LaneEast) * 0.5f, RoadTop, z), new Vector3(0.14f, 0.03f, 0.9f), markings, castShadow: false);
            LevelBuildKit.Box("LaneKerb", root, new Vector3(CafeBuilder.LaneWest - 0.06f, 0.075f, (laneRect.yMin + laneRect.yMax) * 0.5f), new Vector3(0.12f, 0.05f, laneRect.height), kerb, castShadow: false);

            // The sidewalk: paving over the whole town floor except the road, the lane, the florist's meadow (a garden) and the park.
            var roadRect = new Rect(roadWest - 2f, RoadZ - 1.4f, roadEast - roadWest + 4f, 2.8f);
            var meadow = new Rect(FloristBuilder.ShopWest - 0.1f, FloristBuilder.ShopNorth, FloristBuilder.ShopEast - FloristBuilder.ShopWest + 0.2f,
                FloristBuilder.MeadowNorth - FloristBuilder.ShopNorth);
            BuildPaving(root, new[] { roadRect, laneRect, bayRect, meadow, ParkLawn });

            BuildPark(root);

            // No plain grass inside the fence: the tufts go only outside it (the park and the florist's meadow have their own ground).
            const float beyond = 8f;
            var area = new Rect(EdgeWest - beyond, EdgeSouth - beyond,
                                (EdgeEast - EdgeWest) + beyond * 2f,
                                (EdgeNorth - EdgeSouth) + beyond * 2f);
            var keepClear = new[]
            {
                // The road and the strip either side of it, out to the end of the tarmac.
                new Rect(roadWest - 1f, RoadZ - 2.2f, (roadEast - roadWest) + 2f, 4.4f),
                // The whole town floor inside the fence (and a margin round it).
                new Rect(EdgeWest - 1f, EdgeSouth - 1f, (EdgeEast - EdgeWest) + 2f, (EdgeNorth - EdgeSouth) + 2f),
            };

            LevelBuildKit.BuildGrass(root, area, keepClear, tufts: 420, seed: 20261014,
                meshName: "TownGrassCover", objectName: "Grass");
        }

        /// <summary>
        /// The park across the street from the shops: a lawn, a gravel path with a branch to the sidewalk, nine trees, four benches and a low hedge.
        /// A few shared materials, plain primitives, no colliders (nobody has any business in it, and nothing in it is a square).
        /// </summary>
        private static void BuildPark(Transform root)
        {
            var park = new GameObject("Park").transform;
            park.SetParent(root, false);
            var lawn = LevelBuildKit.Mat("Town_ParkLawn", new Color(0.48f, 0.72f, 0.34f), 0.05f);
            var path = LevelBuildKit.Mat("Town_ParkPath", new Color(0.84f, 0.77f, 0.60f), 0.05f);
            var trunk = LevelBuildKit.Mat("Town_Trunk", new Color(0.42f, 0.28f, 0.17f), 0.1f);
            var leafA = LevelBuildKit.Mat("Town_LeafA", new Color(0.26f, 0.55f, 0.24f), 0.1f);
            var leafB = LevelBuildKit.Mat("Town_LeafB", new Color(0.36f, 0.62f, 0.26f), 0.1f);
            var hedge = LevelBuildKit.Mat("Town_Hedge", new Color(0.22f, 0.46f, 0.22f), 0.1f);
            var wood = LevelBuildKit.Mat("Town_BenchWood", new Color(0.62f, 0.42f, 0.24f), 0.15f);
            var iron = LevelBuildKit.Mat("Town_BenchIron", new Color(0.20f, 0.22f, 0.24f), 0.3f);

            var c = ParkLawn.center;
            LevelBuildKit.Box("Lawn", park, new Vector3(c.x, 0.03f, c.y), new Vector3(ParkLawn.width, 0.06f, ParkLawn.height), lawn, castShadow: false);
            float pathZ = c.y;
            LevelBuildKit.Box("Path", park, new Vector3(c.x, 0.065f, pathZ), new Vector3(ParkLawn.width - 1f, 0.02f, 1.3f), path, castShadow: false);
            LevelBuildKit.Box("PathBranch", park, new Vector3(2.0f, 0.065f, (pathZ + ParkLawn.yMax) * 0.5f), new Vector3(1.3f, 0.02f, ParkLawn.yMax - pathZ), path, castShadow: false);

            // The low hedge along the south side, with a gap, and short runs at the ends of the north side.
            foreach (var run in new[] { new Vector2(-12.2f, -3.0f), new Vector2(-1.0f, 16.2f) })
                LevelBuildKit.Box("Hedge", park, new Vector3((run.x + run.y) * 0.5f, 0.35f, ParkLawn.yMin + 0.3f), new Vector3(run.y - run.x, 0.6f, 0.5f), hedge, castShadow: false);
            foreach (var run in new[] { new Vector2(-12.2f, -8.0f), new Vector2(12.0f, 16.2f) })
                LevelBuildKit.Box("Hedge", park, new Vector3((run.x + run.y) * 0.5f, 0.35f, ParkLawn.yMax - 0.3f), new Vector3(run.y - run.x, 0.6f, 0.5f), hedge, castShadow: false);

            var treeAt = new[]
            {
                new Vector2(-10.5f, -9.4f), new Vector2(-6.5f, -7.6f), new Vector2(-3.2f, -9.6f), new Vector2(0.2f, -7.4f), new Vector2(4.6f, -9.5f),
                new Vector2(7.4f, -7.5f), new Vector2(10.4f, -9.4f), new Vector2(13.6f, -7.7f), new Vector2(15.2f, -9.8f),
            };
            for (int i = 0; i < treeAt.Length; i++)
            {
                float h = 1.5f + (i % 3) * 0.35f, r = 1.0f + (i % 2) * 0.25f;
                LevelBuildKit.Cylinder("Trunk", park, new Vector3(treeAt[i].x, 0.06f + h * 0.5f, treeAt[i].y), new Vector3(0.28f, h * 0.5f, 0.28f), trunk, castShadow: false);
                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "Crown";
                Object.DestroyImmediate(crown.GetComponent<Collider>());
                crown.transform.SetParent(park, false);
                crown.transform.localPosition = new Vector3(treeAt[i].x, 0.06f + h + r * 0.55f, treeAt[i].y);
                crown.transform.localScale = new Vector3(r * 2f, r * 1.7f, r * 2f);
                var rend = crown.GetComponent<Renderer>();
                rend.sharedMaterial = i % 2 == 0 ? leafA : leafB;
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Benches along the path, seats facing it.
            foreach (var at in new[] { new Vector2(-8.2f, pathZ + 1.0f), new Vector2(-1.8f, pathZ - 1.0f), new Vector2(5.4f, pathZ + 1.0f), new Vector2(11.2f, pathZ - 1.0f) })
            {
                float side = at.y > pathZ ? 1f : -1f;
                LevelBuildKit.Box("BenchSeat", park, new Vector3(at.x, 0.46f, at.y), new Vector3(1.5f, 0.07f, 0.45f), wood, castShadow: false);
                LevelBuildKit.Box("BenchBack", park, new Vector3(at.x, 0.78f, at.y + side * 0.2f), new Vector3(1.5f, 0.36f, 0.06f), wood, castShadow: false);
                foreach (float lx in new[] { -0.65f, 0.65f })
                    LevelBuildKit.Box("BenchLeg", park, new Vector3(at.x + lx, 0.25f, at.y), new Vector3(0.07f, 0.42f, 0.4f), iron, castShadow: false);
            }
        }
    }
}
