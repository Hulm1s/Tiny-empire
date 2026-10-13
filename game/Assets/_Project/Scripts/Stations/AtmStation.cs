using System;
using Tycoon.Audio;
using Tycoon.Core;
using Tycoon.Player;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The cash machine. The account (the <see cref="Wallet"/>) is where farm income lands once the
    /// supermarket is open and where an older save's balance lives; this moves it to and from the
    /// player's hand.
    ///
    /// Standing in the WITHDRAW square draws the account into the hand in growing steps. The small
    /// DEPOSIT square beside it does the reverse. Player-only, and stateless: both balances are
    /// already saved by their own owners.
    /// </summary>
    public class AtmStation : StationBase
    {
        [Tooltip("False withdraws from the account into the hand; true puts carried cash back.")]
        public bool deposit;

        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Bank;

        public override string ActionLabel => deposit ? "Deposit" : "ATM";

        public override string StatusValue
        {
            get
            {
                if (deposit)
                {
                    var cash = CashStack.Instance;
                    return cash == null ? string.Empty : MoneyFormat.Short(cash.Amount);
                }

                var wallet = GameRoot.Money;
                return wallet == null ? string.Empty : MoneyFormat.Short(wallet.Balance);
            }
        }

        public override bool IsOperational
        {
            get
            {
                if (deposit) return CashStack.Instance != null && CashStack.Instance.Amount >= 1d;
                var wallet = GameRoot.Money;
                return wallet != null && wallet.Balance >= 1d;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            workerCompatible = false;
        }

        protected override bool TickWithPlayer()
        {
            var cash = CashStack.Of(Carry);
            var wallet = GameRoot.Money;
            if (cash == null || wallet == null) return false;

            if (deposit)
            {
                if (cash.Amount < 1d) return false;
                double moved = cash.TrySpend(Math.Max(BundleStackView.BaseBundle, cash.Amount * 0.34d));
                if (moved <= 0d) return false;
                wallet.Add(moved);
            }
            else
            {
                if (wallet.Balance < 1d) return false;
                double moved = wallet.TrySpend(Math.Max(BundleStackView.BaseBundle, wallet.Balance * 0.34d));
                if (moved <= 0d) return false;
                cash.Add(moved);
            }

            SoundFx.PlayAt(Sfx.Coin, transform.position);
            return true;
        }
    }
}
