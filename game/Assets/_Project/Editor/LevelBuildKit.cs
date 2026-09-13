using System.IO;
using Tycoon.Config;
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
        public static T Station<T>(string name, Transform parent, Vector3 position, Vector2 size,
            string label, Color color) where T : StationBase
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, 2.5f, size.y);
            trigger.center = new Vector3(0f, 1.25f, 0f);

            var station = go.AddComponent<T>();
            station.label = label;
            station.zoneColor = color;

            Decal(go.transform, size, color, $"Zone_{name}");

            var sign = go.AddComponent<InfoSign>();
            sign.station = station;
            // Station signs sit low and machine signs sit high, so a building's own status
            // never collides with the labels of the squares around it.
            sign.height = 1.5f;

            return station;
        }

        public static ItemBuffer Buffer(string name, Transform parent, Vector3 position,
            ItemDefinition item, int capacity, int starting = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;

            var buffer = go.AddComponent<ItemBuffer>();
            buffer.item = item;
            buffer.capacity = capacity;

            var so = new SerializedObject(buffer);
            so.FindProperty("startingCount").intValue = starting;
            so.ApplyModifiedPropertiesWithoutUndo();

            return buffer;
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
        }

        public static Workshop BuildWorkshop(
            string name, Transform parent, Vector3 position,
            ItemDefinition input, ItemDefinition output,
            float secondsPerOutput, int inputCapacity, int outputCapacity,
            Color bodyColor, float wearPerOutput = 2f, int inputPerOutput = 1)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            var body = Mat($"Body_{name}", bodyColor);
            var roof = Mat($"Roof_{name}", bodyColor * 0.65f);

            Box("Body", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(3.0f, 1.8f, 2.2f), body);
            Box("Roof", root.transform, new Vector3(0f, 1.95f, 0f), new Vector3(3.4f, 0.3f, 2.6f), roof);

            var kit = new Workshop { Root = root };

            kit.Input = Buffer("InputBuffer", root.transform, new Vector3(0f, 0.3f, 1.4f),
                input, inputCapacity);
            kit.Output = Buffer("OutputBuffer", root.transform, new Vector3(0f, 0.3f, -1.4f),
                output, outputCapacity);

            kit.Durability = root.AddComponent<Durability>();
            kit.Durability.wearPerOutput = wearPerOutput;

            kit.Machine = root.AddComponent<ProducerMachine>();
            kit.Machine.input = kit.Input;
            kit.Machine.output = kit.Output;
            kit.Machine.secondsPerOutput = secondsPerOutput;
            kit.Machine.inputPerOutput = inputPerOutput;
            kit.Machine.durability = kit.Durability;

            var machineSign = root.AddComponent<InfoSign>();
            machineSign.machine = kit.Machine;
            machineSign.durability = kit.Durability;
            machineSign.height = 3.5f;

            // Squares are arranged along the screen's vertical axis, never side by side. A
            // portrait phone only shows about 7.8 world units across, so a building that puts
            // its input on the left and its output on the right runs off both edges at once.
            // Stacking them also gives the level a single downhill flow: harvest at the top,
            // feed, collect, sell at the bottom.
            kit.Feed = Station<DepositStation>("Feed", root.transform, new Vector3(0f, 0f, 2.5f),
                new Vector2(2.6f, 2.0f), "Feed", new Color(0.42f, 0.72f, 1f));
            kit.Feed.target = kit.Input;

            kit.Collect = Station<CollectStation>("Collect", root.transform, new Vector3(0f, 0f, -2.5f),
                new Vector2(2.6f, 2.0f), output.displayName, new Color(0.55f, 0.9f, 0.5f));
            kit.Collect.source = kit.Output;

            kit.Repair = Station<RepairStation>("Repair", root.transform, new Vector3(-2.8f, 0f, 0f),
                new Vector2(1.8f, 2.0f), "Fix", new Color(1f, 0.72f, 0.3f));
            kit.Repair.target = kit.Durability;

            return kit;
        }

        /// <summary>A crop field the player harvests by walking through it.</summary>
        public static HarvestStation BuildField(string name, Transform parent, Vector3 position,
            ItemDefinition crop, int plots, float regrowSeconds, Vector2 size)
        {
            var station = Station<HarvestStation>(name, parent, position, size,
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
