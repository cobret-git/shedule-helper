namespace Stint.Core
{
    /// <summary>
    /// The company's clock rounding rule: clocking in rounds <i>forward</i> to the next grid
    /// point, clocking out rounds <i>back</i> to the previous one (with a 5-minute grid, 07:02 in
    /// becomes 07:05 and 16:07 out becomes 16:05; a time already on the grid is left alone).
    /// </summary>
    /// <remarks>
    /// Seconds are dropped before rounding whenever rounding is on, so 07:00:40 counts as 07:00.
    /// With <see cref="ClockRoundingInterval.Off"/> the time is returned exactly as given.
    /// </remarks>
    public static class ClockRounder
    {
        #region Methods

        /// <summary>Rounds a clock-in time forward to the next grid point (or leaves it if already on one).</summary>
        public static DateTime RoundClockIn(DateTime time, ClockRoundingInterval interval)
        {
            var step = (int)interval;
            if (step <= 0)
            {
                return time;
            }

            var minuteOfDay = (int)time.TimeOfDay.TotalMinutes;
            var rounded = (minuteOfDay + step - 1) / step * step;

            // Rounding 23:58 forward would land on the next day's 00:00 - keep it on the same
            // calendar day (the attendance log is for this date), at its last minute instead.
            return time.Date.AddMinutes(Math.Min(rounded, 24 * 60 - 1));
        }

        /// <summary>Rounds a clock-out time back to the previous grid point (or leaves it if already on one).</summary>
        public static DateTime RoundClockOut(DateTime time, ClockRoundingInterval interval)
        {
            var step = (int)interval;
            if (step <= 0)
            {
                return time;
            }

            var minuteOfDay = (int)time.TimeOfDay.TotalMinutes;
            return time.Date.AddMinutes(minuteOfDay / step * step);
        }

        #endregion
    }
}
