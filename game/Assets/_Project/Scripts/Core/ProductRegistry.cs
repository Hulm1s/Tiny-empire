using System.Collections.Generic;
using Tycoon.Config;

namespace Tycoon.Core
{
    /// <summary>
    /// Tracks which goods the farm can currently make.
    ///
    /// Producers add their output while they are active, and locked buildings are inactive, so
    /// a shop only ever asks for things the player has some way of supplying. Without this, a
    /// counter that lists milk would start sending milk customers before the player owns a
    /// single cow - orders that can only ever time out, wrecking reputation for no reason.
    ///
    /// It also means new products need no wiring: unlock a cow shed and milk starts appearing
    /// in orders by itself.
    /// </summary>
    public static class ProductRegistry
    {
        private static readonly Dictionary<ItemDefinition, int> Producers =
            new Dictionary<ItemDefinition, int>();

        public static void Register(ItemDefinition item)
        {
            if (item == null) return;
            Producers.TryGetValue(item, out int count);
            Producers[item] = count + 1;
        }

        public static void Unregister(ItemDefinition item)
        {
            if (item == null) return;
            if (!Producers.TryGetValue(item, out int count)) return;

            if (count <= 1) Producers.Remove(item);
            else Producers[item] = count - 1;
        }

        public static bool CanProduce(ItemDefinition item) =>
            item != null && Producers.ContainsKey(item);
    }
}
