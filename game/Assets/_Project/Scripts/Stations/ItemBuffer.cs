using System;
using Tycoon.Config;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// A pile of one kind of goods sitting in the world: the chicken feed trough, the egg
    /// basket, the shop shelf. Machines and stations only ever talk to buffers, never to each
    /// other, which is what lets new station types be dropped in without rewiring anything.
    ///
    /// Buffers also own spoilage, so perishable goods rot wherever they are left.
    /// </summary>
    public class ItemBuffer : MonoBehaviour, ISaveable
    {
        [Header("Contents")]
        public ItemDefinition item;

        [Min(1)] public int capacity = 10;

        [SerializeField, Min(0)] private int _count;

        [Header("Debug")]
        [Tooltip("Units granted when there is no save file. Handy for testing later stages.")]
        [SerializeField, Min(0)] private int startingCount;

        /// <summary>Accumulated seconds of stock sitting around, used to pace spoilage.</summary>
        private double _spoilAccumulator;

        private double _lastSimulatedUnix;

        public event Action Changed;

        public int Count => _count;
        public bool IsFull => _count >= capacity;
        public bool IsEmpty => _count <= 0;
        public int FreeSpace => Mathf.Max(0, capacity - _count);
        public float Fill => capacity <= 0 ? 0f : (float)_count / capacity;

        public string SaveKey => SaveKeys.For(this);

        private void Awake()
        {
            _count = Mathf.Clamp(startingCount, 0, capacity);
            _lastSimulatedUnix = GameClock.NowUnix;
        }

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        private void Update()
        {
            if (_count > 0) Spoil(Time.deltaTime);
            _lastSimulatedUnix = GameClock.NowUnix;
        }

        /// <summary>Adds up to <paramref name="amount"/> units, returning how many fit.</summary>
        public int Add(int amount)
        {
            if (amount <= 0) return 0;
            int accepted = Mathf.Min(amount, FreeSpace);
            if (accepted <= 0) return 0;
            _count += accepted;
            Changed?.Invoke();
            return accepted;
        }

        /// <summary>Removes up to <paramref name="amount"/> units, returning how many were taken.</summary>
        public int Remove(int amount)
        {
            if (amount <= 0) return 0;
            int taken = Mathf.Min(amount, _count);
            if (taken <= 0) return 0;
            _count -= taken;
            if (_count == 0) _spoilAccumulator = 0d;
            Changed?.Invoke();
            return taken;
        }

        /// <summary>
        /// Advances spoilage by a span of time. Called every frame while playing and once with
        /// the whole offline gap when a level is restored from a save.
        /// </summary>
        public void Spoil(double seconds)
        {
            if (item == null || !item.perishable || _count <= 0 || item.spoilSeconds <= 0f) return;

            _spoilAccumulator += seconds;
            int lost = 0;
            while (_spoilAccumulator >= item.spoilSeconds && _count - lost > 0)
            {
                _spoilAccumulator -= item.spoilSeconds;
                lost++;
            }

            if (lost > 0)
            {
                _count = Mathf.Max(0, _count - lost);
                if (_count == 0) _spoilAccumulator = 0d;
                Changed?.Invoke();
            }
        }

        [Serializable]
        private struct State
        {
            public int count;
            public double spoilAccumulator;
            public double lastUnix;
        }

        public string CaptureState() => JsonUtility.ToJson(new State
        {
            count = _count,
            spoilAccumulator = _spoilAccumulator,
            lastUnix = GameClock.NowUnix
        });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _count = Mathf.Clamp(s.count, 0, capacity);
            _spoilAccumulator = s.spoilAccumulator;
            _lastSimulatedUnix = s.lastUnix;

            // Catch up on rot that happened while the game was closed.
            double gap = GameClock.ClampOffline(GameClock.NowUnix - s.lastUnix);
            if (gap > 0d) Spoil(gap);

            Changed?.Invoke();
        }
    }
}
