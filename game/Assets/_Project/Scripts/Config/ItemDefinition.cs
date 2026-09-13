using UnityEngine;

namespace Tycoon.Config
{
    /// <summary>
    /// One kind of goods: corn, eggs, bread. Create via Assets > Create > Tycoon > Item.
    ///
    /// Everything about an item that the designer might want to change lives here, so adding
    /// a new product to the game never requires touching a script.
    /// </summary>
    [CreateAssetMenu(menuName = "Tycoon/Item", fileName = "Item_")]
    public class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id used in save files. Never change this once players have progress.")]
        public string id = "corn";

        public string displayName = "Corn";

        [Header("Appearance")]
        [Tooltip("Colour used for the greybox cube that represents this item while carried.")]
        public Color color = new Color(0.95f, 0.78f, 0.2f);

        [Tooltip("Optional real mesh. When empty the item renders as a tinted cube.")]
        public GameObject visualPrefab;

        [Tooltip("Material for the greybox cube. This must be a real asset reference: a cube " +
                 "created at runtime gets Unity's built-in default material, which does not " +
                 "exist in a URP build and renders as solid magenta. Referencing the material " +
                 "here is also what pulls its shader into the build.")]
        public Material carryMaterial;

        [Tooltip("Shown in customer order bubbles. Drop in an egg sprite here and every " +
                 "bubble asking for eggs updates. When empty the bubble falls back to a " +
                 "coloured dot using the colour above.")]
        public Sprite icon;

        [Tooltip("Height of one unit in the carried stack. Keeps tall and flat goods looking right.")]
        public float stackHeight = 0.28f;

        [Header("Economy")]
        [Tooltip("Money paid per unit at a sell counter, before any level or reputation multiplier.")]
        public double basePrice = 3d;

        [Header("Upkeep")]
        [Tooltip("Perishable goods rot in stockpiles, which stops the player hoarding output " +
                 "and is one of the pressures that keeps a finished business demanding attention.")]
        public bool perishable;

        [Tooltip("Seconds a single unit survives in a stockpile before it is lost.")]
        public float spoilSeconds = 120f;
    }
}
