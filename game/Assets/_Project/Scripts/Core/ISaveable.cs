using UnityEngine;

namespace Tycoon.Core
{
    /// <summary>
    /// Implemented by anything with state worth keeping between sessions: buffers, machines,
    /// unlock progress, reputation.
    ///
    /// State travels as a JSON string produced by JsonUtility so each component owns its own
    /// serializable struct and the save file never needs a central schema. That is what makes
    /// adding a new station type a one-file change.
    /// </summary>
    public interface ISaveable
    {
        /// <summary>Stable identity across sessions. Defaults to the hierarchy path.</summary>
        string SaveKey { get; }

        string CaptureState();

        /// <summary>
        /// Called once on registration if saved state exists. Components that produce over
        /// time should catch up here using their own stored timestamp - that is how offline
        /// progress works for levels that are not currently loaded.
        /// </summary>
        void RestoreState(string json);
    }

    public static class SaveKeys
    {
        /// <summary>
        /// Hierarchy path plus component type, e.g. "Farm/Coop/Machine#ProducerMachine".
        /// Deterministic, readable in the save file, and needs no editor-assigned GUIDs.
        /// Renaming or reparenting an object resets that object's saved state - acceptable
        /// for a prototype and far less machinery than GUID stamping.
        /// </summary>
        public static string For(Component component)
        {
            var sb = new System.Text.StringBuilder();
            BuildPath(component.transform, sb);
            sb.Append('#').Append(component.GetType().Name);
            return sb.ToString();
        }

        private static void BuildPath(Transform t, System.Text.StringBuilder sb)
        {
            if (t.parent != null)
            {
                BuildPath(t.parent, sb);
                sb.Append('/');
            }
            sb.Append(t.name);
        }
    }
}
