namespace Stint.Core
{
    /// <summary>
    /// Total tracked time across all projects within one calendar month, with any lunch deduction
    /// (see <see cref="AppSettings.LunchStrategy"/>) already applied.
    /// </summary>
    public record ProjectTimeMonthlyTotalEntry(int Year, int Month, TimeSpan Duration);
}
