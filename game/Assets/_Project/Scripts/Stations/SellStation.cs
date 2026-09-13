using System;
using Tycoon.Config;
using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// Turns carried goods into money, one unit at a time.
    ///
    /// The price multiplier is where the level's economy and the business's reputation are
    /// applied, so a neglected shop literally earns less per item without any special casing.
    /// </summary>
    public class SellStation : StationBase
    {
        [Header("Sale")]
        [Tooltip("Leave empty to buy whatever the player is carrying.")]
        public ItemDefinition accepted;

        [Tooltip("Multiplies the item's base price. Reputation and level bonuses feed in here.")]
        public float priceMultiplier = 1f;

        /// <summary>Fired with the money earned per unit sold, for popup text and audio.</summary>
        public event Action<double> Sold;

        public double LastSalePrice { get; private set; }

        public override string StatusText
        {
            get
            {
                var item = accepted;
                if (item == null) return label;
                return $"{label} {MoneyFormat.Short(item.basePrice * priceMultiplier)}";
            }
        }

        protected override bool TickWithPlayer()
        {
            if (Carry == null || Carry.IsEmpty) return false;

            ItemDefinition item = Carry.Peek();
            if (item == null) return false;
            if (accepted != null && item != accepted) return false;

            if (!Carry.TryRemove(item)) return false;

            double price = item.basePrice * priceMultiplier;
            LastSalePrice = price;
            GameRoot.Money?.Add(price);
            Sold?.Invoke(price);
            return true;
        }
    }
}
