using System;
using Tycoon.Core;
using Tycoon.Player;
using Tycoon.Stations;
using UnityEngine;

namespace Tycoon.Tasks
{
    /// <summary>What the player just did, as far as the tutorial cares.</summary>
    public enum TutorialEvent { Harvest, Feed, Collect, Sell, Discard }

    /// <summary>
    /// The one-line hook the stations call when something the tutorial waits for happens.
    ///
    /// A static event rather than a reference, so the stations stay unaware of the tutorial:
    /// they announce "the player harvested", and whether anyone is listening is not their
    /// business. Workers use the same stations, so the actor is checked here and a hired hand
    /// harvesting corn never counts as the player learning how.
    /// </summary>
    public static class TutorialEvents
    {
        public static event Action<TutorialEvent> Raised;

        public static void Raise(TutorialEvent what, CarryStack actor)
        {
            if (actor == null || !actor.CompareTag("Player")) return;
            Raised?.Invoke(what);
        }
    }

    /// <summary>
    /// Where the first-time walkthrough has got to: harvest, feed, collect, sell, bin.
    ///
    /// Only the step number is state. The targets are the squares the builder wired in, and the
    /// text and arrow are the <see cref="TaskBoard"/>'s job, so this class is a counter with a
    /// save entry (id <c>game.tutorial</c>) and nothing else.
    ///
    /// A save from before the tutorial existed that already has progress starts completed: a
    /// player with money or purchases does not need to be told what a field is.
    /// </summary>
    public class TutorialProgress : MonoBehaviour, ISaveable
    {
        public const int StepCount = 5;

        [Header("Targets (wired by the farm builder)")]
        public HarvestStation field;
        public DepositStation feed;
        public CollectStation collect;
        public RegisterStation till;

        public static TutorialProgress Instance { get; private set; }

        /// <summary>0 .. StepCount - 1 while running, StepCount once finished.</summary>
        public int Step { get; private set; }

        public bool IsDone => Step >= StepCount;

        private bool _restored;

        public string SaveKey => SaveKeys.For(this);

        private void OnEnable()
        {
            Instance = this;
            TutorialEvents.Raised += OnRaised;
            SaveSystem.Register(this);
        }

        private void OnDisable()
        {
            TutorialEvents.Raised -= OnRaised;
            SaveSystem.Unregister(this);
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Runs after every station has registered and restored itself, which is the first
        /// moment "has this save made any progress?" can be answered.
        /// </summary>
        private void Start()
        {
            if (_restored || IsDone || !SaveSystem.HasSave) return;

            var wallet = GameRoot.Money;
            bool hasMoney = wallet != null && wallet.Balance > 0d;
            if (hasMoney || AnyPurchaseMade()) Step = StepCount;
        }

        private static bool AnyPurchaseMade()
        {
            var unlocks = FindObjectsByType<UnlockStation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var u in unlocks)
                if (u != null && u.IsUnlocked) return true;

            var upgrades = FindObjectsByType<UpgradeStation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var u in upgrades)
                if (u != null && u.Purchases > 0) return true;

            return false;
        }

        private void OnRaised(TutorialEvent what)
        {
            if (IsDone) return;
            if ((int)what == Step) Step++;
        }

        [Serializable]
        private struct State { public int step; }

        public string CaptureState() => JsonUtility.ToJson(new State { step = Step });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            Step = Mathf.Clamp(s.step, 0, StepCount);
            _restored = true;
        }
    }
}
