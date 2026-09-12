namespace Stint.Cli.Components
{
    /// <summary>
    /// Which of Home's three renders is current, driven by today's <see cref="Core.AttendanceLog"/>.
    /// </summary>
    public enum HomeState
    {
        /// <summary>No attendance log exists for today yet - showing the clock-in picker.</summary>
        NotClockedIn,

        /// <summary>Clocked in and the day isn't over - showing the live shift/project tree.</summary>
        ClockedIn,

        /// <summary>Clocked out for the day - showing the final tally/project tree.</summary>
        ClockedOut
    }
}
