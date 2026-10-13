using System;
using Tycoon.Audio;
using Tycoon.Core;
using Tycoon.Player;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The cash a till has taken, lying beside it as banded bundles stacked two by two, as high as
    /// it gets (bundles merge above <see cref="maxLayers"/> so the height stays sane while the
    /// amount does not).
    ///
    /// Sales put money here instead of in the wallet. Only the player can pick it up - from the
    /// <see cref="CashCollectStation"/> square - so a hired cashier serves the queue without ever
    /// touching the takings. Saved under the till's id, so cash waiting at a till survives a
    /// reload and a stretch away.
    /// </summary>
    public class CashPile : MonoBehaviour, ISaveable
    {
        [Tooltip("Bundle visual, a build-time prefab (no runtime Materials).")]
        public GameObject bundlePrefab;

        [Tooltip("Layers shown before bundles merge into bigger ones. Four bundles a layer.")]
        public int maxLayers = 15;

        private double _amount;
        private BundleStackView _view;

        public double Amount => _amount;
        public double BundleValue => _view != null ? _view.BundleValue : BundleStackView.BaseBundle;

        public string SaveKey => SaveKeys.For(this);

        private void Awake() => EnsureView();

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        private void EnsureView()
        {
            if (_view == null) _view = new BundleStackView(transform, bundlePrefab, maxLayers, 1f);
        }

        public void Deposit(double amount)
        {
            if (amount <= 0d) return;
            _amount += amount;
            Refresh();
        }

        /// <summary>Takes up to <paramref name="requested"/> and returns what came out.</summary>
        public double Take(double requested)
        {
            double taken = Math.Min(requested, _amount);
            if (taken <= 0d) return 0d;
            _amount -= taken;
            if (_amount < 0.0001d) _amount = 0d;
            Refresh();
            return taken;
        }

        private void Refresh()
        {
            EnsureView();
            _view.SetAmount(_amount);
        }

        private void Update() => _view?.Tick(Time.deltaTime);

        [Serializable]
        private struct State { public double amount; }

        public string CaptureState() => JsonUtility.ToJson(new State { amount = _amount });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _amount = Math.Max(0d, s.amount);
            EnsureView();
            _view.SetAmount(_amount, instant: true);
        }
    }
}
