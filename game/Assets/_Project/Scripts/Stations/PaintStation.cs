using Tycoon.Core;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The PAINT square. Stand in it and the paint menu opens; what you choose is paid for there.
    ///
    /// It is the one square that opens a menu rather than doing its work in the world, because a
    /// colour and a pattern are not something that can be carried or stood on. The entry is
    /// still physical - a short ring to fill, so brushing across it on the way past does nothing
    /// - and the menu is the same kind of exception the pause panel is.
    ///
    /// The first confirmed job is the renovation step that lets the shop open, and costs
    /// <see cref="firstPrice"/>; every job after that costs <see cref="changePrice"/>. Both come
    /// off the builder through its Price() so a test override flattens them like every other
    /// purchase. Painting is cosmetic, so a player who cannot afford it loses nothing and is
    /// told so in the menu - nothing here can leave anyone stuck.
    /// </summary>
    public class PaintStation : StationBase
    {
        [Header("Paint")]
        public MarketDecor decor;

        [Tooltip("The renovation step: the first paint job.")]
        public double firstPrice = 1000d;

        [Tooltip("Every job after the first.")]
        public double changePrice = 200d;

        [Header("Presentation")]
        public Tycoon.UI.SquareIcon icon = Tycoon.UI.SquareIcon.Paint;

        // Opening the menu leaves the player standing in the square, and the ring would simply
        // fill again and reopen it. It re-arms once they step out.
        private bool _armed = true;

        public bool IsPainted => decor != null && decor.HasPainted;

        /// <summary>What a job costs right now.</summary>
        public double CurrentPrice => IsPainted ? changePrice : firstPrice;

        public override string ActionLabel => label;
        public override string StatusValue => MoneyFormat.Short(CurrentPrice);
        public override Tycoon.UI.SquareIcon Icon => icon;
        public override bool IsOperational => decor != null;

        protected override void Awake()
        {
            base.Awake();
            // A timed entry, and the player's alone: workers have no taste and no wallet.
            mode = InteractionMode.Task;
            workerCompatible = false;
        }

        protected override bool CanPerformTask() =>
            _armed && decor != null && Tycoon.UI.PaintMenu.Instance != null;

        protected override void CompleteTask()
        {
            var menu = Tycoon.UI.PaintMenu.Instance;
            if (menu == null || decor == null) return;

            _armed = false;
            menu.Open(this);
        }

        protected override void OnPlayerExit()
        {
            if (!IsOccupied) _armed = true;
        }
    }
}
