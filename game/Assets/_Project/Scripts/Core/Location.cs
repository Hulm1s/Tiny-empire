using UnityEngine;

namespace Tycoon.Core
{
    /// <summary>
    /// Marks the root of one place the player can be: the farm, the supermarket, and whatever
    /// business comes after them.
    ///
    /// The hierarchy the project is growing towards is Location, then Business, then Buildings,
    /// then Stations. Today every location is a root object in the one scene, built by its own
    /// editor builder, and this component is the whole of "what is a location": an id, a name,
    /// and the spot the player is put when they arrive from somewhere else.
    ///
    /// It deliberately does nothing at runtime beyond holding those. The layout audit walks
    /// every Location in the scene, so a new business is measured the moment it has one.
    /// </summary>
    public class Location : MonoBehaviour
    {
        [Tooltip("Stable short id, e.g. 'farm' or 'market'. Matches the prefix of the save ids " +
                 "its objects use.")]
        public string locationId = "farm";

        public string displayName = "Farm";

        [Tooltip("Where the player appears when they travel here. Keep it clear of every " +
                 "interaction square, especially the one that brought them: standing inside a " +
                 "travel square on arrival would send them straight back.")]
        public Transform spawnPoint;

        public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    }
}
