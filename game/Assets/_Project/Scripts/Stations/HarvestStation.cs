using System;
using Tycoon.Config;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// A field the player harvests by standing in it: the corn patch.
    ///
    /// Crops regrow on a timer whether or not anyone is watching, and catch up on regrowth
    /// that happened while the game was closed, so the field is never the bottleneck after a
    /// break - the player's attention is.
    /// </summary>
    public class HarvestStation : StationBase, ISaveable
    {
        [Header("Crop")]
        public ItemDefinition crop;

        [Tooltip("How many units the field holds when fully grown.")]
        [Min(1)] public int plots = 6;

        [Tooltip("Seconds to regrow one unit.")]
        [Min(0.1f)] public float regrowSeconds = 2.5f;

        [Header("Visuals")]
        [Tooltip("Parent whose children are the individual crop meshes. The first N children " +
                 "are shown, where N is the amount currently ready to harvest.")]
        public Transform cropVisuals;

        [SerializeField] private int _ready;
        private float _growthTimer;

        public string SaveKey => SaveKeys.For(this);
        public int Ready => _ready;
        public override bool IsOperational => _ready > 0;
        public override string StatusText =>
            crop == null ? label : $"{label} {_ready}/{plots}";

        /// <summary>The square's ring shows how much of the field is still standing.</summary>
        protected override float TransferProgress => plots <= 0 ? 0f : (float)_ready / plots;

        protected override void Awake()
        {
            base.Awake();
            _ready = plots; // a fresh field starts full so the game opens with something to do
        }

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        private void Start() => RefreshVisuals();

        protected override void TickAlways(float deltaTime)
        {
            Grow(deltaTime);
        }

        private void Grow(double seconds)
        {
            if (_ready >= plots || regrowSeconds <= 0f) return;

            _growthTimer += (float)seconds;
            bool changed = false;
            while (_growthTimer >= regrowSeconds && _ready < plots)
            {
                _growthTimer -= regrowSeconds;
                _ready++;
                changed = true;
            }

            if (_ready >= plots) _growthTimer = 0f;
            if (changed) RefreshVisuals();
        }

        protected override bool TickWithPlayer()
        {
            if (crop == null || _ready <= 0) return false;
            if (Carry == null || !Carry.CanAccept(crop)) return false;

            if (!Carry.TryAdd(crop)) return false;

            _ready--;
            RefreshVisuals();

            Tycoon.UI.WorldFeedback.Add($"harvest{GetInstanceID()}",
                Carry.transform.position + Vector3.up * 2.2f, 1, crop.displayName, crop.color);

            return true;
        }

        private void RefreshVisuals()
        {
            if (cropVisuals == null) return;

            for (int i = 0; i < cropVisuals.childCount; i++)
            {
                cropVisuals.GetChild(i).gameObject.SetActive(i < _ready);
            }
        }

        [Serializable]
        private struct State
        {
            public int ready;
            public float growthTimer;
            public double lastUnix;
        }

        public string CaptureState() => JsonUtility.ToJson(new State
        {
            ready = _ready,
            growthTimer = _growthTimer,
            lastUnix = GameClock.NowUnix
        });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _ready = Mathf.Clamp(s.ready, 0, plots);
            _growthTimer = s.growthTimer;

            double gap = GameClock.ClampOffline(GameClock.NowUnix - s.lastUnix);
            if (gap > 0d) Grow(gap);

            RefreshVisuals();
        }
    }
}
