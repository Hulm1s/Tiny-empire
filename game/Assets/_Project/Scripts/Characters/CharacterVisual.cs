using Tycoon.Player;
using UnityEngine;

namespace Tycoon.Characters
{
    /// <summary>
    /// Drives a villager's animator from what the character is already doing.
    ///
    /// Deliberately a watcher, not a controller: it reads position and the carry
    /// stack rather than being told. That means the player, workers and customers all
    /// animate correctly without a single line changing in PlayerMotor, WorkerAgent
    /// or CustomerAgent - which is the point, because those systems work and the
    /// models are only meant to be a new coat of paint on them.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterVisual : MonoBehaviour
    {
        /// <summary>Matches the states wired up in the generated animator controller.</summary>
        public enum Motion { Idle = 0, Walk = 1, CarryIdle = 2, CarryWalk = 3 }

        [Tooltip("Animator on the villager model. Found automatically when empty.")]
        public Animator animator;

        [Tooltip("Carry stack to watch. Found on this object or a parent when empty.")]
        public CarryStack carry;

        [Tooltip("Metres per second above which the character counts as walking.")]
        public float walkThreshold = 0.25f;

        private static readonly int StateParameter = Animator.StringToHash("State");

        private Vector3 _lastPosition;
        private float _smoothedSpeed;
        private int _appliedState = -1;

        /// <summary>Seconds between animator updates. Locomotion state is not subtle.</summary>
        private const float Interval = 0.1f;

        private float _timer;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (carry == null) carry = GetComponentInParent<CarryStack>();

            _lastPosition = transform.position;
            // Stagger, so a crowd of villagers does not all re-evaluate on one frame.
            _timer = Random.value * Interval;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;

            float elapsed = Interval - _timer;
            _timer = Interval;

            Vector3 position = transform.position;
            float speed = elapsed > 0f ? (position - _lastPosition).magnitude / elapsed : 0f;
            _lastPosition = position;

            // Smoothed, or a character stopping for one sample flickers between clips.
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, 0.6f);

            bool moving = _smoothedSpeed > walkThreshold;
            bool loaded = carry != null && carry.Count > 0;

            Motion wanted = loaded
                ? (moving ? Motion.CarryWalk : Motion.CarryIdle)
                : (moving ? Motion.Walk : Motion.Idle);

            Apply(wanted);
        }

        private void Apply(Motion motion)
        {
            int state = (int)motion;
            if (state == _appliedState || animator == null) return;

            _appliedState = state;
            animator.SetInteger(StateParameter, state);
        }
    }
}
