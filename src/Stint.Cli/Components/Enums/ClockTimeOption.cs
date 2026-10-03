namespace Stint.Cli.Components
{
    /// <summary>
    /// One selectable row in Home's clock-in or clock-out time picker.
    /// </summary>
    public enum ClockTimeOption
    {
        /// <summary>Clock in/out at the current time, rounded per <see cref="Core.AppSettings.ClockRounding"/>.</summary>
        Now,

        /// <summary><see cref="Core.AppSettings.DefaultClockInTime"/>/<see cref="Core.AppSettings.DefaultClockOutTime"/>.</summary>
        Default,

        /// <summary>A time typed in by the user, HH:MM.</summary>
        Custom
    }
}
