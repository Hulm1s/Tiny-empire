using UnityEngine;

namespace Tycoon.Player
{
    /// <summary>
    /// Moves the character across the ground plane from a single 2D input vector.
    ///
    /// Movement is camera-relative: because the camera sits at a fixed 45 degree isometric
    /// yaw, pushing the joystick "up" must send the character up the screen, not along world Z.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5.5f;

        [Tooltip("How quickly the character reaches full speed. Low values feel floaty.")]
        public float acceleration = 40f;

        [Tooltip("Degrees per second the character turns to face travel direction.")]
        public float turnSpeed = 900f;

        [Header("Feel")]
        [Tooltip("Bobbing while running. Purely cosmetic, sells the movement without animation.")]
        public Transform visual;

        public float bobHeight = 0.08f;
        public float bobSpeed = 12f;

        private CharacterController _controller;
        private Transform _cameraTransform;
        private Vector3 _velocity;
        private float _bobPhase;
        private Vector3 _visualBasePosition;

        /// <summary>Normalised 0-1 speed, for animation and footstep effects later.</summary>
        public float NormalisedSpeed { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (visual != null) _visualBasePosition = visual.localPosition;
        }

        private void Start()
        {
            if (Camera.main != null) _cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            // The first frame after a Web build finishes loading can be several seconds long.
            // Feeding that straight into Move() would displace the character tens of metres in
            // one step, which tunnels straight through the ground collider and drops them out
            // of the world. Clamping the step is the fix.
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            Vector2 input = PlayerInputSource.Read();
            Vector3 desired = ToWorldDirection(input) * moveSpeed;

            _velocity = Vector3.MoveTowards(_velocity, desired, acceleration * dt);

            // A constant downward push keeps the CharacterController grounded on slopes and
            // stops it hovering after a step; the world is flat so no real gravity is needed.
            Vector3 motion = _velocity + Vector3.down * 9.81f;
            _controller.Move(motion * dt);

            NormalisedSpeed = moveSpeed <= 0f ? 0f : Mathf.Clamp01(_velocity.magnitude / moveSpeed);

            if (_velocity.sqrMagnitude > 0.05f)
            {
                Quaternion target = Quaternion.LookRotation(new Vector3(_velocity.x, 0f, _velocity.z));
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, turnSpeed * Time.deltaTime);
            }

            ApplyBob();
        }

        private Vector3 ToWorldDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f) return Vector3.zero;

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (_cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
                if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            }

            return (forward * input.y + right * input.x).normalized * input.magnitude;
        }

        private void ApplyBob()
        {
            if (visual == null) return;

            if (NormalisedSpeed > 0.05f)
            {
                _bobPhase += Time.deltaTime * bobSpeed * NormalisedSpeed;
                float offset = Mathf.Abs(Mathf.Sin(_bobPhase)) * bobHeight * NormalisedSpeed;
                visual.localPosition = _visualBasePosition + Vector3.up * offset;
            }
            else
            {
                _bobPhase = 0f;
                visual.localPosition = Vector3.Lerp(
                    visual.localPosition, _visualBasePosition, Time.deltaTime * 10f);
            }
        }
    }
}
