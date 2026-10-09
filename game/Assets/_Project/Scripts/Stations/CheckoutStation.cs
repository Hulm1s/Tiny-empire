using Tycoon.Audio;
using System;
using Tycoon.Config;
using Tycoon.Core;
using Tycoon.Customers;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The supermarket checkout. Stand behind it and the shopper at the front of the queue has
    /// their basket scanned, one unit per tick, paying as they go.
    ///
    /// The sibling of <see cref="RegisterStation"/>, and the difference is the whole point: a
    /// farm till takes goods the player carries over to it, whereas a supermarket shopper has
    /// already taken their goods off the shelves, so there is nothing to hand over - just a
    /// basket to scan. The queue is the same <see cref="CustomerQueue"/> and the head-of-line
    /// rule is the same <see cref="CustomerQueue.Front"/>; only what happens at the front differs.
    ///
    /// Workers can stand here too, which is how a cashier is hired. A cashier never carries
    /// anything, so the usual piece rate - charged by the worker when its arms empty - would
    /// never fire; the fee is charged here instead, per unit scanned, to keep a cashier a
    /// running cost that scales with the work done.
    /// </summary>
    public class CheckoutStation : StationBase
    {
        [Header("Customers")]
        public CustomerQueue queue;

        [Tooltip("Applied on top of the item price and the shop's reputation multiplier. The " +
                 "supermarket sells at one and a half times what the same goods fetch on the farm.")]
        public float priceMultiplier = 1.5f;

        /// <summary>Fired with the money taken per unit, for popups and audio.</summary>
        public event Action<double> Sold;

        public override bool IsOperational => queue != null && queue.Front != null;

        /// <summary>The ring fills as the front shopper's basket is scanned.</summary>
        protected override float TransferProgress
        {
            get
            {
                var customer = queue != null ? queue.Front : null;
                if (customer == null || customer.Requested <= 0) return 0f;
                return 1f - (float)customer.Remaining / customer.Requested;
            }
        }

        public override string StatusValue
        {
            get
            {
                var customer = queue != null ? queue.Front : null;
                if (customer == null) return "-";
                return "x" + customer.Remaining;
            }
        }

        /// <summary>What is about to be scanned, so the square reads the basket like a bubble does.</summary>
        public override ItemDefinition IconItem
        {
            get
            {
                var customer = queue != null ? queue.Front : null;
                return customer != null ? customer.Wanted : null;
            }
        }

        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Serve;

        protected override bool TickWithPlayer()
        {
            if (queue == null) return false;

            var customer = queue.Front;
            if (customer == null) return false;

            if (!customer.ScanNext(out ItemDefinition item) || item == null) return false;

            double price = item.basePrice * priceMultiplier * queue.PriceMultiplier;
            GameRoot.Money?.Add(price);
            Sold?.Invoke(price);

            SoundFx.PlayAt(Sfx.Coin, transform.position);

            Tycoon.UI.WorldFeedback.AddMoney($"sale{GetInstanceID()}",
                transform.position + Vector3.up * 1.8f, Mathf.RoundToInt((float)price));

            ChargeWorker();
            return true;
        }

        /// <summary>
        /// A hired cashier takes its cut of every unit it scans. Short pay is tolerated, never
        /// fatal: a cashier who downs tools when the wallet is empty would stop the only thing
        /// that earns money here.
        /// </summary>
        private void ChargeWorker()
        {
            if (Carry == null) return;

            var worker = Carry.GetComponent<Tycoon.Upkeep.WorkerAgent>();
            if (worker == null || worker.feePerDelivery <= 0d) return;

            GameRoot.Money?.TrySpend(worker.feePerDelivery);
        }
    }
}
