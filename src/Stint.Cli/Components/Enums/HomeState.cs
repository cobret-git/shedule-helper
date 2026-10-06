namespace Stint.Cli.Components
{
    /// <summary>
    /// Which of Home's renders is current, driven by today's <see cref="Core.AttendanceLog"/>.
    /// </summary>
    public enum HomeState
    {
        /// <summary>No attendance log exists for today yet - Home opens the clock-in picker dialog.</summary>
        NotClockedIn,

        /// <summary>Clocked in and the day isn't over - showing the live shift/project tree.</summary>
        ClockedIn,

        /// <summary>Clocked in and away (part-day absence open), and the "back at" time picker is open on top of the live shift.</summary>
        Returning,

        /// <summary>Clocked out for the day - showing the final tally/project tree. Final: there is no second clock-in the same day.</summary>
        ClockedOut
    }
}
