using System.IO;
using Tycoon.Config;
using Tycoon.Core;
using Tycoon.Player;
using Tycoon.Stations;
using Tycoon.UI;
using Tycoon.Upkeep;
using UnityEditor;
using UnityEngine;

namespace Tycoon.EditorTools
{
    /// <summary>
    /// Reusable pieces for assembling a level from code.
    ///
    /// The point of this kit is that a new business - a bakery, a juice bar, a car wash - is
    /// built from the same five or six calls as the farm. Levels stay cheap to add, and they
    /// all behave consistently because they are literally the same parts.
    /// </summary>
    public static class LevelBuildKit
    {
        /// <summary>
        /// Extra turn applied to building shells relative to the level grid.
        ///
        /// Zero: the camera provides the viewing angle now (see IsometricCameraRig.pitchYaw),
        /// so buildings sit square on their plots. Left as a knob because turning individual
        /// buildings a little is a cheap way to stop a street of them looking identical.
        /// </summary>
        public const float BuildingYaw = 0f;

        private const string MaterialFolder = "Assets/_Project/Materials";
        private const string ConfigFolder = "Assets/_Project/Configs";

        // ---------------------------------------------------------------- assets

        public static Material Mat(string name, Color color, float smoothness = 0.1f)
        {
            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.SetColor("_BaseColor", color);
                existing.SetFloat("_Smoothness", smoothness);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        public static ItemDefinition Item(string id, string displayName, Color color, double price,
            bool perishable = false, float spoilSeconds = 180f, float stackHeight = 0.28f)
        {
            EnsureFolder(ConfigFolder);
            string path = $"{ConfigFolder}/Item_{id}.asset";

            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (item == null)
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }

            item.id = id;
            item.displayName = displayName;
            item.color = color;
            item.basePrice = price;
            item.perishable = perishable;
            item.spoilSeconds = spoilSeconds;
            item.stackHeight = stackHeight;

            // Give the item a real material asset so carried units are not rendered with the
            // built-in default material, which is magenta in a URP build.
            item.carryMaterial = Mat($"Item_{id}", color);

            EditorUtility.SetDirty(item);
            return item;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ---------------------------------------------------------------- primitives

        /// <summary>
        /// A greybox block.
        ///
        /// <paramref name="castShadow"/> is worth thinking about for every call. Anything that
        /// casts a shadow is drawn a second time into the shadow map, and this level is built
        /// from hundreds of small props - crop stalks, road markings, fence rails. None of them
        /// casts a shadow anyone would miss, and together they were most of the shadow pass.
        /// </summary>
        public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool collider = false, bool castShadow = true, Vector3 rot = default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(rot);
            go.transform.localScale = scale;

            var box = go.GetComponent<Collider>();
            if (!collider && box != null) Object.DestroyImmediate(box);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (!castShadow)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return go;
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool castShadow = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            if (!castShadow)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return go;
        }

        /// <summary>The painted square on the ground that tells the player "stand here".</summary>
        public static GameObject Decal(Transform parent, Vector2 size, Color color, string materialName)
        {
            var decal = Box("Decal", parent, new Vector3(0f, 0.03f, 0f),
                new Vector3(size.x, 0.06f, size.y), Mat(materialName, color));
            decal.GetComponent<Renderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;
            return decal;
        }


        // ---------------------------------------------------------------- ground cover

        /// <summary>
        /// Scatters grass as a single combined mesh.
        ///
        /// Thousands of little grass objects would each cost a draw call and a transform;
        /// baked into one mesh they cost one of each, which is the difference between grass
        /// being free and grass being the reason the phone gets warm. Nothing here casts a
        /// shadow either - a tuft's shadow is invisible and the shadow pass is not.
        /// </summary>
        public static GameObject BuildGrass(Transform parent, Rect area, Rect[] keepClear,
            int tufts = 900, int seed = 20260915, string meshName = "GrassCover",
            string objectName = "Grass")
        {
            var random = new System.Random(seed);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            var colours = new System.Collections.Generic.List<Color>();

            var pale = new Color(0.52f, 0.68f, 0.34f);
            var deep = new Color(0.30f, 0.50f, 0.22f);

            int placed = 0, attempts = 0;
            while (placed < tufts && attempts < tufts * 12)
            {
                attempts++;

                float x = (float)(random.NextDouble() * area.width + area.xMin);
                float z = (float)(random.NextDouble() * area.height + area.yMin);

                bool blocked = false;
                for (int i = 0; i < keepClear.Length; i++)
                    if (keepClear[i].Contains(new Vector2(x, z))) { blocked = true; break; }
                if (blocked) continue;

                placed++;

                // Three blades per tuft, each a narrow triangle leaning a different way.
                float height = 0.18f + (float)random.NextDouble() * 0.22f;
                Color tint = Color.Lerp(pale, deep, (float)random.NextDouble());

                for (int blade = 0; blade < 3; blade++)
                {
                    float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                    float width = 0.05f + (float)random.NextDouble() * 0.04f;
                    float lean = ((float)random.NextDouble() - 0.5f) * 0.14f;

                    Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * width;
                    Vector3 root3 = new Vector3(x, 0f, z) +
                                    new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 0.06f;

                    int b = vertices.Count;
                    vertices.Add(root3 - side);
                    vertices.Add(root3 + side);
                    vertices.Add(root3 + new Vector3(lean, height, lean));

                    // Both windings, so a blade is visible from either side without
                    // needing a two-sided shader.
                    triangles.Add(b); triangles.Add(b + 2); triangles.Add(b + 1);
                    triangles.Add(b); triangles.Add(b + 1); triangles.Add(b + 2);

                    colours.Add(tint * 0.75f);
                    colours.Add(tint * 0.75f);
                    colours.Add(tint);
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colours);

            // Every blade points straight up, whatever way it leans.
            //
            // Recalculating normals here gives black grass: each blade is written with both
            // windings so it is visible from either side, and averaging two opposite face
            // normals cancels them out. Facing them all at the sky also lights the grass like
            // the ground it grows from, which is what stylised foliage wants anyway.
            var up = new Vector3[vertices.Count];
            for (int i = 0; i < up.Length; i++) up[i] = Vector3.up;
            mesh.normals = up;

            mesh.RecalculateBounds();

            EnsureFolder("Assets/_Project/Meshes");
            // One asset per location: they all share this builder, and a second call that
            // reused the first one's path would delete the mesh the first scene object holds.
            string meshPath = $"Assets/_Project/Meshes/{meshName}.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat("Grass", new Color(0.45f, 0.62f, 0.30f));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Debug.Log($"[LevelBuildKit] Grass: {placed} tufts, {triangles.Count / 3} triangles, 1 draw call");
            return go;
        }

        /// <summary>One straight run of fence, in the level's own grid.</summary>
        public struct FenceRun
        {
            public Vector2 From;
            public Vector2 To;

            public FenceRun(float fromX, float fromZ, float toX, float toZ)
            {
                From = new Vector2(fromX, fromZ);
                To = new Vector2(toX, toZ);
            }
        }

        /// <summary>
        /// A post-and-rail fence round the edge of the farm.
        ///
        /// It exists to answer a question the player asks by walking: where does this place
        /// end? Without it the grass simply carries on until the invisible wall stops you,
        /// which reads as the level running out rather than as the farm having a boundary.
        /// A fence turns the same wall into somewhere deliberate.
        ///
        /// Built as one combined mesh for the same reason the grass is: fifty posts and a
        /// hundred rail segments as separate objects would be a hundred and fifty draw calls
        /// to draw something nobody looks at directly.
        /// </summary>
        public static GameObject BuildFence(Transform parent, FenceRun[] runs,
            float postSpacing = 2.6f, string meshName = "FenceLine", string objectName = "Fence")
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            var colours = new System.Collections.Generic.List<Color>();

            var postTop = new Color(0.68f, 0.50f, 0.32f);
            var postBottom = new Color(0.44f, 0.31f, 0.19f);
            var railColour = new Color(0.78f, 0.62f, 0.42f);

            const float postHeight = 1.15f;
            const float postThickness = 0.14f;
            const float railThickness = 0.07f;
            const float railDepth = 0.11f;

            int posts = 0;

            foreach (var run in runs)
            {
                Vector2 along = run.To - run.From;
                float length = along.magnitude;
                if (length < 0.01f) continue;

                Vector2 direction = along / length;
                // Perpendicular in the ground plane, so a rail has thickness across the run
                // whichever way the run happens to point.
                var across = new Vector2(-direction.y, direction.x);

                int spans = Mathf.Max(1, Mathf.RoundToInt(length / postSpacing));
                for (int i = 0; i <= spans; i++)
                {
                    Vector2 at = Vector2.Lerp(run.From, run.To, i / (float)spans);
                    AddBox(vertices, triangles, colours,
                        new Vector3(at.x, postHeight * 0.5f, at.y),
                        direction, across,
                        postThickness, postHeight, postThickness,
                        postBottom, postTop);
                    posts++;
                }

                // Two rails per run, one long box each rather than one per span: the posts
                // already break the silhouette up, so nothing is gained by segmenting them.
                Vector2 middle = (run.From + run.To) * 0.5f;
                foreach (float y in new[] { 0.42f, 0.82f })
                {
                    AddBox(vertices, triangles, colours,
                        new Vector3(middle.x, y, middle.y),
                        direction, across,
                        length, railThickness, railDepth,
                        railColour * 0.82f, railColour);
                }
            }

            var mesh = new Mesh { name = meshName };
            mesh.indexFormat = vertices.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colours);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            EnsureFolder("Assets/_Project/Meshes");
            string meshPath = $"Assets/_Project/Meshes/{meshName}.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Mat("Fence", new Color(0.72f, 0.56f, 0.36f));
            renderer.receiveShadows = false;

            Debug.Log($"[LevelBuildKit] Fence: {posts} posts, {triangles.Count / 3} triangles, 1 draw call");
            return go;
        }

        /// <summary>
        /// Appends one box to a mesh being built up, oriented along a run rather than along
        /// the axes, so a fence can turn a corner without a transform per plank.
        /// </summary>
        private static void AddBox(
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<int> triangles,
            System.Collections.Generic.List<Color> colours,
            Vector3 centre, Vector2 direction, Vector2 across,
            float length, float height, float depth,
            Color bottom, Color top)
        {
            Vector3 dx = new Vector3(direction.x, 0f, direction.y) * (length * 0.5f);
            Vector3 dz = new Vector3(across.x, 0f, across.y) * (depth * 0.5f);
            Vector3 dy = Vector3.up * (height * 0.5f);

            int b = vertices.Count;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sx = (corner & 1) == 0 ? -dx : dx;
                Vector3 sy = (corner & 2) == 0 ? -dy : dy;
                Vector3 sz = (corner & 4) == 0 ? -dz : dz;
                vertices.Add(centre + sx + sy + sz);
                colours.Add((corner & 2) == 0 ? bottom : top);
            }

            // Corner bits: 1 = +x, 2 = +y, 4 = +z. Wound so every face points outwards.
            int[] faces =
            {
                0, 2, 3, 0, 3, 1,   // -z
                5, 7, 6, 5, 6, 4,   // +z
                4, 6, 2, 4, 2, 0,   // -x
                1, 3, 7, 1, 7, 5,   // +x
                2, 6, 7, 2, 7, 3,   // +y
                4, 0, 1, 4, 1, 5,   // -y
            };

            for (int i = 0; i < faces.Length; i++) triangles.Add(b + faces[i]);
        }

        // ---------------------------------------------------------------- location edges

        /// <summary>
        /// Invisible walls round a location's playable area, in that location's own grid.
        ///
        /// Shared by the farm and the market so the edge of the world is built one way. The
        /// walls line up with the fence exactly: the thing that stops the player is the thing
        /// they can see, never an invisible barrier in open grass or a fence you can walk
        /// through. The layout audit reads the child called "Boundary" to measure how close the
        /// camera can get to the edge of the ground.
        /// </summary>
        public static void BuildBoundary(Transform root, float west, float east, float south, float north)
        {
            const float thickness = 2f;
            const float height = 6f;

            float midZ = (north + south) * 0.5f;
            float midX = (east + west) * 0.5f;
            float depth = north - south;
            float width = east - west;

            var boundary = new GameObject("Boundary");
            boundary.transform.SetParent(root, false);

            AddWall(boundary.transform, "North", new Vector3(midX, height * 0.5f, north),
                new Vector3(width, height, thickness));
            AddWall(boundary.transform, "South", new Vector3(midX, height * 0.5f, south),
                new Vector3(width, height, thickness));
            AddWall(boundary.transform, "East", new Vector3(east, height * 0.5f, midZ),
                new Vector3(thickness, height, depth));
            AddWall(boundary.transform, "West", new Vector3(west, height * 0.5f, midZ),
                new Vector3(thickness, height, depth));
        }

        private static void AddWall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            // Solid, not a trigger: the CharacterController must actually be stopped by it.
            box.isTrigger = false;
        }

        /// <summary>
        /// A post-and-rail fence along the same four edges as <see cref="BuildBoundary"/>, with
        /// one break on each of the west and east sides where the road at <paramref name="roadZ"/>
        /// passes through.
        /// </summary>
        public static GameObject BuildFenceAround(Transform root, float west, float east,
            float south, float north, float roadZ, string meshName = "FenceLine")
        {
            // The road box is 2.8 m deep, centred on roadZ. A little clearance either side so
            // the last post does not sit half on the tarmac.
            const float roadHalfDepth = 1.4f;
            const float clearance = 0.35f;
            float gapNorth = roadZ + roadHalfDepth + clearance;
            float gapSouth = roadZ - roadHalfDepth - clearance;

            var runs = new[]
            {
                new FenceRun(west, north, east, north),
                new FenceRun(west, south, east, south),

                new FenceRun(west, north, west, gapNorth),
                new FenceRun(west, gapSouth, west, south),

                new FenceRun(east, north, east, gapNorth),
                new FenceRun(east, gapSouth, east, south),
            };

            return BuildFence(root, runs, meshName: meshName);
        }

        // ---------------------------------------------------------------- stations

        /// <summary>
        /// Creates a station: trigger volume, floor decal and floating sign, all sized together.
        /// This is the single call every interaction in the game is built from.
        /// </summary>
        public static T Station<T>(string id, string name, Transform parent, Vector3 position,
            Vector2 size, string label, Color color, Vector2 smallestCard = default)
            where T : StationBase
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            Identify(go, id);

            var trigger = go.AddComponent<BoxCollider>();
            var station = go.AddComponent<T>();

            // Sized AFTER the station component exists, not before.
            //
            // Adding a StationBase in the editor fires its Reset(), which stamps the collider
            // back to a default two metre cube. Setting the size first therefore did nothing:
            // half the farm ended up with a 2x2 trigger under a 5.5x3.5 outline, so a player
            // standing on most of a corn field was outside the square that told them to stand
            // there. It also fed the wrong field width to the hire-square spacing.
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, 2.5f, size.y);
            trigger.center = new Vector3(0f, 1.25f, 0f);
            station.label = label;
            station.zoneColor = color;

            // The square replaces the old flat slab. It is the game's main way of talking to
            // the player, so it carries the label, the "you are standing here" state and the
            // progress ring all in one.
            var square = go.AddComponent<InteractionSquare>();
            square.Configure(station, size, color, smallestCard);

            // No floating sign here on purpose: the square itself now carries the label and
            // the numbers. Two labels for one action was clutter, and putting the text on the
            // ground keeps the information where the action happens.
            return station;
        }

        public static ItemBuffer Buffer(string id, string name, Transform parent, Vector3 position,
            ItemDefinition item, int capacity, int starting = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            Identify(go, id);

            var buffer = go.AddComponent<ItemBuffer>();
            buffer.item = item;
            buffer.capacity = capacity;

            var so = new SerializedObject(buffer);
            so.FindProperty("startingCount").intValue = starting;
            so.ApplyModifiedPropertiesWithoutUndo();

            return buffer;
        }

        /// <summary>
        /// Stamps a stable save id. Every object that saves state needs one: without it the
        /// save key falls back to the hierarchy path, and renaming or moving the object
        /// silently wipes its progress.
        /// </summary>
        public static SaveIdentity Identify(GameObject go, string id)
        {
            var identity = go.GetComponent<SaveIdentity>();
            if (identity == null) identity = go.AddComponent<SaveIdentity>();
            identity.Assign(id);
            return identity;
        }

        /// <summary>
        /// A complete production building: input hopper, machine, output basket, and the three
        /// squares that let the player feed it, empty it and repair it.
        /// </summary>
        public class Workshop
        {
            public GameObject Root;
            public ItemBuffer Input;
            public ItemBuffer Output;
            public ProducerMachine Machine;
            public Durability Durability;
            public DepositStation Feed;
            public CollectStation Collect;
            public RepairStation Repair;
            public UpgradeStation Upgrade;
            public AlertBeacon Beacon;
        }

        /// <summary>What lives in a building's pen. Drives the animal shape and the pen size.</summary>
        public enum Livestock { None, Chicken, Cow }

        /// <summary>
        /// A fenced pen attached to the side of a building, with the animals inside and nest
        /// boxes against the wall.
        ///
        /// The animals used to stand loose in front of the building, which read as strays
        /// wandering the farm rather than stock the player owns. A fence makes the pen look
        /// like part of the property, and it keeps the ground in front of the doors clear for
        /// the interaction squares.
        /// </summary>
        private static Transform BuildPen(Transform parent, int maxUnits, Livestock kind)
        {
            if (kind == Livestock.None) return null;

            // Sized against the column spacing, not by eye.
            //
            // A workshop reaches 5.15 m west of its centre for the repair and buy squares, so
            // whatever the pen reaches east of centre sets how far apart two columns have to
            // stand. The old pasture reached 4.9 m and the columns were 8.5 m apart, which is
            // how a cow shed's squares ended up painted across its neighbour's pasture and the
            // first dairy ended up sitting on the chicken pen. These reach 4.1 and 3.7.
            bool cows = kind == Livestock.Cow;
            Vector2 size = cows ? new Vector2(2.8f, 4.2f) : new Vector2(2.4f, 3.6f);

            var pen = new GameObject(cows ? "Pasture" : "Pen");
            pen.transform.SetParent(parent, false);
            pen.transform.localPosition = new Vector3(cows ? 2.7f : 2.5f, 0f, 0f);

            var dirt = Mat(cows ? "Pen_Pasture" : "Pen_Dirt",
                cows ? new Color(0.46f, 0.62f, 0.32f) : new Color(0.62f, 0.53f, 0.38f));
            var post = Mat("Pen_Post", new Color(0.72f, 0.58f, 0.4f));
            var rail = Mat("Pen_Rail", new Color(0.85f, 0.73f, 0.55f));

            Box("Ground", pen.transform, new Vector3(0f, 0.02f, 0f),
                new Vector3(size.x, 0.04f, size.y), dirt, castShadow: false);

            float hx = size.x * 0.5f, hz = size.y * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Box($"Post_{sx}_{sz}", pen.transform, new Vector3(hx * sx, 0.35f, hz * sz),
                        new Vector3(0.12f, 0.7f, 0.12f), post, castShadow: false);

            Box("RailN", pen.transform, new Vector3(0f, 0.45f, hz), new Vector3(size.x, 0.08f, 0.07f), rail, castShadow: false);
            Box("RailS", pen.transform, new Vector3(0f, 0.45f, -hz), new Vector3(size.x, 0.08f, 0.07f), rail, castShadow: false);
            Box("RailE", pen.transform, new Vector3(hx, 0.45f, 0f), new Vector3(0.07f, 0.08f, size.y), rail, castShadow: false);

            if (!cows)
            {
                // Nest boxes against the building wall - where the eggs actually come from.
                var nest = Mat("Pen_Nest", new Color(0.75f, 0.62f, 0.42f));
                var straw = Mat("Pen_Straw", new Color(0.93f, 0.85f, 0.5f));
                for (int i = 0; i < 3; i++)
                {
                    float z = Mathf.Lerp(-hz * 0.6f, hz * 0.6f, i / 2f);
                    Box($"Nest_{i}", pen.transform, new Vector3(-hx + 0.3f, 0.17f, z),
                        new Vector3(0.5f, 0.34f, 0.6f), nest, castShadow: false);
                    Box($"Straw_{i}", pen.transform, new Vector3(-hx + 0.3f, 0.36f, z),
                        new Vector3(0.42f, 0.06f, 0.5f), straw, castShadow: false);
                }
            }

            var holder = new GameObject("Animals");
            holder.transform.SetParent(pen.transform, false);

            for (int i = 0; i < maxUnits; i++)
            {
                var animal = new GameObject($"{kind}_{i}");
                animal.transform.SetParent(holder.transform, false);

                float z = maxUnits <= 1 ? 0f : Mathf.Lerp(-hz * 0.55f, hz * 0.55f, i / (float)(maxUnits - 1));
                animal.transform.localPosition = new Vector3(0.5f, 0f, z);
                // A little scatter so a row of animals does not look stamped out.
                animal.transform.localRotation = Quaternion.Euler(0f, (i * 53) % 360, 0f);

                if (cows) BuildCow(animal.transform);
                else BuildChicken(animal.transform);
            }

            return holder.transform;
        }

        private static void BuildChicken(Transform parent)
        {
            var feather = Mat("Chicken_Body", new Color(0.97f, 0.96f, 0.92f));
            var comb = Mat("Chicken_Comb", new Color(0.9f, 0.32f, 0.28f));

            Box("Body", parent, new Vector3(0f, 0.22f, 0f), new Vector3(0.3f, 0.3f, 0.4f), feather);
            Box("Head", parent, new Vector3(0f, 0.45f, 0.12f), new Vector3(0.18f, 0.2f, 0.18f), feather);
            Box("Comb", parent, new Vector3(0f, 0.58f, 0.12f), new Vector3(0.07f, 0.1f, 0.13f), comb);
        }

        private static void BuildCow(Transform parent)
        {
            var hide = Mat("Cow_Hide", new Color(0.96f, 0.95f, 0.93f));
            var patch = Mat("Cow_Patch", new Color(0.24f, 0.22f, 0.22f));
            var udder = Mat("Cow_Udder", new Color(0.94f, 0.7f, 0.72f));

            Box("Body", parent, new Vector3(0f, 0.62f, 0f), new Vector3(0.55f, 0.5f, 1f), hide);
            Box("Patch", parent, new Vector3(0.01f, 0.72f, 0.15f), new Vector3(0.57f, 0.22f, 0.34f), patch);
            Box("Head", parent, new Vector3(0f, 0.72f, 0.66f), new Vector3(0.34f, 0.34f, 0.36f), hide);
            Box("Snout", parent, new Vector3(0f, 0.63f, 0.86f), new Vector3(0.24f, 0.18f, 0.12f), udder);
            Box("Udder", parent, new Vector3(0f, 0.34f, -0.22f), new Vector3(0.26f, 0.2f, 0.26f), udder);

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.2f : 0.2f;
                float z = (i < 2) ? 0.32f : -0.32f;
                Box($"Leg_{i}", parent, new Vector3(x, 0.18f, z), new Vector3(0.13f, 0.37f, 0.13f), patch);
            }
        }

        public static WorkerAgent BuildWorker(string name, Transform parent, Vector3 position,
            StationBase pickup, StationBase dropoff, Color color, double feePerDelivery,
            int capacity = 4, string role = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            // Sliding along a wall is not navigation - a worker pressed against the side of a
            // coop just stays there. A NavMeshAgent actually routes around buildings, and
            // handles workers avoiding each other for free.
            var nav = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
            nav.radius = 0.3f;
            nav.height = 1.3f;
            nav.baseOffset = 0f;
            nav.acceleration = 24f;
            nav.autoBraking = false;   // keeps the loop moving rather than easing in each time
            nav.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.3f;
            capsule.radius = 0.3f;
            capsule.center = new Vector3(0f, 0.65f, 0f);

            // The agent moves the transform; a kinematic rigidbody is what makes the station
            // trigger volumes fire for it.
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);

            // The model brings its own animator; the shirt is tinted so two workers on
            // the same route are still telling apart at a glance.
            var model = CharacterLibrary.Spawn(role ?? CharacterLibrary.Farmer, visual.transform, "Model");
            if (model != null) CharacterLibrary.Tint(model, CharacterLibrary.ShirtSlot, color);

            // Goods ride in front of the chest, where the carry animation puts the hands.
            // They used to float above the head, which was fine on a box and looks broken
            // the moment the character has arms and raises them.
            var anchor = new GameObject("CarryAnchor");
            anchor.transform.SetParent(visual.transform, false);
            anchor.transform.localPosition = CharacterLibrary.HandAnchor;

            var carry = go.AddComponent<CarryStack>();
            carry.anchor = anchor.transform;
            carry.capacity = capacity;

            var agent = go.AddComponent<WorkerAgent>();
            agent.pickup = pickup;
            agent.dropoff = dropoff;
            agent.visual = visual.transform;
            agent.feePerDelivery = feePerDelivery;

            return agent;
        }

        public static Workshop BuildWorkshop(
            string id, string name, Transform parent, Vector3 position,
            ItemDefinition input, ItemDefinition output,
            float secondsPerOutput, int inputCapacity, int outputCapacity,
            Color bodyColor, float wearPerOutput = 2f, int inputPerOutput = 1,
            int startUnits = 1, int maxUnits = 3, double unitPrice = 120d,
            string unitName = "Chicken", Livestock livestock = Livestock.Chicken)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            var body = Mat($"Body_{name}", bodyColor);
            var roof = Mat($"Roof_{name}", bodyColor * 0.65f);

            // Visuals sit on their own turned pivot so the squares below stay screen-aligned.
            var shell = new GameObject("Shell");
            shell.transform.SetParent(root.transform, false);
            shell.transform.localRotation = Quaternion.Euler(0f, BuildingYaw, 0f);

            // Solid: the player and workers must walk around a building, not through it.
            var bodyGo = Box("Body", shell.transform, new Vector3(0f, 0.9f, 0f),
                new Vector3(3.0f, 1.8f, 2.2f), body, collider: true);

            // Carving obstacle rather than baked geometry, so a coop that is unlocked later
            // cuts its own hole without the navmesh needing a rebake.
            var obstacle = bodyGo.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            obstacle.shape = UnityEngine.AI.NavMeshObstacleShape.Box;
            obstacle.size = Vector3.one;      // local space; the box is already scaled
            obstacle.carving = true;
            // The roof is left non-solid - it sits above head height and a collider up there
            // only creates invisible ledges to get caught on.
            Box("Roof", shell.transform, new Vector3(0f, 1.95f, 0f),
                new Vector3(3.4f, 0.3f, 2.6f), roof);

            // The machine and its durability both live on the root; the type suffix in the
            // save key keeps them apart.
            Identify(root, id);

            var kit = new Workshop { Root = root };

            kit.Input = Buffer($"{id}.input", "InputBuffer", root.transform, new Vector3(0f, 0.3f, 1.4f),
                input, inputCapacity);
            kit.Output = Buffer($"{id}.output", "OutputBuffer", root.transform, new Vector3(0f, 0.3f, -1.4f),
                output, outputCapacity);

            kit.Durability = root.AddComponent<Durability>();
            kit.Durability.wearPerOutput = wearPerOutput;

            kit.Machine = root.AddComponent<ProducerMachine>();
            kit.Machine.input = kit.Input;
            kit.Machine.output = kit.Output;
            kit.Machine.secondsPerOutput = secondsPerOutput;
            kit.Machine.inputPerOutput = inputPerOutput;
            kit.Machine.durability = kit.Durability;
            kit.Machine.maxUnits = Mathf.Max(1, maxUnits);
            kit.Machine.units = Mathf.Clamp(startUnits, 1, kit.Machine.maxUnits);
            kit.Machine.unitVisuals = BuildPen(root.transform, kit.Machine.maxUnits, livestock);

            // What the building holds, how many animals work it, and whether it has stopped
            // because the output is full. Kept low, just clear of the roof: the camera looks
            // across the farm, so a sign hung high is drawn over the ground well behind it.
            var sign = root.AddComponent<CapacitySign>();
            sign.input = kit.Input;
            sign.output = kit.Output;
            sign.machine = kit.Machine;
            sign.height = 2.65f;

            // Sits above the board, so the two read as one column of information rather than
            // competing for the same bit of sky. Hidden until the building starts wearing out.
            var marker = root.AddComponent<AttentionMarker>();
            marker.durability = kit.Durability;
            marker.height = 4.35f;

            // Squares are arranged along the screen's vertical axis, never side by side. A
            // portrait phone only shows about 7.8 world units across, so a building that puts
            // its input on the left and its output on the right runs off both edges at once.
            // Stacking them also gives the level a single downhill flow: harvest at the top,
            // feed, collect, sell at the bottom.
            // Wider than the building, and wider than they used to be. These two are the most
            // walked-into squares in the game, and they sit under a field 5.5 m across - so a
            // player who harvested the west end of the crop and walked straight down used to
            // miss the feed square entirely and land in the buy-a-chicken square beside it.
            // Paying for a chicken by accident, on the main loop, is exactly the kind of tax on
            // walking that the hire squares were moved off the fields to avoid.
            kit.Feed = Station<DepositStation>($"{id}.feed", "Feed", root.transform, new Vector3(0f, 0f, 2.8f),
                new Vector2(3.4f, 2.0f), "Feed", new Color(0.42f, 0.72f, 1f));
            kit.Feed.target = kit.Input;

            kit.Collect = Station<CollectStation>($"{id}.collect", "Collect", root.transform, new Vector3(0f, 0f, -2.8f),
                new Vector2(3.4f, 2.0f), "Collect", new Color(0.55f, 0.9f, 0.5f));
            kit.Collect.source = kit.Output;

            // Repair and buy sit together on the west side; the east belongs to the pen, which
            // is walkable and is the way round the building. Held a metre clear of the feed and
            // collect squares so that stepping into one of these is always a decision.
            kit.Repair = Station<RepairStation>($"{id}.repair", "Repair", root.transform,
                new Vector3(-3.9f, 0f, -1.6f),
                new Vector2(1.8f, 1.8f), "Fix", new Color(1f, 0.72f, 0.3f));
            kit.Repair.target = kit.Durability;

            kit.Upgrade = Station<UpgradeStation>($"{id}.upgrade", "BuyUnit", root.transform,
                new Vector3(-3.9f, 0f, 1.6f), new Vector2(1.8f, 1.8f),
                unitName, new Color(0.55f, 0.85f, 0.45f));
            kit.Upgrade.target = kit.Machine;
            kit.Upgrade.basePrice = unitPrice;
            kit.Upgrade.unitName = unitName;

            kit.Beacon = root.AddComponent<AlertBeacon>();
            kit.Beacon.businessName = name;
            kit.Beacon.machine = kit.Machine;
            kit.Beacon.durability = kit.Durability;
            kit.Beacon.inputBuffer = kit.Input;
            kit.Beacon.outputBuffer = kit.Output;
            // Where the task list's arrow sends the player for each kind of alert.
            kit.Beacon.repairSquare = kit.Repair;
            kit.Beacon.feedSquare = kit.Feed;
            kit.Beacon.collectSquare = kit.Collect;

            return kit;
        }

        /// <summary>
        /// The bin: somewhere to put down goods that have nowhere else to go.
        ///
        /// A physical place rather than a button, like everything else here. The player walks
        /// to it and stands in it, and what they are carrying goes in the rubbish - which is
        /// the whole safety net for the one piece of state that could otherwise strand them.
        /// See <see cref="DiscardStation"/> for why that matters.
        /// </summary>
        public static DiscardStation BuildBin(string id, string name, Transform parent, Vector3 position,
            bool squareToSouth = false)
        {
            // The square normally lies on the north side of the can, which is the far side from
            // the camera. A bin that has to stand NORTH of the ground its square needs (the
            // market's, on the forecourt outside the shop wall) mirrors itself instead: the
            // can, its lid and its pad turn round and the square goes to the south.
            float side = squareToSouth ? -1f : 1f;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            var pad = Mat("Bin_Pad", new Color(0.78f, 0.77f, 0.73f));

            // Turned with the buildings so it sits in the level like everything else, while
            // the square underneath stays screen-aligned.
            var shell = new GameObject("Shell");
            shell.transform.SetParent(root.transform, false);
            shell.transform.localRotation = Quaternion.Euler(0f, BuildingYaw + (squareToSouth ? 180f : 0f), 0f);

            // A slab under the bin AND under the square in front of it, so the two read as one
            // fixture and the square is painted on paving rather than on grass. Parented to the
            // unturned root, not the shell, so it stays lined up with the square whatever angle
            // the bin itself is set at.
            Box("Pad", root.transform, new Vector3(0f, 0.03f, 0.9f * side),
                new Vector3(2.0f, 0.06f, 3.3f), pad, castShadow: false);

            // A proper tapered can rather than a stack of boxes: narrow at the foot, wide at
            // the mouth, lid leaning against the back. Eight sided, so the facets themselves
            // read as the ribs down a metal bin without costing any extra geometry.
            //
            // Deliberately not solid. A bin the player can be stopped by is a bin they can be
            // wedged against while trying to empty their arms into it.
            var can = new GameObject("Can");
            can.transform.SetParent(shell.transform, false);
            can.AddComponent<MeshFilter>().sharedMesh = BinMesh();

            var canRenderer = can.AddComponent<MeshRenderer>();
            canRenderer.sharedMaterial = Mat("Bin_Body", new Color(0.30f, 0.56f, 0.49f),
                smoothness: 0.35f);

            Identify(root, id);

            // Smaller than any other square on the farm, and smaller than the minimum the rest
            // are drawn at - the one place where being easy to step into is a fault rather than
            // a feature. Everything else here gives something back; this one destroys what you
            // are carrying, so brushing past it on the way to the till has to be impossible
            // rather than merely unlikely. The player has to mean it.
            //
            // The drawn card is pulled down to match, so the dashes are still exactly the
            // ground the bin works from. "BIN" is one short word and needs no number, which is
            // what lets this square afford to be small.
            var zone = new Vector2(1.7f, 1.5f);

            var station = Station<DiscardStation>($"{id}.discard", "Discard", root.transform,
                new Vector3(0f, 0f, 1.7f * side), zone, "Bin",
                new Color(0.72f, 0.76f, 0.82f), smallestCard: zone);

            // Fast, once you are actually in it. This is a way out of a mistake, not a chore -
            // nobody should stand in the bin for four seconds paying for having picked up the
            // wrong thing.
            station.tickInterval = 0.09f;

            return station;
        }

        private static Mesh _binMesh;

        /// <summary>
        /// The waste bin's geometry, built once and shared by every bin on the map.
        ///
        /// A tapered eight-sided can, a dark mouth so the top reads as open rather than shut,
        /// and a lid resting against the back of it. Generated rather than assembled out of
        /// primitives because a Unity cylinder cannot taper, and a stack of boxes is exactly
        /// what this replaced - it read as a crate rather than as a bin.
        /// </summary>
        private static Mesh BinMesh()
        {
            if (_binMesh != null) return _binMesh;

            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            var colours = new System.Collections.Generic.List<Color>();

            var foot = new Color(0.66f, 0.74f, 0.72f);
            var mouth = new Color(1.05f, 1.08f, 1.05f);
            var lid = new Color(0.82f, 0.90f, 0.88f);
            var hole = new Color(0.14f, 0.20f, 0.19f);

            // Body: narrow foot, wide mouth.
            AddFrustum(vertices, triangles, colours, Vector3.zero,
                0.40f, 0.56f, 1.18f, 8, foot, mouth, false, 0f);

            // The rim the lid drops onto, with the dark opening just inside it. The opening is
            // barely visible under the lid, but it is what stops the top reading as a solid
            // block when the lid is seen edge-on from across the farm.
            AddFrustum(vertices, triangles, colours, new Vector3(0f, 1.18f, 0f),
                0.60f, 0.58f, 0.10f, 8, lid, lid, false, 0f);

            AddFrustum(vertices, triangles, colours, new Vector3(0f, 1.19f, 0f),
                0.50f, 0.50f, 0.02f, 8, hole, hole, true, 0f);

            // Lid sitting squarely on top with a handle, the way the reference drawing has it.
            // Set down rather than leaning: a lid propped against the back read as a second
            // slab lying on the paving unless you were looking straight at it.
            AddFrustum(vertices, triangles, colours, new Vector3(0f, 1.28f, 0f),
                0.62f, 0.52f, 0.13f, 8, lid, lid, true, 0f);

            AddFrustum(vertices, triangles, colours, new Vector3(0f, 1.41f, 0f),
                0.12f, 0.10f, 0.11f, 8, lid, mouth, true, 0f);

            var mesh = new Mesh { name = "TrashCan" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colours);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            EnsureFolder("Assets/_Project/Meshes");
            const string meshPath = "Assets/_Project/Meshes/TrashCan.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            Debug.Log($"[LevelBuildKit] TrashCan mesh: {vertices.Count} verts, " +
                      $"{triangles.Count / 3} tris, bounds {mesh.bounds.size}");

            _binMesh = mesh;
            return mesh;
        }

        /// <summary>
        /// Appends a tapered prism - a cylinder whose two ends differ in radius - to a mesh
        /// being built up. Every face gets its own four vertices so the facets stay crisp when
        /// normals are recalculated, which is what makes eight sides read as a ribbed metal
        /// bin rather than as a smooth tube.
        /// </summary>
        private static void AddFrustum(
            System.Collections.Generic.List<Vector3> vertices,
            System.Collections.Generic.List<int> triangles,
            System.Collections.Generic.List<Color> colours,
            Vector3 origin, float bottomRadius, float topRadius, float height, int sides,
            Color bottom, Color top, bool capTop, float tilt)
        {
            var turn = Quaternion.Euler(tilt, 0f, 0f);

            Vector3 At(float radius, float y, int i)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                var local = new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius);
                return origin + turn * local;
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;

                int v = vertices.Count;
                vertices.Add(At(bottomRadius, 0f, i));
                vertices.Add(At(bottomRadius, 0f, next));
                vertices.Add(At(topRadius, height, next));
                vertices.Add(At(topRadius, height, i));
                colours.Add(bottom); colours.Add(bottom); colours.Add(top); colours.Add(top);

                triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
                triangles.Add(v); triangles.Add(v + 3); triangles.Add(v + 2);
            }

            if (!capTop) return;

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int v = vertices.Count;
                vertices.Add(origin + turn * new Vector3(0f, height, 0f));
                vertices.Add(At(topRadius, height, i));
                vertices.Add(At(topRadius, height, next));
                colours.Add(top); colours.Add(top); colours.Add(top);

                triangles.Add(v); triangles.Add(v + 2); triangles.Add(v + 1);
            }
        }

        /// <summary>The shared street a market's counters face onto.</summary>
        public class Shopfront
        {
            public GameObject Root;

            /// <summary>Half the length of the tarmac itself, which runs off past the farm.</summary>
            public float RoadHalfLength;

            /// <summary>
            /// Half the length shoppers actually use. Shorter than the road on purpose: the
            /// tarmac carries on out of the level so the player never sees it stop, but a
            /// shopper spawned out there would be beyond the boundary and off the navmesh.
            /// </summary>
            public float WalkHalfLength;
        }

        /// <summary>One till, with its own queue of shoppers.</summary>
        public class Counter
        {
            public GameObject Root;
            public RegisterStation Register;
            public Tycoon.Customers.CustomerQueue Queue;
        }

        /// <summary>
        /// The street: tarmac, markings, and nothing else. Counters are added onto it
        /// separately, so a market can grow a second till without a second road appearing.
        /// </summary>
        public static Shopfront BuildShopfront(string id, string name, Transform parent,
            Vector3 position, float roadHalfLength = 17f, float walkHalfLength = 0f)
        {
            if (walkHalfLength <= 0f) walkHalfLength = roadHalfLength;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            var tarmac = Mat("Road", new Color(0.36f, 0.36f, 0.39f));
            var markings = Mat("RoadLine", new Color(0.88f, 0.88f, 0.8f));

            Box("Road", root.transform, new Vector3(0f, 0.02f, -4.6f),
                new Vector3(roadHalfLength * 2f, 0.04f, 2.8f), tarmac, castShadow: false);

            // Dashes down the middle so it reads as a road rather than a grey strip.
            int dashes = Mathf.RoundToInt(roadHalfLength);
            for (int i = -dashes; i <= dashes; i++)
            {
                Box($"Line_{i + dashes}", root.transform,
                    new Vector3(i * 2f, 0.05f, -4.6f), new Vector3(0.9f, 0.04f, 0.14f), markings,
                    castShadow: false);
            }

            return new Shopfront
            {
                Root = root,
                RoadHalfLength = roadHalfLength,
                WalkHalfLength = walkHalfLength
            };
        }

        /// <summary>
        /// Adds a till to a street: stall, serving square, queue positions and its own stream
        /// of shoppers.
        ///
        /// Each counter gets its own queue rather than sharing one, so a second till genuinely
        /// doubles how many people the market can serve instead of just giving the existing
        /// queue somewhere else to stand.
        /// </summary>
        /// <param name="sells">
        /// What this till deals in. This is the whole of the customer routing: a shopper is only
        /// ever created for something their own counter can hand over, so a milk shopper can
        /// never end up queueing at a counter that only does eggs. Give a future till
        /// <c>{ milk }</c> and milk shoppers start using it with no other change.
        /// </param>
        public static Counter AddCounter(Shopfront shop, string id, string name, float localX,
            ItemDefinition[] sells, int queueLength = 3, bool enterFromWest = true)
        {
            var root = new GameObject(name);
            root.transform.SetParent(shop.Root.transform, false);
            root.transform.localPosition = new Vector3(localX, 0f, 0f);

            Identify(root, id);

            var register = Station<RegisterStation>($"{id}.register", "Register", root.transform,
                Vector3.zero, new Vector2(3.6f, 2.2f), "Serve", new Color(0.45f, 0.85f, 0.6f));
            register.sells = sells;

            BuildStall(root.transform, new Vector3(0f, 0f, -1.6f));

            var counterPoint = new GameObject("CounterPoint");
            counterPoint.transform.SetParent(root.transform, false);
            counterPoint.transform.localPosition = new Vector3(0f, 0f, -1.9f);

            // Shoppers for each till arrive from opposite ends of the street, so two queues do
            // not tangle up walking through one another.
            float inX = (enterFromWest ? -shop.WalkHalfLength : shop.WalkHalfLength) - localX;
            float outX = (enterFromWest ? shop.WalkHalfLength : -shop.WalkHalfLength) - localX;

            var spawnPoint = new GameObject("SpawnPoint");
            spawnPoint.transform.SetParent(root.transform, false);
            spawnPoint.transform.localPosition = new Vector3(inX, 0f, -4.6f);

            var exitPoint = new GameObject("ExitPoint");
            exitPoint.transform.SetParent(root.transform, false);
            exitPoint.transform.localPosition = new Vector3(outX, 0f, -4.6f);

            var slots = new Transform[Mathf.Max(1, queueLength)];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(root.transform, false);
                // The queue trails back the way its shoppers came in. Spaced wider than the
                // shoppers need, because what actually has to fit side by side is their order
                // bubbles - at 1.5 m apart the bubbles overlapped and hid each other's counts.
                slot.transform.localPosition =
                    new Vector3((enterFromWest ? -2.2f : 2.2f) * i, 0f, -3.4f);
                slots[i] = slot.transform;
            }

            var template = BuildCustomerTemplate(root.transform);

            var queue = root.AddComponent<Tycoon.Customers.CustomerQueue>();
            queue.customerTemplate = template;
            queue.spawnPoint = spawnPoint.transform;
            queue.exitPoint = exitPoint.transform;
            queue.counterPoint = counterPoint.transform;
            queue.slots = slots;

            // The queue asks the register what it may sell, rather than carrying its own copy
            // of the menu. One list, on the thing that actually hands the goods over.
            queue.register = register;
            register.queue = queue;

            return new Counter { Root = root, Register = register, Queue = queue };
        }

        private static void BuildStall(Transform parent, Vector3 position)
        {
            var stall = new GameObject("Stall");
            stall.transform.SetParent(parent, false);
            stall.transform.localPosition = position;

            var wood = Mat("Market_Wood", new Color(0.55f, 0.38f, 0.24f));
            var awning = Mat("Market_Awning", new Color(0.9f, 0.35f, 0.35f));

            // Solid so the player serves from behind the counter rather than standing in it.
            Box("Counter", stall.transform, new Vector3(0f, 0.5f, 0f),
                new Vector3(3.4f, 1f, 0.5f), wood, collider: true);
            // Posts, and nothing spanning between them.
            //
            // The camera looks at the market from the road, so anything standing on the counter
            // is drawn over the ground behind it - and the ground behind the counter is exactly
            // where the serving square lies. The geometry is unforgiving: at this camera angle
            // anything above about knee height on the stall lands somewhere on that card, so a
            // canopy blotted out the whole thing and even a thin crossbar struck straight
            // through the word SERVE.
            //
            // The posts are pushed out past the card's edges instead, where they frame it rather
            // than cross it. A flag on each one gives the stall its colour back.
            Box("PostL", stall.transform, new Vector3(-1.72f, 1.1f, 0f),
                new Vector3(0.16f, 2.2f, 0.16f), wood, castShadow: false);
            Box("PostR", stall.transform, new Vector3(1.72f, 1.1f, 0f),
                new Vector3(0.16f, 2.2f, 0.16f), wood, castShadow: false);
            Box("FlagL", stall.transform, new Vector3(-1.72f, 2.1f, -0.26f),
                new Vector3(0.14f, 0.5f, 0.42f), awning, castShadow: false);
            Box("FlagR", stall.transform, new Vector3(1.72f, 2.1f, -0.26f),
                new Vector3(0.14f, 0.5f, 0.42f), awning, castShadow: false);
        }

        /// <summary>
        /// The shopper the queue clones. Kept inactive in the scene so its materials are real
        /// asset references - a customer built from scratch at runtime would render magenta.
        /// </summary>
        public static GameObject BuildCustomerTemplate(Transform parent)
        {
            var go = new GameObject("CustomerTemplate");
            go.transform.SetParent(parent, false);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);

            var model = CharacterLibrary.Spawn(CharacterLibrary.Customer, visual.transform, "Model");

            var bubble = go.AddComponent<OrderBubble>();
            var agent = go.AddComponent<Tycoon.Customers.CustomerAgent>();
            agent.visual = visual.transform;
            // Only the shirt gets recoloured per shopper. Tinting the whole renderer
            // would wash the skin and boots through the same colour as well.
            agent.tintTarget = model != null ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            agent.tintMaterialIndex = CharacterLibrary.ShirtSlot;
            agent.bubble = bubble;

            go.SetActive(false);
            return go;
        }

        /// <summary>What a field looks like. Purely visual; the harvesting is identical.</summary>
        public enum FieldStyle
        {
            /// <summary>Tall stalks with a cob on top. Corn.</summary>
            Cereal,

            /// <summary>Low golden clumps on dry stubble, with bales. Hay.</summary>
            Meadow
        }

        /// <summary>A crop field the player harvests by walking through it.</summary>
        public static HarvestStation BuildField(string id, string name, Transform parent, Vector3 position,
            ItemDefinition crop, int plots, float regrowSeconds, Vector2 size,
            FieldStyle style = FieldStyle.Cereal)
        {
            var station = Station<HarvestStation>(id, name, parent, position, size,
                "Harvest", new Color(0.95f, 0.83f, 0.35f));
            station.crop = crop;
            station.plots = plots;
            station.regrowSeconds = regrowSeconds;

            bool meadow = style == FieldStyle.Meadow;

            // Ground under the plot. Dark tilled soil for a sown crop, pale dry stubble for a
            // meadow - two fields side by side have to be tellable apart from the ground up,
            // not just by the colour of whatever is growing on them.
            Box("Soil", station.transform, new Vector3(0f, 0.02f, 0f),
                new Vector3(size.x, 0.04f, size.y),
                Mat($"Field_Soil_{crop.id}",
                    meadow ? new Color(0.68f, 0.60f, 0.34f) : new Color(0.46f, 0.34f, 0.22f)),
                castShadow: false);

            if (meadow)
            {
                // Mown stripes, the way a cut meadow actually looks, running the other way
                // from the corn's furrows so even the ground pattern differs.
                var mown = Mat("Field_Mown", new Color(0.60f, 0.53f, 0.29f));
                int stripes = Mathf.Max(3, Mathf.RoundToInt(size.x / 0.9f));
                for (int i = 0; i < stripes; i += 2)
                {
                    float x = Mathf.Lerp(-size.x * 0.42f, size.x * 0.42f, i / (float)(stripes - 1));
                    Box($"Mown_{i}", station.transform, new Vector3(x, 0.045f, 0f),
                        new Vector3(size.x / stripes * 0.9f, 0.02f, size.y * 0.92f), mown,
                        castShadow: false);
                }
            }
            else
            {
                // Furrows, so the soil is not one flat slab of brown. Kept close to the soil's
                // own colour and thin: at higher contrast and any wider they stop reading as
                // tilled rows and start reading as decking.
                var furrow = Mat("Field_Furrow", new Color(0.4f, 0.29f, 0.18f));
                int furrows = Mathf.Max(3, Mathf.RoundToInt(size.y / 0.5f));
                for (int i = 0; i < furrows; i++)
                {
                    float z = Mathf.Lerp(-size.y * 0.4f, size.y * 0.4f, i / (float)(furrows - 1));
                    Box($"Furrow_{i}", station.transform, new Vector3(0f, 0.045f, z),
                        new Vector3(size.x * 0.94f, 0.02f, 0.06f), furrow, castShadow: false);
                }
            }

            var visuals = new GameObject("Crops");
            visuals.transform.SetParent(station.transform, false);
            station.cropVisuals = visuals.transform;

            var stalk = Mat($"Stalk_{crop.id}",
                meadow ? new Color(0.78f, 0.66f, 0.36f) : new Color(0.35f, 0.62f, 0.28f));
            var head = Mat($"Head_{crop.id}", crop.color);

            // Lay the plots out in a grid that fills the square, so the field visibly empties
            // as it is harvested and visibly fills back up as it regrows.
            int columns = Mathf.CeilToInt(Mathf.Sqrt(plots));
            int rows = Mathf.CeilToInt(plots / (float)columns);

            for (int i = 0; i < plots; i++)
            {
                int cx = i % columns;
                int cz = i / columns;
                float x = Mathf.Lerp(-size.x * 0.40f, size.x * 0.40f, columns <= 1 ? 0.5f : cx / (float)(columns - 1));
                float z = Mathf.Lerp(-size.y * 0.40f, size.y * 0.40f, rows <= 1 ? 0.5f : cz / (float)(rows - 1));

                var plot = new GameObject($"Plot_{i}");
                plot.transform.SetParent(visuals.transform, false);
                plot.transform.localPosition = new Vector3(x, 0f, z);

                if (meadow)
                {
                    // A low fan of blades and a rolled bale - knee height, not head height.
                    // Corn is a forest of vertical stalks; this has to read as the opposite.
                    for (int blade = 0; blade < 5; blade++)
                    {
                        float lean = (blade - 2) * 11f;
                        Box($"Blade_{blade}", plot.transform,
                            new Vector3((blade - 2) * 0.07f, 0.17f, 0f),
                            new Vector3(0.07f, 0.34f, 0.07f), head,
                            rot: new Vector3(0f, (blade * 37) % 180, lean));
                    }

                    Cylinder("Bale", plot.transform, new Vector3(0.12f, 0.17f, 0.1f),
                        new Vector3(0.34f, 0.16f, 0.34f), stalk, castShadow: false)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                else
                {
                    Cylinder("Stalk", plot.transform, new Vector3(0f, 0.45f, 0f),
                        new Vector3(0.12f, 0.45f, 0.12f), stalk, castShadow: false);
                    Box("Head", plot.transform, new Vector3(0f, 1f, 0f),
                        new Vector3(0.3f, 0.42f, 0.3f), head, castShadow: false);
                }
            }

            return station;
        }
    }
}
