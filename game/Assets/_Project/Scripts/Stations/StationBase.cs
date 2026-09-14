using System.Collections.Generic;
using Tycoon.Player;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Implemented by an actor that is choosy about which squares it uses.
    ///
    /// This exists because the carry stack now holds mixed goods. Previously a worker walking
    /// past the corn field with its arms full of eggs was refused by type, which accidentally
    /// kept workers on their route. With a mixed stack there is nothing to refuse, so a seller
    /// would wander into the field and fill up with corn it has nowhere to take.
    ///
    /// The player implements nothing and may use everything, which is the whole point of them.
    /// </summary>
    public interface IStationUser
    {
        bool WillUse(StationBase station);
    }

    /// <summary>How a station turns time spent standing in it into a result.</summary>
    public enum InteractionMode
    {
        /// <summary>
        /// Goods move one unit at a time while an actor stands here. This is the signature
        /// feel of the genre: the carried stack visibly grows item by item, and the player can
        /// walk away with a partial load. Used for harvest, feed, collect and sell.
        /// </summary>
        Transfer,

        /// <summary>
        /// A job with a duration: enter, fill a progress ring, get a result. Used where there
        /// is nothing to carry - cleaning, repairing, painting, renovating.
        /// </summary>
        Task
    }

    /// <summary>
    /// Base class for every "designated square" in the game. Standing in one does the work -
    /// there is never a button to press.
    ///
    /// The same class serves the player and hired workers. A worker is simply something with a
    /// <see cref="CarryStack"/> that walks into the trigger, which is why employees need no
    /// parallel gameplay logic anywhere in the project.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public abstract class StationBase : MonoBehaviour
    {
        [Header("Station")]
        [Tooltip("Shown on the square and on the floating sign above it.")]
        public string label = "";

        [Tooltip("Colour of the square, so each station type reads at a glance.")]
        public Color zoneColor = new Color(0.3f, 0.8f, 1f, 0.55f);

        [Header("Interaction")]
        public InteractionMode mode = InteractionMode.Transfer;

        [Tooltip("Transfer mode: seconds between single-unit transfers. Lower is faster and " +
                 "more satisfying; too low and the stack appears to teleport.")]
        [Min(0.02f)] public float tickInterval = 0.18f;

        [Tooltip("Task mode: seconds of work per completed task.")]
        [Min(0.05f)] public float taskDuration = 1.2f;

        [Tooltip("Seconds an actor must wait before repeating the action here.")]
        [Min(0f)] public float cooldown = 0f;

        [Header("Who may use this")]
        public bool playerCompatible = true;
        public bool workerCompatible = true;

        /// <summary>
        /// Whoever is currently being ticked. Subclasses read this inside
        /// <see cref="TickWithPlayer"/> without caring whether it is the player or a worker.
        /// </summary>
        protected CarryStack Carry { get; private set; }

        private class Occupant
        {
            public CarryStack Carry;
            public bool IsPlayer;
            public float TransferTimer;
            public float TaskTimer;
            public float CooldownLeft;
        }

        private readonly List<Occupant> _occupants = new List<Occupant>();
        private float _displayedTaskProgress;

        /// <summary>True while at least one compatible actor is standing here.</summary>
        public bool IsOccupied => _occupants.Count > 0;

        /// <summary>True while the player specifically is standing here.</summary>
        public bool HasPlayer
        {
            get
            {
                for (int i = 0; i < _occupants.Count; i++)
                    if (_occupants[i].IsPlayer) return true;
                return false;
            }
        }

        /// <summary>
        /// 0-1 for the square's progress ring.
        ///
        /// It deliberately means different things per mode: in Task mode it is the work timer,
        /// in Transfer mode it is whatever the station considers "how this is going" - how full
        /// the field still is, how full the hopper is. Either way the player reads one ring.
        /// </summary>
        public float Progress01 => mode == InteractionMode.Task
            ? Mathf.Clamp01(_displayedTaskProgress)
            : Mathf.Clamp01(TransferProgress);

        /// <summary>Override to drive the ring in Transfer mode. Zero hides it.</summary>
        protected virtual float TransferProgress => 0f;

        /// <summary>Text for the floating sign. Override to show live numbers or prices.</summary>
        public virtual string StatusText => label;

        /// <summary>False greys the square out, e.g. a machine that is jammed or a full hopper.</summary>
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

            bool isPlayer = carry.CompareTag("Player");
            if (isPlayer && !playerCompatible) return;
            if (!isPlayer && !workerCompatible) return;

            // Workers only stop at the squares on their own route; see IStationUser.
            var chooser = carry.GetComponentInParent<IStationUser>();
            if (chooser != null && !chooser.WillUse(this)) return;

            _occupants.Add(new Occupant { Carry = carry, IsPlayer = isPlayer });
            OnPlayerEnter();
        }

        private void OnTriggerExit(Collider other)
        {
            var carry = other.GetComponentInParent<CarryStack>();
            if (carry == null) return;

            int index = _occupants.FindIndex(o => o.Carry == carry);
            if (index < 0) return;

            _occupants.RemoveAt(index);
            if (_occupants.Count == 0) _displayedTaskProgress = 0f;
            OnPlayerExit();
        }

        protected virtual void Update()
        {
            TickAlways(Time.deltaTime);

            if (_occupants.Count == 0) return;

            // Clamped for the same reason as the player's movement: after a long loading frame
            // an unclamped delta would drain a whole field into the player's arms instantly.
            float delta = Mathf.Min(Time.deltaTime, 0.1f);
            float bestProgress = 0f;

            for (int i = _occupants.Count - 1; i >= 0; i--)
            {
                var occupant = _occupants[i];
                if (occupant.Carry == null)
                {
                    _occupants.RemoveAt(i);
                    continue;
                }

                if (occupant.CooldownLeft > 0f)
                {
                    occupant.CooldownLeft -= delta;
                    continue;
                }

                Carry = occupant.Carry;

                if (mode == InteractionMode.Task) RunTask(occupant, delta, ref bestProgress);
                else RunTransfer(occupant, delta);
            }

            Carry = null;
            if (mode == InteractionMode.Task) _displayedTaskProgress = bestProgress;
        }

        private void RunTransfer(Occupant occupant, float delta)
        {
            occupant.TransferTimer += delta;

            // A while loop rather than an if, so a very short tickInterval still keeps up
            // with the frame rate instead of silently throttling to one unit per frame.
            int guard = 0;
            while (occupant.TransferTimer >= tickInterval && guard++ < 16)
            {
                occupant.TransferTimer -= tickInterval;
                if (!TickWithPlayer()) { occupant.TransferTimer = 0f; break; }
            }
        }

        private void RunTask(Occupant occupant, float delta, ref float bestProgress)
        {
            if (!CanPerformTask())
            {
                occupant.TaskTimer = 0f;
                return;
            }

            occupant.TaskTimer += delta;

            if (occupant.TaskTimer >= taskDuration)
            {
                occupant.TaskTimer = 0f;
                occupant.CooldownLeft = cooldown;
                CompleteTask();
            }

            float progress = taskDuration <= 0f ? 0f : occupant.TaskTimer / taskDuration;
            if (progress > bestProgress) bestProgress = progress;
        }

        /// <summary>
        /// Transfer mode: move one unit of whatever this station moves.
        /// Return false when nothing could happen, to stop retrying until the next frame.
        /// Task-mode stations leave this returning false.
        /// </summary>
        protected virtual bool TickWithPlayer() => false;

        /// <summary>Task mode: false pauses the progress ring, e.g. nothing left to repair.</summary>
        protected virtual bool CanPerformTask() => true;

        /// <summary>Task mode: fired once each time the progress ring fills.</summary>
        protected virtual void CompleteTask() { }

        /// <summary>Runs every frame whether or not anyone is present (production, decay).</summary>
        protected virtual void TickAlways(float deltaTime) { }

        protected virtual void OnPlayerEnter() { }
        protected virtual void OnPlayerExit() { }
    }
}
