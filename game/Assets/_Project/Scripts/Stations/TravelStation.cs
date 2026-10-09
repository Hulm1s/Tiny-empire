using Tycoon.Audio;
using System.Collections;
using Tycoon.Core;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The doorway between two locations: stand in it, the screen fades out, the player
    /// appears at the other location's spawn point, and the screen fades back in.
    ///
    /// Every location lives in the one scene, a long way apart and walled in, so this square is
    /// the only way between them. The camera is snapped across rather than allowed to pan, or it
    /// would sweep seventy metres of empty grass.
    ///
    /// The player's position is not part of the save: after a reload the player starts at the
    /// farm, which is the accepted cost of not tracking it.
    /// </summary>
    public class TravelStation : StationBase
    {
        [Header("Travel")]
        [Tooltip("Where this square leads. The player is placed at its spawn point.")]
        public Location destination;

        [Tooltip("Seconds to fade to black, and back.")]
        public float fadeOutSeconds = 0.25f;
        public float fadeInSeconds = 0.3f;

        private bool _travelling;

        public override bool IsOperational => destination != null && !_travelling;
        public override string StatusValue => string.Empty;
        public override SquareIcon Icon => SquareIcon.Travel;

        protected override void Awake()
        {
            base.Awake();
            // A timed stand-still rather than an instant trigger, so brushing past the end of
            // the road never whisks the player away. Never for workers: a hired hand must not
            // wander into another location.
            mode = InteractionMode.Task;
            workerCompatible = false;
        }

        protected override bool CanPerformTask() => destination != null && !_travelling;

        protected override void CompleteTask()
        {
            if (_travelling || destination == null || Carry == null) return;
            if (!Carry.CompareTag("Player")) return;

            // Belt and braces: only send a player who is actually still inside the square.
            // The base class keeps an occupant until the physics exit event arrives, and a
            // stale one must never teleport somebody who has already walked away.
            var box = GetComponent<BoxCollider>();
            Vector3 at = Carry.transform.position;
            if (box != null && (box.ClosestPoint(at) - at).sqrMagnitude > 0.05f) return;

            StartCoroutine(Travel(Carry.transform));
        }

        private IEnumerator Travel(Transform player)
        {
            _travelling = true;
            SoundFx.Play(Sfx.Whoosh);

            var fade = ScreenFade.Instance;
            if (fade != null) yield return fade.FadeTo(1f, fadeOutSeconds);

            if (player != null)
            {
                // A CharacterController overrides a plain position change on the next Move, so
                // it is switched off for the teleport and the physics world told about it.
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;

                player.position = destination.SpawnPosition + Vector3.up * 0.2f;

                if (controller != null) controller.enabled = true;
                Physics.SyncTransforms();

                var rig = FindFirstObjectByType<IsometricCameraRig>();
                if (rig != null) rig.SnapToTarget();
            }

            // One frame for the camera and the new square's trigger to settle before showing it.
            yield return null;

            if (fade != null) yield return fade.FadeTo(0f, fadeInSeconds);

            _travelling = false;
        }
    }
}
