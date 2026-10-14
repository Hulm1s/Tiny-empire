using Tycoon.Config;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// "This is on sale now": announces one product to <see cref="ProductRegistry"/> for as long as
    /// the object is active, exactly as a <see cref="ProducerMachine"/> announces what it makes.
    ///
    /// The butcher's counter has no machine behind it - steaks and sausages are cut by hand - but
    /// its shoppers must still only ask for what the shop can actually serve. Putting one of these
    /// under the object a purchase reveals does that: shoppers start asking for steaks the moment
    /// the steak counter is bought, and for sausages only once the sausage line is. Demand scales
    /// with <see cref="capacity"/> the way it does with hens and cows (CustomerQueue.DemandMultiplier),
    /// which is how opening the sausage line makes the counter busier.
    ///
    /// It holds no state and is not saved: the purchase that reveals it is.
    /// </summary>
    public class ProductOffer : MonoBehaviour, IProducer
    {
        public ItemDefinition item;

        [Tooltip("How much demand this line adds, like the number of hens in a coop.")]
        [Min(1)] public int capacity = 1;

        public ItemDefinition Output => item;
        public int Capacity => capacity;

        private void OnEnable() => ProductRegistry.Register(this);
        private void OnDisable() => ProductRegistry.Unregister(this);
    }
}
