using System;
using Tycoon.Core;
using Tycoon.Stations;
using UnityEngine;

namespace Tycoon.Upkeep
{
    /// <summary>
    /// Machines wear out as they work and eventually jam, which is the first of the pressures
    /// that stop a finished business running itself forever.
    ///
    /// Wear is charged per unit produced rather than per second on purpose: an idle machine
    /// does not rot, so the player is punished for scaling up without maintaining, not for
    /// being away.
    /// </summary>
    public class Durability : MonoBehaviour, ISaveable
    {
        [Header("Condition")]
        [Min(1f)] public float max = 100f;

        [Tooltip("Condition lost per unit produced. max / this = units before a jam.")]
        [Min(0f)] public float wearPerOutput = 2f;

        [Tooltip("Condition restored per second while the player stands in the repair square.")]
        [Min(0f)] public float repairPerSecond = 35f;

        [Tooltip("Below this fraction the machine raises an alert but still runs.")]
        [Range(0f, 1f)] public float warnBelow = 0.3f;

        [Header("Wear off")]
        [Tooltip("When this purchase is owned the machine stops wearing for good, is restored to full, " +
                 "and the objects below are switched off. The farm points this at the supermarket: " +
                 "after that the player never has to come back to fix anything. Derived from the saved " +
                 "purchase, so it holds after a reload with no save entry of its own.")]
        public UnlockStation retireWhen;

        [Tooltip("Switched off (with their triggers) when this retires - the repair square.")]
        public Behaviour[] retireDisable;

        [SerializeField] private float _current = -1f;

        private bool _retired;

        public event Action Changed;

        public float Current => _current;
        public float Fraction => max <= 0f ? 1f : Mathf.Clamp01(_current / max);
        public bool IsBroken => !_retired && _current <= 0f;

        /// <summary>True once <see cref="retireWhen"/> has been bought: no more wear, ever.</summary>
        public bool Retired => _retired;
        public bool NeedsAttention => Fraction <= warnBelow;

        public string SaveKey => SaveKeys.For(this);

        private void Awake()
        {
            if (_current < 0f) _current = max;
        }

        private void OnEnable()
        {
            SaveSystem.Register(this);

            if (retireWhen != null)
            {
                retireWhen.Completed += Retire;
                if (retireWhen.IsUnlocked) Retire();
            }
        }

        private void OnDisable()
        {
            if (retireWhen != null) retireWhen.Completed -= Retire;
            SaveSystem.Unregister(this);
        }

        /// <summary>Restores the machine to full and switches wear (and its repair square) off for good.</summary>
        private void Retire()
        {
            _retired = true;
            _current = max;

            if (retireDisable != null)
            {
                foreach (var b in retireDisable)
                {
                    if (b == null) continue;
                    var collider = b.GetComponent<Collider>();
                    if (collider != null) collider.enabled = false;
                    b.enabled = false;
                }
            }
            Changed?.Invoke();
        }

        /// <summary>Called by the machine each time it completes a unit.</summary>
        public void Wear(float amount)
        {
            if (_retired || amount <= 0f || _current <= 0f) return;
            _current = Mathf.Max(0f, _current - amount);
            Changed?.Invoke();
        }

        /// <summary>Returns true if any repair actually happened, so the station can keep ticking.</summary>
        public bool Repair(float amount)
        {
            if (amount <= 0f || _current >= max) return false;
            _current = Mathf.Min(max, _current + amount);
            Changed?.Invoke();
            return true;
        }

        [Serializable]
        private struct State { public float current; }

        public string CaptureState() => JsonUtility.ToJson(new State { current = _current });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _current = _retired ? max : Mathf.Clamp(s.current, 0f, max);
            Changed?.Invoke();
        }
    }
}
