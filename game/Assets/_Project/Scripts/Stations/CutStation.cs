using Tycoon.Audio;
using Tycoon.Config;
using Tycoon.Player;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// One of the butcher's two jobs at a bench: the cutting table (1 raw meat to 1 steak) or the sausage
    /// grinder (1 raw meat to 3 sausages). One place, one product - what the square shows is what it makes.
    /// Stand here and raw meat from the drop pile is worked, one joint per ring, and the result goes straight
    /// into the arms of whoever is standing here (the player, or the butcher), so the next leg is the
    /// ordinary carry to the counter and nothing needs a second station or a parallel way of moving goods.
    ///
    /// It never makes what the counter has no room for (counting what the cutter already carries), so a
    /// butcher can never fill its arms with something nobody can take. Before the counter is bought there is
    /// no case to ask and the cutter simply fills its arms - the stock it carries over once the counter opens.
    ///
    /// Task mode, like every timed job here, so the progress ring is the work. Workers can stand here, which
    /// is how the butcher is hired: WorkerAgent chooses between the bench and the grinder by which counter
    /// case is emptier, and deliverPartialLoads stops it waiting here with a half load when there is nothing
    /// to do.
    /// </summary>
    public class CutStation : StationBase
    {
        [Header("Input")]
        [Tooltip("The drop pile the van unloads onto.")]
        public ItemBuffer meat;

        [Header("Output")]
        [Tooltip("What one joint becomes: the steak at the table, the sausage at the grinder.")]
        public ItemDefinition output;

        [Tooltip("How many of it one joint makes: 1 steak, 3 sausages.")]
        [Min(1)] public int makes = 1;

        [Tooltip("The counter's case for this product. An inactive one (the counter not bought yet) is treated as having room.")]
        public ItemBuffer outputCase;

        protected override void Awake()
        {
            base.Awake();
            // Cutting is a timed job, never a transfer - forced here, as for the repair square.
            mode = InteractionMode.Task;
        }

        public override bool IsOperational => meat != null && !meat.IsEmpty;

        public override Tycoon.Config.ItemDefinition IconItem => output;

        /// <summary>How full the counter's case for this product is, 0-1 (0 while there is no case yet).</summary>
        public float CaseFill =>
            outputCase != null && outputCase.isActiveAndEnabled ? outputCase.Fill : 0f;

        public override string StatusValue
        {
            get
            {
                if (meat == null) return string.Empty;
                if (meat.IsEmpty) return "No meat";
                if (!Wanted(null)) return "Full";
                return $"{meat.Count} meat";
            }
        }

        /// <summary>Is there room for one more joint's worth, in the arms (when given) and in the case?</summary>
        private bool Wanted(CarryStack carry)
        {
            if (output == null) return false;
            if (carry != null && carry.capacity - carry.Count < makes) return false;

            if (outputCase != null && outputCase.isActiveAndEnabled)
            {
                int held = carry != null ? carry.CountOf(output) : 0;
                if (outputCase.capacity - outputCase.Count - held < 1) return false;
            }
            return true;
        }

        /// <summary>
        /// True when standing here would achieve nothing for this pair of arms: no meat, or nothing the
        /// counter wants and the arms can hold. A butcher in this state takes what it has to the counter (or
        /// tries the other bench) instead of waiting for a full load.
        /// </summary>
        public bool HasNothingToDo(CarryStack carry) =>
            meat == null || meat.IsEmpty || !Wanted(carry);

        protected override bool CanPerformTask() =>
            Carry != null && meat != null && !meat.IsEmpty && Wanted(Carry);

        protected override void CompleteTask()
        {
            if (Carry == null || meat == null || !Wanted(Carry) || meat.Remove(1) == 0) return;

            for (int i = 0; i < makes; i++)
            {
                // Planned to fit, so this never refuses; if it somehow does, the joint is not lost.
                if (!Carry.TryAdd(output))
                {
                    if (i == 0) meat.Add(1);
                    return;
                }
            }

            SoundFx.PlayAt(Sfx.Fix, transform.position);
            Tycoon.UI.WorldFeedback.Add($"cut{GetInstanceID()}",
                Carry.transform.position + Vector3.up * 2.2f, makes, output.displayName, output.color);
        }
    }
}
