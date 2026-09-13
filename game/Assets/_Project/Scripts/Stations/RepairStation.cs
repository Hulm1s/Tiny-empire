using Tycoon.Core;
using Tycoon.Upkeep;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Stand here to nurse a worn machine back to full condition.
    ///
    /// Repair costs money as well as time, so neglect is a real expense rather than a chore
    /// the player can always walk off.
    /// </summary>
    public class RepairStation : StationBase
    {
        [Header("Target")]
        public Durability target;

        [Header("Cost")]
        [Tooltip("Money charged per point of condition restored. Zero makes repairs free.")]
        public double costPerPoint = 0.4d;

        [Tooltip("Fraction of the normal repair speed the player always gets, even with an " +
                 "empty wallet.\n\n" +
                 "This must never be zero. A jammed machine and no money would otherwise be a " +
                 "dead end: no repair means no production, no production means no income, and " +
                 "no income means the repair can never be afforded. Paying makes it four times " +
                 "faster, which is pressure enough without being a trap.")]
        [Range(0.05f, 1f)] public float freeRepairFraction = 0.25f;

        public override bool IsOperational => target != null && target.Fraction < 1f;

        public override string StatusText
        {
            get
            {
                if (target == null) return label;
                if (target.Fraction >= 1f) return $"{label} OK";
                return $"{label} {Mathf.RoundToInt(target.Fraction * 100f)}%";
            }
        }

        protected override bool TickWithPlayer()
        {
            if (target == null || target.Fraction >= 1f) return false;

            float points = target.repairPerSecond * tickInterval;
            if (points <= 0f) return false;

            if (costPerPoint > 0d)
            {
                var wallet = GameRoot.Money;
                double spent = wallet != null ? wallet.TrySpend(points * costPerPoint) : 0d;

                float paidFor = (float)(spent / costPerPoint);
                float freeOfCharge = points * freeRepairFraction;

                // Whatever was paid for, or the guaranteed slow rate - whichever is better.
                points = Mathf.Max(paidFor, freeOfCharge);
            }

            return target.Repair(points);
        }
    }
}
