using Tycoon.Audio;
using Tycoon.Config;
using Tycoon.Core;
using Tycoon.Player;
using UnityEngine;

namespace Tycoon.Stations
{
    /// <summary>
    /// The premium coffee, made by hand at the premium machine: the square the upgrade opens where the UPGRADE square stood.
    ///
    /// Standing here with a cup's worth of milk in the counter fridge (<see cref="milk"/>, the machine's hopper) and room in
    /// the arms makes ONE premium coffee into the arms of whoever stands here. The player does it in the latte-art
    /// minigame (<see cref="Tycoon.UI.LatteMinigame"/>: pour, trace the pattern, stars -> a tip into the cafe's cash pile);
    /// a hired barista (a worker whose route starts here) does it by timer, at the base price and with no tip. Either way
    /// the cup then goes to a table's SERVE square like any carried good, and the customer pays the till's price there.
    ///
    /// Nothing here is saved: it holds no state between visits (the milk is the fridge's own saved buffer).
    /// </summary>
    public class LatteStation : StationBase
    {
        [Header("Latte")]
        [Tooltip("The counter fridge, which is also the coffee machine's milk hopper. One milk per premium coffee.")]
        public ItemBuffer milk;

        [Tooltip("What is made: the premium coffee.")]
        public ItemDefinition premiumItem;

        [Tooltip("Where the tip goes (the cafe's cash pile). Empty pays the account.")]
        public CashPile pile;

        [Tooltip("Seconds a hired barista takes per cup (the player's time is the minigame's).")]
        [Min(0.5f)] public float makeSeconds = 6f;

        [Tooltip("Seconds of standing here before the minigame opens, so walking across the square does nothing.")]
        [Min(0f)] public float openAfter = 0.6f;

        [Tooltip("Tip as a share of the coffee's price, for one, two and three stars.")]
        public float[] tipShare = { 0.2f, 0.45f, 0.75f };

        private float _work, _dwell;
        private bool _armed = true;
        private CarryStack _making;

        private void Start() => Tycoon.UI.LatteMinigame.Instance?.Prewarm();

        /// <summary>Milk in the fridge, and a recipe: a cup can be made now.</summary>
        public bool CanMake => milk != null && !milk.IsEmpty && premiumItem != null;

        public override bool IsOperational => CanMake;

        public override Tycoon.UI.SquareIcon Icon => Tycoon.UI.SquareIcon.Serve;
        public override ItemDefinition IconItem => premiumItem;
        public override string StatusValue => milk == null ? string.Empty : "milk " + milk.Count;
        protected override float TransferProgress => makeSeconds <= 0f ? 0f : Mathf.Clamp01(_work / makeSeconds);

        /// <summary>What the cup sells for at a table (the base price), shown in the minigame.</summary>
        public double Quote => premiumItem != null ? premiumItem.basePrice : 0d;

        public double Tip(int stars) =>
            tipShare == null || tipShare.Length == 0 || stars <= 0 ? 0d
                : Quote * tipShare[Mathf.Clamp(stars, 1, tipShare.Length) - 1];

        protected override bool TickWithPlayer()
        {
            if (!CanMake || Carry == null || !Carry.CanAccept(premiumItem)) { _work = _dwell = 0f; return false; }

            var game = Tycoon.UI.LatteMinigame.Instance;
            bool player = Carry.CompareTag("Player");
            if (player && game != null)
            {
                if (!_armed || game.IsOpen) return false;
                _dwell += tickInterval;
                if (_dwell < openAfter) return false;
                _dwell = 0f;
                _making = Carry;
                game.Open(this, Quote);
                return false;
            }

            // A hired barista: by the clock.
            _work += tickInterval;
            if (_work < makeSeconds) return true;
            _work = 0f;
            return Make(Carry, 0);
        }

        /// <summary>Takes the milk and puts the cup in the arms. Pays the tip for the stars. False if it could not be made.</summary>
        private bool Make(CarryStack into, int stars)
        {
            if (into == null || !CanMake || !into.CanAccept(premiumItem)) return false;
            if (milk.Remove(1) == 0) return false;
            if (!into.TryAdd(premiumItem)) { milk.Add(1); return false; }

            double tip = Tip(stars);
            if (tip > 0d)
            {
                if (pile != null) pile.Deposit(tip); else GameRoot.Money?.Add(tip);
                Tycoon.UI.WorldFeedback.Show(transform.position + Vector3.up * 2.6f,
                    "TIP +" + MoneyFormat.Short(tip), new Color(1f, 0.86f, 0.35f));
            }
            SoundFx.PlayAt(Sfx.Pop, transform.position);
            return true;
        }

        /// <summary>The minigame finished with this many stars (0 to 3): the cup is made.</summary>
        public bool Finish(int stars, out double tip)
        {
            tip = Tip(stars);
            var into = _making;
            _making = null;
            return Make(into, stars);
        }

        public void Abandon()
        {
            _making = null;
            _armed = false;
            _dwell = 0f;
        }

        protected override void OnPlayerExit()
        {
            base.OnPlayerExit();
            if (HasPlayer) return;
            _armed = true;
            _dwell = _work = 0f;
        }
    }
}
