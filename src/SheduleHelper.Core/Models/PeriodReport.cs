namespace SheduleHelper.Core.Models
{
    /// <summary>
    /// The granularity a <see cref="Services.IReportingService"/> report is viewed at. Each level
    /// uses a different bucket size (day/day/week/month) so the number of columns in any chart
    /// stays small regardless of zoom - see <see cref="PeriodReport.Buckets"/>.
    /// </summary>
    public enum ReportZoom
    {
        Week,
        Month,
        Quarter,
        Year,
    }

    /// <summary>
    /// One column of a <see cref="PeriodReport"/> - a single day (Week/Month zoom), week (Quarter
    /// zoom), or month (Year zoom).
    /// </summary>
    /// <param name="Label">A short column heading, e.g. "Mon", "15", "W31", "Aug".</param>
    /// <param name="Date">The bucket's first instant - a calendar day, the Monday of a week, or the first of a month.</param>
    /// <param name="Worked">Net worked time summed across every attendance log whose clock-in falls in this bucket.</param>
    /// <param name="Target">The target time for this bucket - the user's daily target multiplied by how many of its days were actually logged.</param>
    /// <param name="HasData">Whether at least one attendance log falls in this bucket.</param>
    /// <param name="IsWeekend">Whether <see cref="Date"/> is a Saturday or Sunday - only meaningful for day buckets.</param>
    public sealed record ReportBucket(string Label, DateTime Date, TimeSpan Worked, TimeSpan Target, bool HasData, bool IsWeekend);

    /// <summary>
    /// One row of a <see cref="PeriodReport"/>'s project/task breakdown.
    /// </summary>
    public sealed record ProjectBreakdownEntry(string Name, TimeSpan Time);

    /// <summary>
    /// A fully-resolved report for one period at one <see cref="ReportZoom"/> level, as produced by
    /// <see cref="Services.IReportingService"/>.
    /// </summary>
    /// <param name="PeriodLabel">A human-readable label for the whole period, e.g. "August 2026", "Q3 2026".</param>
    /// <param name="RangeStart">The first instant covered by <see cref="Buckets"/>.</param>
    /// <param name="RangeEnd">The last instant covered by <see cref="Buckets"/>.</param>
    /// <param name="TotalWorked">The sum of every bucket's <see cref="ReportBucket.Worked"/>.</param>
    /// <param name="TotalTarget">The sum of every bucket's <see cref="ReportBucket.Target"/>.</param>
    /// <param name="LoggedBucketCount">How many buckets have <see cref="ReportBucket.HasData"/> set.</param>
    /// <param name="BucketUnitLabel">The singular noun for one bucket - "day", "week", or "month" - for captions like "9 of 13 weeks logged".</param>
    /// <param name="Buckets">The period's columns, in chronological order.</param>
    /// <param name="ProjectBreakdown">Time spent per project within the period, descending.</param>
    /// <param name="TaskBreakdown">Time spent per task within the period, descending.</param>
    public sealed record PeriodReport(
        string PeriodLabel,
        DateTime RangeStart,
        DateTime RangeEnd,
        TimeSpan TotalWorked,
        TimeSpan TotalTarget,
        int LoggedBucketCount,
        string BucketUnitLabel,
        IReadOnlyList<ReportBucket> Buckets,
        IReadOnlyList<ProjectBreakdownEntry> ProjectBreakdown,
        IReadOnlyList<ProjectBreakdownEntry> TaskBreakdown);
}
