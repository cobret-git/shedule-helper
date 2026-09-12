namespace Stint.Core
{
    /// <summary>
    /// Total tracked time for one project within one calendar month, with any lunch deduction
    /// (see <see cref="AppSettings.LunchStrategy"/>) already applied.
    /// </summary>
    public record ProjectTimeMonthlyEntry(int Year, int Month, string ProjectName, TimeSpan Duration);
}
