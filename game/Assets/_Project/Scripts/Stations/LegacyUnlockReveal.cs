using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Keeps a purchase that no longer exists honoured: it reads the save entry of a retired
    /// <see cref="UnlockStation"/> and, if that gate had been bought, switches its content on.
    ///
    /// Used once so far: the Dairy Till used to be its own gate ("farm.unlock.counterB"); it now
    /// comes with the Dairy. A save that had already bought the till but not the Dairy must keep
    /// its till, and nothing else would reveal it. The entry stays in the stored blob untouched
    /// (this writes nothing back, and the save keeps every entry it does not own), so it keeps
    /// working on every later load. In a new game the entry does not exist and this does nothing.
    ///
    /// Put it on an object that is always active; the targets must be saved INACTIVE.
    /// </summary>
    public class LegacyUnlockReveal : MonoBehaviour, ISaveable
    {
        [Tooltip("The full save key of the retired gate, e.g. \"farm.unlock.counterB#UnlockStation\".")]
        public string legacyKey;

        [Tooltip("Activated if the retired gate had been bought.")]
        public GameObject[] reveal;

        [System.Serializable]
        private struct State
        {
            public double paid;
            public bool unlocked;
        }

        public string SaveKey => legacyKey;

        private void OnEnable() => SaveSystem.Register(this);
        private void OnDisable() => SaveSystem.Unregister(this);

        // Empty: nothing is written, so the old entry is left exactly as it was.
        public string CaptureState() => string.Empty;

        public void RestoreState(string json)
        {
            if (!JsonUtility.FromJson<State>(json).unlocked) return;
            foreach (var go in reveal)
                if (go != null) go.SetActive(true);
        }
    }
}
