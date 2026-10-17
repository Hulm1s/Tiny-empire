using Tycoon.Stations;
using UnityEngine;

namespace Tycoon.UI
{
    /// <summary>
    /// Marks the ground a business's stock panel (<see cref="StockPanel"/>) is shown over, and which stocks it
    /// reports. The florist has one: its shop and its meadow. Pure data: the panel reads it, nothing here runs.
    /// A stock on a switched-off object (a flower not yet bought) is left out of the panel by the panel itself.
    /// </summary>
    public class StockInfoZone : MonoBehaviour
    {
        [Tooltip("The stocks to report, in the order they are shown.")]
        public ItemBuffer[] stocks;

        [Tooltip("The ground the panel shows over, in this object's own grid: x and z from (min) to (max).")]
        public Vector2 min = new Vector2(-1f, -1f);
        public Vector2 max = new Vector2(1f, 1f);

        /// <summary>True when the world point is inside the zone and the zone is switched on (its town is bought).</summary>
        public bool Contains(Vector3 world)
        {
            if (!gameObject.activeInHierarchy) return false;
            Vector3 p = transform.InverseTransformPoint(world);
            return p.x >= min.x && p.x <= max.x && p.z >= min.y && p.z <= max.y;
        }
    }
}
