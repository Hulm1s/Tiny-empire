using System.Collections.Generic;
using Tycoon.Player;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Base class for every "designated square" in the game. Standing in one does the work -
    /// there is never a button to press.
    ///
    /// Subclasses only implement <see cref="TickWithPlayer"/>, which runs on a fixed cadence
    /// while the player stands inside and moves exactly one unit of something. That cadence is
    /// what produces the characteristic drip-drip-drip transfer of this genre, and it means a
    /// brand new station type is usually about forty lines.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public abstract class StationBase : MonoBehaviour
    {
        [Header("Station")]
        [Tooltip("Shown on the floating sign above the square.")]
        public string label = "";

        [Tooltip("Seconds between single-unit transfers while the player stands here. " +
                 "Lower is faster and more satisfying; too low and the stack teleports.")]
        [Min(0.02f)] public float tickInterval = 0.18f;

        [Tooltip("Colour of the floor decal, so each station type reads at a glance.")]
        public Color zoneColor = new Color(0.3f, 0.8f, 1f, 0.55f);

        /// <summary>
        /// Whoever is currently being ticked. Subclasses read this inside
        /// <see cref="TickWithPlayer"/> without caring whether it is the player or a hired
        /// worker - which is exactly why workers need no special-case code anywhere.
        /// </summary>
        protected CarryStack Carry { get; private set; }

        private class Occupant
        {
            public CarryStack Carry;
            public float Timer;
        }

        private readonly List<Occupant> _occupants = new List<Occupant>();

        /// <summary>True while at least one worker or the player is standing here.</summary>
        public bool IsOccupied => _occupants.Count > 0;

        /// <summary>Text for the floating sign. Override to show live numbers or prices.</summary>
        public virtual string StatusText => label;

        /// <summary>False greys out the decal and sign, e.g. a machine that is jammed.</summary>
        public virtual bool IsOperational => true;

        protected virtual void Reset()
        {
            var box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2f, 2f, 2f);
            box.center = new Vector3(0f, 1f, 0f);
        }

        protected virtual void Awake()
        {
            var box = GetComponent<BoxCollider>();
            if (box != null) box.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var carry = other.GetComponentInParent<CarryStack>();
            if (carry == null) return;
            if (_occupants.Exists(o => o.Carry == carry)) return;

            _occupants.Add(new Occupant { Carry = carry, Timer = 0f });
            OnPlayerEnter();
        }

        private void OnTriggerExit(Collider other)
        {
            var carry = other.GetComponentInParent<CarryStack>();
            if (carry == null) return;

            int index = _occupants.FindIndex(o => o.Carry == carry);
            if (index < 0) return;

            _occupants.RemoveAt(index);
            OnPlayerExit();
        }

        protected virtual void Update()
        {
            TickAlways(Time.deltaTime);

            if (_occupants.Count == 0) return;

            // Clamped for the same reason as the player's movement: after a long loading frame
            // an unclamped delta would drain a whole field into the player's arms instantly.
            float delta = Mathf.Min(Time.deltaTime, 0.1f);

            for (int i = _occupants.Count - 1; i >= 0; i--)
            {
                var occupant = _occupants[i];
                if (occupant.Carry == null)
                {
                    _occupants.RemoveAt(i);
                    continue;
                }

                occupant.Timer += delta;
                Carry = occupant.Carry;

                // A while loop rather than an if, so a very short tickInterval still keeps up
                // with the frame rate instead of silently throttling to one unit per frame.
                int guard = 0;
                while (occupant.Timer >= tickInterval && guard++ < 16)
                {
                    occupant.Timer -= tickInterval;
                    if (!TickWithPlayer()) { occupant.Timer = 0f; break; }
                }
            }

            Carry = null;
        }

        /// <summary>
        /// Move one unit of whatever this station moves.
        /// Return false when nothing could happen, to stop retrying until the next frame.
        /// </summary>
        protected abstract bool TickWithPlayer();

        /// <summary>Runs every frame whether or not the player is present (production, decay).</summary>
        protected virtual void TickAlways(float deltaTime) { }

        protected virtual void OnPlayerEnter() { }
        protected virtual void OnPlayerExit() { }
    }
}
