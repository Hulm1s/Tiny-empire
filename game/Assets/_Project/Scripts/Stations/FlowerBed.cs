using System;
using Tycoon.Audio;
using Tycoon.Config;
using Tycoon.Core;
using Tycoon.Player;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>Where a flower bed is in its cycle. Stored in the save as the number, so the order is fixed.</summary>
    public enum BedStage
    {
        /// <summary>Bare soil: waiting for a seed.</summary>
        Empty = 0,
        /// <summary>Seed in: seedlings, dry, waiting for the first water.</summary>
        NeedsWater1 = 1,
        /// <summary>Watered once: the seedlings are growing.</summary>
        Growing1 = 2,
        /// <summary>Half-grown, dry again, waiting for the second water.</summary>
        NeedsWater2 = 3,
        /// <summary>Watered twice: growing to full bloom.</summary>
        Growing2 = 4,
        /// <summary>In full bloom: waiting for someone to stand in the square and pick it.</summary>
        Ready = 5,
    }

    /// <summary>
    /// One of the florist's flower beds, and the cycle that fills the flower stock behind the counter:
    ///
    ///     empty -> plant a seed -> needs water -> grows -> needs water again -> grows -> ready -> harvest
    ///
    /// Nothing happens by itself except the two growing spells. Planting, both waterings and the harvest
    /// are done by standing in the bed's one square (<see cref="BedStation"/>) with the right thing in your
    /// arms (a seed, a can of water) or, for the harvest, with room left in the flower's stock: the player
    /// does them, or the gardener does. A harvest puts <see cref="flowersPerHarvest"/> flowers straight
    /// into the flower's stock (the <see cref="ItemBuffer"/> the counter serves from) and the bed is bare
    /// again. Two beds of one flower share one stock.
    ///
    /// The waiting stages (empty, needs water, ready) never decay and nothing here costs money: seeds
    /// and water are free, so a player who has nothing can always get the next bed going.
    ///
    /// The bed shows one look per stage (the six models), so nothing swaps while it grows, and a bubble
    /// over it asks for what it needs: a seed, water, or a harvest. It is hidden while the bed is growing.
    ///
    /// Saved: the stage and the progress of the growing spell; the time the game was closed counts
    /// towards a spell that was running (on the first frame after a load). A save from before the cycle
    /// existed (no version) loads as bare soil.
    /// </summary>
    public class FlowerBed : MonoBehaviour, ISaveable
    {
        [Header("Flowers")]
        [Tooltip("The flower's stock behind the counter, shared by its beds.")]
        public ItemBuffer stock;

        [Tooltip("Flowers a harvest puts in the stock.")]
        [Min(1)] public int flowersPerHarvest = 4;

        [Tooltip("Seconds each of the two growing spells takes.")]
        [Min(1f)] public float secondsPerSpell = 12f;

        [Header("What it needs")]
        public ItemDefinition seed;
        public ItemDefinition water;

        [Header("For the task list")]
        public BedStation square;
        public CollectStation seedSquare;
        public CollectStation wellSquare;

        [Header("Looks (one per stage, in BedStage order)")]
        public GameObject[] looks;

        public BedBubble bubble;

        private BedStage _stage;
        private float _timer;
        private double _pendingOffline;
        private int _shown = -1;

        public string SaveKey => SaveKeys.For(this);

        public BedStage Stage => _stage;
        public bool IsEmpty => _stage == BedStage.Empty;
        public bool NeedsWater => _stage == BedStage.NeedsWater1 || _stage == BedStage.NeedsWater2;
        public bool IsReady => _stage == BedStage.Ready;
        public bool IsGrowing => _stage == BedStage.Growing1 || _stage == BedStage.Growing2;

        /// <summary>The flower this bed grows.</summary>
        public ItemDefinition Flower => stock != null ? stock.item : null;

        /// <summary>Room in the stock for a whole harvest. A full stock leaves a ripe bed waiting.</summary>
        public bool StockHasRoom => stock != null && stock.FreeSpace >= flowersPerHarvest;

        /// <summary>0-1 through the growing spell the bed is in (0 when it is not growing).</summary>
        public float SpellProgress => IsGrowing ? Mathf.Clamp01(_timer / secondsPerSpell) : 0f;

        /// <summary>What the bed wants done to it right now: 1 plant, 2 water, 3 harvest, 0 nothing.</summary>
        public int Job => IsReady ? (StockHasRoom ? 3 : 0) : NeedsWater ? 2 : IsEmpty ? 1 : 0;

        /// <summary>Can whoever has these arms do this bed's job right now?</summary>
        public bool CanServe(CarryStack by)
        {
            if (by == null) return false;
            switch (Job)
            {
                case 1: return seed != null && by.Has(seed);
                case 2: return water != null && by.Has(water);
                case 3: return true;
                default: return false;
            }
        }

        /// <summary>Does the job this bed wants take what is in these arms (a seed, a can)?</summary>
        public bool WantsItem(ItemDefinition item) =>
            item != null && ((Job == 1 && item == seed) || (Job == 2 && item == water));

        /// <summary>Does the bed's job, taking the seed or the water out of the arms. False if it could not.</summary>
        public bool Serve(CarryStack by)
        {
            if (!CanServe(by)) return false;

            switch (Job)
            {
                case 1:
                    if (!by.TryRemove(seed)) return false;
                    _stage = BedStage.NeedsWater1;
                    SoundFx.PlayAt(Sfx.Drop, transform.position);
                    break;

                case 2:
                    if (!by.TryRemove(water)) return false;
                    _stage = _stage == BedStage.NeedsWater1 ? BedStage.Growing1 : BedStage.Growing2;
                    _timer = 0f;
                    SoundFx.PlayAt(Sfx.Drop, transform.position);
                    break;

                default:
                    int added = stock.Add(flowersPerHarvest);
                    if (added <= 0) return false;
                    _stage = BedStage.Empty;
                    _timer = 0f;
                    SoundFx.PlayAt(Sfx.Pop, transform.position);
                    WorldFeedback.Show(transform.position + Vector3.up * 1.8f, $"+{added} {stock.item.displayName}",
                        stock.item.color);
                    break;
            }

            RefreshLook();
            return true;
        }

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        private void Start()
        {
            if (_pendingOffline > 0d)
            {
                Advance((float)_pendingOffline);
                _pendingOffline = 0d;
            }
            RefreshLook();
        }

        private void Update()
        {
            // Clamped as everywhere else, so a long loading frame is not paid out as a burst.
            Advance(Mathf.Min(Time.deltaTime, 0.1f));
            RefreshLook();
        }

        /// <summary>Only the two growing spells move on their own; every waiting stage holds for ever.</summary>
        private void Advance(float seconds)
        {
            if (!IsGrowing || seconds <= 0f) return;

            _timer += seconds;
            if (_timer < secondsPerSpell) return;

            _timer = 0f;
            _stage = _stage == BedStage.Growing1 ? BedStage.NeedsWater2 : BedStage.Ready;
        }

        private void RefreshLook()
        {
            int look = (int)_stage;
            if (look != _shown)
            {
                _shown = look;
                if (looks != null)
                    for (int i = 0; i < looks.Length; i++)
                        if (looks[i] != null) looks[i].SetActive(i == look);
            }

            if (bubble != null)
                bubble.Show(NeedsWater ? BedBubble.Kind.Water : IsEmpty ? BedBubble.Kind.Seed
                    : IsReady ? BedBubble.Kind.Ready : BedBubble.Kind.None);
        }

        [Serializable]
        private struct State
        {
            // 0 = a save from before the plant, water, harvest cycle (progress/moist fields only).
            public int version;
            public int stage;
            public float timer;
            public double lastUnix;
        }

        public string CaptureState() => JsonUtility.ToJson(new State
        {
            version = 1,
            stage = (int)_stage,
            timer = _timer,
            lastUnix = GameClock.NowUnix,
        });

        public void RestoreState(string json)
        {
            var s = JsonUtility.FromJson<State>(json);
            if (s.version < 1)
            {
                // The old beds grew by themselves; there is nothing in them to carry over.
                _stage = BedStage.Empty;
                _timer = 0f;
                _pendingOffline = 0d;
            }
            else
            {
                _stage = (BedStage)Mathf.Clamp(s.stage, 0, (int)BedStage.Ready);
                _timer = Mathf.Max(0f, s.timer);
                _pendingOffline = IsGrowing ? GameClock.ClampOffline(GameClock.NowUnix - s.lastUnix) : 0d;
            }
            _shown = -1;
        }
    }
}
