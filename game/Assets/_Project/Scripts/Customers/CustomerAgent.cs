using Tycoon.Config;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.Customers
{
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
        public enum Phase { Arriving, Waiting, Leaving }

        [Header("Movement")]
        public float moveSpeed = 2.7f;
        public float turnSpeed = 720f;
        public float arriveRadius = 0.22f;

        [Header("Patience")]
        [Tooltip("Seconds they will stand at the counter before giving up.")]
        public float patienceSeconds = 45f;

        [Header("Parts")]
        public Transform visual;
        public OrderBubble bubble;
        public Renderer tintTarget;

        private CustomerQueue _queue;
        private ItemDefinition _wanted;
        private int _requested;
        private int _delivered;
        private float _patienceLeft;
        private Vector3 _target;
        private float _bobPhase;

        public Phase CurrentPhase { get; private set; } = Phase.Arriving;
        public ItemDefinition Wanted => _wanted;
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

            if (tintTarget != null)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor", tint);
                block.SetColor("_Color", tint);
                tintTarget.SetPropertyBlock(block);
            }

            if (bubble != null) bubble.Show(_wanted, Remaining);
        }

        /// <summary>Called when the queue shuffles forward.</summary>
        public void MoveToSlot(Vector3 slot)
        {
            if (CurrentPhase == Phase.Leaving) return;
            _target = slot;
            if (CurrentPhase == Phase.Waiting) CurrentPhase = Phase.Arriving;
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
                case Phase.Arriving:
                    if (StepTowards(_target, delta)) CurrentPhase = Phase.Waiting;
                    break;

                case Phase.Waiting:
                    Bob(delta, false);
                    _patienceLeft -= delta;
                    if (bubble != null) bubble.SetPatience(_patienceLeft / Mathf.Max(1f, patienceSeconds));
                    if (_patienceLeft <= 0f) Leave(false);
                    break;

                case Phase.Leaving:
                    if (StepTowards(_target, delta)) Destroy(gameObject);
                    break;
            }
        }

        private void Leave(bool happy)
        {
            CurrentPhase = Phase.Leaving;
            if (bubble != null) bubble.SetVisible(false);

            if (_queue != null)
            {
                _target = _queue.ExitPosition;
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
            Vector3 toCounter = _queue.CounterPosition - transform.position;
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
