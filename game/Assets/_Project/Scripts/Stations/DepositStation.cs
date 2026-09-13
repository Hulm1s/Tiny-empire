using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Unloads the player's carried stack into a buffer: tipping corn into the chicken feeder,
    /// stacking bread onto a shop shelf.
    /// </summary>
    public class DepositStation : StationBase
    {
        [Header("Destination")]
        public ItemBuffer target;

        public override bool IsOperational => target != null && !target.IsFull;

        public override string StatusText =>
            target == null ? label : $"{label} {target.Count}/{target.capacity}";

        protected override bool TickWithPlayer()
        {
            if (target == null || target.item == null || target.IsFull) return false;
            if (Carry == null || !Carry.Has(target.item)) return false;

            if (!Carry.TryRemove(target.item)) return false;

            // If the buffer somehow refuses the unit, hand it back rather than destroying goods.
            if (target.Add(1) == 0)
            {
                Carry.TryAdd(target.item);
                return false;
            }

            return true;
        }
    }
}
