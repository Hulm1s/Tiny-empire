using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The cafe's dishwasher: a sink for dirty cups. Cups arrive through an ordinary DepositStation whose target is
    /// <see cref="dirty"/> (a buffer of the dirty-cup item, so the square takes ONLY dirty cups - nothing else in the
    /// arms is touched), and this washes them away one at a time. A full sink simply refuses more until a cup is done,
    /// so it never loses anything and never needs money.
    /// </summary>
    public class Dishwasher : MonoBehaviour
    {
        public ItemBuffer dirty;

        [Tooltip("Seconds to wash one cup.")]
        [Min(0.1f)] public float secondsPerCup = 1.2f;

        private float _clock;

        private void Update()
        {
            if (dirty == null || dirty.IsEmpty) { _clock = 0f; return; }

            _clock += Mathf.Min(Time.deltaTime, 0.1f);
            if (_clock < secondsPerCup) return;
            _clock -= secondsPerCup;
            dirty.Remove(1);
        }
    }
}
