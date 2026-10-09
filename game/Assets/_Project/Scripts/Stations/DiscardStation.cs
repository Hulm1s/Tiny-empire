using Tycoon.Audio;
using Tycoon.Config;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The bin. Stand in it and whatever is in your arms goes in the rubbish, one unit at a
    /// time, for nothing.
    ///
    /// This exists purely as a way out. The carry stack is the one piece of state the player
    /// can fill with something they have nowhere to put: hay before a cow shed exists, corn
    /// with every hopper full, a load picked up by mistake. A full stack cannot accept eggs,
    /// eggs are the income, and every building costs money - so a stack of dead weight used to
    /// mean waiting, or starting over. The project rule is that no state may need money to
    /// escape from, and this is what enforces it for the one bit of state that could.
    ///
    /// It gives nothing back, so it cannot be farmed: throwing goods away is strictly worse
    /// than selling them, and there is no price, refund or timer attached to it.
    ///
    /// Workers never use it. A hired hand standing in the bin would quietly destroy the load
    /// it was paid to deliver, which would look exactly like goods vanishing at random.
    /// </summary>
    public class DiscardStation : StationBase
    {
        /// <summary>Always available. A bin that greys out would defeat the point of it.</summary>
        public override bool IsOperational => true;

        public override string StatusValue => string.Empty;

        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Discard;

        protected override void Awake()
        {
            base.Awake();

            // Forced here rather than left to the level builder, for the same reason the repair
            // square forces Task mode: a bin that workers could walk into is a goods leak, and
            // it must not be possible to configure one by accident.
            workerCompatible = false;
        }

        /// <summary>
        /// The ring shows how full the arms still are, so it empties as the player stands
        /// there and reads as a job finishing rather than a number ticking down.
        /// </summary>
        protected override float TransferProgress
        {
            get
            {
                var carry = PrimaryCarry;
                if (carry == null || carry.capacity <= 0) return 0f;
                return Mathf.Clamp01((float)carry.Count / carry.capacity);
            }
        }

        protected override bool TickWithPlayer()
        {
            if (Carry == null || Carry.IsEmpty) return false;

            // Null takes whatever is on top, so a mixed stack comes off in the order it went
            // on. Read before removing, because the stack forgets it immediately afterwards.
            ItemDefinition thrown = Carry.Peek();
            if (!Carry.TryRemove(null)) return false;

            SoundFx.PlayAt(Sfx.Trash, transform.position);

            Tycoon.UI.WorldFeedback.Remove($"bin{GetInstanceID()}",
                transform.position + Vector3.up * 1.6f, 1,
                thrown != null ? thrown.displayName : "",
                new Color(0.86f, 0.88f, 0.92f));

            return true;
        }
    }
}
