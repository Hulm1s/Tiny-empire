using System;
using System.Collections.Generic;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Player
{
    /// <summary>
    /// The cash the player is carrying, on their back.
    ///
    /// Money is a physical thing now: sales land in a <see cref="Tycoon.Stations.CashPile"/> at the
    /// till, the player collects it, and every purchase is paid by standing in its square with the
    /// cash in hand. This is the hand: one number, drawn as a stack of banded bundles that grows
    /// upwards two by two behind the character - separate from the <see cref="CarryStack"/> of goods
    /// in front, which it never touches and which never limits it. The amount is unlimited.
    ///
    /// The wallet (<see cref="Wallet"/>) is the ACCOUNT, which the ATM moves money in and out of. It
    /// is unchanged, so a save from before cash existed keeps its balance on the account with
    /// nothing carried.
    ///
    /// Workers have no CashStack: <see cref="Of"/> returns null for them, which is how a worker can
    /// neither pay for anything nor empty a till.
    /// </summary>
    public class CashStack : MonoBehaviour, ISaveable
    {
        public static CashStack Instance { get; private set; }

        [Tooltip("Where the stack grows from: an empty on the character's back.")]
        public Transform anchor;

        [Tooltip("Bundle visual, a build-time prefab (no runtime Materials).")]
        public GameObject bundlePrefab;

        [Tooltip("Layers shown before bundles merge into bigger ones. Four bundles a layer.")]
        public int maxLayers = 4;

        [Tooltip("Scale of each bundle, so the 2 x 2 footprint fits behind the shoulders.")]
        public float bundleScale = 0.75f;

        private double _amount;
        private BundleStackView _view;

        /// <summary>Fired with the new amount whenever it changes.</summary>
        public event Action Changed;

        public double Amount => _amount;

        public string SaveKey => SaveKeys.For(this);

        /// <summary>The cash of whoever this carry belongs to: the player's, or null for a worker.</summary>
        public static CashStack Of(CarryStack carry) => carry != null ? carry.GetComponent<CashStack>() : null;

        private void Awake()
        {
            Instance = this;
            EnsureView();
        }

        private void OnEnable()
        {
            Instance = this;
            SaveSystem.Register(this);
        }

        private void OnDisable()
        {
            SaveSystem.Unregister(this);
            if (Instance == this) Instance = null;
        }

        private void EnsureView()
        {
            if (_view != null) return;
            _view = new BundleStackView(anchor != null ? anchor : transform, bundlePrefab, maxLayers, bundleScale);
        }

        public void Add(double amount)
        {
            if (amount <= 0d) return;
            _amount += amount;
            Refresh();
        }

        public bool CanAfford(double amount) => _amount >= amount;

        /// <summary>Spends up to <paramref name="requested"/> and returns what was actually spent.</summary>
        public double TrySpend(double requested)
        {
            if (requested <= 0d) return 0d;
            double spent = Math.Min(requested, _amount);
            if (spent <= 0d) return 0d;
            _amount -= spent;
            Refresh();
            return spent;
        }

        private void Refresh()
        {
            EnsureView();
            _view.SetAmount(_amount);
            Changed?.Invoke();
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
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Draws an amount of money as banded bundles stacked two by two, layer on layer.
    ///
    /// One bundle per <see cref="BaseBundle"/> dollars, until the stack would be taller than
    /// <c>maxLayers</c>; past that the bundles MERGE - each stands for ten times as much - so the
    /// stack stays a sane height however rich the player gets, while the amount itself has no limit.
    /// Only the top of the stack is added to or taken from, and nothing runs per frame once the
    /// bundles have settled.
    /// </summary>
    public class BundleStackView
    {
        public const double BaseBundle = 10d;

        private readonly Transform _anchor;
        private readonly GameObject _prefab;
        private readonly int _maxBundles;
        private readonly float _scale;
        private readonly List<Transform> _bundles = new List<Transform>();
        private readonly List<float> _grow = new List<float>();
        private int _growing;
        private double _bundleValue = BaseBundle;

        // Bundle size 0.50 x 0.16 x 0.32 (see CashModels): a 2 x 2 layer is about 1.0 x 0.65.
        private const float HalfX = 0.2575f, HalfZ = 0.1675f, Thickness = 0.16f;

        public BundleStackView(Transform anchor, GameObject prefab, int maxLayers, float scale)
        {
            _anchor = anchor;
            _prefab = prefab;
            _maxBundles = Mathf.Max(1, maxLayers) * 4;
            _scale = scale;
        }

        /// <summary>What one bundle stands for right now.</summary>
        public double BundleValue => _bundleValue;

        public void SetAmount(double amount, bool instant = false)
        {
            // The smallest denomination that fits under the layer cap.
            double value = BaseBundle;
            while (Math.Ceiling(amount / value) > _maxBundles) value *= 10d;
            _bundleValue = value;

            int target = amount < 1d ? 0 : (int)Math.Max(1d, Math.Ceiling(amount / value));

            while (_bundles.Count > target)
            {
                int last = _bundles.Count - 1;
                if (_bundles[last] != null) UnityEngine.Object.Destroy(_bundles[last].gameObject);
                _bundles.RemoveAt(last);
                if (_grow[last] < 1f) _growing--;
                _grow.RemoveAt(last);
            }

            while (_bundles.Count < target)
            {
                if (_prefab == null) { _bundles.Add(null); _grow.Add(1f); continue; }

                int i = _bundles.Count;
                var go = UnityEngine.Object.Instantiate(_prefab, _anchor, false);
                go.name = "Bundle" + i;
                go.transform.localPosition = Slot(i) * _scale;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = instant ? Vector3.one * _scale : Vector3.one * 0.01f;
                _bundles.Add(go.transform);
                _grow.Add(instant ? 1f : 0f);
                if (!instant) _growing++;
            }
        }

        private static Vector3 Slot(int index)
        {
            int layer = index / 4, slot = index % 4;
            return new Vector3((slot % 2 == 0 ? -1f : 1f) * HalfX, layer * Thickness,
                               (slot / 2 == 0 ? -1f : 1f) * HalfZ);
        }

        /// <summary>Pops new bundles up to full size. Does nothing once they have all settled.</summary>
        public void Tick(float dt)
        {
            if (_growing <= 0) return;

            for (int i = 0; i < _bundles.Count; i++)
            {
                if (_grow[i] >= 1f) continue;
                _grow[i] = Mathf.Min(1f, _grow[i] + dt * 7f);
                if (_bundles[i] != null) _bundles[i].localScale = Vector3.one * (_scale * Mathf.Max(0.01f, _grow[i]));
                if (_grow[i] >= 1f) _growing--;
            }
        }
    }
}
