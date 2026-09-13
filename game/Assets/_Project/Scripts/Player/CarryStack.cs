using System;
using System.Collections.Generic;
using Tycoon.Config;
using UnityEngine;

namespace Tycoon.Player
{
    /// <summary>
    /// The signature mechanic of this genre: the player physically carries goods in a wobbling
    /// stack above their head, and every transfer is one unit at a time.
    ///
    /// Only one kind of goods can be carried at once, which is what forces the player to
    /// actually run the production chain in order instead of hoovering up everything at once.
    /// </summary>
    public class CarryStack : MonoBehaviour
    {
        [Tooltip("Where the stack grows from. Usually an empty just above the character's head.")]
        public Transform anchor;

        [Min(1)] public int capacity = 8;

        [Tooltip("How fast stacked items settle into place. Purely cosmetic.")]
        public float settleSpeed = 14f;

        [Tooltip("Sideways wobble of the stack while running. Purely cosmetic.")]
        public float swayAmount = 6f;

        private readonly List<Transform> _visuals = new List<Transform>();
        private ItemDefinition _item;

        public event Action Changed;

        public ItemDefinition Item => _item;
        public int Count => _visuals.Count;
        public bool IsEmpty => _visuals.Count == 0;
        public bool IsFull => _visuals.Count >= capacity;

        /// <summary>True if one more unit of this item would be accepted right now.</summary>
        public bool CanAccept(ItemDefinition candidate)
        {
            if (candidate == null || IsFull) return false;
            return _item == null || _item == candidate;
        }

        public bool Has(ItemDefinition candidate) => _item == candidate && _visuals.Count > 0;

        public bool TryAdd(ItemDefinition candidate)
        {
            if (!CanAccept(candidate)) return false;

            _item = candidate;
            _visuals.Add(CreateVisual(candidate, _visuals.Count));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the top unit. Returns false if the stack is empty or holds something else.</summary>
        public bool TryRemove(ItemDefinition expected)
        {
            if (expected != null && _item != expected) return false;
            if (_visuals.Count == 0) return false;

            int last = _visuals.Count - 1;
            if (_visuals[last] != null) Destroy(_visuals[last].gameObject);
            _visuals.RemoveAt(last);

            if (_visuals.Count == 0) _item = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Whatever is on top, regardless of type. Used by sell counters that take anything.</summary>
        public ItemDefinition Peek() => _visuals.Count > 0 ? _item : null;

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
            t.localPosition = LocalSlot(definition, index);
            t.localRotation = Quaternion.identity;
            t.localScale *= 0.01f; // pops up to full size in Update
            return t;
        }

        private static Vector3 LocalSlot(ItemDefinition definition, int index)
        {
            return new Vector3(0f, index * definition.stackHeight, 0f);
        }

        private void Update()
        {
            if (_item == null || _visuals.Count == 0) return;

            float sway = Mathf.Sin(Time.time * 8f) * swayAmount;

            for (int i = 0; i < _visuals.Count; i++)
            {
                var t = _visuals[i];
                if (t == null) continue;

                Vector3 target = LocalSlot(_item, i);
                t.localPosition = Vector3.Lerp(t.localPosition, target, Time.deltaTime * settleSpeed);

                Vector3 fullScale = _item.visualPrefab != null
                    ? Vector3.one
                    : new Vector3(0.34f, _item.stackHeight, 0.34f);
                t.localScale = Vector3.Lerp(t.localScale, fullScale, Time.deltaTime * settleSpeed);

                // Higher items in the stack lean further, which reads as weight and momentum.
                float lean = sway * (i + 1) / _visuals.Count;
                t.localRotation = Quaternion.Euler(0f, 0f, lean);
            }
        }
    }
}
