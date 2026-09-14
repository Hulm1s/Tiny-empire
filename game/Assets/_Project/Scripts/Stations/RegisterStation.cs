using System;
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

        [Tooltip("Applied on top of the item price and the shop's reputation multiplier.")]
        public float priceMultiplier = 1f;

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

        public override string StatusText
        {
            get
            {
                if (queue == null) return label;

                var customer = queue.Front;
                if (customer == null) return $"{label} - waiting";

                return customer.Wanted == null
                    ? label
                    : $"{label} {customer.Wanted.displayName} x{customer.Remaining}";
            }
        }

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
