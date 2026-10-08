using System;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Adds one unit of goods to a buffer every few seconds, for nothing.
    ///
    /// The supermarket's storage room fills itself: it is a wholesale delivery, not a second
    /// farm. Nothing is taken from the real farm, so running the shop cannot starve the tills at
    /// home and the two businesses stay independent. The player's job is the carrying - collect
    /// from storage, stock the shelf - and the fixed rate is what keeps the shop's income
    /// bounded.
    ///
    /// Stops at the buffer's capacity and resumes the moment there is room. Perishable goods
    /// still rot in the storage like anywhere else (that is the buffer's behaviour, not this
    /// one's), which is the pressure to keep carrying.
    ///
    /// Offline: the time the game was closed is converted into units on the first frame after a
    /// load, capped by <see cref="GameClock.MaxOfflineSeconds"/> and by the buffer's capacity.
    /// Applied after every component has restored, so the buffer's own load cannot overwrite it.
    /// </summary>
    public class SupplyFeed : MonoBehaviour, ISaveable
    {
        public ItemBuffer target;

        [Tooltip("Seconds per unit delivered. 4 gives 15 a minute per product, comfortably " +
                 "above what the shoppers take, so a shelf that is kept stocked never runs dry " +
                 "for want of supply - only for want of carrying.")]
        [Min(0.1f)] public float secondsPerUnit = 4f;

        private float _clock;
        private double _pendingOffline;

        public string SaveKey => SaveKeys.For(this);

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        private void Start()
        {
            if (_pendingOffline <= 0d) return;

            Produce(_pendingOffline);
            _pendingOffline = 0d;
        }

        private void Update()
        {
            // Clamped for the same reason as everywhere else: a long loading frame must not
            // be paid out as a burst of goods.
            Produce(Mathf.Min(Time.deltaTime, 0.1f));
        }

        private void Produce(double seconds)
        {
            if (target == null) return;

            if (target.IsFull)
            {
                // A full store does not bank time: the next unit is a full interval after
                // room appears, not instantly.
                _clock = 0f;
                return;
            }

            _clock += (float)seconds;
            int units = (int)(_clock / secondsPerUnit);
            if (units <= 0) return;

            _clock -= units * secondsPerUnit;
            target.Add(units);
            if (target.IsFull) _clock = 0f;
        }

        [Serializable]
        private struct State
        {
            public float clock;
            public double lastUnix;
        }

        public string CaptureState() =>
            JsonUtility.ToJson(new State { clock = _clock, lastUnix = GameClock.NowUnix });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _clock = Mathf.Max(0f, s.clock);
            _pendingOffline = GameClock.ClampOffline(GameClock.NowUnix - s.lastUnix);
        }
    }
}
