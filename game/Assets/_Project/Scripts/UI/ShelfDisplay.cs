using Tycoon.Stations;
using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// Shows what is on a shelf (or in a crate) as one small unit per unit (modelled goods, else a cube), so the shop's
    /// stock is something you can SEE: a stocked shelf is full, a shopper takes goods and it
    /// visibly thins, an empty one is bare.
    ///
    /// The same greybox cube <see cref="Tycoon.Player.CarryStack"/> uses for carried goods, in the
    /// item's own material asset - never a material created here, which would render magenta in
    /// the web build. All the cubes are made once, up front, and only switched on and off as the
    /// buffer changes, so a busy shelf allocates nothing.
    /// </summary>
    public class ShelfDisplay : MonoBehaviour
    {
        [Tooltip("The buffer shown. The display is built for its capacity.")]
        public ItemBuffer buffer;

        [Header("Layout (local space)")]
        [Tooltip("Cubes per row, along local X.")]
        [Min(1)] public int columns = 6;

        [Tooltip("Rows per board, along local Z.")]
        [Min(1)] public int rowsPerBoard = 1;

        public float pitchX = 0.55f;
        public float pitchZ = 0.3f;

        [Tooltip("Where the centre of the arrangement sits sideways and back-to-front.")]
        public Vector2 centre = Vector2.zero;

        [Tooltip("Heights of the surfaces units stand on, in order. A unit past the first " +
                 "board's worth goes on the next.")]
        public float[] boardHeights = { 0.66f, 1.18f };

        [Tooltip("Modelled goods are shrunk (never grown) to fit under the next board and inside " +
                 "their cell: height at most this, width at most the pitch.")]
        public float maxUnitHeight = 0.46f;

        [Tooltip("Footprint of one cube. Its height comes from the item.")]
        public Vector2 cubeFootprint = new Vector2(0.42f, 0.5f);

        private GameObject[] _cubes;
        private int _shown = -1;

        private void Awake() => Build();

        private void OnEnable()
        {
            if (buffer == null) return;
            Build();
            buffer.Changed += Refresh;
            _shown = -1;
            Refresh();
        }

        private void OnDisable()
        {
            if (buffer != null) buffer.Changed -= Refresh;
        }

        private void Build()
        {
            if (_cubes != null || buffer == null || buffer.item == null) return;

            var item = buffer.item;
            _cubes = new GameObject[buffer.capacity];
            int perBoard = columns * rowsPerBoard;

            for (int i = 0; i < _cubes.Length; i++)
            {
                int board = Mathf.Min(i / perBoard, boardHeights.Length - 1);
                int within = i % perBoard;
                int column = within % columns;
                int row = within / columns;

                float x = centre.x + (column - (columns - 1) * 0.5f) * pitchX;
                float z = centre.y + (row - (rowsPerBoard - 1) * 0.5f) * pitchZ;
                float height = Mathf.Max(0.05f, item.stackHeight);

                // A modelled item (egg, bottle, loaf...) stands in the same cell as the cube it
                // replaces: pivot at the cell centre, unit scale, sharing the prefab's mesh and
                // material assets. Items without a prefab keep the greybox cube below.
                if (item.visualPrefab != null)
                {
                    var model = Instantiate(item.visualPrefab, transform, false);
                    model.name = $"Unit_{i}";
                    float fit = Mathf.Min(1f, maxUnitHeight / height);
                    var mf = item.visualPrefab.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        var size = mf.sharedMesh.bounds.size;
                        fit = Mathf.Min(fit, pitchX * 0.97f / Mathf.Max(0.01f, size.x));
                        if (rowsPerBoard > 1) fit = Mathf.Min(fit, pitchZ * 0.97f / Mathf.Max(0.01f, size.z));
                    }
                    model.transform.localScale = Vector3.one * fit;
                    model.transform.localPosition = new Vector3(x, boardHeights[board] + height * fit * 0.5f, z);
                    model.transform.localRotation = Quaternion.identity;
                    model.SetActive(false);
                    _cubes[i] = model;
                    continue;
                }

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Unit_{i}";
                var collider = cube.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = cube.GetComponent<Renderer>();
                // The item's material asset, so the shader is in the build. Fall back to a
                // tint only if an item has none.
                if (item.carryMaterial != null)
                {
                    renderer.sharedMaterial = item.carryMaterial;
                }
                else
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", item.color);
                    block.SetColor("_Color", item.color);
                    renderer.SetPropertyBlock(block);
                }
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                var t = cube.transform;
                t.SetParent(transform, false);
                t.localPosition = new Vector3(x, boardHeights[board] + height * 0.5f, z);
                t.localScale = new Vector3(cubeFootprint.x, height, cubeFootprint.y);

                cube.SetActive(false);
                _cubes[i] = cube;
            }
        }

        private void Refresh()
        {
            if (_cubes == null || buffer == null) return;

            int count = Mathf.Clamp(buffer.Count, 0, _cubes.Length);
            if (count == _shown) return;
            _shown = count;

            for (int i = 0; i < _cubes.Length; i++)
            {
                bool on = i < count;
                if (_cubes[i].activeSelf != on) _cubes[i].SetActive(on);
            }
        }
    }
}
