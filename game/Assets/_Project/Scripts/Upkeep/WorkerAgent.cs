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
    [RequireComponent(typeof(CarryStack))]
    public class WorkerAgent : MonoBehaviour, IStationUser
    {
        [Header("Route")]
        [Tooltip("Square where the worker fills up - a field, or a machine's output.")]
        public StationBase pickup;

        [Tooltip("Square where the worker empties out - a machine's input, or a sell counter.")]
        public StationBase dropoff;

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

        /// <summary>Only the two squares on this worker's route; see IStationUser.</summary>
        public bool WillUse(StationBase station) => station == pickup || station == dropoff;

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

            if (delivered <= 0 || !_headingToDropoff || feePerDelivery <= 0d) return;

            var wallet = GameRoot.Money;
            if (wallet == null) return;

            double due = feePerDelivery * delivered;
            double paid = wallet.TrySpend(due);

            // Short pay is noted for the HUD but never stops the work: the whole point of
            // piece rates here is that the game cannot wedge itself.
            if (paid < due * 0.999d) _underpaidTimer = 3f;
        }

        private void ChooseTarget()
        {
            if (_carry == null) return;

            // Self-correcting: full means deliver, empty means go and fetch, anything in
            // between means carry on with whatever leg is already underway.
            if (_carry.IsFull) _headingToDropoff = true;
            else if (_carry.IsEmpty) _headingToDropoff = false;
        }

        private void MoveTowardsTarget(float delta)
        {
            StationBase target = _headingToDropoff ? dropoff : pickup;
            if (target == null) return;

            Vector3 here = transform.position;
            Vector3 there = target.transform.position;

            if (_agent != null && _agent.isOnNavMesh)
            {
                // Only re-path when the destination actually changes; SetDestination every
                // frame throws away the path it just computed.
                if (_routedTo != target)
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
