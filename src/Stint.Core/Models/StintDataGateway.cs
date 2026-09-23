using Microsoft.EntityFrameworkCore;

namespace Stint.Core
{
    public class StintDataGateway : IStintDataGateway
    {
        #region Fields
        private readonly IDbContextFactory<LocalDbContext> _dbContextFactory;
        #endregion

        #region Constructors
        public StintDataGateway(IDbContextFactory<LocalDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }
        #endregion

        #region Methods

        // Projects

        public async Task<List<Project>> GetActiveProjectsAsync(CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.Projects
                .Where(p => p.IsActive)
                .OrderBy(p => p.CreatedAt)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<Project?> GetProjectByIdAsync(int projectId, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId, ct);
        }

        public async Task<Project?> GetProjectByNameAsync(string name, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Name == name, ct);
        }

        public async Task<Project> AddProjectAsync(Project project, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            context.Projects.Add(project);
            await context.SaveChangesAsync(ct);
            return project;
        }

        public async Task UpdateProjectAsync(Project project, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var existing = await context.Projects.FirstOrDefaultAsync(p => p.Id == project.Id, ct)
                ?? throw new InvalidOperationException($"Project {project.Id} was not found.");

            existing.Name = project.Name;
            existing.IsActive = project.IsActive;
            await context.SaveChangesAsync(ct);
        }

        public async Task SetProjectActiveAsync(int projectId, bool isActive, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var project = await context.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
                ?? throw new InvalidOperationException($"Project {projectId} was not found.");

            project.IsActive = isActive;
            await context.SaveChangesAsync(ct);
        }

        // Tasks

        public async Task<List<TaskItem>> GetTasksForProjectAsync(int projectId, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.Tasks
                .Where(t => t.ProjectId == projectId && t.IsActive)
                .OrderBy(t => t.CreatedAt)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<TaskItem?> GetTaskByIdAsync(int taskId, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == taskId, ct);
        }

        public async Task<int> GetTaskCountForProjectAsync(int projectId, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.Tasks
                .CountAsync(t => t.ProjectId == projectId && t.IsActive, ct);
        }

        public async Task<TaskItem> AddTaskAsync(TaskItem task, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            context.Tasks.Add(task);
            await context.SaveChangesAsync(ct);
            return task;
        }

        public async Task UpdateTaskAsync(TaskItem task, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var existing = await context.Tasks.FirstOrDefaultAsync(t => t.Id == task.Id, ct)
                ?? throw new InvalidOperationException($"Task {task.Id} was not found.");

            existing.Title = task.Title;
            existing.Status = task.Status;
            existing.IsActive = task.IsActive;
            await context.SaveChangesAsync(ct);
        }

        public async Task UpdateTaskStatusAsync(int taskId, TaskItemStatus status, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var task = await context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
                ?? throw new InvalidOperationException($"Task {taskId} was not found.");

            task.Status = status;
            await context.SaveChangesAsync(ct);
        }

        public async Task SetTaskActiveAsync(int taskId, bool isActive, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var task = await context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
                ?? throw new InvalidOperationException($"Task {taskId} was not found.");

            task.IsActive = isActive;
            await context.SaveChangesAsync(ct);
        }

        // Attendance

        public async Task<AttendanceLog> RecordLeaveDayAsync(string workDate, DayType dayType, TimeSpan creditedDuration, CancellationToken ct = default)
        {
            if (dayType == DayType.Worked)
                throw new ArgumentException("Use ClockInAsync for worked days.", nameof(dayType));

            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var attendanceLog = new AttendanceLog
            {
                WorkDate = workDate,
                DayType = dayType,
                CreditedDuration = creditedDuration
            };

            context.AttendanceLogs.Add(attendanceLog);
            await context.SaveChangesAsync(ct);
            return attendanceLog;
        }

        // Not yet exposed via IStintDataGateway - no caller needs a leave-day breakdown yet. Kept
        // private until a real use case shows up; promote it (and add to the interface) then.
        private async Task<Dictionary<DayType, int>> GetLeaveDaySummaryAsync(string rangeStart, string rangeEnd, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.AttendanceLogs
                .Where(a => a.WorkDate.CompareTo(rangeStart) >= 0 && a.WorkDate.CompareTo(rangeEnd) <= 0)
                .Where(a => a.DayType != DayType.Worked)
                .GroupBy(a => a.DayType)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        }

        public async Task<AttendanceLog?> GetAttendanceForDateAsync(string workDate, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.AttendanceLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.WorkDate == workDate, ct);
        }

        public async Task<AttendanceLog> ClockInAsync(string workDate, DateTime clockIn, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var attendanceLog = new AttendanceLog
            {
                WorkDate = workDate,
                ClockIn = clockIn
            };

            context.AttendanceLogs.Add(attendanceLog);
            await context.SaveChangesAsync(ct);
            return attendanceLog;
        }

        public async Task ClockOutAsync(int attendanceLogId, DateTime clockOut, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var attendanceLog = await context.AttendanceLogs.FirstOrDefaultAsync(a => a.Id == attendanceLogId, ct)
                ?? throw new InvalidOperationException($"Attendance log {attendanceLogId} was not found.");

            attendanceLog.ClockOut = clockOut;
            await context.SaveChangesAsync(ct);
        }

        // Project Time Logs

        public async Task<List<ProjectTimeLog>> GetTimeLogsForAttendanceAsync(int attendanceLogId, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.ProjectTimeLogs
                .Where(l => l.AttendanceLogId == attendanceLogId)
                .Include(l => l.Project)
                .Include(l => l.Task)
                .OrderByDescending(l => l.StartTime)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<ProjectTimeLog?> GetOpenTimeLogAsync(int attendanceLogId, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.ProjectTimeLogs
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.AttendanceLogId == attendanceLogId && l.EndTime == null, ct);
        }

        public async Task<ProjectTimeLog?> GetLastResumableTimeLogAsync(CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            return await context.ProjectTimeLogs
                .Where(l => l.ClosedReason == TimeLogCloseReason.ClockedOut)
                .OrderByDescending(l => l.EndTime)
                .AsNoTracking()
                .FirstOrDefaultAsync(ct);
        }

        public async Task<ProjectTimeLog> StartTimeLogAsync(int attendanceLogId, int projectId, int? taskId, DateTime startTime, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var timeLog = new ProjectTimeLog
            {
                AttendanceLogId = attendanceLogId,
                ProjectId = projectId,
                TaskId = taskId,
                StartTime = startTime
            };

            context.ProjectTimeLogs.Add(timeLog);
            await context.SaveChangesAsync(ct);
            return timeLog;
        }

        public async Task CloseTimeLogAsync(int timeLogId, DateTime endTime, TimeLogCloseReason reason, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var timeLog = await context.ProjectTimeLogs.FirstOrDefaultAsync(l => l.Id == timeLogId, ct)
                ?? throw new InvalidOperationException($"Time log {timeLogId} was not found.");

            timeLog.EndTime = endTime;
            timeLog.ClosedReason = reason;
            await context.SaveChangesAsync(ct);
        }

        // Reporting

        public async Task<List<ProjectTimeDailyEntry>> GetProjectTimeDailyAsync(DateOnly rangeStart, DateOnly rangeEnd, AppSettings settings, CancellationToken ct = default)
        {
            var segments = await GetTrackedSegmentsAsync(rangeStart, rangeEnd, settings, ct);
            return segments
                .GroupBy(s => (s.Date, s.ProjectName))
                .Select(g => new ProjectTimeDailyEntry(g.Key.Date, g.Key.ProjectName, SumDurations(g)))
                .OrderBy(e => e.Date).ThenBy(e => e.ProjectName)
                .ToList();
        }

        public async Task<List<ProjectTimeMonthlyEntry>> GetProjectTimeMonthlyAsync(DateOnly rangeStart, DateOnly rangeEnd, AppSettings settings, CancellationToken ct = default)
        {
            var segments = await GetTrackedSegmentsAsync(rangeStart, rangeEnd, settings, ct);
            return segments
                .GroupBy(s => (s.Date.Year, s.Date.Month, s.ProjectName))
                .Select(g => new ProjectTimeMonthlyEntry(g.Key.Year, g.Key.Month, g.Key.ProjectName, SumDurations(g)))
                .OrderBy(e => e.Year).ThenBy(e => e.Month).ThenBy(e => e.ProjectName)
                .ToList();
        }

        public async Task<List<ProjectTimeMonthlyTotalEntry>> GetProjectTimeMonthlyTotalAsync(DateOnly rangeStart, DateOnly rangeEnd, AppSettings settings, CancellationToken ct = default)
        {
            var segments = await GetTrackedSegmentsAsync(rangeStart, rangeEnd, settings, ct);
            return segments
                .GroupBy(s => (s.Date.Year, s.Date.Month))
                .Select(g => new ProjectTimeMonthlyTotalEntry(g.Key.Year, g.Key.Month, SumDurations(g)))
                .OrderBy(e => e.Year).ThenBy(e => e.Month)
                .ToList();
        }

        #endregion

        #region Helpers

        // Loads every closed segment in the range, joined to its project name, with lunch already
        // deducted per-segment. Small local dataset - simpler and safer to aggregate the three
        // Reporting shapes above in memory than to translate the date-overlap math to SQL.
        private async Task<List<(DateOnly Date, string ProjectName, TimeSpan Duration)>> GetTrackedSegmentsAsync(
            DateOnly rangeStart, DateOnly rangeEnd, AppSettings settings, CancellationToken ct)
        {
            var rangeStartInclusive = rangeStart.ToDateTime(TimeOnly.MinValue);
            var rangeEndExclusive = rangeEnd.ToDateTime(TimeOnly.MinValue).AddDays(1);

            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var rawSegments = await context.ProjectTimeLogs
                .Where(l => l.EndTime != null && l.StartTime >= rangeStartInclusive && l.StartTime < rangeEndExclusive)
                .Select(l => new { l.StartTime, EndTime = l.EndTime!.Value, ProjectName = l.Project.Name })
                .AsNoTracking()
                .ToListAsync(ct);

            return rawSegments
                .Select(s => (
                    Date: DateOnly.FromDateTime(s.StartTime),
                    s.ProjectName,
                    Duration: DeductLunch(s.StartTime, s.EndTime, settings)))
                .ToList();
        }

        // Applies AppSettings.LunchStrategy to one tracked segment.
        private static TimeSpan DeductLunch(DateTime start, DateTime end, AppSettings settings)
        {
            switch (settings.LunchStrategy)
            {
                case LunchStrategy.None:
                    return end - start;

                case LunchStrategy.FixedWindow:
                    var day = start.Date;
                    var windowStart = day + settings.LunchStartTime.ToTimeSpan();
                    var windowEnd = day + settings.LunchEndTime.ToTimeSpan();

                    var overlapStart = start > windowStart ? start : windowStart;
                    var overlapEnd = end < windowEnd ? end : windowEnd;
                    var overlap = overlapEnd > overlapStart ? overlapEnd - overlapStart : TimeSpan.Zero;

                    return (end - start) - overlap;

                case LunchStrategy.DurationBased:
                default:
                    // AppSettings has no "attendance exceeds a threshold" value defined yet - rather
                    // than guess at one, refuse until that setting exists and this is wired up for real.
                    throw new NotSupportedException(
                        $"{nameof(LunchStrategy.DurationBased)} lunch deduction isn't implemented yet - no threshold is defined in {nameof(AppSettings)}.");
            }
        }

        private static TimeSpan SumDurations(IEnumerable<(DateOnly Date, string ProjectName, TimeSpan Duration)> segments)
        {
            var totalTicks = 0L;
            foreach (var segment in segments)
                totalTicks += segment.Duration.Ticks;

            return TimeSpan.FromTicks(totalTicks);
        }

        #endregion
    }
}
