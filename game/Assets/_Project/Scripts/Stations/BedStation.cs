using Tycoon.Config;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The one square on a flower bed. Stand in it and it does whatever the bed wants next, if you can:
    /// plant with a seed in your arms, water with a can, harvest when the bed is in bloom and the stock
    /// has room. A Task-mode square (a short ring to fill), open to the player and to the gardener;
    /// the state lives in the <see cref="FlowerBed"/>, which this only asks.
    ///
    /// It says what it wants: the seed or the water drop on the square, "Growing" with a percentage
    /// while there is nothing to do, "Harvest" when the bed is ripe.
    /// </summary>
    public class BedStation : StationBase
    {
        [Header("Bed")]
        public FlowerBed bed;

        protected override void Awake()
        {
            base.Awake();
            mode = InteractionMode.Task;
        }

        protected override bool CanPerformTask() => bed != null && bed.CanServe(Carry);

        protected override void CompleteTask()
        {
            if (bed != null) bed.Serve(Carry);
        }

        public override bool IsOperational => bed != null && bed.Job != 0;

        public override string ActionLabel
        {
            get
            {
                if (bed == null) return label;
                switch (bed.Stage)
                {
                    case BedStage.Empty: return "Plant";
                    case BedStage.NeedsWater1:
                    case BedStage.NeedsWater2: return "Water";
                    case BedStage.Ready: return "Harvest";
                    default: return "Growing";
                }
            }
        }

        public override string StatusValue
        {
            get
            {
                if (bed == null) return string.Empty;
                if (bed.IsGrowing) return $"{Mathf.RoundToInt(bed.SpellProgress * 100f)}%";
                if (bed.IsReady) return $"x{bed.flowersPerHarvest}";
                return string.Empty;
            }
        }

        public override ItemDefinition IconItem
        {
            get
            {
                if (bed == null) return null;
                switch (bed.Stage)
                {
                    case BedStage.Empty: return bed.seed;
                    case BedStage.NeedsWater1:
                    case BedStage.NeedsWater2: return bed.water;
                    default: return bed.Flower;
                }
            }
        }
    }
}
