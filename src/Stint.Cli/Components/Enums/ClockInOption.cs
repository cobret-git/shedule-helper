namespace Stint.Cli.Components
{
    /// <summary>
    /// One selectable row in Home's not-clocked-in clock-in time picker.
    /// </summary>
    public enum ClockInOption
    {
        /// <summary>Clock in at the current time.</summary>
        Now,

        /// <summary><see cref="Core.AppSettings.DefaultClockInTime"/>.</summary>
        Default,

        /// <summary>A time typed in by the user, HH:MM.</summary>
        Custom
    }
}
