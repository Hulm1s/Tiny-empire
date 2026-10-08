using Tycoon.Core;
using Tycoon.Player;
using Tycoon.Stations;
using UnityEngine;

namespace Tycoon.Upkeep
{
    /// <summary>
    /// A hired hand that runs one leg of the production chain on a loop: fill up at one
    /// square, walk to another, empty out, walk back.
    ///
    /// It carries a <see cref="CarryStack"/> and walks into station triggers exactly like the
    /// player does, so every station type works with workers without a line of special-case
    /// code - including station types added later.
    ///
    /// Crucially, a worker is not free: they take a cut of every unit they deliver, so
    /// automating the farm is a running cost that scales with throughput. That is what stops a
    /// finished business running itself forever, and it is why the player still has to come back.
    /// </summary>
    /// <summary>One leg a multi-route worker may run: a crate's collect square to a shelf's stocking square.</summary>
    [System.Serializable]
    public class StockRoute
    {
        public StationBase pickup;
        public StationBase dropoff;
    }

    [RequireComponent(typeof(CarryStack))]
    public class WorkerAgent : MonoBehaviour, IStationUser
    {
        // Which shelf each multi-route worker is currently restocking, so two stockers in one
        // room never both go for the same one. Static because the workers know nothing of each
        // other; cleared on every start so an editor play session never inherits a stale claim.
        private static readonly System.Collections.Generic.Dictionary<ItemBuffer, WorkerAgent> Claims =
            new System.Collections.Generic.Dictionary<ItemBuffer, WorkerAgent>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetClaims() => Claims.Clear();

        private ItemBuffer _claimed;

        // A multi-route worker with nothing worth doing: every shelf full, empty-crated, not
        // open yet, or already taken by the other stocker. It stands where it is rather than
        // walking to a crate to fetch goods for a shelf somebody else is filling.
        private bool _idle;

        [Header("Route")]
        [Tooltip("Square where the worker fills up - a field, or a machine's output.")]
        public StationBase pickup;

        [Tooltip("Square where the worker empties out - a machine's input, or a sell counter.")]
        public StationBase dropoff;

        [Header("Several routes (market stockers only)")]
        [Tooltip("Leave empty for an ordinary one-route worker, which is every farm hand and " +
                 "every cashier. When filled, the worker serves a whole storage room: each " +
                 "time it is empty-handed it picks the route whose shelf needs stock most, " +
                 "skipping shelves that are not open yet, are full, have nothing waiting in " +
                 "their crate, or are already being worked by another stocker. The chosen " +
                 "route simply becomes `pickup` and `dropoff`, so everything below - " +
                 "walking, standing off for the player, piece-rate pay - is unchanged.")]
        public StockRoute[] routes;

        [Header("Movement")]
        public float moveSpeed = 3.1f;
        public float turnSpeed = 720f;

        [Tooltip("How close counts as standing in the square.")]
        public float arriveRadius = 0.35f;

        [Header("Pay")]
        [Tooltip("Money taken per unit actually delivered - piece work, not an hourly wage.\n\n" +
                 "Paying by the minute looked reasonable but deadlocks the game: once the " +
                 "wallet empties the workers stop, and if a stopped worker was the one taking " +
                 "goods to the counter then nothing can ever earn money again. Charging per " +
                 "delivery means pay is always a cut of work that just happened, so the " +
                 "balance can never run away.")]
        public double feePerDelivery = 0.6d;

        [Header("Visuals")]
        public Transform visual;

        private CarryStack _carry;
        private UnityEngine.AI.NavMeshAgent _agent;
        private StationBase _routedTo;
        private bool _headingToDropoff;
        private int _lastCarryCount;
        private float _underpaidTimer;
        private float _yieldHold;
        private Vector3 _holdSpot;

        /// <summary>
        /// True if a recent fee could not be covered in full. Purely informational - the worker
        /// keeps going regardless, because a worker that downs tools can strand the player.
        /// </summary>
        public bool UnderpaidRecently => _underpaidTimer > 0f;

        /// <summary>One-line state for the debug readout: where it is going and whether it can.</summary>
        public string DebugState
        {
            get
            {
                string leg = _headingToDropoff ? "->drop" : "->pick";
                if (IsStandingOff) leg += " (yielding)";
                if (_agent == null) return $"{name} {leg} (no agent)";
                if (!_agent.isOnNavMesh) return $"{name} {leg} OFF-NAVMESH";

                string path = _agent.pathPending ? "pending"
                    : _agent.pathStatus == UnityEngine.AI.NavMeshPathStatus.PathComplete ? "ok"
                    : _agent.pathStatus.ToString();

                return $"{name} {leg} {path} d={_agent.remainingDistance:0.0} v={_agent.velocity.magnitude:0.0}";
            }
        }

        public string RouteName =>
            pickup != null && dropoff != null ? $"{pickup.label} to {dropoff.label}" : name;

        /// <summary>
        /// Only the two squares on this worker's CURRENT route; see IStationUser.
        ///
        /// For a multi-route worker that is deliberately just the route it chose this trip, not
        /// every route it owns: a stocker walking to the bread crate passes the apple crate's
        /// square on the way, and if that counted it would fill its arms with apples and carry
        /// a mixed stack to a shelf that wants one thing.
        /// </summary>
        public bool WillUse(StationBase station) => station == pickup || station == dropoff;

        /// <summary>True while this worker is waiting for the player to finish at its target.</summary>
        public bool IsStandingOff => _yieldHold > 0f;

        /// <summary>
        /// Seconds a worker keeps standing off after the player has gone.
        ///
        /// The player walks in and out of a trigger constantly while working a square, and
        /// without this the worker would turn round on the spot every time they crossed the
        /// edge - which looks broken and gets nothing done. Long enough to ride out the
        /// flicker, short enough that a worker never looks idle.
        /// </summary>
        private const float HoldAfterPlayerLeaves = 0.8f;

        /// <summary>How far back a standing-off worker waits. Clear of any square's outline.</summary>
        private const float HoldDistance = 2.9f;

        private void Awake()
        {
            _carry = GetComponent<CarryStack>();
            _agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (_carry != null) _lastCarryCount = _carry.Count;

            if (_agent != null)
            {
                _agent.speed = moveSpeed;
                _agent.angularSpeed = turnSpeed;
                // Stop just inside the square rather than dead on its centre.
                _agent.stoppingDistance = arriveRadius;
            }
        }

        private void Update()
        {
            float delta = Mathf.Min(Time.deltaTime, 0.05f);

            if (_underpaidTimer > 0f) _underpaidTimer -= delta;
            if (_yieldHold > 0f) _yieldHold -= delta;

            ChargeForDeliveries();
            ChooseTarget();
            MoveTowardsTarget(delta);
        }

        /// <summary>
        /// Charges the fee for any units that just left the worker's arms.
        ///
        /// The station does the actual transfer, so the worker detects its own deliveries by
        /// watching its carried count drop while on the delivery leg. That keeps stations
        /// completely unaware that workers exist.
        /// </summary>
        private void ChargeForDeliveries()
        {
            if (_carry == null) return;

            int count = _carry.Count;
            int delivered = _lastCarryCount - count;
            _lastCarryCount = count;

            // Arms empty after a delivery: this trip is over, so the shelf is free for anyone.
            if (delivered > 0 && count == 0) ReleaseClaim();

            if (delivered <= 0 || !_headingToDropoff || feePerDelivery <= 0d) return;

            var wallet = GameRoot.Money;
            if (wallet == null) return;

            double due = feePerDelivery * delivered;
            double paid = wallet.TrySpend(due);

            // Short pay is noted for the HUD but never stops the work: the whole point of
            // piece rates here is that the game cannot wedge itself.
            if (paid < due * 0.999d) _underpaidTimer = 3f;
        }

        private void OnDisable() => ReleaseClaim();

        private void ReleaseClaim()
        {
            if (_claimed == null) return;
            if (Claims.TryGetValue(_claimed, out var owner) && owner == this) Claims.Remove(_claimed);
            _claimed = null;
        }

        /// <summary>
        /// Open for business, not full, with something waiting to carry, and nobody else on it.
        /// A route whose crate or shelf has not been bought yet is simply not offered.
        /// </summary>
        private bool RouteUsable(StationBase from, StationBase to)
        {
            if (from == null || to == null) return false;
            if (!from.isActiveAndEnabled || !to.isActiveAndEnabled) return false;

            var crate = (from as CollectStation)?.source;
            var shelf = (to as DepositStation)?.target;
            if (crate != null && crate.IsEmpty) return false;
            if (shelf == null) return true;
            if (shelf.IsFull) return false;

            return !Claims.TryGetValue(shelf, out var owner) || owner == this ||
                   owner == null || !owner.isActiveAndEnabled;
        }

        /// <summary>
        /// Picks this trip's route when the arms are empty: the emptiest usable shelf. Kept for
        /// the whole trip once chosen (so a worker never dithers between two shelves as their
        /// fill levels cross), and dropped only when it stops being usable or the load is
        /// delivered.
        /// </summary>
        private void ChooseRoute()
        {
            if (routes == null || routes.Length == 0 || !_carry.IsEmpty) return;

            if (_claimed != null)
            {
                if (RouteUsable(pickup, dropoff)) return;
                ReleaseClaim();
            }

            StockRoute best = null;
            float bestFill = float.MaxValue;
            foreach (var route in routes)
            {
                if (route == null || !RouteUsable(route.pickup, route.dropoff)) continue;

                var shelf = (route.dropoff as DepositStation)?.target;
                float fill = shelf != null ? shelf.Fill : 0f;
                if (fill >= bestFill) continue;

                best = route;
                bestFill = fill;
            }

            // Nothing needs anything, or the only thing that does is somebody else's: wait.
            if (best == null)
            {
                _idle = true;
                _routedTo = null;
                if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
                return;
            }

            _idle = false;
            pickup = best.pickup;
            dropoff = best.dropoff;
            _routedTo = null;

            var target = (best.dropoff as DepositStation)?.target;
            if (target != null)
            {
                _claimed = target;
                Claims[target] = this;
            }
        }

        private void ChooseTarget()
        {
            if (_carry == null) return;

            ChooseRoute();

            // Self-correcting: full means deliver, empty means go and fetch, anything in
            // between means carry on with whatever leg is already underway.
            if (_carry.IsFull) _headingToDropoff = true;
            else if (_carry.IsEmpty) _headingToDropoff = false;
        }

        /// <summary>
        /// Where to wait while the player has the target square.
        ///
        /// Back down the worker's own route, never off to one side: that is ground it has
        /// already walked, so it is reachable, and it leaves the square approachable from the
        /// direction the player uses. Falls back to standing still if the route is half wired.
        /// </summary>
        private Vector3 HoldSpotFor(StationBase target)
        {
            StationBase other = _headingToDropoff ? pickup : dropoff;
            if (other == null || other == target) return transform.position;

            Vector3 from = target.transform.position;
            Vector3 back = other.transform.position - from;
            back.y = 0f;
            if (back.sqrMagnitude < 0.01f) return transform.position;

            return from + back.normalized * HoldDistance;
        }

        private void MoveTowardsTarget(float delta)
        {
            // Empty-handed with no job: stand still. (Loaded workers always finish their trip.)
            if (_idle && _carry != null && _carry.IsEmpty)
            {
                Bob(delta, false);
                return;
            }

            StationBase target = _headingToDropoff ? dropoff : pickup;
            if (target == null) return;

            // A square that has not been bought yet still has a position, but no live trigger.
            // A worker sent there walks to the empty grass where the building will one day
            // stand and waits full for ever, looking every bit like broken AI. Standing still
            // instead is recoverable the moment the other half of the route is paid for.
            //
            // The level builder sells a hire with the building its worker delivers into, so
            // this should never fire in the farm as shipped - it is here so that a level wired
            // up wrongly degrades into an idle worker rather than a stuck one.
            if (!target.isActiveAndEnabled)
            {
                Bob(delta, false);
                return;
            }

            // The player owns the square while they are in it. The worker waits its turn
            // rather than draining the basket out from under them - see
            // StationBase.ReservedForPlayer, which is also what stops it working in there.
            //
            // Nothing about the worker's own state changes here: same leg, same carried
            // stack, same target. It is purely where it chooses to stand.
            if (target.ReservedForPlayer)
            {
                if (_yieldHold <= 0f) _holdSpot = HoldSpotFor(target);
                _yieldHold = HoldAfterPlayerLeaves;
            }

            Vector3 here = transform.position;
            Vector3 there = _yieldHold > 0f ? _holdSpot : target.transform.position;
            if (_yieldHold > 0f) target = null;

            if (_agent != null && _agent.isOnNavMesh)
            {
                // Only re-path when the destination actually changes; SetDestination every
                // frame throws away the path it just computed.
                if (_routedTo != target || (target == null && _agent.destination != there))
                {
                    _routedTo = target;
                    _agent.SetDestination(there);
                }

                bool arrived = !_agent.pathPending &&
                               _agent.remainingDistance <= Mathf.Max(0.05f, _agent.stoppingDistance);
                Bob(delta, !arrived);
                return;
            }

            // Fallback for a worker that somehow ended up off the navmesh: walk straight at
            // the target so it can never be stranded forever.
            Vector3 flat = new Vector3(there.x - here.x, 0f, there.z - here.z);
            if (flat.sqrMagnitude <= arriveRadius * arriveRadius)
            {
                Bob(delta, false);
                return;
            }

            transform.position = here + flat.normalized * moveSpeed * delta;
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, Quaternion.LookRotation(flat.normalized), turnSpeed * delta);
            Bob(delta, true);
        }

        private float _bobPhase;

        private void Bob(float delta, bool walking)
        {
            if (visual == null) return;

            if (walking)
            {
                _bobPhase += delta * 11f;
                visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(_bobPhase)) * 0.07f, 0f);
            }
            else
            {
                visual.localPosition = Vector3.Lerp(visual.localPosition, Vector3.zero, delta * 10f);
            }
        }
    }
}
