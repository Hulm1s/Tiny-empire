using Tycoon.Customers;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The salon's NEXT square: stand in it and the customer at the head of the waiting room is sent to the
    /// first free, clean chair (<see cref="CustomerQueue.DispatchFront()"/>), one per ring.
    ///
    /// A plain Task-mode square, so it costs nothing and needs nothing in the player's hands - calling the next
    /// customer can never need money. The ring only runs while a call would succeed (somebody seated at the head
    /// AND a chair free), so standing here with every chair busy does nothing rather than queue up calls.
    /// A hired receptionist works it like any one-square job: a worker whose pickup and dropoff are both this
    /// square walks here and stands in it.
    /// </summary>
    public class CallNextStation : StationBase
    {
        [Header("Calling")]
        [Tooltip("The waiting room whose head is called. Its `chairs` are the chairs they can be sent to.")]
        public CustomerQueue queue;

        protected override void Awake()
        {
            base.Awake();
            mode = InteractionMode.Task;
        }

        public override bool IsOperational => queue != null && queue.CanDispatch;

        /// <summary>How many are waiting, e.g. "2" - empty when nobody is.</summary>
        public override string StatusValue =>
            queue == null || queue.WaitingCount == 0 ? string.Empty : queue.WaitingCount.ToString();

        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Serve;

        protected override bool CanPerformTask() => queue != null && queue.CanDispatch;

        protected override void CompleteTask()
        {
            if (queue != null) queue.DispatchFront();
        }
    }
}
