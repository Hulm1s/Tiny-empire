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

        public static GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var box = go.GetComponent<Collider>();
            if (!collider && box != null) Object.DestroyImmediate(box);

            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 scale,
            Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;

            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            go.GetComponent<Renderer>().sharedMaterial = material;
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

        // ---------------------------------------------------------------- stations

        /// <summary>
        /// Creates a station: trigger volume, floor decal and floating sign, all sized together.
        /// This is the single call every interaction in the game is built from.
        /// </summary>
        public static T Station<T>(string id, string name, Transform parent, Vector3 position,
            Vector2 size, string label, Color color) where T : StationBase
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            Identify(go, id);

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, 2.5f, size.y);
            trigger.center = new Vector3(0f, 1.25f, 0f);

            var station = go.AddComponent<T>();
            station.label = label;
            station.zoneColor = color;

            // The square replaces the old flat slab. It is the game's main way of talking to
            // the player, so it carries the label, the "you are standing here" state and the
            // progress ring all in one.
            var square = go.AddComponent<InteractionSquare>();
            square.Configure(station, size, color);

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

        /// <summary>
        /// The chickens themselves, one object per possible bird. The machine shows the first
        /// N, so buying one makes a chicken visibly appear in the yard rather than just
        /// changing a number.
        /// </summary>
        private static Transform BuildFlock(Transform parent, int maxUnits)
        {
            var holder = new GameObject("Flock");
            holder.transform.SetParent(parent, false);

            var feather = Mat("Chicken_Body", new Color(0.97f, 0.96f, 0.92f));
            var comb = Mat("Chicken_Comb", new Color(0.9f, 0.32f, 0.28f));

            for (int i = 0; i < maxUnits; i++)
            {
                var bird = new GameObject($"Chicken_{i}");
                bird.transform.SetParent(holder.transform, false);

                float x = maxUnits <= 1 ? 0f : Mathf.Lerp(-1.05f, 1.05f, i / (float)(maxUnits - 1));
                bird.transform.localPosition = new Vector3(x, 0f, -1.45f);
                // A little scatter so a row of birds does not look stamped out.
                bird.transform.localRotation = Quaternion.Euler(0f, (i * 47) % 360, 0f);

                Box("Body", bird.transform, new Vector3(0f, 0.22f, 0f),
                    new Vector3(0.3f, 0.3f, 0.4f), feather);
                Box("Head", bird.transform, new Vector3(0f, 0.45f, 0.12f),
                    new Vector3(0.18f, 0.2f, 0.18f), feather);
                Box("Comb", bird.transform, new Vector3(0f, 0.58f, 0.12f),
                    new Vector3(0.07f, 0.1f, 0.13f), comb);
            }

            return holder.transform;
        }

        /// <summary>
        /// A hired hand that shuttles goods between two squares for a wage.
        /// It walks into station triggers exactly like the player, so it needs no cooperation
        /// from the stations themselves.
        /// </summary>
        public static WorkerAgent BuildWorker(string name, Transform parent, Vector3 position,
            StationBase pickup, StationBase dropoff, Color color, double feePerDelivery,
            int capacity = 4)
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

            Box("Body", visual.transform, new Vector3(0f, 0.45f, 0f),
                new Vector3(0.5f, 0.66f, 0.38f), Mat($"Worker_{name}", color));
            Box("Head", visual.transform, new Vector3(0f, 0.98f, 0f),
                new Vector3(0.42f, 0.42f, 0.42f), Mat("Player_Head", new Color(0.98f, 0.82f, 0.68f)));

            var anchor = new GameObject("CarryAnchor");
            anchor.transform.SetParent(visual.transform, false);
            anchor.transform.localPosition = new Vector3(0f, 1.3f, 0f);

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
            string unitName = "Chicken")
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
            kit.Machine.unitVisuals = BuildFlock(root.transform, kit.Machine.maxUnits);

            var machineSign = root.AddComponent<InfoSign>();
            machineSign.machine = kit.Machine;
            machineSign.durability = kit.Durability;
            machineSign.height = 3.5f;

            // Squares are arranged along the screen's vertical axis, never side by side. A
            // portrait phone only shows about 7.8 world units across, so a building that puts
            // its input on the left and its output on the right runs off both edges at once.
            // Stacking them also gives the level a single downhill flow: harvest at the top,
            // feed, collect, sell at the bottom.
            kit.Feed = Station<DepositStation>($"{id}.feed", "Feed", root.transform, new Vector3(0f, 0f, 2.8f),
                new Vector2(2.6f, 2.0f), "Feed", new Color(0.42f, 0.72f, 1f));
            kit.Feed.target = kit.Input;

            kit.Collect = Station<CollectStation>($"{id}.collect", "Collect", root.transform, new Vector3(0f, 0f, -2.8f),
                new Vector2(2.6f, 2.0f), output.displayName, new Color(0.55f, 0.9f, 0.5f));
            kit.Collect.source = kit.Output;

            kit.Repair = Station<RepairStation>($"{id}.repair", "Repair", root.transform, new Vector3(-3.1f, 0f, 0f),
                new Vector2(1.8f, 2.0f), "Fix", new Color(1f, 0.72f, 0.3f));
            kit.Repair.target = kit.Durability;

            // Buying capacity sits opposite the repair square, so the two things you spend
            // money on at a building are on either side of it.
            kit.Upgrade = Station<UpgradeStation>($"{id}.upgrade", "BuyUnit", root.transform,
                new Vector3(3.1f, 0f, 0f), new Vector2(1.8f, 2.0f),
                $"+1 {unitName}", new Color(0.55f, 0.85f, 0.45f));
            kit.Upgrade.target = kit.Machine;
            kit.Upgrade.basePrice = unitPrice;
            kit.Upgrade.unitName = unitName;

            kit.Beacon = root.AddComponent<AlertBeacon>();
            kit.Beacon.businessName = name;
            kit.Beacon.machine = kit.Machine;
            kit.Beacon.durability = kit.Durability;
            kit.Beacon.inputBuffer = kit.Input;
            kit.Beacon.outputBuffer = kit.Output;

            return kit;
        }

        /// <summary>
        /// The customer-facing half of a business: till, stall, queue, and the road shoppers
        /// walk in along. Laid out relative to one position so a second shop is one more call.
        /// </summary>
        public class Shopfront
        {
            public GameObject Root;
            public RegisterStation Register;
            public Tycoon.Customers.CustomerQueue Queue;
        }

        public static Shopfront BuildShopfront(string id, string name, Transform parent, Vector3 position,
            ItemDefinition[] catalogue, int queueLength = 3, float roadHalfLength = 14f)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            // --- the road, running across the screen behind the counter ------------------
            var tarmac = Mat("Road", new Color(0.36f, 0.36f, 0.39f));
            var markings = Mat("RoadLine", new Color(0.88f, 0.88f, 0.8f));

            Box("Road", root.transform, new Vector3(0f, 0.02f, -4.6f),
                new Vector3(roadHalfLength * 2f, 0.04f, 2.8f), tarmac);

            // Dashes down the middle so it reads as a road rather than a grey strip.
            int dashes = Mathf.RoundToInt(roadHalfLength);
            for (int i = -dashes; i <= dashes; i++)
            {
                Box($"Line_{i + dashes}", root.transform,
                    new Vector3(i * 2f, 0.05f, -4.6f), new Vector3(0.9f, 0.04f, 0.14f), markings);
            }

            // --- the till the player stands at -------------------------------------------
            Identify(root, id);

            var register = Station<RegisterStation>($"{id}.register", "Register", root.transform, Vector3.zero,
                new Vector2(4f, 2.2f), "Serve", new Color(0.45f, 0.85f, 0.6f));

            BuildStall(root.transform, new Vector3(0f, 0f, -1.6f));

            // --- queue furniture ----------------------------------------------------------
            var counterPoint = new GameObject("CounterPoint");
            counterPoint.transform.SetParent(root.transform, false);
            counterPoint.transform.localPosition = new Vector3(0f, 0f, -1.9f);

            var spawnPoint = new GameObject("SpawnPoint");
            spawnPoint.transform.SetParent(root.transform, false);
            spawnPoint.transform.localPosition = new Vector3(-roadHalfLength, 0f, -4.6f);

            var exitPoint = new GameObject("ExitPoint");
            exitPoint.transform.SetParent(root.transform, false);
            exitPoint.transform.localPosition = new Vector3(roadHalfLength, 0f, -4.6f);

            var slots = new Transform[Mathf.Max(1, queueLength)];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = new GameObject($"Slot_{i}");
                slot.transform.SetParent(root.transform, false);
                // Queue trails away to the left along the road.
                slot.transform.localPosition = new Vector3(-1.5f * i, 0f, -3.4f);
                slots[i] = slot.transform;
            }

            var template = BuildCustomerTemplate(root.transform);

            var queue = root.AddComponent<Tycoon.Customers.CustomerQueue>();
            queue.customerTemplate = template;
            queue.spawnPoint = spawnPoint.transform;
            queue.exitPoint = exitPoint.transform;
            queue.counterPoint = counterPoint.transform;
            queue.slots = slots;
            queue.catalogue = catalogue;

            register.queue = queue;

            return new Shopfront { Root = root, Register = register, Queue = queue };
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
                new Vector3(4f, 1f, 0.5f), wood, collider: true);
            Box("PostL", stall.transform, new Vector3(-1.8f, 1.1f, 0f),
                new Vector3(0.16f, 2.2f, 0.16f), wood);
            Box("PostR", stall.transform, new Vector3(1.8f, 1.1f, 0f),
                new Vector3(0.16f, 2.2f, 0.16f), wood);
            Box("Awning", stall.transform, new Vector3(0f, 2.2f, -0.3f),
                new Vector3(4.2f, 0.18f, 1.4f), awning);
        }

        /// <summary>
        /// The shopper the queue clones. Kept inactive in the scene so its materials are real
        /// asset references - a customer built from scratch at runtime would render magenta.
        /// </summary>
        private static GameObject BuildCustomerTemplate(Transform parent)
        {
            var go = new GameObject("CustomerTemplate");
            go.transform.SetParent(parent, false);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);

            var bodyGo = Box("Body", visual.transform, new Vector3(0f, 0.45f, 0f),
                new Vector3(0.48f, 0.66f, 0.36f), Mat("Customer_Body", Color.white));
            Box("Head", visual.transform, new Vector3(0f, 0.98f, 0f),
                new Vector3(0.4f, 0.4f, 0.4f), Mat("Player_Head", new Color(0.98f, 0.82f, 0.68f)));

            var bubble = go.AddComponent<OrderBubble>();
            var agent = go.AddComponent<Tycoon.Customers.CustomerAgent>();
            agent.visual = visual.transform;
            agent.tintTarget = bodyGo.GetComponent<Renderer>();
            agent.bubble = bubble;

            go.SetActive(false);
            return go;
        }

        /// <summary>A crop field the player harvests by walking through it.</summary>
        public static HarvestStation BuildField(string id, string name, Transform parent, Vector3 position,
            ItemDefinition crop, int plots, float regrowSeconds, Vector2 size)
        {
            var station = Station<HarvestStation>(id, name, parent, position, size,
                crop.displayName, new Color(0.95f, 0.83f, 0.35f));
            station.crop = crop;
            station.plots = plots;
            station.regrowSeconds = regrowSeconds;

            var visuals = new GameObject("Crops");
            visuals.transform.SetParent(station.transform, false);
            station.cropVisuals = visuals.transform;

            var stalk = Mat($"Stalk_{crop.id}", new Color(0.35f, 0.62f, 0.28f));
            var head = Mat($"Head_{crop.id}", crop.color);

            // Lay the plots out in a grid that fills the square, so the field visibly empties
            // as it is harvested and visibly fills back up as it regrows.
            int columns = Mathf.CeilToInt(Mathf.Sqrt(plots));
            int rows = Mathf.CeilToInt(plots / (float)columns);

            for (int i = 0; i < plots; i++)
            {
                int cx = i % columns;
                int cz = i / columns;
                float x = Mathf.Lerp(-size.x * 0.34f, size.x * 0.34f, columns <= 1 ? 0.5f : cx / (float)(columns - 1));
                float z = Mathf.Lerp(-size.y * 0.34f, size.y * 0.34f, rows <= 1 ? 0.5f : cz / (float)(rows - 1));

                var plot = new GameObject($"Plot_{i}");
                plot.transform.SetParent(visuals.transform, false);
                plot.transform.localPosition = new Vector3(x, 0f, z);

                Cylinder("Stalk", plot.transform, new Vector3(0f, 0.45f, 0f),
                    new Vector3(0.12f, 0.45f, 0.12f), stalk);
                Box("Head", plot.transform, new Vector3(0f, 1f, 0f),
                    new Vector3(0.3f, 0.42f, 0.3f), head);
            }

            return station;
        }
    }
}
