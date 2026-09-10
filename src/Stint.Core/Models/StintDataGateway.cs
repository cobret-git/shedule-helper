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
                .OrderBy(p => p.Name)
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
                .Where(t => t.ProjectId == projectId)
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

        public async Task<TaskItem> AddTaskAsync(TaskItem task, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            context.Tasks.Add(task);
            await context.SaveChangesAsync(ct);
            return task;
        }

        public async Task UpdateTaskStatusAsync(int taskId, TaskItemStatus status, CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var task = await context.Tasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
                ?? throw new InvalidOperationException($"Task {taskId} was not found.");

            task.Status = status;
            await context.SaveChangesAsync(ct);
        }

        // Attendance

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
                .OrderBy(l => l.StartTime)
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

        #endregion
    }
}
