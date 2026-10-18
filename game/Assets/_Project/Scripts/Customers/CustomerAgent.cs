using Tycoon.Audio;
using System.Collections.Generic;
using Tycoon.Characters;
using Tycoon.Config;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.Customers
{
    /// <summary>
    /// One thing on a supermarket shopper's list: take up to <see cref="wanted"/> units of an
    /// item from a shelf. <see cref="taken"/> is how many they actually got.
    /// </summary>
    public class ShoppingStop
    {
        public StoreShelf shelf;
        public ItemDefinition item;
        public int wanted;
        public int taken;
    }

    /// <summary>
    /// One shopper. Walks in off the road, stands at the counter holding up an order, and
    /// either leaves happy once it is filled or stomps off when they run out of patience.
    ///
    /// Customers are what turn "sell goods" into "run a shop": eggs are only worth money if
    /// somebody is at the counter wanting eggs, so an unattended shop stops earning even
    /// while the coops keep producing.
    /// </summary>
    public class CustomerAgent : MonoBehaviour, ISeatedPose
    {
        // Shopping is last on purpose: the values are never saved, but nothing that already
        // reads Arriving, Waiting or Leaving should have to care that browsing exists. The chair
        // phases come after it for the same reason: a customer only ever enters them when a
        // queue sends them to a ServiceChair (CustomerQueue.DispatchFront), so a till that has
        // no chairs never sees them.
        //   ToChair - called out of the queue, walking to the chair (ServiceChair.approach, then the seat)
        //   AtChair - sitting in the chair waiting to be served; patience and bubble as at a till
        public enum Phase { Arriving, Waiting, Leaving, Shopping, ToChair, AtChair }

        [Header("Movement")]
        public float moveSpeed = 2.7f;
        public float turnSpeed = 720f;
        public float arriveRadius = 0.22f;

        [Header("Patience")]
        [Tooltip("Seconds they will stand at the counter before giving up. Generous on " +
                 "purpose: a queue that times out faster than the player can walk the length " +
                 "of the farm punishes them for playing it as designed.")]
        public float patienceSeconds = 75f;

        [Header("Browsing (supermarket shoppers only)")]
        [Tooltip("Seconds between units lifted off a shelf, so the shelf visibly thins rather " +
                 "than being emptied in a frame.")]
        public float takeInterval = 0.35f;

        [Tooltip("Seconds a shopper will stand at an empty shelf hoping for stock before they " +
                 "give up on that item and move on.")]
        public float maxWaitAtShelf = 8f;

        [Header("Parts")]
        public Transform visual;
        public OrderBubble bubble;
        public Renderer tintTarget;

        [Tooltip("Which material slot gets the shopper's colour. -1 tints the whole " +
                 "renderer, which on a villager would recolour skin and boots too.")]
        public int tintMaterialIndex = -1;

        private CustomerQueue _queue;
        private ItemDefinition _wanted;
        private int _requested;
        private int _delivered;
        private float _patienceLeft;
        private Vector3 _target;
        private float _bobPhase;

        // Browsing. A farm customer never touches any of this: with no stops and no route they
        // go straight from the road to their slot exactly as they always did.
        private readonly List<Vector3> _route = new List<Vector3>();
        private List<ShoppingStop> _stops;
        private readonly List<BasketLine> _basket = new List<BasketLine>();
        private int _stopIndex;
        private float _takeTimer;
        private float _shelfWait;
        private bool _hasFacePoint;
        private Vector3 _facePoint;
        private bool _approached;

        // The shelf-route nodes this shopper has walked out along (see StoreShelf.via), hub
        // first. Empty for a farm customer and for any shelf with no waypoints.
        private readonly List<Transform> _trail = new List<Transform>();
        private bool _stopPrepared;

        // A florist customer: the basket is filled at the door with the bouquet they want
        // (BeginOrder), and is handed over line by line by Deliver rather than scanned.
        private bool _isOrder;

        // Seated customers (the salon). _seat is the seat they are walking to or sitting on - a
        // waiting-room place of a queue with seatedWaiting, or a ServiceChair's seat - and null for
        // every standing customer, which is every customer of every other shop. _chair is the
        // chair they were sent to; it stays set until they get up to leave.
        private Transform _seat;
        private ServiceChair _chair;

        // A seat may stand higher than the floor (a hydraulic salon chair): the customer is lifted onto
        // it and walks back down to _groundY - the height they came in at - once they get up. Off for
        // anyone who has never sat on a raised seat, so nobody else's height is ever touched.
        private float _groundY;
        private bool _raised;

        private class BasketLine
        {
            public ItemDefinition item;
            public int count;
            public int scanned;
        }

        public Phase CurrentPhase { get; private set; } = Phase.Arriving;
        public ItemDefinition Wanted => _wanted;
        public int Requested => _requested;
        public int Remaining => Mathf.Max(0, _requested - _delivered);
        public bool IsSatisfied => _delivered >= _requested;

        /// <summary>Ready to be served: standing still at the counter with an unfilled order.</summary>
        public bool IsWaiting => CurrentPhase == Phase.Waiting;

        /// <summary>Sitting in a <see cref="ServiceChair"/>, waiting to be served there.</summary>
        public bool IsAtChair => CurrentPhase == Phase.AtChair;

        /// <summary>The chair this customer was sent to (walking there, or in it); null for everyone else.</summary>
        public ServiceChair Chair => _chair;

        /// <summary>
        /// On a seat right now - a waiting-room chair or a service chair. Read by
        /// <see cref="CharacterVisual"/>, which plays the Sit clip while it is true.
        /// </summary>
        public bool IsSeated => _seat != null && (CurrentPhase == Phase.Waiting || CurrentPhase == Phase.AtChair);

        /// <summary>Where an order can be handed over: at the front of a queue, or in a chair.</summary>
        private bool IsServable => CurrentPhase == Phase.Waiting || CurrentPhase == Phase.AtChair;

        /// <param name="entryRoute">
        /// Optional waypoints to walk before heading for the slot - the butcher's shoppers come off the
        /// street and must go in through the door, not straight across the wall. A farm customer has
        /// none and walks the straight line, as it always did.
        /// </param>
        /// <param name="seat">
        /// The waiting-room seat at <paramref name="slot"/> when the queue's places are chairs
        /// (<see cref="CustomerQueue.seatedWaiting"/>); null for a standing queue - every till so far.
        /// </param>
        public void Begin(CustomerQueue queue, ItemDefinition item, int count, Vector3 slot, Color tint,
            IList<Vector3> entryRoute = null, Transform seat = null)
        {
            _queue = queue;
            _isOrder = false;
            _seat = seat;
            _chair = null;
            _groundY = transform.position.y;
            _raised = false;
            _basket.Clear();
            _wanted = item;
            _requested = Mathf.Max(1, count);
            _delivered = 0;
            _patienceLeft = patienceSeconds;
            CurrentPhase = Phase.Arriving;
            _target = slot;

            _route.Clear();
            if (entryRoute != null) _route.AddRange(entryRoute);

            ApplyTint(tint);

            if (bubble != null) bubble.Show(_wanted, Remaining);
        }

        private void ApplyTint(Color tint)
        {
            if (tintTarget == null) return;

            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);

            if (tintMaterialIndex >= 0)
                tintTarget.SetPropertyBlock(block, tintMaterialIndex);
            else
                tintTarget.SetPropertyBlock(block);
        }

        /// <summary>
        /// Starts a customer who wants several products at once - a florist's bouquet, e.g. three
        /// roses and two tulips. Walks in and queues exactly like a farm customer (<see cref="Begin"/>);
        /// the order is kept in the same basket lines a supermarket shopper uses, and the till hands it
        /// over one flower at a time through <see cref="Deliver"/>, in any order.
        /// </summary>
        public void BeginOrder(CustomerQueue queue, IList<ItemDefinition> items, IList<int> counts, Vector3 slot,
            Color tint, IList<Vector3> entryRoute = null, Transform seat = null)
        {
            _queue = queue;
            _isOrder = true;
            _seat = seat;
            _chair = null;
            _groundY = transform.position.y;
            _raised = false;
            _stops = null;
            _basket.Clear();
            _requested = 0;
            for (int i = 0; i < items.Count; i++)
            {
                int n = Mathf.Max(1, counts[i]);
                _basket.Add(new BasketLine { item = items[i], count = n });
                _requested += n;
            }
            _delivered = 0;
            _wanted = null;
            RefreshWanted();
            _patienceLeft = patienceSeconds;
            CurrentPhase = Phase.Arriving;
            _target = slot;

            _route.Clear();
            if (entryRoute != null) _route.AddRange(entryRoute);

            ApplyTint(tint);
            ShowBubble();
        }

        /// <summary>True for a several-product order (a bouquet).</summary>
        public bool IsOrder => _isOrder;

        /// <summary>Lines in a several-product order (0 for anyone else).</summary>
        public int OrderLines => _isOrder ? _basket.Count : 0;

        public ItemDefinition OrderItem(int line) => _basket[line].item;
        public int OrderCount(int line) => _basket[line].count;
        public int OrderRemaining(int line) => Mathf.Max(0, _basket[line].count - _basket[line].scanned);

        /// <summary>Would this customer take one of these now? The till's one question.</summary>
        public bool Wants(ItemDefinition item)
        {
            if (item == null || !IsServable || IsSatisfied) return false;
            if (!_isOrder) return item == _wanted;
            for (int i = 0; i < _basket.Count; i++)
                if (_basket[i].item == item && _basket[i].scanned < _basket[i].count) return true;
            return false;
        }

        private void ShowBubble()
        {
            if (bubble == null) return;
            if (!_isOrder) { bubble.Show(_wanted, Remaining); return; }

            var items = new ItemDefinition[_basket.Count];
            var left = new int[_basket.Count];
            for (int i = 0; i < _basket.Count; i++)
            {
                items[i] = _basket[i].item;
                left[i] = OrderRemaining(i);
            }
            bubble.ShowLines(items, left);
        }

        /// <summary>
        /// Starts a supermarket shopper: walk the entry route, visit each shelf on the list and
        /// take what they came for, then queue at the checkout with the basket.
        ///
        /// Nothing about the checkout is decided yet - the basket is whatever the shelves
        /// actually gave them. Shelves that were empty cost the shop reputation; a shopper who
        /// got nothing at all never queues.
        /// </summary>
        public void BeginShopping(CustomerQueue queue, List<ShoppingStop> stops,
            IList<Vector3> entryRoute, Color tint)
        {
            _queue = queue;
            _isOrder = false;
            _seat = null;
            _chair = null;
            _groundY = transform.position.y;
            _raised = false;
            _stops = stops;
            _stopIndex = 0;
            _takeTimer = 0f;
            _shelfWait = 0f;
            _basket.Clear();
            _hasFacePoint = false;
            _approached = false;
            _trail.Clear();
            _stopPrepared = false;
            _requested = 0;
            _delivered = 0;
            _wanted = null;
            _patienceLeft = patienceSeconds;

            _route.Clear();
            if (entryRoute != null) _route.AddRange(entryRoute);

            CurrentPhase = Phase.Shopping;
            _target = transform.position;

            ApplyTint(tint);
            if (bubble != null) bubble.SetVisible(false);
        }

        /// <summary>Called when the queue shuffles forward.</summary>
        /// <param name="seat">The seat at that place when the waiting room is seated; null when standing.</param>
        public void MoveToSlot(Vector3 slot, Transform seat = null)
        {
            if (CurrentPhase == Phase.Leaving) return;
            // Already out of the queue (sent to a chair): the queue no longer moves them.
            if (CurrentPhase == Phase.ToChair || CurrentPhase == Phase.AtChair) return;
            // Already sitting in that very seat: stay seated rather than stand up and sit down again.
            // Standing customers keep the old behaviour exactly (a frame of Arriving, then Waiting).
            if (seat != null && seat == _seat && CurrentPhase == Phase.Waiting) return;

            _seat = seat;
            _target = slot;
            if (CurrentPhase == Phase.Waiting) CurrentPhase = Phase.Arriving;
        }

        /// <summary>
        /// Called by <see cref="CustomerQueue.DispatchFront(ServiceChair)"/>: leave the queue (standing up
        /// if seated), walk <paramref name="route"/> and sit in <paramref name="chair"/> to be served.
        /// Patience starts again in full: being called is progress, and a customer who had nearly run out
        /// in the waiting room must not storm out of the chair a second after sitting down.
        /// </summary>
        public void GoToChair(ServiceChair chair, IList<Vector3> route)
        {
            _chair = chair;
            _seat = chair != null ? chair.Seat : null;
            _route.Clear();
            if (route != null) _route.AddRange(route);
            _target = _seat != null ? _seat.position : transform.position;
            _patienceLeft = patienceSeconds;
            CurrentPhase = Phase.ToChair;
            ShowBubble();
        }

        /// <summary>
        /// Rings up one unit of the basket. The supermarket's counterpart to
        /// <see cref="Deliver"/>: there the player hands goods over; here the goods are already in
        /// the basket and the checkout just scans them, one per tick.
        /// </summary>
        public bool ScanNext(out ItemDefinition scanned)
        {
            scanned = null;
            if (!IsWaiting || IsSatisfied) return false;

            for (int i = 0; i < _basket.Count; i++)
            {
                if (_basket[i].scanned >= _basket[i].count) continue;
                _basket[i].scanned++;
                scanned = _basket[i].item;
                break;
            }

            if (scanned == null) return false;

            _delivered++;
            RefreshWanted();
            if (bubble != null) bubble.Show(_wanted, Remaining);

            if (IsSatisfied) Leave(true);
            return true;
        }

        /// <summary>The next line of the basket still to be scanned, for the bubble and the till icon.</summary>
        private void RefreshWanted()
        {
            for (int i = 0; i < _basket.Count; i++)
            {
                if (_basket[i].scanned >= _basket[i].count) continue;
                _wanted = _basket[i].item;
                return;
            }
        }

        /// <summary>Hands over one unit. Returns false if this customer does not want it.</summary>
        public bool Deliver(ItemDefinition item)
        {
            if (!Wants(item)) return false;

            if (_isOrder)
            {
                for (int i = 0; i < _basket.Count; i++)
                {
                    if (_basket[i].item != item || _basket[i].scanned >= _basket[i].count) continue;
                    _basket[i].scanned++;
                    break;
                }
                _delivered++;
                RefreshWanted();
                ShowBubble();
                if (IsSatisfied) Leave(true);
                return true;
            }

            _delivered++;
            if (bubble != null) bubble.Show(_wanted, Remaining);

            if (IsSatisfied) Leave(true);
            return true;
        }

        private void Update()
        {
            float delta = Mathf.Min(Time.deltaTime, 0.05f);

            switch (CurrentPhase)
            {
                case Phase.Shopping:
                    UpdateShopping(delta);
                    break;

                case Phase.Arriving:
                    // A shopper first walks the joined till's approach, round its counter.
                    if (_route.Count > 0)
                    {
                        if (StepTowards(_route[0], delta)) _route.RemoveAt(0);
                    }
                    else if (StepTowards(_target, delta))
                    {
                        CurrentPhase = Phase.Waiting;
                    }
                    break;

                case Phase.Waiting:
                    Bob(delta, false);
                    if (_seat != null) SettleOnSeat(delta);
                    _patienceLeft -= delta;
                    if (bubble != null) bubble.SetPatience(_patienceLeft / Mathf.Max(1f, patienceSeconds));
                    if (_patienceLeft <= 0f) Leave(false);
                    break;

                case Phase.ToChair:
                    if (_route.Count > 0)
                    {
                        if (StepTowards(_route[0], delta)) _route.RemoveAt(0);
                    }
                    else if (StepTowards(_target, delta))
                    {
                        CurrentPhase = Phase.AtChair;
                    }
                    break;

                case Phase.AtChair:
                    Bob(delta, false);
                    SettleOnSeat(delta);
                    _patienceLeft -= delta;
                    if (bubble != null) bubble.SetPatience(_patienceLeft / Mathf.Max(1f, patienceSeconds));
                    if (_patienceLeft <= 0f) Leave(false);
                    break;

                case Phase.Leaving:
                    // Waypoints first (out through the door), then the exit itself. A farm
                    // customer has none and heads straight there.
                    if (_route.Count > 0)
                    {
                        if (StepTowards(_route[0], delta)) _route.RemoveAt(0);
                    }
                    else if (StepTowards(_target, delta))
                    {
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        /// <summary>Walk in, visit each shelf in turn, then go and queue.</summary>
        private void UpdateShopping(float delta)
        {
            if (_route.Count > 0)
            {
                if (StepTowards(_route[0], delta)) _route.RemoveAt(0);
                return;
            }

            if (_stops != null && _stopIndex < _stops.Count)
            {
                // Before walking up to a shelf, walk its aisle: back out of the aisle the last
                // one was in and in along this one's, as far as they differ.
                if (!_stopPrepared)
                {
                    _stopPrepared = true;
                    StoreShelf.Connect(_trail, _stops[_stopIndex].shelf.via, _route);
                    if (_route.Count > 0) return;
                }

                BrowseShelf(_stops[_stopIndex], delta);
                return;
            }

            FinishShopping(delta);
        }

        private void BrowseShelf(ShoppingStop stop, float delta)
        {
            var buffer = stop.shelf.buffer;
            if (stop.shelf.standPoint != null)
            {
                _hasFacePoint = true;
                _facePoint = buffer != null ? buffer.transform.position : stop.shelf.standPoint.position;
                if (!StepTowards(stop.shelf.standPoint.position, delta)) return;
            }

            bool done = stop.taken >= stop.wanted;

            if (!done)
            {
                _takeTimer -= delta;

                if (buffer != null && !buffer.IsEmpty)
                {
                    _shelfWait = 0f;
                    if (_takeTimer <= 0f && buffer.Remove(1) > 0)
                    {
                        stop.taken++;
                        _takeTimer = takeInterval;
                    }
                }
                else
                {
                    // Nothing there. Wait a while - somebody may be restocking - and then give
                    // up on this item. The shop pays for the empty shelf, not the shopper.
                    _shelfWait += delta;
                    if (_shelfWait >= maxWaitAtShelf)
                    {
                        if (_queue != null) _queue.OnItemSkipped();
                        done = true;
                    }
                }

                if (stop.taken >= stop.wanted) done = true;
            }

            Bob(delta, false);
            if (!done) return;

            if (stop.taken > 0)
                _basket.Add(new BasketLine { item = stop.item, count = stop.taken });

            _stopIndex++;
            _stopPrepared = false;
            _takeTimer = 0f;
            _shelfWait = 0f;
        }

        private void FinishShopping(float delta)
        {
            _hasFacePoint = false;

            if (_basket.Count == 0)
            {
                // Came for something and found nothing. Out of the door without queueing.
                Leave(false);
                return;
            }

            if (_requested == 0)
            {
                for (int i = 0; i < _basket.Count; i++) _requested += _basket[i].count;
                _delivered = 0;
                RefreshWanted();
            }

            // Out of the aisle first. The approach to a till is added only once a place in one
            // is taken: with two tills the shopper may join the other one, and each till's
            // approach rounds its own counter - the other's would lead across a counter.
            if (!_approached)
            {
                _approached = true;
                StoreShelf.Unwind(_trail, _route);
                if (_route.Count > 0) return;
            }

            // The queue may be full of people who are still being served. Wait where we stand
            // until a place opens up, rather than piling onto the last one.
            if (_queue != null && _queue.TryJoinQueue(this, out Vector3 slot, out CustomerQueue joined))
            {
                // From here on this is the till they actually joined, which matters when a
                // shop has two: it is the one they face, leave from and are counted by.
                _queue = joined;
                _route.Clear();
                joined.AppendQueueApproach(_route);
                _target = slot;
                _patienceLeft = patienceSeconds;
                CurrentPhase = Phase.Arriving;
                if (bubble != null) bubble.Show(_wanted, Remaining);
                return;
            }

            Bob(delta, false);
        }

        /// <summary>
        /// Onto the seat exactly and facing its way. Arrival counts within <see cref="arriveRadius"/>,
        /// which is fine standing but reads as hovering beside the chair once seated.
        /// </summary>
        private void SettleOnSeat(float delta)
        {
            Vector3 here = transform.position;
            Vector3 at = _seat.position;
            // Up onto a raised seat, too (the seat transform's height is where the feet go).
            if (!Mathf.Approximately(at.y, here.y)) _raised = true;
            transform.position = Vector3.MoveTowards(here, _raised ? at : new Vector3(at.x, here.y, at.z),
                moveSpeed * 0.5f * delta);

            Vector3 facing = _seat.forward;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(facing.normalized), turnSpeed * delta);
        }

        private void Leave(bool happy)
        {
            bool fromQueue = CurrentPhase == Phase.Arriving || CurrentPhase == Phase.Waiting;
            bool fromChair = _chair != null && (CurrentPhase == Phase.ToChair || CurrentPhase == Phase.AtChair);
            CurrentPhase = Phase.Leaving;
            _seat = null;
            if (bubble != null) bubble.SetVisible(false);

            SoundFx.PlayAt(happy ? Sfx.Happy : Sfx.Grumble, transform.position);

            Tycoon.UI.WorldFeedback.Show(
                transform.position + Vector3.up * 2.1f,
                happy ? "THANKS!" : "LEFT ANGRY",
                happy ? new Color(0.55f, 0.92f, 0.55f) : new Color(0.95f, 0.4f, 0.35f));

            if (_queue != null)
            {
                _target = _queue.ExitPosition;
                _route.Clear();
                // Out of the aisle first, if they are still in one (empty-handed, or timed out).
                StoreShelf.Unwind(_trail, _route);
                // Out of a chair by the chair's own way out; out of the queue by the queue's.
                if (fromChair) _chair.AppendExit(_route);
                else if (fromQueue) _queue.AppendQueueExit(_route);
                _queue.AppendExitRoute(_route);
            }

            // The chair is given up before the queue hears about it, so whatever the salon does on
            // ServiceChair.Released (hair on the floor) is in place before anybody can be called to it.
            if (_chair != null)
            {
                var chair = _chair;
                _chair = null;
                chair.Release(this, happy);
            }

            if (_queue != null) _queue.OnCustomerLeft(this, happy);
        }

        /// <summary>Returns true once standing on the target.</summary>
        private bool StepTowards(Vector3 destination, float delta)
        {
            Vector3 here = transform.position;
            Vector3 flat = new Vector3(destination.x - here.x, 0f, destination.z - here.z);

            if (flat.sqrMagnitude <= arriveRadius * arriveRadius)
            {
                Bob(delta, false);
                FaceCounter(delta);
                return true;
            }

            Vector3 next = here + flat.normalized * moveSpeed * delta;
            if (_raised)
            {
                // Down off a raised seat while walking away from it.
                next.y = Mathf.MoveTowards(here.y, _groundY, moveSpeed * delta);
                if (Mathf.Approximately(next.y, _groundY)) _raised = false;
            }
            transform.position = next;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(flat.normalized), turnSpeed * delta);

            Bob(delta, true);
            return false;
        }

        private void FaceCounter(float delta)
        {
            if (_queue == null) return;
            // Somebody taking a seat turns the way the seat faces (SettleOnSeat finishes the turn).
            if (_seat != null && (CurrentPhase == Phase.Arriving || CurrentPhase == Phase.ToChair))
            {
                Vector3 facing = _seat.forward;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.RotateTowards(
                        transform.rotation, Quaternion.LookRotation(facing.normalized), turnSpeed * delta);
                return;
            }
            // A browsing shopper looks at the shelf in front of them; everyone else at the till.
            Vector3 toCounter = (_hasFacePoint ? _facePoint : _queue.CounterPosition) - transform.position;
            toCounter.y = 0f;
            if (toCounter.sqrMagnitude < 0.01f) return;

            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(toCounter.normalized), turnSpeed * delta);
        }

        private void Bob(float delta, bool walking)
        {
            if (visual == null) return;

            if (walking)
            {
                _bobPhase += delta * 10f;
                visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.06f, 0f);
            }
            else
            {
                visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, delta * 10f);
            }
        }
    }
}
