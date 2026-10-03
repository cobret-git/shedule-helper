namespace Stint.Core
{
    /// <summary>
    /// The grid "Now" clock-in/clock-out times are snapped to, see <see cref="ClockRounder"/>.
    /// Every value divides an hour evenly, so the grid is the same in every hour of the day. The
    /// numeric value of each member is its length in minutes (0 for <see cref="Off"/>).
    /// </summary>
    public enum ClockRoundingInterval
    {
        /// <summary>No rounding - "Now" is stored exactly as it was.</summary>
        Off = 0,

        FiveMinutes = 5,

        TenMinutes = 10,

        FifteenMinutes = 15,

        TwentyMinutes = 20,

        ThirtyMinutes = 30,

        OneHour = 60
    }
}
