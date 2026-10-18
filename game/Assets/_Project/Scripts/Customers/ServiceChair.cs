using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tycoon.Customers
{
    /// <summary>
    /// A place a customer is served sitting down - the salon's styling chair - as opposed to a till,
    /// where the customer at the front of the queue is served standing.
    ///
    /// Not a station and not a square. It is the customer-side half of a chair: where the customer
    /// sits, how they get there and away, and who is in it. The square the server stands on to do
    /// the work is a separate <see cref="Tycoon.Stations.ChairServiceStation"/> that points at it, so
    /// the player and a hired worker serve a chair exactly as they serve anything else.
    ///
    /// The flow, end to end:
    ///   1. something calls <see cref="CustomerQueue.DispatchFront(ServiceChair)"/> (the NEXT square,
    ///      <see cref="Tycoon.Stations.CallNextStation"/>) - the head of the queue is sent here only if
    ///      <see cref="IsFree"/>; the chair is <see cref="Occupant"/>'s from that moment, so two calls
    ///      can never send two people to one chair;
    ///   2. the customer walks <see cref="approach"/> then sits on <see cref="Seat"/>, facing its forward,
    ///      and waits with the usual bubble and patience (<see cref="CustomerAgent.Phase.AtChair"/>);
    ///   3. the chair's station serves <see cref="ReadyOccupant"/> and is paid through RegisterStation's
    ///      Settle, into whichever cash pile that station names;
    ///   4. served or out of patience, the customer stands up, walks <see cref="exitRoute"/> and then the
    ///      queue's own exit route, and <see cref="Released"/> fires - with served = true only for a
    ///      finished service, which is the moment the salon drops hair on the floor.
    ///
    /// Nothing here is saved, on purpose. Customers are never saved (a reload starts with an empty
    /// shop), so an occupant can never outlive the session it was sent in. The occupant is also read
    /// through Unity's null - a customer destroyed for any reason frees the chair by itself - and a
    /// customer who is no longer heading for or sitting in this chair is not counted as its occupant.
    /// What DOES persist is <see cref="dirtyWhen"/>, because that is the salon's own saved object.
    /// </summary>
    public class ServiceChair : MonoBehaviour
    {
        [Tooltip("Where the seated customer's origin goes: the middle of the seat in plan. Its forward is the " +
                 "way they face (towards the mirror). Empty means this object.\n\n" +
                 "Height: the villager's Sit clip puts the underside of the seated body 0.16 m above the origin " +
                 "(hips 0.26 m), slid back 0.12 m; knees reach 0.24 m and toes 0.31 m in front of it. So put this " +
                 "point at (seat top - 0.16 m). For a low chair whose seat top is ~0.16 m that is floor level and " +
                 "the feet rest on the floor; for a taller chair it is above the floor, the customer is lifted onto " +
                 "it (feet on the footrest) and walks back down when they get up.")]
        public Transform seat;

        [Tooltip("Waypoints walked, in order, from the waiting room to the seat - e.g. a point in front of the " +
                 "chair, so nobody walks through its back or the mirror. Empty means a straight line.")]
        public Transform[] approach;

        [Tooltip("Waypoints walked, in order, from the seat after standing up, before the queue's own exit " +
                 "route (CustomerQueue.exitRoute) and exit point.")]
        public Transform[] exitRoute;

        [Tooltip("Optional. While this object is active the chair is not clean and nobody is sent to it - the " +
                 "salon's hair heap (or its SWEEP square). Sweeping it away (deactivating it) frees the chair. " +
                 "Sweeping is free, so a dirty chair never needs money to recover.")]
        public GameObject dirtyWhen;

        private CustomerAgent _occupant;

        /// <summary>
        /// Fired when the occupant gets up for good: (this chair, the customer, served). Served is true
        /// only when the service was completed (they leave happy), false when they ran out of patience.
        /// </summary>
        public event Action<ServiceChair, CustomerAgent, bool> Released;

        public Transform Seat => seat != null ? seat : transform;

        /// <summary>
        /// Whoever this chair belongs to right now: walking to it, or sitting in it. Self-healing - a
        /// destroyed customer, or one that has gone elsewhere, does not count.
        /// </summary>
        public CustomerAgent Occupant
        {
            get
            {
                if (_occupant == null) return null; // Unity null: destroyed
                if (_occupant.Chair != this) { _occupant = null; return null; }
                return _occupant;
            }
        }

        /// <summary>The occupant, if they are sitting here waiting to be served (not still walking in).</summary>
        public CustomerAgent ReadyOccupant
        {
            get
            {
                var c = Occupant;
                return c != null && c.IsAtChair && !c.IsSatisfied ? c : null;
            }
        }

        /// <summary>Not under hair: <see cref="dirtyWhen"/> is unset or inactive.</summary>
        public bool IsClean => dirtyWhen == null || !dirtyWhen.activeInHierarchy;

        /// <summary>Can a customer be sent here now: the chair is bought (active), clean and nobody's.</summary>
        public bool IsFree => isActiveAndEnabled && IsClean && Occupant == null;

        /// <summary>Called by the queue when it sends a customer here. False if the chair is not free.</summary>
        internal bool Claim(CustomerAgent customer)
        {
            if (customer == null || !IsFree) return false;
            _occupant = customer;
            return true;
        }

        /// <summary>Called by the customer as they get up to leave.</summary>
        internal void Release(CustomerAgent customer, bool served)
        {
            if (customer == null || (_occupant != null && _occupant != customer)) return;
            _occupant = null;
            Released?.Invoke(this, customer, served);
        }

        public void AppendApproach(List<Vector3> into)
        {
            if (approach == null) return;
            foreach (var point in approach)
                if (point != null) into.Add(point.position);
        }

        public void AppendExit(List<Vector3> into)
        {
            if (exitRoute == null) return;
            foreach (var point in exitRoute)
                if (point != null) into.Add(point.position);
        }
    }
}
