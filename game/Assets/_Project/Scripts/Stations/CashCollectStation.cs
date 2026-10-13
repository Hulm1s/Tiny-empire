using System;
using Tycoon.Audio;
using Tycoon.Core;
using Tycoon.Player;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The square beside a till that gathers its cash: stand in it and the pile flows into the
    /// player's hand, in growing steps so a big pile still empties in a few seconds.
    ///
    /// The player's alone. Forced here rather than left to the builder, because the whole point is
    /// that a cashier on the till never carries the takings off.
    /// </summary>
    public class CashCollectStation : StationBase
    {
        public CashPile pile;

        public override bool IsOperational => pile != null && pile.Amount >= 1d;

        public override string StatusValue =>
            pile == null ? string.Empty : MoneyFormat.Short(pile.Amount);

        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Cash;

        protected override float TransferProgress => 0f;

        protected override void Awake()
        {
            base.Awake();
            workerCompatible = false;
        }

        protected override bool TickWithPlayer()
        {
            if (pile == null || pile.Amount < 1d) return false;

            var cash = CashStack.Of(Carry);
            if (cash == null) return false;

            // A growing step: at least one bundle, a third of what is left otherwise.
            double step = Math.Max(pile.BundleValue, pile.Amount * 0.34d);
            double moved = pile.Take(step);
            if (moved <= 0d) return false;

            cash.Add(moved);
            SoundFx.PlayAt(Sfx.Coin, transform.position);
            return true;
        }
    }
}
