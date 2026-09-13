using System;

namespace Tycoon.Core
{
    /// <summary>
    /// Single authoritative time source for the whole game.
    ///
    /// Everything that produces, decays, spoils or pays wages asks this class for the time
    /// rather than using Time.time, because Time.time resets to zero on every page load and
    /// we need progression to survive the player closing Safari for two days.
    /// </summary>
    public static class GameClock
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Wall-clock seconds since the Unix epoch. Survives app restarts.</summary>
        public static double NowUnix => (DateTime.UtcNow - Epoch).TotalSeconds;

        /// <summary>
        /// Offline time is capped so that leaving the game for a month does not hand the
        /// player an unearned empire (and does not destroy one through decay either).
        /// </summary>
        public const double MaxOfflineSeconds = 8 * 60 * 60;

        public static double ClampOffline(double seconds)
        {
            if (seconds < 0d) return 0d;
            return seconds > MaxOfflineSeconds ? MaxOfflineSeconds : seconds;
        }
    }
}
