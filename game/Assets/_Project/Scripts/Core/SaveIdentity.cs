using UnityEngine;

namespace Tycoon.Core
{
    /// <summary>
    /// A stable, human-readable identity for anything that saves state, e.g. "farm.coopA.feed".
    ///
    /// Save keys used to be built from the hierarchy path, which meant renaming or reparenting
    /// an object silently wiped its saved progress. That is survivable on a farm with a dozen
    /// objects and a save-corruption generator once there are supermarkets, malls, renovation
    /// state and condition to remember.
    ///
    /// The id is assigned by the level builder rather than generated, so it is identical every
    /// time the scene is regenerated - a random GUID would reset everyone's progress on every
    /// rebuild.
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveIdentity : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Stable logical id. Never change this once players have progress against it.")]
        private string _id;

        public string Id => _id;
        public bool HasId => !string.IsNullOrEmpty(_id);

        /// <summary>Called by the level builder. Not intended for runtime use.</summary>
        public void Assign(string id) => _id = id;
    }
}
