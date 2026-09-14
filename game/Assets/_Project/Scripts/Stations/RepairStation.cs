using Tycoon.Core;
using Tycoon.UI;
using Tycoon.Upkeep;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Stand here to nurse a worn machine back to full condition.
    ///
    /// This is the first station to use <see cref="InteractionMode.Task"/>: there is nothing
    /// being carried, so the player enters, watches a progress ring fill, and gets a chunk of
    /// condition back. Cleaning, painting and renovation will all work the same way.
    /// </summary>
    public class RepairStation : StationBase
    {
        [Header("Target")]
        public Durability target;

        [Header("Repair")]
        [Tooltip("Condition restored each time the progress ring fills.")]
        [Min(1f)] public float repairPerTask = 25f;

        [Header("Cost")]
        [Tooltip("Money charged per point of condition restored. Zero makes repairs free.")]
        public double costPerPoint = 0.4d;

        [Tooltip("Fraction of the repair the player always gets, even with an empty wallet.\n\n" +
                 "This must never be zero. A jammed machine and no money would otherwise be a " +
                 "dead end: no repair means no production, no production means no income, and " +
                 "no income means the repair can never be afforded. Paying makes it four times " +
                 "faster, which is pressure enough without being a trap.")]
        [Range(0.05f, 1f)] public float freeRepairFraction = 0.25f;

        protected override void Awake()
        {
            base.Awake();
            // Repairing is inherently a timed job, never a transfer. Forced here rather than
            // left to the level builder so it cannot be configured wrongly.
            mode = InteractionMode.Task;
        }

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

        protected override bool CanPerformTask() => target != null && target.Fraction < 1f;

        protected override void CompleteTask()
        {
            if (target == null) return;

            float points = repairPerTask;

            if (costPerPoint > 0d)
            {
                var wallet = GameRoot.Money;
                double spent = wallet != null ? wallet.TrySpend(points * costPerPoint) : 0d;

                float paidFor = (float)(spent / costPerPoint);
                float freeOfCharge = points * freeRepairFraction;

                // Whatever was paid for, or the guaranteed slow rate - whichever is better.
                points = Mathf.Max(paidFor, freeOfCharge);
            }

            if (!target.Repair(points)) return;

            WorldFeedback.Show(transform.position + Vector3.up * 1.2f,
                $"+{Mathf.RoundToInt(points)}%", new Color(0.55f, 0.9f, 0.5f));

            if (target.Fraction >= 1f)
            {
                WorldFeedback.Show(target.transform.position + Vector3.up * 2.6f,
                    "FIXED", new Color(0.6f, 0.95f, 0.6f));
            }
        }
    }
}
