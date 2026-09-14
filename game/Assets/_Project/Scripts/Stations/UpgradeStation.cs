using System;
using Tycoon.Core;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// A square you can buy from repeatedly: each purchase adds one chicken to a coop, and the
    /// next one costs more.
    ///
    /// This is the repeatable cousin of <see cref="UnlockStation"/>, and the first of the
    /// upgrade family. The rising price is what turns "buy the thing" into a decision: early
    /// chickens are obviously worth it, later ones have to be weighed against a second coop, a
    /// worker, or simply keeping the cash.
    ///
    /// Like every purchase, workers may not use it - spending money is the player's call.
    /// </summary>
    public class UpgradeStation : StationBase, ISaveable
    {
        [Header("Target")]
        [Tooltip("The building this square adds capacity to.")]
        public ProducerMachine target;

        [Header("Cost")]
        [Tooltip("Price of the next purchase, before growth is applied.")]
        public double basePrice = 120d;

        [Tooltip("Each purchase multiplies the price by this. 1 makes every one cost the same.")]
        [Min(1f)] public float priceGrowth = 2.2f;

        [Tooltip("Money drained per tick while standing here.")]
        public double payPerTick = 5d;

        [Header("Naming")]
        [Tooltip("What one purchase adds, e.g. 'Chicken'. Used on the square's label.")]
        public string unitName = "Chicken";

        private int _purchases;
        private double _paid;

        public event Action Purchased;

        /// <summary>Cost of the next purchase at the current level.</summary>
        public double CurrentPrice => basePrice * Math.Pow(priceGrowth, _purchases);

        public double Remaining => Math.Max(0d, CurrentPrice - _paid);

        public bool SoldOut => target == null || target.IsAtMaxUnits;

        public string SaveKey => SaveKeys.For(this);

        protected override void Awake()
        {
            base.Awake();
            // Buying is the player's decision, never a worker's.
            workerCompatible = false;
        }

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        public override bool IsOperational => !SoldOut;

        /// <summary>Just the livestock, upper-cased by the square. The plus icon says "buy".</summary>
        public override string ActionLabel => string.IsNullOrEmpty(unitName) ? label : unitName;

        public override string StatusValue
        {
            get
            {
                if (target == null) return string.Empty;
                if (SoldOut) return $"{target.units}/{target.maxUnits}";
                return MoneyFormat.Short(Remaining);
            }
        }

        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Buy;

        /// <summary>The ring fills as this purchase is paid off across however many visits.</summary>
        protected override float TransferProgress =>
            SoldOut || CurrentPrice <= 0d ? 0f : Mathf.Clamp01((float)(_paid / CurrentPrice));

        protected override bool TickWithPlayer()
        {
            if (SoldOut) return false;

            var wallet = GameRoot.Money;
            if (wallet == null) return false;

            double want = Math.Min(payPerTick, Remaining);
            if (want <= 0d) { Complete(); return false; }

            double spent = wallet.TrySpend(want);
            if (spent <= 0d) return false; // broke: standing here simply does nothing

            _paid += spent;
            if (_paid >= CurrentPrice) Complete();
            return true;
        }

        private void Complete()
        {
            if (target == null || !target.TryAddUnit())
            {
                // Nothing to buy after all - hand the money back rather than pocketing it.
                if (_paid > 0d) GameRoot.Money?.Add(_paid);
                _paid = 0d;
                return;
            }

            _purchases++;
            _paid = 0d;

            WorldFeedback.Show(transform.position + Vector3.up * 1.6f,
                $"+1 {unitName.ToUpperInvariant()}", new Color(1f, 0.85f, 0.35f));

            Purchased?.Invoke();
        }

        [Serializable]
        private struct State
        {
            public int purchases;
            public double paid;
        }

        public string CaptureState() =>
            JsonUtility.ToJson(new State { purchases = _purchases, paid = _paid });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _purchases = Mathf.Max(0, s.purchases);
            _paid = Math.Max(0d, s.paid);
        }
    }
}
