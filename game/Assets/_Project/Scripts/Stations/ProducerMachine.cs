using System;
using Tycoon.Core;
using Tycoon.Upkeep;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Converts one buffer into another over time: chickens turning corn into eggs, an oven
    /// turning flour and eggs into bread.
    ///
    /// It runs on its own without the player present, and catches up on production that
    /// happened while the game was closed. It stops dead when its input runs dry, its output
    /// backs up, or it wears out - which are exactly the three things the player is there
    /// to prevent.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class ProducerMachine : MonoBehaviour, ISaveable
    {
        public enum Blockage { None, NoInput, OutputFull, Broken }

        [Header("Recipe")]
        [Tooltip("Leave empty for a machine that produces from nothing, like a well.")]
        public ItemBuffer input;

        public ItemBuffer output;

        [Min(1)] public int inputPerOutput = 1;

        [Tooltip("Seconds of work per unit produced.")]
        [Min(0.05f)] public float secondsPerOutput = 3f;

        [Header("Upkeep")]
        [Tooltip("Optional. Without it the machine never wears out and never needs repair.")]
        public Durability durability;

        private float _progress;
        private double _restoredAtUnix;
        private bool _awaitingCatchUp;

        public event Action Changed;

        /// <summary>0-1 progress through the current unit, for the floating progress ring.</summary>
        public float Progress01 => secondsPerOutput <= 0f ? 0f : Mathf.Clamp01(_progress / secondsPerOutput);

        public Blockage CurrentBlockage { get; private set; }
        public bool IsBlocked => CurrentBlockage != Blockage.None;

        public string SaveKey => SaveKeys.For(this);

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        private void Start()
        {
            // Deliberately deferred to Start: every buffer in the scene has finished restoring
            // its own saved contents by now, so offline production runs against real numbers.
            if (!_awaitingCatchUp) return;
            _awaitingCatchUp = false;

            double gap = GameClock.ClampOffline(GameClock.NowUnix - _restoredAtUnix);
            if (gap > 0d) Advance(gap);
        }

        private void Update() => Advance(Time.deltaTime);

        private void Advance(double seconds)
        {
            if (secondsPerOutput <= 0f || output == null) return;

            double remaining = seconds;
            int guard = 0;

            while (remaining > 0d && guard++ < 5000)
            {
                CurrentBlockage = EvaluateBlockage();
                if (CurrentBlockage != Blockage.None) break;

                double needed = secondsPerOutput - _progress;
                if (remaining < needed)
                {
                    _progress += (float)remaining;
                    break;
                }

                remaining -= needed;
                _progress = 0f;
                ProduceOne();
            }

            CurrentBlockage = EvaluateBlockage();
        }

        private Blockage EvaluateBlockage()
        {
            if (durability != null && durability.IsBroken) return Blockage.Broken;
            if (output == null || output.IsFull) return Blockage.OutputFull;
            if (input != null && input.Count < inputPerOutput) return Blockage.NoInput;
            return Blockage.None;
        }

        private void ProduceOne()
        {
            if (input != null && input.Remove(inputPerOutput) < inputPerOutput) return;

            output.Add(1);
            if (durability != null) durability.Wear(durability.wearPerOutput);
            Changed?.Invoke();
        }

        [Serializable]
        private struct State
        {
            public float progress;
            public double lastUnix;
        }

        public string CaptureState() => JsonUtility.ToJson(new State
        {
            progress = _progress,
            lastUnix = GameClock.NowUnix
        });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            _progress = s.progress;
            _restoredAtUnix = s.lastUnix;
            _awaitingCatchUp = true;
        }
    }
}
