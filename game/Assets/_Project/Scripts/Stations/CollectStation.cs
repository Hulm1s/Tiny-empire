using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Loads goods out of a buffer into the player's arms: picking eggs out of the nest boxes.
    /// </summary>
    public class CollectStation : StationBase
    {
        [Header("Source")]
        public ItemBuffer source;

        public override bool IsOperational => source != null && !source.IsEmpty;

        public override string StatusText =>
            source == null ? label : $"{label} {source.Count}";

        /// <summary>The ring shows how much is left waiting to be picked up.</summary>
        protected override float TransferProgress => source != null ? source.Fill : 0f;

        protected override bool TickWithPlayer()
        {
            if (source == null || source.item == null || source.IsEmpty) return false;
            if (Carry == null || !Carry.CanAccept(source.item)) return false;

            if (source.Remove(1) == 0) return false;

            // Put it back if the stack refused it, so goods are never silently lost.
            if (!Carry.TryAdd(source.item))
            {
                source.Add(1);
                return false;
            }

            Tycoon.UI.WorldFeedback.Add($"collect{GetInstanceID()}",
                Carry.transform.position + Vector3.up * 2.2f, 1,
                source.item.displayName, source.item.color);

            return true;
        }
    }
}
