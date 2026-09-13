using System.Text;
using Tycoon.Stations;
using Tycoon.UI;
using UnityEngine;

namespace Tycoon.Upkeep
{
    /// <summary>
    /// Watches one business and tells the HUD when it needs the player.
    ///
    /// Once several businesses are running at once, decay on its own is just an unpleasant
    /// surprise - the player cannot be everywhere, and finding the jammed machine by walking
    /// the whole map is a chore rather than a decision. The alert is what turns upkeep into
    /// "which problem do I deal with first".
    /// </summary>
    public class AlertBeacon : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Name used in the alert line, e.g. 'Coop A'.")]
        public string businessName = "Business";

        [Header("What to watch (all optional)")]
        public ProducerMachine machine;
        public Durability durability;
        public WorkerAgent worker;

        [Tooltip("Warns when this fills up, because a full output buffer stalls production.")]
        public ItemBuffer outputBuffer;

        [Tooltip("Warns when this runs dry.")]
        public ItemBuffer inputBuffer;

        [Header("Thresholds")]
        [Range(0f, 1f)] public float outputFullAbove = 0.999f;

        private readonly StringBuilder _builder = new StringBuilder(64);

        /// <summary>Non-empty when this business wants attention. Read by the world map later.</summary>
        public string CurrentAlert { get; private set; }

        private void Update()
        {
            _builder.Clear();

            if (durability != null && durability.IsBroken) Add("jammed");
            else if (durability != null && durability.NeedsAttention) Add("wearing out");

            if (worker != null && worker.UnderpaidRecently) Add("worker underpaid");

            if (machine != null && machine.CurrentBlockage == ProducerMachine.Blockage.NoInput)
                Add("out of feed");

            if (outputBuffer != null && outputBuffer.Fill >= outputFullAbove) Add("output full");
            else if (inputBuffer != null && inputBuffer.IsEmpty && machine != null) { /* covered above */ }

            CurrentAlert = _builder.Length == 0 ? "" : $"{businessName}: {_builder}";

            if (!string.IsNullOrEmpty(CurrentAlert) && HudRoot.Instance != null)
                HudRoot.Instance.ReportAlert(CurrentAlert);
        }

        private void Add(string reason)
        {
            if (_builder.Length > 0) _builder.Append(", ");
            _builder.Append(reason);
        }
    }
}
