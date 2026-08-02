namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// Small display-formatting helpers shared across screens, kept in one place so "8h 05m" vs
    /// "+8h 05m" vs "8:05" formatting never drifts between screens.
    /// </summary>
    public static class Formatting
    {
        #region Methods

        /// <summary>
        /// Formats a duration as "<c>Xh YYm</c>", e.g. "8h 05m". The sign is dropped - callers that
        /// care about sign (e.g. a balance) prepend it themselves, see <see cref="Balance"/>.
        /// </summary>
        public static string Duration(TimeSpan duration)
        {
            var totalMinutes = (int)Math.Round(Math.Abs(duration.TotalMinutes));
            return $"{totalMinutes / 60}h {totalMinutes % 60:00}m";
        }

        /// <summary>
        /// Formats a balance as a signed duration, e.g. "+2h 15m" or "-0h 45m".
        /// </summary>
        public static string Balance(TimeSpan balance)
        {
            var sign = balance < TimeSpan.Zero ? "-" : "+";
            return $"{sign}{Duration(balance)}";
        }

        /// <summary>
        /// Formats a <see cref="TimeOnly"/> as "HH:mm", with the colon escaped so it is never
        /// swapped for the current culture's time separator.
        /// </summary>
        public static string Time(TimeOnly time) => time.ToString(@"HH\:mm");

        /// <summary>
        /// Formats a <see cref="DateTime"/>'s time-of-day as "HH:mm", with the colon escaped so it
        /// is never swapped for the current culture's time separator.
        /// </summary>
        public static string Time(DateTime time) => time.ToString(@"HH\:mm");

        #endregion
    }
}
