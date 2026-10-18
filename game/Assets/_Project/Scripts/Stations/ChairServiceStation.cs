using Tycoon.Customers;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The square beside a <see cref="ServiceChair"/> where the server stands: the salon's STYLE square.
    ///
    /// A <see cref="RegisterStation"/> in every way that matters - same menu (<see cref="RegisterStation.sells"/>),
    /// same price (item price x <see cref="RegisterStation.priceMultiplier"/> x the queue's reputation), same
    /// <c>Settle</c>, same cash pile (<see cref="RegisterStation.pile"/>, which may be a pile anywhere: the salon
    /// points every chair at the one pile on the reception desk) - except for WHO it serves: not the head of
    /// the queue but whoever is sitting in <see cref="chair"/>. That is the only thing overridden
    /// (<see cref="Serving"/>), so a chair cannot drift from how every other till is paid.
    ///
    /// What is handed over is a service, not goods: nothing is taken from anybody's arms. Standing here with a
    /// customer in the chair fills the ring over <see cref="serviceSeconds"/>; when it is full the whole order is
    /// settled unit by unit (each paid, then delivered - the customer then leaves happy). The player and a hired
    /// worker (route pickup = dropoff = this square) serve it identically; the salon's own subclass overrides
    /// <see cref="TickWithPlayer"/> for the player only (the haircut minigame) and finishes through
    /// <see cref="ServeWhole"/> plus <c>Pay(tip)</c>, exactly as <see cref="BouquetCounter"/> does.
    ///
    /// Wiring checklist (the level builder): <c>queue</c> = the salon's queue (prices read its reputation - with
    /// no queue a service is free), <c>sells</c> = the services on offer, <c>pile</c> = the reception pile,
    /// <c>chair</c> = the chair this square stands beside.
    /// </summary>
    public class ChairServiceStation : RegisterStation
    {
        [Header("Chair")]
        [Tooltip("The chair whose occupant this square serves.")]
        public ServiceChair chair;

        [Tooltip("Seconds of standing here, with a customer in the chair, for one whole service.")]
        [Min(0f)] public float serviceSeconds = 3f;

        private float _work;
        private CustomerAgent _workingOn;

        protected override CustomerAgent Serving => chair != null ? chair.ReadyOccupant : null;

        /// <summary>The ring: how far through the current customer's service.</summary>
        protected override float TransferProgress
        {
            get
            {
                var customer = Serving;
                if (customer == null || customer != _workingOn || serviceSeconds <= 0f) return 0f;
                return Mathf.Clamp01(_work / serviceSeconds);
            }
        }

        protected override bool TickWithPlayer()
        {
            var customer = Serving;
            if (customer == null)
            {
                _work = 0f;
                _workingOn = null;
                return false;
            }

            // A new face in the chair starts from nothing; a server who steps out and back in carries on.
            if (customer != _workingOn)
            {
                _workingOn = customer;
                _work = 0f;
            }

            _work += tickInterval;
            if (_work < serviceSeconds) return true;

            _work = 0f;
            _workingOn = null;
            int before = customer.Remaining;
            bool served = ServeWhole(customer) > 0d;
            ChargeWorker(before - customer.Remaining);
            return served;
        }

        /// <summary>
        /// A hired server takes its cut of every unit served, as a cashier does at the supermarket's checkout
        /// (<see cref="CheckoutStation"/>): a service moves nothing out of the worker's arms, so the carry-count
        /// fee in WorkerAgent never sees it. Short pay is tolerated, never fatal - a hairdresser who downed tools
        /// on an empty wallet would stop the only thing earning money here.
        /// </summary>
        private void ChargeWorker(int units)
        {
            if (Carry == null || units <= 0) return;

            var worker = Carry.GetComponent<Tycoon.Upkeep.WorkerAgent>();
            if (worker == null || worker.feePerDelivery <= 0d) return;

            Tycoon.Core.GameRoot.Money?.TrySpend(worker.feePerDelivery * units);
        }

        /// <summary>
        /// Settles and hands over everything <paramref name="customer"/> still wants, one unit at a time, at the
        /// usual price - paid BEFORE each unit is delivered, because the last one makes the customer leave (and
        /// nudges the shop's reputation). Returns the money taken. Safe to call from a minigame's finish, outside
        /// a tick: only a customer sitting in this square's chair is served.
        /// </summary>
        protected double ServeWhole(CustomerAgent customer)
        {
            if (customer == null || customer != Serving) return 0d;

            double total = 0d;
            for (int guard = 0; guard < 64 && customer.IsAtChair && !customer.IsSatisfied; guard++)
            {
                var item = customer.Wanted;
                if (item == null || !customer.Wants(item)) break;
                total += UnitPrice(item);
                Settle(item);
                customer.Deliver(item);
            }
            return total;
        }
    }
}
