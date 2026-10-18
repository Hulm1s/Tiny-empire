using Tycoon.Player;
using UnityEngine;

namespace Tycoon.Characters
{
    /// <summary>
    /// Implemented by a character that can be sitting down (a salon customer: CustomerAgent). The one
    /// fact <see cref="CharacterVisual"/> cannot measure for itself - a seated character is standing
    /// still, exactly like an idle one.
    /// </summary>
    public interface ISeatedPose
    {
        bool IsSeated { get; }
    }

    /// <summary>
    /// Drives a villager's animator from what the character is already doing.
    ///
    /// Deliberately a watcher, not a controller: it reads position and the carry
    /// stack rather than being told. That means the player, workers and customers all
    /// animate correctly without a single line changing in PlayerMotor, WorkerAgent
    /// or CustomerAgent - which is the point, because those systems work and the
    /// models are only meant to be a new coat of paint on them.
    ///
    /// Sitting is the one thing it cannot see for itself, so a character that can sit says so
    /// through <see cref="ISeatedPose"/> (only CustomerAgent does) and gets the Sit state.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterVisual : MonoBehaviour
    {
        /// <summary>Matches the states wired up in the generated animator controller.</summary>
        public enum Motion { Idle = 0, Walk = 1, CarryIdle = 2, CarryWalk = 3, Sit = 4 }

        [Tooltip("Animator on the villager model. Found automatically when empty.")]
        public Animator animator;

        [Tooltip("Carry stack to watch. Found on this object or a parent when empty.")]
        public CarryStack carry;

        // A character that can sit (a salon customer). Found on a parent; null for everyone else,
        // who therefore animates exactly as before.
        private ISeatedPose _seated;

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
            _seated = GetComponentInParent<ISeatedPose>();

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

            // Seated beats everything: the last steps onto a seat are still "moving" for a sample or two,
            // and the sit-down should start the moment they arrive, not after the speed has settled.
            if (_seated != null && _seated.IsSeated) wanted = Motion.Sit;

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
