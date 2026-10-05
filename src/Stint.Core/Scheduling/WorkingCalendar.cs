namespace Stint.Core
{
    /// <summary>
    /// Which calendar days count as working days - the basis for turning a planned date range into
    /// "5 working days - 37h 30m".
    /// </summary>
    /// <remarks>
    /// Monday to Friday for now: there is no work-week setting yet, and public holidays inside a
    /// range aren't skipped. Both belong here when they arrive, so every caller picks them up.
    /// </remarks>
    public static class WorkingCalendar
    {
        #region Methods

        /// <summary>Whether <paramref name="date"/> is a working day (Monday to Friday).</summary>
        public static bool IsWorkingDay(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

        /// <summary>
        /// The working days in <paramref name="start"/>..<paramref name="end"/> (inclusive), in
        /// order. Empty when <paramref name="end"/> is before <paramref name="start"/>.
        /// </summary>
        public static IEnumerable<DateOnly> GetWorkingDays(DateOnly start, DateOnly end)
        {
            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (IsWorkingDay(date))
                {
                    yield return date;
                }
            }
        }

        /// <summary>How many working days fall in <paramref name="start"/>..<paramref name="end"/> (inclusive).</summary>
        public static int CountWorkingDays(DateOnly start, DateOnly end) => GetWorkingDays(start, end).Count();

        #endregion
    }
}
