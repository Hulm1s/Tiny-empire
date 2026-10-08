using System.Collections.Generic;
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
    public class CustomerAgent : MonoBehaviour
    {
        // Shopping is last on purpose: the values are never saved, but nothing that already
        // reads Arriving, Waiting or Leaving should have to care that browsing exists.
        public enum Phase { Arriving, Waiting, Leaving, Shopping }

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

        public void Begin(CustomerQueue queue, ItemDefinition item, int count, Vector3 slot, Color tint)
        {
            _queue = queue;
            _wanted = item;
            _requested = Mathf.Max(1, count);
            _delivered = 0;
            _patienceLeft = patienceSeconds;
            CurrentPhase = Phase.Arriving;
            _target = slot;

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
        public void MoveToSlot(Vector3 slot)
        {
            if (CurrentPhase == Phase.Leaving) return;
            _target = slot;
            if (CurrentPhase == Phase.Waiting) CurrentPhase = Phase.Arriving;
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
            if (!IsWaiting || item != _wanted || IsSatisfied) return false;

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

        private void Leave(bool happy)
        {
            bool fromQueue = CurrentPhase == Phase.Arriving || CurrentPhase == Phase.Waiting;
            CurrentPhase = Phase.Leaving;
            if (bubble != null) bubble.SetVisible(false);

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
                if (fromQueue) _queue.AppendQueueExit(_route);
                _queue.AppendExitRoute(_route);
                _queue.OnCustomerLeft(this, happy);
            }
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

            transform.position = here + flat.normalized * moveSpeed * delta;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(flat.normalized), turnSpeed * delta);

            Bob(delta, true);
            return false;
        }

        private void FaceCounter(float delta)
        {
            if (_queue == null) return;
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
