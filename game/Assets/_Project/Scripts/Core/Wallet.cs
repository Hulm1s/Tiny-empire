using System;
using UnityEngine;

namespace Tycoon.Core
{
    /// <summary>
    /// The player's money. Deliberately the only place currency is mutated so that every
    /// income and cost in the game is observable from one event.
    /// </summary>
    [Serializable]
    public class Wallet
    {
        [SerializeField] private double _balance;

        /// <summary>Fired with (newBalance, delta) whenever money changes.</summary>
        public event Action<double, double> Changed;

        public double Balance => _balance;

        public void Add(double amount)
        {
            if (amount == 0d) return;
            _balance += amount;
            if (_balance < 0d) _balance = 0d;
            Changed?.Invoke(_balance, amount);
        }

        public bool CanAfford(double amount) => _balance >= amount;

        /// <summary>Spends up to <paramref name="requested"/> and returns what was actually spent.</summary>
        public double TrySpend(double requested)
        {
            if (requested <= 0d) return 0d;
            double spent = Math.Min(requested, _balance);
            if (spent <= 0d) return 0d;
            _balance -= spent;
            Changed?.Invoke(_balance, -spent);
            return spent;
        }

        public void SetSilently(double value)
        {
            _balance = value < 0d ? 0d : value;
            Changed?.Invoke(_balance, 0d);
        }
    }

    public static class MoneyFormat
    {
        /// <summary>Compact currency string: 950, 1.2K, 3.4M. Keeps the HUD narrow on a phone.</summary>
        public static string Short(double value)
        {
            double v = Math.Floor(value);
            if (v < 1_000d) return "$" + v.ToString("0");
            if (v < 1_000_000d) return "$" + (v / 1_000d).ToString("0.#") + "K";
            if (v < 1_000_000_000d) return "$" + (v / 1_000_000d).ToString("0.#") + "M";
            return "$" + (v / 1_000_000_000d).ToString("0.#") + "B";
        }
    }
}
