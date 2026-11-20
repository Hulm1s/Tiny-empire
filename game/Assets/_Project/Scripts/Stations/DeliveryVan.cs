using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The cafe's delivery van: pure scenery that moves. Every <see cref="intervalSeconds"/> it drives in from the street to
    /// the back door, stands for a few seconds as if unloading, and drives off again. The milk and eggs themselves come from
    /// the crates' free SupplyFeed - this van touches no buffer, no machine and nothing on the farm, so it can never take
    /// anything from anyone and the supply never depends on it. No collider, no navmesh obstacle: it cannot block anybody.
    /// Nothing is saved (a reload starts it away).
    /// </summary>
    public class DeliveryVan : MonoBehaviour
    {
        [Tooltip("The model, switched off while the van is away.")]
        public GameObject visual;

        [Tooltip("First point: where it appears (off the street). Last: its parking place at the back door. In between: the road.")]
        public Transform[] route;

        public float speed = 5f;
        public float turnSpeed = 180f;
        public float stopSeconds = 7f;
        public float intervalSeconds = 70f;

        private enum Phase { Away, In, Parked, Out }
        private Phase _phase = Phase.Away;
        private float _timer;
        private int _index;

        private void OnEnable()
        {
            _phase = Phase.Away;
            _timer = intervalSeconds * 0.35f;       // the first one comes a little after the cafe opens
            if (visual != null) visual.SetActive(false);
        }

        private void Update()
        {
            if (route == null || route.Length < 2) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);

            switch (_phase)
            {
                case Phase.Away:
                    _timer -= dt;
                    if (_timer > 0f) return;
                    transform.position = route[0].position;
                    _index = 1;
                    if (visual != null) visual.SetActive(true);
                    _phase = Phase.In;
                    break;

                case Phase.In:
                    if (Drive(route[_index].position, dt) && ++_index >= route.Length)
                    {
                        _phase = Phase.Parked;
                        _timer = stopSeconds;
                    }
                    break;

                case Phase.Parked:
                    _timer -= dt;
                    if (_timer > 0f) return;
                    _index = route.Length - 2;
                    _phase = Phase.Out;
                    break;

                case Phase.Out:
                    if (Drive(route[_index].position, dt) && --_index < 0)
                    {
                        if (visual != null) visual.SetActive(false);
                        _phase = Phase.Away;
                        _timer = intervalSeconds;
                    }
                    break;
            }
        }

        /// <summary>Drives towards a point, nose first. True on arrival.</summary>
        private bool Drive(Vector3 target, float dt)
        {
            var to = target - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.04f) return true;

            var look = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
            if (Quaternion.Angle(transform.rotation, look) < 60f)
                transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
            return (target - transform.position).sqrMagnitude < 0.04f;
        }
    }
}
