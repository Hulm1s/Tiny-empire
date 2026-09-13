using System;
using Tycoon.Core;
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

        [SerializeField] private float _current = -1f;

        public event Action Changed;

        public float Current => _current;
        public float Fraction => max <= 0f ? 1f : Mathf.Clamp01(_current / max);
        public bool IsBroken => _current <= 0f;
        public bool NeedsAttention => Fraction <= warnBelow;

        public string SaveKey => SaveKeys.For(this);

        private void Awake()
        {
            if (_current < 0f) _current = max;
        }

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        /// <summary>Called by the machine each time it completes a unit.</summary>
        public void Wear(float amount)
        {
            if (amount <= 0f || _current <= 0f) return;
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
            _current = Mathf.Clamp(s.current, 0f, max);
            Changed?.Invoke();
        }
    }
}
