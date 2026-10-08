using System;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// A free job done by standing in a square: sweeping up a pile of rubbish, switching a shop
    /// to OPEN.
    ///
    /// The Task-mode sibling of <see cref="UnlockStation"/>. Where a gate drains money, this
    /// costs only time - the progress ring fills <see cref="completionsNeeded"/> times - and when
    /// it finishes it hides the mess and reveals whatever the job makes possible. Renovation is
    /// built from these: labour is free so a broke player is never stuck, and the money steps
    /// (repair, paint) are ordinary gates that follow them.
    ///
    /// Like every purchase-shaped square, workers may not use it.
    /// </summary>
    public class ChoreStation : StationBase, ISaveable
    {
        [Header("Chore")]
        [Tooltip("How many times the progress ring has to fill. 3 rings at the default task " +
                 "length is a little under four seconds of standing still.")]
        [Min(1)] public int completionsNeeded = 3;

        [Tooltip("Deactivated when the job is done - the rubbish itself.")]
        public GameObject[] hideOnDone;

        [Tooltip("Activated when the job is done. Keep these inactive in the scene.")]
        public GameObject[] revealOnDone;

        [Tooltip("Turn off the trigger and card once done, leaving the ground clear.")]
        public bool hideSelfOnDone = true;

        [Header("Presentation")]
        public Tycoon.UI.SquareIcon icon = Tycoon.UI.SquareIcon.Clean;
        public string doneMessage = "CLEAN!";

        private int _completed;
        private bool _done;
        private bool _announce = true;

        public bool IsDone => _done;

        public string SaveKey => SaveKeys.For(this);

        public override bool IsOperational => !_done;
        public override string ActionLabel => _done ? string.Empty : label;
        public override string StatusValue => _done ? string.Empty : $"{_completed}/{completionsNeeded}";
        public override Tycoon.UI.SquareIcon Icon => _done ? Tycoon.UI.SquareIcon.None : icon;

        protected override void Awake()
        {
            base.Awake();
            // Forced here rather than left to the level builder, as RepairStation does: a chore
            // is a timed job, and it is the player's to do.
            mode = InteractionMode.Task;
            workerCompatible = false;
        }

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        protected override bool CanPerformTask() => !_done;

        protected override void CompleteTask()
        {
            if (_done) return;

            _completed++;
            if (_completed >= completionsNeeded)
            {
                Finish();
                return;
            }

            Tycoon.UI.WorldFeedback.Show(transform.position + Vector3.up * 1.4f,
                $"{_completed}/{completionsNeeded}", new Color(0.6f, 0.9f, 1f));
        }

        private void Finish()
        {
            _completed = completionsNeeded;
            Apply();
        }

        private void Apply()
        {
            _done = true;

            if (hideOnDone != null)
                foreach (var go in hideOnDone)
                    if (go != null) go.SetActive(false);

            if (revealOnDone != null)
                foreach (var go in revealOnDone)
                    if (go != null) go.SetActive(true);

            // Only celebrate a job finished now, not one being restored from a save.
            if (Application.isPlaying && _announce)
            {
                Tycoon.UI.WorldFeedback.Show(transform.position + Vector3.up * 1.8f,
                    doneMessage, new Color(0.6f, 0.95f, 0.65f));
            }

            if (hideSelfOnDone)
            {
                var box = GetComponent<BoxCollider>();
                if (box != null) box.enabled = false;
                enabled = false;
            }
        }

        [Serializable]
        private struct State
        {
            public int completed;
            public bool done;
        }

        public string CaptureState() =>
            JsonUtility.ToJson(new State { completed = _completed, done = _done });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _completed = Mathf.Clamp(s.completed, 0, completionsNeeded);
            if (!s.done) return;

            _announce = false;
            Finish();
            _announce = true;
        }
    }
}
