using Tycoon.Customers;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The hair a served customer leaves on the floor round a salon chair, and the thing that makes the chair
    /// wait for the broom.
    ///
    /// Shaped like <see cref="ClearablePile"/>: the heap is an <see cref="ItemBuffer"/> of hair bags with a
    /// collect square on it (the SWEEP square - the player, or the cleaner, carries the bags to the bin), and the
    /// pictures of the heap shrink as the buffer empties. The difference is that a pile of rubbish is gone for
    /// good, while hair comes back with every haircut: when the <see cref="ServiceChair"/> reports a finished
    /// service (<see cref="ServiceChair.Released"/> with served = true) the buffer is filled again.
    ///
    /// The chair itself is kept out of use by <see cref="dirty"/>: an empty marker object that is active exactly
    /// while there is hair, which is the chair's <see cref="ServiceChair.dirtyWhen"/>. So nobody is sent to a
    /// chair under hair, sweeping is free (it never needs money), and a walk-out that was never served leaves
    /// nothing. The one thing that is saved is the buffer's own count, so a reload mid-cycle brings the heap back.
    ///
    /// The buffer object, the heap pictures, the square and the marker all stay alive through the whole game and
    /// are only switched, never destroyed, so the saved buffer is always there to be restored.
    /// </summary>
    [RequireComponent(typeof(ItemBuffer))]
    public class HairMess : MonoBehaviour
    {
        [Tooltip("The chair whose served customers leave this hair behind.")]
        public ServiceChair chair;

        [Tooltip("Hair bags left by one finished haircut.")]
        [Min(1)] public int bagsPerService = 3;

        [Tooltip("The heap's pictures, small to large: shown from the first as the buffer empties.")]
        public GameObject[] heap;

        [Tooltip("The SWEEP square. On only while there is hair.")]
        public GameObject square;

        [Tooltip("Active exactly while there is hair: the chair's dirtyWhen.")]
        public GameObject dirty;

        private ItemBuffer _buffer;

        private void Awake() => _buffer = GetComponent<ItemBuffer>();

        private void OnEnable()
        {
            if (_buffer == null) _buffer = GetComponent<ItemBuffer>();
            _buffer.Changed += Refresh;
            if (chair != null) chair.Released += OnReleased;
        }

        private void OnDisable()
        {
            if (_buffer != null) _buffer.Changed -= Refresh;
            if (chair != null) chair.Released -= OnReleased;
        }

        // The buffer may be restored after this enables: look again once everything has had its turn.
        private void Start() => Refresh();

        private void OnReleased(ServiceChair released, CustomerAgent customer, bool served)
        {
            if (served) _buffer.Add(bagsPerService);
        }

        private void Refresh()
        {
            if (_buffer == null) return;

            bool any = !_buffer.IsEmpty;
            int shown = heap != null ? Mathf.CeilToInt(heap.Length * _buffer.Fill) : 0;
            if (heap != null)
                for (int i = 0; i < heap.Length; i++)
                    if (heap[i] != null) heap[i].SetActive(i < shown);

            if (square != null && square.activeSelf != any)
            {
                // Switched off with the sweeper still in it, the square would never hear them leave and would
                // keep sweeping the next heap into their arms from anywhere (see StationBase.ClearOccupants).
                if (!any && square.TryGetComponent(out StationBase station)) station.ClearOccupants();
                square.SetActive(any);
            }
            if (dirty != null && dirty.activeSelf != any) dirty.SetActive(any);
        }
    }
}
