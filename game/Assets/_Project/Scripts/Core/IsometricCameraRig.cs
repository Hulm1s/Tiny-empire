using UnityEngine;

namespace Tycoon.Core
{
    /// <summary>
    /// Fixed-angle isometric camera that trails the player.
    ///
    /// Orthographic on purpose: on a phone-sized portrait screen a perspective camera makes
    /// buildings at the edges lean away and the world read as smaller than it is. Orthographic
    /// keeps every plot the same size wherever it sits, which is what gives these games their
    /// clean diorama look.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class IsometricCameraRig : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Tooltip("Looked up by tag at start when no target is assigned, so new scenes need no wiring.")]
        public string targetTag = "Player";

        [Header("Framing")]
        [Tooltip("Pitch/yaw of the isometric view. 30/45 is the classic diorama angle.")]
        public Vector2 pitchYaw = new Vector2(30f, 45f);

        [Tooltip("How far back along the view direction the camera sits. Only affects clipping.")]
        public float distance = 24f;

        [Tooltip("Half the vertical world height on screen. Larger shows more of the farm.")]
        public float orthographicSize = 7.5f;

        [Tooltip("Nudges the framing so the player sits above the joystick rather than centred.")]
        public Vector3 lookOffset = new Vector3(0f, 0f, 1.2f);

        [Header("Feel")]
        [Tooltip("Seconds for the camera to catch up. Small values feel locked, large feel lazy.")]
        public float smoothTime = 0.18f;

        private Camera _camera;
        private Vector3 _currentFocus;
        private Vector3 _focusVelocity;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = orthographicSize;
            transform.rotation = Quaternion.Euler(pitchYaw.x, pitchYaw.y, 0f);
        }

        private void Start()
        {
            if (target == null && !string.IsNullOrEmpty(targetTag))
            {
                var found = GameObject.FindGameObjectWithTag(targetTag);
                if (found != null) target = found.transform;
            }

            if (target != null)
            {
                _currentFocus = target.position + lookOffset;
                SnapToFocus();
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            _currentFocus = Vector3.SmoothDamp(
                _currentFocus, target.position + lookOffset, ref _focusVelocity, smoothTime);

            SnapToFocus();

            // Kept in sync so the value can be tweaked live in the inspector while playing.
            if (!Mathf.Approximately(_camera.orthographicSize, orthographicSize))
                _camera.orthographicSize = orthographicSize;
        }

        private void SnapToFocus()
        {
            var rotation = Quaternion.Euler(pitchYaw.x, pitchYaw.y, 0f);
            transform.rotation = rotation;
            transform.position = _currentFocus - rotation * Vector3.forward * distance;
        }
    }
}
