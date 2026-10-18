using Tycoon.Audio;
using Tycoon.Customers;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The salon's STYLE square: a <see cref="ChairServiceStation"/> whose service, when the PLAYER does it, is the
    /// haircut minigame (<see cref="Tycoon.UI.HaircutMinigame"/>) - the way <see cref="BouquetCounter"/> turns the
    /// florist's counter into the bouquet minigame.
    ///
    /// Who serves decides how, and that is the design rather than a worker special case:
    ///   a hired hairdresser (a worker with this square as pickup and dropoff) is served by the base class: stand
    ///     here <c>serviceSeconds</c> and the whole order is settled at the base price, no tip;
    ///   the PLAYER cuts the hair by hand: after a short wait in the square with a customer sitting in the chair
    ///     the minigame opens; when it is done the haircut is paid at the same price plus a tip for how close it was.
    /// Nothing is paid until the minigame finishes, so closing it early loses nothing. The square re-arms when the
    /// player steps out (after a close) or for the next customer (after a finished cut).
    /// </summary>
    public class SalonChairStation : ChairServiceStation
    {
        [Header("Haircut")]
        [Tooltip("Seconds of standing in the square before the minigame opens, so walking across it does nothing.")]
        [Min(0f)] public float openAfter = 0.6f;

        [Tooltip("Tip as a share of the haircut's price, for one, two and three stars.")]
        public float[] tipShare = { 0.2f, 0.45f, 0.75f };

        private float _dwell;
        private bool _armed = true;
        private CustomerAgent _cutting;

        private void Start()
        {
            // The screen is built when the salon opens, so the first haircut does not wait for it.
            Tycoon.UI.HaircutMinigame.Instance?.Prewarm();
        }

        /// <summary>What this haircut sells for before any tip.</summary>
        public double Quote(CustomerAgent customer)
        {
            if (customer == null || customer.Wanted == null) return 0d;
            return UnitPrice(customer.Wanted) * customer.Remaining;
        }

        public double Tip(double price, int stars) =>
            tipShare == null || tipShare.Length == 0 || stars <= 0 ? 0d
                : price * tipShare[Mathf.Clamp(stars, 1, tipShare.Length) - 1];

        /// <summary>The customer sitting in the chair, ready to be cut (what the task list and the minigame ask).</summary>
        public CustomerAgent ReadyCustomer => Serving;

        protected override bool TickWithPlayer()
        {
            var minigame = Tycoon.UI.HaircutMinigame.Instance;
            bool player = Carry != null && Carry.CompareTag("Player");
            if (!player || minigame == null) return base.TickWithPlayer();

            // The player: no counting down here, the haircut is done in the minigame.
            if (!_armed || minigame.IsOpen) return false;

            var customer = Serving;
            if (customer == null) { _dwell = 0f; return false; }

            _dwell += tickInterval;
            if (_dwell < openAfter) return false;

            _dwell = 0f;
            _cutting = customer;
            minigame.Open(this, customer, Quote(customer));
            return false;
        }

        /// <summary>
        /// The minigame is done: the haircut is paid at the usual price (unit by unit, as a worker would, which also
        /// sends the customer off happy), then the tip on top. False and nothing taken if anything changed under it.
        /// </summary>
        public bool Finish(int stars, out double price, out double tip)
        {
            price = 0d;
            tip = 0d;
            var customer = _cutting;
            _cutting = null;
            if (customer == null || customer != Serving) return false;

            double quote = Quote(customer);
            tip = Tip(quote, stars);
            price = ServeWhole(customer);
            if (price <= 0d) { tip = 0d; return false; }

            if (tip > 0d)
            {
                Pay(tip);
                Tycoon.UI.WorldFeedback.Show(transform.position + Vector3.up * 2.6f,
                    "TIP +" + Tycoon.Core.MoneyFormat.Short(tip), new Color(1f, 0.86f, 0.35f));
            }
            SoundFx.PlayAt(Sfx.Coin, transform.position);
            return true;
        }

        /// <summary>Closed without finishing: nothing was paid; wait until the player steps out.</summary>
        public void Abandon()
        {
            _cutting = null;
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
