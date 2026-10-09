using System.Collections.Generic;
using System.Text;
using Tycoon.Stations;
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

        [Header("Where to send the player (all optional)")]
        [Tooltip("The square that fixes this business. The task list's arrow points here.")]
        public StationBase repairSquare;

        [Tooltip("The square that feeds this business.")]
        public StationBase feedSquare;

        [Tooltip("The square that empties this business's output.")]
        public StationBase collectSquare;

        [Header("Thresholds")]
        [Range(0f, 1f)] public float outputFullAbove = 0.999f;

        private readonly StringBuilder _builder = new StringBuilder(64);

        /// <summary>
        /// Every beacon currently running, so the HUD can poll them on its own schedule.
        ///
        /// Beacons used to push into the HUD from their own Update, which meant every business
        /// composed an alert string sixty times a second to report a condition that changes
        /// once a minute. The HUD now asks, a few times a second, and nothing is built in
        /// between.
        /// </summary>
        public static readonly List<AlertBeacon> Active = new List<AlertBeacon>();

        /// <summary>Non-empty when this business wants attention. Read by the world map later.</summary>
        public string CurrentAlert { get; private set; }

        // The same conditions as the text, kept as flags so the task list can turn each one into
        // its own line and arrow target without parsing the sentence back apart.
        public bool IsJammed { get; private set; }
        public bool IsWearing { get; private set; }
        public bool NeedsFeed { get; private set; }
        public bool OutputIsFull { get; private set; }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        /// <summary>Recomputes <see cref="CurrentAlert"/>. Called by the HUD, not per frame.</summary>
        public void Refresh()
        {
            _builder.Clear();

            IsJammed = durability != null && durability.IsBroken;
            IsWearing = !IsJammed && durability != null && durability.NeedsAttention;
            NeedsFeed = machine != null && machine.CurrentBlockage == ProducerMachine.Blockage.NoInput;
            OutputIsFull = outputBuffer != null && outputBuffer.Fill >= outputFullAbove;

            if (IsJammed) Add("jammed");
            else if (IsWearing) Add("wearing out");

            if (NeedsFeed) Add("out of feed");

            if (OutputIsFull) Add("output full");
            else if (inputBuffer != null && inputBuffer.IsEmpty && machine != null) { /* covered above */ }

            CurrentAlert = _builder.Length == 0 ? "" : $"{businessName}: {_builder}";
        }

        private void Add(string reason)
        {
            if (_builder.Length > 0) _builder.Append(", ");
            _builder.Append(reason);
        }
    }
}
