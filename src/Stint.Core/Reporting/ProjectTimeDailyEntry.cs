namespace Stint.Core
{
    /// <summary>
    /// Total tracked time for one project on one calendar day, with any lunch deduction
    /// (see <see cref="AppSettings.LunchStrategy"/>) already applied.
    /// </summary>
    public record ProjectTimeDailyEntry(DateOnly Date, string ProjectName, TimeSpan Duration);
}
