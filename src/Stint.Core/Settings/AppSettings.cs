using Stint.Core;

public class AppSettings
{
    #region Properties

    /// <summary>
    /// Daily target shift duration.
    /// </summary>
    public TimeSpan TargetShiftDuration { get; set; } = TimeSpan.FromHours(7.5);

    /// <summary>
    /// The default daily start time.
    /// </summary>
    public TimeOnly DefaultClockInTime { get; set; } = new(8, 30);

    /// <summary>
    /// The default daily end time.
    /// </summary>
    public TimeOnly DefaultClockOutTime { get; set; } = new(17, 0);

    /// <summary>
    /// Defines the user's lunch break strategy.
    /// </summary>
    public LunchStrategy LunchStrategy { get; set; } = LunchStrategy.FixedWindow;

    /// <summary>
    /// The daily start time of the lunch window.
    /// </summary>
    public TimeOnly LunchStartTime { get; set; } = new(11, 0);

    /// <summary>
    /// The daily end time of the lunch window.
    /// </summary>
    public TimeOnly LunchEndTime { get; set; } = new(11, 30);

    /// <summary>
    /// Total lunch break duration.
    /// </summary>
    public TimeSpan LunchDuration { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How much of the start-of-day clock-in/clock-out ritual the app resolves by itself.
    /// Defaults to <see cref="DayStartAutomation.Off"/> so an existing database keeps
    /// behaving exactly as before until the user opts in.
    /// </summary>
    public DayStartAutomation DayStartAutomation { get; set; } = DayStartAutomation.Off;

    /// <summary>
    /// Whether clocking in should pick the previously tracked project/task back up, so work
    /// that spans several days doesn't have to be re-selected each morning. Applies to manual
    /// clock-ins as well as those made by <see cref="DayStartAutomation"/>.
    /// </summary>
    public bool ResumeTrackingOnClockIn { get; set; } = true;

    /// <summary>
    /// Whether the app registers itself to launch when the system starts.
    /// </summary>
    public bool LaunchAtStartup { get; set; } = false;

    #endregion
}