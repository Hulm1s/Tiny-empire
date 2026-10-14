using Tycoon.Audio;
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

        [Header("Takings")]
        [Tooltip("Where the money from a sale lands: the cash pile beside the till. Empty pays the account.")]
        public CashPile pile;

        [Tooltip("Once this purchase is owned, takings go straight to the account instead of the pile. " +
                 "The farm's tills point at the supermarket: after that the player never has to come back.")]
        public UnlockStation accountWhenUnlocked;

        [Tooltip("Share of a sale that reaches the account once the farm is let go (accountWhenUnlocked is owned). " +
                 "The farm tills use 0.5: the farm becomes wholesale after the supermarket, so the shop is the " +
                 "growth engine rather than a bigger farm. 1 pays in full. Does not touch the pile (before the " +
                 "supermarket) and does not touch a till with no pile (the shop's own).")]
        [Range(0f, 1f)] public float accountShare = 1f;

        [Header("Display counter (the butcher's only)")]
        [Tooltip("Leave empty for an ordinary till, which hands over what the player is carrying. " +
                 "Filled, this is a counter with a glass case: whoever stands here (the player or the " +
                 "butcher) first puts what they carry into the case - into the buffer that holds that " +
                 "product - and then serves the shopper at the front OUT of the case. A case that has not " +
                 "been bought yet is inactive and is simply not served from. " +
                 "One square does both on purpose: a hired butcher has one pickup and one dropoff, and a " +
                 "dropoff that restocks AND serves is what lets one worker keep the counter going.")]
        public ItemBuffer[] stock;

        /// <summary>True for a display counter (the butcher's), false for a plain till.</summary>
        public bool HasDisplay => stock != null && stock.Length > 0;

        /// <summary>The case for this product, if there is one that is switched on.</summary>
        public ItemBuffer StockFor(ItemDefinition item)
        {
            if (item == null || stock == null) return null;
            for (int i = 0; i < stock.Length; i++)
            {
                var b = stock[i];
                if (b != null && b.item == item && b.isActiveAndEnabled) return b;
            }
            return null;
        }

        /// <summary>Fired with the money taken per unit, for popups and audio.</summary>
        public event Action<double> Sold;

        /// <summary>Puts a sale where it belongs: the pile, or the account once the farm is let go.</summary>
        private void Pay(double price)
        {
            bool letGo = accountWhenUnlocked != null && accountWhenUnlocked.IsUnlocked;
            if (pile == null) GameRoot.Money?.Add(price);
            else if (letGo) GameRoot.Money?.Add(price * accountShare);
            else pile.Deposit(price);
        }

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
            if (HasDisplay) return TickCounter();

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

            Settle(wanted);
            return true;
        }

        /// <summary>
        /// A display counter's tick. Serve first: the shopper at the front takes one unit of what
        /// they asked for from the case. Otherwise restock: one carried unit goes into the case for
        /// its product, if the case has room. Doing both in one square is what lets the butcher
        /// (whose dropoff is this square) keep the case full AND the queue moving.
        /// </summary>
        private bool TickCounter()
        {
            var customer = queue.Front;
            var wanted = customer != null ? customer.Wanted : null;
            if (wanted != null)
            {
                var shelf = StockFor(wanted);
                if (shelf != null && shelf.Remove(1) > 0)
                {
                    if (customer.Deliver(wanted))
                    {
                        Settle(wanted);
                        return true;
                    }

                    // Not wanted after all: back into the case, never destroyed.
                    shelf.Add(1);
                }
            }

            if (Carry == null || Carry.IsEmpty) return false;

            for (int i = 0; i < stock.Length; i++)
            {
                var shelf = stock[i];
                if (shelf == null || !shelf.isActiveAndEnabled || shelf.item == null || shelf.IsFull) continue;
                if (!Carry.Has(shelf.item) || !Carry.TryRemove(shelf.item)) continue;

                // If the case somehow refuses it, hand it back rather than destroying goods.
                if (shelf.Add(1) == 0)
                {
                    Carry.TryAdd(shelf.item);
                    continue;
                }

                SoundFx.PlayAt(Sfx.Drop, shelf.transform.position);
                return true;
            }

            return false;
        }

        /// <summary>One unit sold: the money, the event, the sound and the popup.</summary>
        private void Settle(ItemDefinition wanted)
        {
            double price = wanted.basePrice * priceMultiplier * queue.PriceMultiplier;
            Pay(price);
            Sold?.Invoke(price);
            Tycoon.Tasks.TutorialEvents.Raise(Tycoon.Tasks.TutorialEvent.Sell, Carry);

            SoundFx.PlayAt(Sfx.Coin, transform.position);

            Tycoon.UI.WorldFeedback.AddMoney($"sale{GetInstanceID()}",
                transform.position + Vector3.up * 1.8f, Mathf.RoundToInt((float)price));
        }
    }
}
