using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// A heap of rubbish that is cleared by carrying it away.
    ///
    /// The heap is an <see cref="ItemBuffer"/> of trash with a collect square beside it, which is
    /// all the game needs to move goods: the player stands in the square, bags go into their
    /// <see cref="Tycoon.Player.CarryStack"/>, and they walk them to a bin. This component only
    /// does the part a plain buffer cannot - it makes the pile LOOK like what is left in it, and
    /// takes the square away once the pile is gone so the ground is clear for the next step.
    ///
    /// It sits on the buffer's own object, after the buffer, so that when a saved game restores
    /// the buffer first this reads the restored count. It keeps no state of its own: a half
    /// cleared pile is just a buffer with fewer bags in it, which the buffer already saves.
    /// </summary>
    [RequireComponent(typeof(ItemBuffer))]
    public class ClearablePile : MonoBehaviour
    {
        [Tooltip("The litter that makes up the heap. Switched off from the last one backwards " +
                 "as the bags are taken, so a pile shrinks as it empties.")]
        public GameObject[] litter;

        [Tooltip("The collect square. Switched off for good once the pile is empty.")]
        public GameObject square;

        private ItemBuffer _buffer;

        public bool IsCleared => _buffer != null && _buffer.IsEmpty;

        private void Awake() => _buffer = GetComponent<ItemBuffer>();

        private void OnEnable()
        {
            if (_buffer == null) _buffer = GetComponent<ItemBuffer>();
            // Subscribe only. Looking now would be wrong: the buffer sets its starting count in
            // its own Awake, which may not have run yet, and an empty reading here would switch
            // the square off for good. Start looks once everything has woken.
            _buffer.Changed += Refresh;
        }

        private void OnDisable()
        {
            if (_buffer != null) _buffer.Changed -= Refresh;
        }

        // The buffer may be restored after this component enables (component order is not
        // guaranteed across a hierarchy), so look again once everything has had its turn.
        private void Start() => Refresh();

        private void Refresh()
        {
            if (_buffer == null || litter == null) return;

            // Round up: one bag left still leaves a visible heap, and the heap is only gone
            // when the last bag is.
            int shown = Mathf.CeilToInt(litter.Length * _buffer.Fill);

            for (int i = 0; i < litter.Length; i++)
                if (litter[i] != null) litter[i].SetActive(i < shown);

            if (_buffer.IsEmpty && square != null && square.activeSelf) square.SetActive(false);
        }
    }
}
