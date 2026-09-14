using System;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The progression square: stand on it and money drains out of your wallet until the next
    /// plot, machine or doorway is paid off. Partial payment is kept, so the player can chip
    /// away at something expensive across several visits.
    ///
    /// Objects in <see cref="revealOnUnlock"/> must be saved INACTIVE in the scene. That way
    /// they never run, register or save anything until they are genuinely bought, and no
    /// script-execution-order tricks are needed.
    /// </summary>
    public class UnlockStation : StationBase, ISaveable
    {
        [Header("Cost")]
        public double price = 100d;

        [Tooltip("Money drained per tick. With the default tick that is roughly 5x this per second.")]
        public double payPerTick = 4d;

        [Header("What this buys")]
        [Tooltip("Activated when fully paid. Keep these inactive in the scene.")]
        public GameObject[] revealOnUnlock;

        [Tooltip("Deactivated when fully paid - the padlock decal, the price sign, this square.")]
        public GameObject[] hideOnUnlock;

        [Tooltip("Turn off the trigger and decal once bought, leaving the ground clear.")]
        public bool hideSelfOnUnlock = true;

        private double _paid;
        private bool _unlocked;

        /// <summary>False while restoring a save, so old purchases do not re-celebrate.</summary>
        private bool _announceUnlock = true;

        public event Action Completed;

        public bool IsUnlocked => _unlocked;
        public double Remaining => Math.Max(0d, price - _paid);
        public float Progress => price <= 0d ? 1f : Mathf.Clamp01((float)(_paid / price));

        public string SaveKey => SaveKeys.For(this);

        // Deliberately not conditioned on the player's balance. Greying out everything the
        // player cannot yet afford made the whole farm look broken on a fresh save; the price
        // on the square already communicates that they need more money.
        public override bool IsOperational => !_unlocked;

        public override string StatusText =>
            _unlocked ? "" : $"{label} {MoneyFormat.Short(Remaining)}";

        /// <summary>The ring fills as the purchase is paid off across however many visits.</summary>
        protected override float TransferProgress => Progress;

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        protected override bool TickWithPlayer()
        {
            if (_unlocked) return false;

            var wallet = GameRoot.Money;
            if (wallet == null) return false;

            double want = Math.Min(payPerTick, Remaining);
            if (want <= 0d) { Apply(true); return false; }

            double spent = wallet.TrySpend(want);
            if (spent <= 0d) return false; // broke: stand here all day, nothing happens

            _paid += spent;
            if (_paid >= price) Apply(true);
            return true;
        }

        private void Apply(bool unlocked)
        {
            _unlocked = unlocked;
            if (!unlocked) return;

            _paid = price;

            if (revealOnUnlock != null)
                foreach (var go in revealOnUnlock)
                    if (go != null) go.SetActive(true);

            if (hideOnUnlock != null)
                foreach (var go in hideOnUnlock)
                    if (go != null) go.SetActive(false);

            Completed?.Invoke();

            // Only celebrate a purchase happening now, not one being restored from a save.
            if (Application.isPlaying && _announceUnlock)
            {
                Tycoon.UI.WorldFeedback.Show(transform.position + Vector3.up * 2f,
                    "UNLOCKED!", new Color(1f, 0.85f, 0.35f));
            }

            if (hideSelfOnUnlock)
            {
                var box = GetComponent<BoxCollider>();
                if (box != null) box.enabled = false;
                enabled = false;
            }
        }

        [Serializable]
        private struct State
        {
            public double paid;
            public bool unlocked;
        }

        public string CaptureState() => JsonUtility.ToJson(new State { paid = _paid, unlocked = _unlocked });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _paid = s.paid;
            if (!s.unlocked) return;

            _announceUnlock = false;
            Apply(true);
            _announceUnlock = true;
        }
    }
}
