using System;
using Tycoon.Config;
using Tycoon.Core;
using Tycoon.Customers;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The till. Stand behind it holding what the shopper at the front of the queue asked for
    /// and they are served one unit at a time, paying as they go.
    ///
    /// Unlike a plain sell counter this only pays out when somebody is actually asking for the
    /// goods, so production and demand have to be kept in balance rather than just maximised.
    /// Workers can stand here too, which is how a shop becomes self-running - at a price.
    /// </summary>
    public class RegisterStation : StationBase
    {
        [Header("Customers")]
        public CustomerQueue queue;

        [Tooltip("What this till sells.\n\n" +
                 "A shopper is only ever sent to a counter that lists what they came for, so a " +
                 "till can never take an order it has no way of filling. Both counters used to " +
                 "share one menu of every product on the farm, which meant milk shoppers queued " +
                 "at the egg counter - where no worker ever brings milk - and blocked everyone " +
                 "behind them until their patience ran out.\n\n" +
                 "Leave empty and this till serves nobody.")]
        public ItemDefinition[] sells;

        [Tooltip("Applied on top of the item price and the shop's reputation multiplier.")]
        public float priceMultiplier = 1f;

        /// <summary>
        /// Whether this till is allowed to take an order for these goods.
        ///
        /// This is the one question the routing turns on. Today each queue asks its own
        /// register before inventing an order; a central router covering several shops would
        /// ask every register the same question and pick a counter that answers yes. Either
        /// way, adding a till that sells a product is all it takes to make that product's
        /// shoppers start using it.
        /// </summary>
        public bool CanFulfill(ItemDefinition item)
        {
            if (item == null || sells == null) return false;

            for (int i = 0; i < sells.Length; i++)
                if (sells[i] == item) return true;

            return false;
        }

        /// <summary>Fired with the money taken per unit, for popups and audio.</summary>
        public event Action<double> Sold;

        public override bool IsOperational => queue != null && queue.Front != null;

        /// <summary>The ring fills as the front customer's order is completed.</summary>
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
                return customer.Wanted == null ? string.Empty : "x" + customer.Remaining;
            }
        }

        /// <summary>
        /// Shows what the shopper at the front is actually asking for, so the player can read
        /// the order off the square without walking round to look at the bubble.
        /// </summary>
        public override ItemDefinition IconItem
        {
            get
            {
                var customer = queue != null ? queue.Front : null;
                if (customer != null && customer.Wanted != null) return customer.Wanted;

                // Nobody waiting: show what this till deals in, so an idle counter still says
                // whether it is the egg one or the milk one.
                return sells != null && sells.Length > 0 ? sells[0] : null;
            }
        }

        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Serve;

        protected override bool TickWithPlayer()
        {
            if (queue == null) return false;

            var customer = queue.Front;
            if (customer == null) return false;

            var wanted = customer.Wanted;
            if (wanted == null) return false;
            if (Carry == null || !Carry.Has(wanted)) return false;

            if (!Carry.TryRemove(wanted)) return false;

            if (!customer.Deliver(wanted))
            {
                // Hand it back rather than destroying goods.
                Carry.TryAdd(wanted);
                return false;
            }

            double price = wanted.basePrice * priceMultiplier * queue.PriceMultiplier;
            GameRoot.Money?.Add(price);
            Sold?.Invoke(price);

            Tycoon.UI.WorldFeedback.AddMoney($"sale{GetInstanceID()}",
                transform.position + Vector3.up * 1.8f, Mathf.RoundToInt((float)price));

            return true;
        }
    }
}
