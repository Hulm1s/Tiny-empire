using System;
using Tycoon.Audio;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The delivery van: it really drives the raw meat from the pig pen to the butcher.
    ///
    /// It waits at the pen's pickup (the first stop of its route) until the pen has a batch, loads,
    /// drives the route - farm road, the link to town, the market's road, the butcher's driveway -
    /// backs up to the back-room door (so it can drive off nose-first), unloads onto the drop pile one unit at a time (so the pile
    /// visibly grows), and drives home again. The route is a plain list of waypoints; the van does
    /// not use the navmesh and has NO collider and NO navmesh obstacle, so it can never block the
    /// player, a worker or a shopper - it is scenery that moves and carries goods.
    ///
    /// The goods only change hands at the two ends (taken from <see cref="source"/> when it leaves,
    /// added to <see cref="destination"/> as it unloads), so nothing is created or lost by where the
    /// van happens to be.
    ///
    /// Offline: the time the game was closed is converted into whole round trips on the first frame
    /// after a load (the same shape as <see cref="SupplyFeed"/>), each moving one batch from the pen
    /// to the pile, capped by the gap (GameClock.ClampOffline) and by the two buffers' room. It runs
    /// after every buffer and machine has restored and caught up (execution order 100).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MeatVan : MonoBehaviour, ISaveable
    {
        [Serializable]
        public class Stop
        {
            public Transform point;

            [Tooltip("Travelling TO this stop from the previous one, the van drives backwards (nose " +
                     "away from where it is going) - how it backs up to a door. The way home is always " +
                     "driven nose-first (it never drives away in reverse), except the last leg into " +
                     "the first stop (the bay), which is backed into.")]
            public bool reverseInto;
        }

        [Header("Goods")]
        [Tooltip("The pen's output. Taken from when the van leaves.")]
        public ItemBuffer source;

        [Tooltip("The butcher's drop pile. Added to as the van unloads.")]
        public ItemBuffer destination;

        [Header("Route")]
        [Tooltip("First stop is where it loads (the pen's pickup), last is where it unloads (the " +
                 "back-room door). Waypoints in between follow the roads.")]
        public Stop[] route;

        public float speed = 7f;
        public float turnSpeed = 220f;

        [Header("Batches")]
        [Tooltip("Most units one trip carries.")]
        [Min(1)] public int batch = 12;

        [Tooltip("The van leaves once the pen has this many...")]
        [Min(1)] public int minBatch = 4;

        [Tooltip("...or, with at least one, after waiting this long, so a lone pig is not stranded.")]
        public float maxWaitSeconds = 25f;

        public float loadSeconds = 1.4f;
        public float unloadInterval = 0.2f;

        private enum Phase { Waiting, Loading, Out, Unloading, Back }

        private Phase _phase = Phase.Waiting;
        private int _stop;          // the stop the van is heading to (Out: upwards, Back: downwards)
        private int _cargo;
        private float _timer;       // waiting / loading / unloading clock
        private double _pendingOffline;

        public string SaveKey => SaveKeys.For(this);

        /// <summary>Units on board right now. Zero while parked at the pen.</summary>
        public int Cargo => _cargo;

        private void OnEnable()
        {
            SaveSystem.Register(this);
            ParkAtBay();
        }

        private void OnDisable() => SaveSystem.Unregister(this);

        private void Start()
        {
            if (_pendingOffline > 0d || _cargo > 0)
            {
                CatchUp(_pendingOffline);
                _pendingOffline = 0d;
            }
        }

        // ---- the trip ----------------------------------------------------------------------

        private void Update()
        {
            if (route == null || route.Length < 2 || source == null || destination == null) return;

            float dt = Mathf.Min(Time.deltaTime, 0.1f);

            switch (_phase)
            {
                case Phase.Waiting: UpdateWaiting(dt); break;
                case Phase.Loading: UpdateLoading(dt); break;
                case Phase.Out: Drive(dt, +1); break;
                case Phase.Unloading: UpdateUnloading(dt); break;
                case Phase.Back: Drive(dt, -1); break;
            }
        }

        private void UpdateWaiting(float dt)
        {
            if (source.Count <= 0 || destination.IsFull)
            {
                _timer = 0f;
                return;
            }

            _timer += dt;
            if (source.Count >= minBatch || _timer >= maxWaitSeconds)
            {
                _phase = Phase.Loading;
                _timer = 0f;
            }
        }

        private void UpdateLoading(float dt)
        {
            _timer += dt;
            if (_timer < loadSeconds) return;

            // Taken now, not when the van pulled up, so the player collecting by hand in the meantime
            // is not double counted. Sized to the room the pile will have.
            _cargo = source.Remove(Mathf.Min(batch, destination.FreeSpace));
            _timer = 0f;
            if (_cargo <= 0) { _phase = Phase.Waiting; return; }

            SoundFx.PlayAt(Sfx.Pop, transform.position);
            _phase = Phase.Out;
            _stop = 1;
        }

        /// <summary>Moves one frame along the route in direction <paramref name="dir"/> (+1 out, -1 home).</summary>
        private void Drive(float dt, int dir)
        {
            var stop = route[_stop];
            if (stop.point == null) { Arrive(dir); return; }

            Vector3 here = transform.position;
            Vector3 there = stop.point.position;
            there.y = here.y;

            Vector3 to = there - here;
            float step = speed * dt;

            // Backwards on this leg? Outbound, the stop says so (backing up to the door). Homebound the
            // van NEVER drives away in reverse - after backing in, it leaves nose-first - with the one
            // exception of the final leg into the bay, which it backs into so it is already facing the
            // way it will set off (the pen's bay is a dead end).
            bool reverse = dir > 0 ? stop.reverseInto : _stop == 0;

            if (to.sqrMagnitude > 0.0001f)
            {
                Vector3 heading = to.normalized;
                float yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg + (reverse ? 180f : 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.Euler(0f, yaw, 0f), turnSpeed * dt);
            }

            if (to.magnitude <= step)
            {
                transform.position = there;
                Arrive(dir);
                return;
            }

            transform.position = here + to.normalized * step;
        }

        private void Arrive(int dir)
        {
            _stop += dir;

            if (dir > 0 && _stop >= route.Length)
            {
                _stop = route.Length - 1;
                _phase = Phase.Unloading;
                _timer = 0f;
            }
            else if (dir < 0 && _stop < 0)
            {
                ParkAtBay();
            }
        }

        private void UpdateUnloading(float dt)
        {
            if (_cargo <= 0)
            {
                _phase = Phase.Back;
                _stop = route.Length - 2;
                _timer = 0f;
                return;
            }

            _timer += dt;
            if (_timer < unloadInterval) return;
            _timer = 0f;

            // A full pile makes the van wait (nothing is lost): the cutters will make room.
            if (destination.Add(1) > 0)
            {
                _cargo--;
                SoundFx.PlayAt(Sfx.Drop, transform.position);
            }
        }

        private void ParkAtBay()
        {
            _phase = Phase.Waiting;
            _stop = 0;
            _timer = 0f;
            if (route != null && route.Length > 0 && route[0].point != null)
            {
                transform.position = route[0].point.position;
                // Facing the way it will drive away from the bay.
                if (route.Length > 1 && route[1].point != null)
                {
                    Vector3 to = route[1].point.position - route[0].point.position;
                    to.y = 0f;
                    if (to.sqrMagnitude > 0.01f)
                    {
                        float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg + (route[1].reverseInto ? 180f : 0f);
                        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                }
            }
        }

        // ---- offline -----------------------------------------------------------------------

        /// <summary>Seconds one whole trip takes: out, unload, home, load.</summary>
        private float TripSeconds()
        {
            float length = 0f;
            for (int i = 1; i < route.Length; i++)
                if (route[i].point != null && route[i - 1].point != null)
                    length += Vector3.Distance(route[i].point.position, route[i - 1].point.position);

            return 2f * length / Mathf.Max(0.1f, speed) + loadSeconds + unloadInterval * batch + 1f;
        }

        /// <summary>
        /// Whatever was on board when the game closed arrives, then as many whole trips as the time
        /// away allows each move a batch. The van is back at the pen afterwards.
        /// </summary>
        private void CatchUp(double gap)
        {
            if (route == null || route.Length < 2 || source == null || destination == null) return;

            if (_cargo > 0)
            {
                int landed = destination.Add(_cargo);
                // Anything the pile has no room for goes back to the pen rather than vanishing.
                int back = _cargo - landed;
                if (back > 0) source.Add(back);
                _cargo = 0;
            }

            float trip = TripSeconds();
            int trips = trip > 0f ? Mathf.FloorToInt((float)(gap / trip)) : 0;
            for (int i = 0; i < trips; i++)
            {
                int moved = source.Remove(Mathf.Min(batch, destination.FreeSpace));
                if (moved <= 0) break;
                destination.Add(moved);
            }

            ParkAtBay();
        }

        // ---- save --------------------------------------------------------------------------

        [Serializable]
        private struct State
        {
            public int cargo;
            public double lastUnix;
        }

        public string CaptureState() =>
            JsonUtility.ToJson(new State { cargo = _cargo, lastUnix = GameClock.NowUnix });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _cargo = Mathf.Max(0, s.cargo);
            _pendingOffline = GameClock.ClampOffline(GameClock.NowUnix - s.lastUnix);
        }
    }
}
