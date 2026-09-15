using System.Collections.Generic;
using Tycoon.Config;
using UnityEngine;

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
    /// <summary>
    /// Anything that turns time into goods. Implemented by ProducerMachine.
    /// </summary>
    public interface IProducer
    {
        /// <summary>What comes out. Null while the machine is not wired up.</summary>
        ItemDefinition Output { get; }

        /// <summary>How much of it this building can make at once - hens, cows, ovens.</summary>
        int Capacity { get; }
    }

    public static class ProductRegistry
    {
        private static readonly List<IProducer> Producers = new List<IProducer>();

        public static void Register(IProducer producer)
        {
            if (producer != null && !Producers.Contains(producer)) Producers.Add(producer);
        }

        public static void Unregister(IProducer producer)
        {
            if (producer != null) Producers.Remove(producer);
        }

        public static bool CanProduce(ItemDefinition item)
        {
            if (item == null) return false;

            for (int i = 0; i < Producers.Count; i++)
                if (Producers[i] != null && Producers[i].Output == item) return true;

            return false;
        }

        /// <summary>
        /// Everything currently able to make this item, added up.
        ///
        /// This is what demand is pegged to: a farm with two coops of three hens each
        /// should have shoppers arriving faster than one with a single hen, without the
        /// spawn rate being retuned by hand every time the farm grows.
        /// </summary>
        public static int Capacity(ItemDefinition item)
        {
            if (item == null) return 0;

            int total = 0;
            for (int i = 0; i < Producers.Count; i++)
                if (Producers[i] != null && Producers[i].Output == item)
                    total += Mathf.Max(0, Producers[i].Capacity);

            return total;
        }

        /// <summary>How many separate buildings make this item.</summary>
        public static int ProducerCount(ItemDefinition item)
        {
            if (item == null) return 0;

            int count = 0;
            for (int i = 0; i < Producers.Count; i++)
                if (Producers[i] != null && Producers[i].Output == item) count++;

            return count;
        }
    }
}
