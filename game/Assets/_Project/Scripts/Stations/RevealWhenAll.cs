using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Turns things on once a whole list of other things has been completed.
    ///
    /// A gate reveals content when IT is paid. This is the "and" version: reveal when ALL of
    /// these gates are bought, ALL of these upgrades are maxed, ALL of these chores are done, ALL of these rubbish piles are empty.
    /// It is how the supermarket stays hidden until the entire farm is owned, and how a derelict
    /// shop is renovated in order - clean, then repair and paint, then open.
    ///
    /// It holds no state of its own and needs no save entry. Everything it watches is saved by
    /// its own component, so on load it simply looks again and reaches the same answer. The
    /// check runs once on start and then twice a second: these are progression milestones, and
    /// polling is cheaper and simpler than wiring an event from every watched station.
    ///
    /// Put it on an object that is always active. The targets must be saved INACTIVE, exactly
    /// as <see cref="UnlockStation.revealOnUnlock"/> requires.
    /// </summary>
    public class RevealWhenAll : MonoBehaviour
    {
        [Header("Watching")]
        [Tooltip("Every one of these must be bought.")]
        public UnlockStation[] unlocks;

        [Tooltip("Every one of these must be at its maximum.")]
        public UpgradeStation[] maxed;

        [Tooltip("Every one of these must be finished.")]
        public ChoreStation[] chores;

        [Tooltip("Every one of these must be empty - the shop's piles of rubbish, once they " +
                 "have all been carried away.")]
        public ItemBuffer[] emptied;

        [Tooltip("Every one of these must have had its first paint job confirmed.")]
        public PaintStation[] painted;

        [Header("Then")]
        [Tooltip("Activated once everything above is complete. Keep these inactive in the scene.")]
        public GameObject[] reveal;

        private const float CheckInterval = 0.5f;

        private float _timer;
        private bool _fired;

        /// <summary>True once every watched thing is complete. An empty list is trivially met.</summary>
        public bool IsMet
        {
            get
            {
                if (unlocks != null)
                    foreach (var u in unlocks)
                        if (u == null || !u.IsUnlocked) return false;

                if (maxed != null)
                    foreach (var m in maxed)
                        if (m == null || !m.IsMaxed) return false;

                if (chores != null)
                    foreach (var c in chores)
                        if (c == null || !c.IsDone) return false;

                if (emptied != null)
                    foreach (var b in emptied)
                        if (b == null || !b.IsEmpty) return false;

                if (painted != null)
                    foreach (var p in painted)
                        if (p == null || !p.IsPainted) return false;

                return true;
            }
        }

        private void Start() => Check();

        private void Update()
        {
            if (_fired) { enabled = false; return; }

            _timer += Time.unscaledDeltaTime;
            if (_timer < CheckInterval) return;

            _timer = 0f;
            Check();
        }

        private void Check()
        {
            if (_fired || !IsMet) return;

            _fired = true;
            if (reveal != null)
                foreach (var go in reveal)
                    if (go != null) go.SetActive(true);
        }
    }
}
