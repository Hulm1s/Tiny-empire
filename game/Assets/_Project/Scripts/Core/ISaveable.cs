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
        /// Stable id plus component type, e.g. "farm.coopA.machine#ProducerMachine".
        ///
        /// Prefers a <see cref="SaveIdentity"/> assigned by the level builder, which survives
        /// renaming, reparenting and scene regeneration. Falls back to the hierarchy path when
        /// no identity is present, so an object that has not been given one still saves - it
        /// just carries the old fragility, and says so once in the log.
        /// </summary>
        public static string For(Component component)
        {
            // Same GameObject only, deliberately. Searching parents would give a coop's input
            // and output buffers the same key, silently merging two different piles of goods.
            var identity = component.GetComponent<SaveIdentity>();
            if (identity != null && identity.HasId)
                return identity.Id + "#" + component.GetType().Name;

            var sb = new System.Text.StringBuilder();
            BuildPath(component.transform, sb);
            sb.Append('#').Append(component.GetType().Name);

            Debug.LogWarning(
                $"[SaveKeys] '{sb}' has no SaveIdentity and is keyed by hierarchy path. " +
                "Renaming or moving it will lose its saved state.");

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
