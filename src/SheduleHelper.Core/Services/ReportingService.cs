using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using System.Globalization;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Default <see cref="IReportingService"/> implementation. Reuses
    /// <see cref="TimeBudgetCalculator.CalculateNetWorkedTime"/> and
    /// <see cref="TimeBudgetCalculator.SummarizeByProject"/>/<see cref="TimeBudgetCalculator.SummarizeByTask"/>
    /// rather than re-deriving worked time or per-project totals - this only adds the bucketing
    /// (day/week/month) that turns raw logs into a <see cref="PeriodReport"/>.
    /// </summary>
    public class ReportingService : IReportingService
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ReportingService"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Creates the <see cref="LocalDbContext"/> used for every report.</param>
        public ReportingService(ILocalDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public async Task<PeriodReport> GetReportAsync(int userId, ReportZoom zoom, DateTime referenceDate, CancellationToken cancellationToken)
        {
            await using var db = _dbContextFactory.CreateDbContext();
            var userSetting = await db.GetUserSettingAsync(userId, cancellationToken)
                ?? await db.CreateUserSettingAsync(userId, cancellationToken);

            var (periodLabel, bucketUnitLabel, bucketDefs) = ResolvePeriod(zoom, referenceDate);
            var rangeStart = bucketDefs[0].Start;
            var rangeEnd = bucketDefs[^1].EndExclusive.AddTicks(-1);

            var attendanceLogs = await db.GetAttendanceLogsAsync(userId, rangeStart, rangeEnd, cancellationToken);
            var projectTimeLogs = await db.GetProjectTimeLogsAsync(userId, rangeStart, rangeEnd, cancellationToken);
            var dailyTarget = TimeSpan.FromHours((double)userSetting.TargetShiftHours);

            var buckets = new List<ReportBucket>(bucketDefs.Count);
            foreach (var def in bucketDefs)
            {
                var logsInBucket = attendanceLogs.Where(l => l.ClockIn >= def.Start && l.ClockIn < def.EndExclusive).ToList();
                var worked = TimeSpan.Zero;
                foreach (var log in logsInBucket)
                {
                    worked += TimeBudgetCalculator.CalculateNetWorkedTime(log, userSetting, log.ClockOut ?? DateTime.Now);
                }

                var target = dailyTarget * logsInBucket.Count;
                var isWeekend = def.Start.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                buckets.Add(new ReportBucket(def.Label, def.Start, worked, target, logsInBucket.Count > 0, isWeekend));
            }

            var totalWorked = buckets.Aggregate(TimeSpan.Zero, (sum, b) => sum + b.Worked);
            var totalTarget = buckets.Aggregate(TimeSpan.Zero, (sum, b) => sum + b.Target);
            var loggedBucketCount = buckets.Count(b => b.HasData);

            var projects = await db.GetProjectsByUserIdAsync(userId, cancellationToken);
            var projectBreakdown = BuildProjectBreakdown(projectTimeLogs, projects);
            var taskBreakdown = await BuildTaskBreakdownAsync(db, projectTimeLogs, projects, cancellationToken);

            return new PeriodReport(periodLabel, rangeStart, rangeEnd, totalWorked, totalTarget, loggedBucketCount, bucketUnitLabel, buckets, projectBreakdown, taskBreakdown);
        }

        #endregion

        #region Helpers

        private readonly record struct BucketDefinition(DateTime Start, DateTime EndExclusive, string Label);

        /// <summary>
        /// Resolves the period containing <paramref name="referenceDate"/> at the given
        /// <paramref name="zoom"/> into its bucket definitions - the one place all four zoom
        /// levels' differing bucket sizes are decided.
        /// </summary>
        private static (string PeriodLabel, string BucketUnitLabel, List<BucketDefinition> Buckets) ResolvePeriod(ReportZoom zoom, DateTime referenceDate)
        {
            var date = referenceDate.Date;

            switch (zoom)
            {
                case ReportZoom.Week:
                {
                    var weekStart = StartOfWeek(date);
                    var buckets = new List<BucketDefinition>();
                    for (var i = 0; i < 7; i++)
                    {
                        var day = weekStart.AddDays(i);
                        buckets.Add(new BucketDefinition(day, day.AddDays(1), day.ToString("ddd")));
                    }

                    var label = $"{weekStart:d MMM} - {weekStart.AddDays(6):d MMM yyyy}";
                    return (label, "day", buckets);
                }

                case ReportZoom.Month:
                {
                    var monthStart = new DateTime(date.Year, date.Month, 1);
                    var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
                    var buckets = new List<BucketDefinition>();
                    for (var i = 0; i < daysInMonth; i++)
                    {
                        var day = monthStart.AddDays(i);
                        buckets.Add(new BucketDefinition(day, day.AddDays(1), day.Day.ToString()));
                    }

                    var label = monthStart.ToString("MMMM yyyy");
                    return (label, "day", buckets);
                }

                case ReportZoom.Quarter:
                {
                    var quarter = (date.Month - 1) / 3 + 1;
                    var quarterStartMonth = (quarter - 1) * 3 + 1;
                    var quarterStart = new DateTime(date.Year, quarterStartMonth, 1);
                    var quarterEndExclusive = quarterStart.AddMonths(3);

                    var buckets = new List<BucketDefinition>();
                    var cursor = StartOfWeek(quarterStart);
                    while (cursor < quarterEndExclusive)
                    {
                        var weekLabel = $"W{ISOWeek.GetWeekOfYear(cursor)}";
                        buckets.Add(new BucketDefinition(cursor, cursor.AddDays(7), weekLabel));
                        cursor = cursor.AddDays(7);
                    }

                    var label = $"Q{quarter} {date.Year}";
                    return (label, "week", buckets);
                }

                case ReportZoom.Year:
                {
                    var buckets = new List<BucketDefinition>();
                    for (var month = 1; month <= 12; month++)
                    {
                        var monthStart = new DateTime(date.Year, month, 1);
                        buckets.Add(new BucketDefinition(monthStart, monthStart.AddMonths(1), monthStart.ToString("MMM")));
                    }

                    var label = date.Year.ToString();
                    return (label, "month", buckets);
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(zoom), zoom, null);
            }
        }

        private static DateTime StartOfWeek(DateTime date)
        {
            var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            return date.AddDays(-diff);
        }

        private static List<ProjectBreakdownEntry> BuildProjectBreakdown(List<ProjectTimeLog> logs, List<Project> projects)
        {
            var totals = TimeBudgetCalculator.SummarizeByProject(logs);
            var names = projects.ToDictionary(p => p.Id, p => p.Name);

            return totals
                .Select(kv => new ProjectBreakdownEntry(names.GetValueOrDefault(kv.Key, "(unknown project)"), kv.Value))
                .OrderByDescending(entry => entry.Time)
                .ToList();
        }

        private static async Task<List<ProjectBreakdownEntry>> BuildTaskBreakdownAsync(LocalDbContext db, List<ProjectTimeLog> logs, List<Project> projects, CancellationToken cancellationToken)
        {
            var totals = TimeBudgetCalculator.SummarizeByTask(logs);
            if (totals.Count == 0)
            {
                return new List<ProjectBreakdownEntry>();
            }

            var labels = new Dictionary<int, string>();
            foreach (var project in projects)
            {
                var tasks = await db.GetTasksByProjectIdAsync(project.Id, cancellationToken);
                foreach (var task in tasks)
                {
                    labels[task.Id] = $"{project.Name} / {task.Title}";
                }
            }

            return totals
                .Select(kv => new ProjectBreakdownEntry(labels.GetValueOrDefault(kv.Key, "(unknown task)"), kv.Value))
                .OrderByDescending(entry => entry.Time)
                .ToList();
        }

        #endregion
    }
}
