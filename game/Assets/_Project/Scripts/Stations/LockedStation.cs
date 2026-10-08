using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// A square that says what is coming and does nothing: the supermarket's "COMING SOON" bay
    /// on the north wall, held for the butcher.
    ///
    /// It is a station only so that it is drawn, labelled and measured like every other square
    /// (the layout audit and the interaction card both work from <see cref="StationBase"/>).
    /// It is never operational, takes no money and changes nothing, so standing in it is
    /// harmless and it can never be part of a state the player is stuck in. It has no state to
    /// save.
    /// </summary>
    public class LockedStation : StationBase
    {
        public override bool IsOperational => false;

        /// <summary>The padlock: shut, and not for sale.</summary>
        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Unlock;
    }
}
