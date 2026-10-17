using Tycoon.Audio;
using Tycoon.Config;
using Tycoon.Customers;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The florist's counter: a <see cref="RegisterStation"/> display counter whose cases are the
    /// flower stocks the beds fill. Customers want a bouquet (several flowers, see
    /// <see cref="CustomerQueue.bouquets"/>), paid flower by flower at the ordinary price.
    ///
    /// Who serves decides how, and that is the design rather than a worker special case:
    ///   the florist (a hired worker) is served by the base class, one flower per tick out of the
    ///     stock, at the base price - exactly like the butcher at his counter;
    ///   the PLAYER makes the bouquet by hand in the bouquet minigame (<see cref="BouquetMinigame"/>):
    ///     after a short wait in the square, with a customer at the front and every flower of their
    ///     bouquet in stock, the minigame opens; when it is done the whole bouquet is handed over at
    ///     the same price plus a tip for how well it was made.
    /// Nothing is taken from the stock until the bouquet is finished, so closing the minigame early
    /// loses nothing. The square re-arms when the player steps out (after a close) or for the next
    /// customer (after a finished bouquet).
    /// </summary>
    public class BouquetCounter : RegisterStation
    {
        [Header("Bouquet")]
        [Tooltip("Seconds of standing in the square before the minigame opens, so walking across it does nothing.")]
        [Min(0f)] public float openAfter = 0.6f;

        [Tooltip("Tip as a share of the bouquet's price, for one, two and three stars.")]
        public float[] tipShare = { 0.2f, 0.45f, 0.75f };

        [Tooltip("What the idle square shows.")]
        public ItemDefinition bouquetIcon;

        private float _dwell;
        private bool _armed = true;
        private CustomerAgent _making;

        private void Start()
        {
            // The minigame's screen is built when the florist opens (or the game loads with it open),
            // so the first bouquet does not wait for it.
            Tycoon.UI.BouquetMinigame.Instance?.Prewarm();
        }

        public override ItemDefinition IconItem => bouquetIcon != null ? bouquetIcon : base.IconItem;

        public override string StatusValue
        {
            get
            {
                var customer = queue != null ? queue.Front : null;
                if (customer == null) return "-";
                if (!CanMakeWhole(customer)) return "Growing";
                return "x" + customer.Remaining;
            }
        }

        /// <summary>Every flower the customer still wants is in stock (the player only makes whole bouquets).</summary>
        public bool CanMakeWhole(CustomerAgent customer)
        {
            if (customer == null || !customer.IsOrder) return false;
            for (int i = 0; i < customer.OrderLines; i++)
            {
                int need = customer.OrderRemaining(i);
                if (need <= 0) continue;
                var shelf = StockFor(customer.OrderItem(i));
                if (shelf == null || shelf.Count < need) return false;
            }
            return true;
        }

        /// <summary>The front customer, if the player could make their bouquet right now.</summary>
        public CustomerAgent ReadyCustomer
        {
            get
            {
                var customer = queue != null ? queue.Front : null;
                return CanMakeWhole(customer) ? customer : null;
            }
        }

        /// <summary>What the front customer's bouquet sells for before any tip.</summary>
        public double Quote(CustomerAgent customer)
        {
            double total = 0d;
            if (customer == null) return total;
            for (int i = 0; i < customer.OrderLines; i++)
                total += UnitPrice(customer.OrderItem(i)) * customer.OrderRemaining(i);
            return total;
        }

        public double Tip(double price, int stars) =>
            tipShare == null || tipShare.Length == 0 || stars <= 0 ? 0d
                : price * tipShare[Mathf.Clamp(stars, 1, tipShare.Length) - 1];

        protected override bool TickWithPlayer()
        {
            var minigame = Tycoon.UI.BouquetMinigame.Instance;
            bool player = Carry != null && Carry.CompareTag("Player");
            if (!player || minigame == null) return base.TickWithPlayer();

            // The player: no flower by flower here, the bouquet is made in the minigame.
            if (!_armed || minigame.IsOpen) return false;

            var customer = ReadyCustomer;
            if (customer == null) { _dwell = 0f; return false; }

            _dwell += tickInterval;
            if (_dwell < openAfter) return false;

            _dwell = 0f;
            _making = customer;
            minigame.Open(this, customer, Quote(customer));
            return false;
        }

        /// <summary>
        /// The minigame is done: hand the whole bouquet over (each flower out of stock and paid at the
        /// usual price, as a worker would), then the tip on top. Returns false and takes nothing if
        /// anything changed under it (it cannot while the world is paused, but never trust that).
        /// </summary>
        public bool Finish(int stars, out double price, out double tip)
        {
            price = 0d;
            tip = 0d;
            var customer = _making;
            _making = null;
            if (customer == null || customer != queue.Front || !CanMakeWhole(customer)) return false;

            price = Quote(customer);
            tip = Tip(price, stars);

            for (int i = 0; i < customer.OrderLines; i++)
            {
                var item = customer.OrderItem(i);
                var shelf = StockFor(item);
                int need = customer.OrderRemaining(i);
                for (int n = 0; n < need; n++)
                {
                    if (shelf == null || shelf.Remove(1) == 0) break;
                    if (!customer.Wants(item)) { shelf.Add(1); break; }
                    // Paid before it is handed over: the last flower makes the customer leave happy, which
                    // nudges the shop's reputation, and the bouquet must cost what the minigame showed.
                    Settle(item);
                    customer.Deliver(item);
                }
            }

            if (tip > 0d)
            {
                Pay(tip);
                Tycoon.UI.WorldFeedback.Show(transform.position + Vector3.up * 2.6f,
                    "TIP +" + Tycoon.Core.MoneyFormat.Short(tip), new Color(1f, 0.86f, 0.35f));
            }
            SoundFx.PlayAt(Sfx.Coin, transform.position);
            return true;
        }

        /// <summary>Closed without finishing: nothing was taken; wait until the player steps out.</summary>
        public void Abandon()
        {
            _making = null;
            _armed = false;
            _dwell = 0f;
        }

        protected override void OnPlayerExit()
        {
            base.OnPlayerExit();
            if (HasPlayer) return;
            _armed = true;
            _dwell = 0f;
        }
    }
}
