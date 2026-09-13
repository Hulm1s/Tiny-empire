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
                if (wallet == null) return false;

                double spent = wallet.TrySpend(points * costPerPoint);
                if (spent <= 0d) return false;

                // Only repair as much as was actually paid for.
                points = (float)(spent / costPerPoint);
            }

            return target.Repair(points);
        }
    }
}
