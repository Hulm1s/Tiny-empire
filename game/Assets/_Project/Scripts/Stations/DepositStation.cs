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

        public override string StatusValue =>
            target == null ? string.Empty : $"{target.Count}/{target.capacity}";

        /// <summary>
        /// An arrow rather than the goods: what matters here is the direction goods travel,
        /// and the collect square opposite already shows what the building makes.
        /// </summary>
        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Feed;

        /// <summary>The ring shows how full the hopper is getting.</summary>
        protected override float TransferProgress => target != null ? target.Fill : 0f;

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
