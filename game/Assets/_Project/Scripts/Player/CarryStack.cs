using System;
using System.Collections.Generic;
using Tycoon.Config;
using UnityEngine;

namespace Tycoon.Player
{
    /// <summary>
    /// The signature mechanic of this genre: goods are carried in a wobbling stack above the
    /// head, and every transfer moves one unit at a time.
    ///
    /// The stack holds a MIXTURE of goods rather than one kind at a time. Restricting it to a
    /// single type created dead ends: carrying corn with a full feed hopper left the player
    /// unable to deposit it, unable to pick up eggs, and unable to sell it - stuck with no way
    /// to empty their hands. A mixed stack removes that whole class of problem, and it means
    /// the player can run a delivery and a collection on the same trip.
    /// </summary>
    public class CarryStack : MonoBehaviour
    {
        [Tooltip("Where the stack grows from. Usually an empty just above the character's head.")]
        public Transform anchor;

        [Tooltip("Total units carried, across all kinds of goods.")]
        [Min(1)] public int capacity = 8;

        [Tooltip("How fast stacked items settle into place. Purely cosmetic.")]
        public float settleSpeed = 14f;

        [Tooltip("Sideways wobble of the stack while running. Purely cosmetic.")]
        public float swayAmount = 6f;

        private class Entry
        {
            public ItemDefinition Item;
            public Transform Visual;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        public event Action Changed;

        public int Count => _entries.Count;
        public bool IsEmpty => _entries.Count == 0;
        public bool IsFull => _entries.Count >= capacity;

        /// <summary>Topmost item, or null when empty. Handy for "sell whatever you are holding".</summary>
        public ItemDefinition Item => _entries.Count > 0 ? _entries[_entries.Count - 1].Item : null;

        /// <summary>
        /// True if one more unit would fit. Deliberately independent of what is already held -
        /// only total capacity matters now.
        /// </summary>
        public bool CanAccept(ItemDefinition candidate) => candidate != null && !IsFull;

        public bool Has(ItemDefinition candidate) => TopIndexOf(candidate) >= 0;

        public int CountOf(ItemDefinition candidate)
        {
            if (candidate == null) return 0;

            int total = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Item == candidate) total++;
            return total;
        }

        /// <summary>Whatever is on top, regardless of type.</summary>
        public ItemDefinition Peek() => Item;

        public bool TryAdd(ItemDefinition candidate)
        {
            if (!CanAccept(candidate)) return false;

            _entries.Add(new Entry
            {
                Item = candidate,
                Visual = CreateVisual(candidate, _entries.Count)
            });

            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Removes the topmost unit of <paramref name="expected"/>, or the topmost unit of
        /// anything when it is null. Returns false if there is none to remove.
        /// </summary>
        public bool TryRemove(ItemDefinition expected)
        {
            int index = expected == null ? _entries.Count - 1 : TopIndexOf(expected);
            if (index < 0) return false;

            var entry = _entries[index];
            if (entry.Visual != null) Destroy(entry.Visual.gameObject);
            _entries.RemoveAt(index);

            Changed?.Invoke();
            return true;
        }

        /// <summary>Index of the highest unit of this kind, or -1. Taking from the top reads best.</summary>
        private int TopIndexOf(ItemDefinition candidate)
        {
            if (candidate == null) return -1;

            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].Item == candidate) return i;
            return -1;
        }

        /// <summary>A short "2 Corn, 3 Egg" summary, for debug readouts and later UI.</summary>
        public string Describe()
        {
            if (_entries.Count == 0) return "empty";

            var counts = new List<KeyValuePair<ItemDefinition, int>>();
            for (int i = 0; i < _entries.Count; i++)
            {
                var item = _entries[i].Item;
                int at = counts.FindIndex(p => p.Key == item);
                if (at >= 0) counts[at] = new KeyValuePair<ItemDefinition, int>(item, counts[at].Value + 1);
                else counts.Add(new KeyValuePair<ItemDefinition, int>(item, 1));
            }

            var parts = new string[counts.Count];
            for (int i = 0; i < counts.Count; i++)
                parts[i] = $"{counts[i].Value} {counts[i].Key.displayName}";
            return string.Join(", ", parts);
        }

        private static float HeightOf(ItemDefinition item) =>
            item != null ? Mathf.Max(0.05f, item.stackHeight) : 0.25f;

        /// <summary>
        /// Where the unit at <paramref name="index"/> sits. Heights accumulate per item, so a
        /// mixed stack of tall and flat goods still stacks without gaps or overlaps.
        /// </summary>
        private Vector3 LocalSlot(int index)
        {
            float y = 0f;
            for (int i = 0; i < index && i < _entries.Count; i++) y += HeightOf(_entries[i].Item);
            return new Vector3(0f, y, 0f);
        }

        private Transform CreateVisual(ItemDefinition definition, int index)
        {
            GameObject go;
            if (definition.visualPrefab != null)
            {
                go = Instantiate(definition.visualPrefab);
            }
            else
            {
                // Greybox fallback: a tinted cube. Art can be swapped in later purely through
                // the ItemDefinition asset, with no code change.
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var collider = go.GetComponent<Collider>();
                if (collider != null) Destroy(collider);

                var renderer = go.GetComponent<Renderer>();

                // CreatePrimitive hands back Unity's built-in default material, which is not
                // part of a URP build and shows up as solid magenta. The item's own material
                // asset is the reliable source.
                if (definition.carryMaterial != null)
                {
                    renderer.sharedMaterial = definition.carryMaterial;
                }
                else
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor("_BaseColor", definition.color);
                    block.SetColor("_Color", definition.color);
                    renderer.SetPropertyBlock(block);
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.localScale = new Vector3(0.34f, definition.stackHeight, 0.34f);
            }

            go.name = $"Carried_{definition.id}_{index}";
            var t = go.transform;
            t.SetParent(anchor != null ? anchor : transform, false);
            t.localPosition = LocalSlot(index);
            t.localRotation = Quaternion.identity;
            t.localScale *= 0.01f; // pops up to full size in Update
            return t;
        }

        private void Update()
        {
            if (_entries.Count == 0) return;

            float sway = Mathf.Sin(Time.time * 8f) * swayAmount;

            for (int i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Visual == null) continue;

                // Recomputed every frame so the stack closes up smoothly when a unit is taken
                // from the middle rather than the top.
                Vector3 target = LocalSlot(i);
                entry.Visual.localPosition = Vector3.Lerp(
                    entry.Visual.localPosition, target, Time.deltaTime * settleSpeed);

                Vector3 fullScale = entry.Item.visualPrefab != null
                    ? Vector3.one
                    : new Vector3(0.34f, entry.Item.stackHeight, 0.34f);
                entry.Visual.localScale = Vector3.Lerp(
                    entry.Visual.localScale, fullScale, Time.deltaTime * settleSpeed);

                // Higher items in the stack lean further, which reads as weight and momentum.
                float lean = sway * (i + 1) / _entries.Count;
                entry.Visual.localRotation = Quaternion.Euler(0f, 0f, lean);
            }
        }
    }
}
