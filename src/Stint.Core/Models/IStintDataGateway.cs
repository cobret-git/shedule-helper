namespace Stint.Core
{
    /// <summary>
    /// Single data-access facade over <see cref="LocalDbContext"/>. Each method opens its own
    /// short-lived context via the injected <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/>,
    /// so callers can hold a singleton instance of the implementation safely.
    /// </summary>
    public interface IStintDataGateway
    {
        #region Projects

        /// <summary>
        /// Returns all projects flagged as active, ordered by name.
        /// </summary>
        Task<List<Project>> GetActiveProjectsAsync(CancellationToken ct = default);

        /// <summary>
        /// Returns the project with the given id, or <see langword="null"/> if it doesn't exist.
        /// </summary>
        Task<Project?> GetProjectByIdAsync(int projectId, CancellationToken ct = default);

        /// <summary>
        /// Returns the project with the given name (case as stored), or <see langword="null"/> if none matches.
        /// </summary>
        Task<Project?> GetProjectByNameAsync(string name, CancellationToken ct = default);

        /// <summary>
        /// Persists a new project and returns it with its generated <see cref="Project.Id"/>.
        /// </summary>
        Task<Project> AddProjectAsync(Project project, CancellationToken ct = default);

        /// <summary>
        /// Updates the editable fields (<see cref="Project.Name"/>, <see cref="Project.IsActive"/>)
        /// of the project matching <see cref="Project.Id"/>.
        /// </summary>
        Task UpdateProjectAsync(Project project, CancellationToken ct = default);

        /// <summary>
        /// Flips <see cref="Project.IsActive"/> on the given project.
        /// </summary>
        Task SetProjectActiveAsync(int projectId, bool isActive, CancellationToken ct = default);

        #endregion

        #region Tasks

        /// <summary>
        /// Returns all active tasks belonging to the given project, ordered by creation date.
        /// </summary>
        Task<List<TaskItem>> GetTasksForProjectAsync(int projectId, CancellationToken ct = default);

        /// <summary>
        /// Returns how many active tasks belong to the given project, without loading them.
        /// </summary>
        Task<int> GetTaskCountForProjectAsync(int projectId, CancellationToken ct = default);

        /// <summary>
        /// Returns the task with the given id, or <see langword="null"/> if it doesn't exist.
        /// </summary>
        Task<TaskItem?> GetTaskByIdAsync(int taskId, CancellationToken ct = default);

        /// <summary>
        /// Persists a new task and returns it with its generated <see cref="TaskItem.Id"/>.
        /// </summary>
        Task<TaskItem> AddTaskAsync(TaskItem task, CancellationToken ct = default);

        /// <summary>
        /// Updates the editable fields (<see cref="TaskItem.Title"/>, <see cref="TaskItem.Status"/>,
        /// <see cref="TaskItem.IsActive"/>) of the task matching <see cref="TaskItem.Id"/>.
        /// </summary>
        Task UpdateTaskAsync(TaskItem task, CancellationToken ct = default);

        /// <summary>
        /// Updates the lifecycle status of the given task.
        /// </summary>
        Task UpdateTaskStatusAsync(int taskId, TaskItemStatus status, CancellationToken ct = default);

        /// <summary>
        /// Flips <see cref="TaskItem.IsActive"/> on the given task - a soft delete/undo-delete,
        /// same as <see cref="SetProjectActiveAsync"/>, so a removed task's <see cref="ProjectTimeLog"/>
        /// history stays attributable instead of being nulled out.
        /// </summary>
        Task SetTaskActiveAsync(int taskId, bool isActive, CancellationToken ct = default);

        #endregion

        #region Attendance

        /// <summary>
        /// Returns the attendance session for the given work date, or <see langword="null"/> if none was started.
        /// </summary>
        Task<AttendanceLog?> GetAttendanceForDateAsync(string workDate, CancellationToken ct = default);

        /// <summary>
        /// Starts a new attendance session (clock in) for the given work date.
        /// </summary>
        Task<AttendanceLog> ClockInAsync(string workDate, DateTime clockIn, CancellationToken ct = default);

        /// <summary>
        /// Records a full-day absence (vacation, sick, holiday, unpaid) for the given work date,
        /// crediting it with <paramref name="creditedDuration"/> instead of tracked clock times.
        /// </summary>
        Task<AttendanceLog> RecordLeaveDayAsync(string workDate, DayType dayType, TimeSpan creditedDuration, CancellationToken ct = default);

        /// <summary>
        /// Ends the given attendance session (clock out).
        /// </summary>
        Task ClockOutAsync(int attendanceLogId, DateTime clockOut, CancellationToken ct = default);

        #endregion

        #region Project Time Logs

        /// <summary>
        /// Returns all time log segments recorded within the given attendance session, most
        /// recently started first, with <see cref="ProjectTimeLog.Project"/> and
        /// <see cref="ProjectTimeLog.Task"/> eager-loaded.
        /// </summary>
        Task<List<ProjectTimeLog>> GetTimeLogsForAttendanceAsync(int attendanceLogId, CancellationToken ct = default);

        /// <summary>
        /// Returns the still-open segment (<see cref="ProjectTimeLog.EndTime"/> is <see langword="null"/>)
        /// for the given attendance session, if tracking is currently active.
        /// </summary>
        Task<ProjectTimeLog?> GetOpenTimeLogAsync(int attendanceLogId, CancellationToken ct = default);

        /// <summary>
        /// Returns the most recent segment closed with <see cref="TimeLogCloseReason.ClockedOut"/>,
        /// i.e. the candidate <see cref="Services.ITrackingService.ResumeLastAsync"/> would pick back up.
        /// </summary>
        Task<ProjectTimeLog?> GetLastResumableTimeLogAsync(CancellationToken ct = default);

        /// <summary>
        /// Opens a new time log segment for the given project/task within an attendance session.
        /// </summary>
        Task<ProjectTimeLog> StartTimeLogAsync(int attendanceLogId, int projectId, int? taskId, DateTime startTime, CancellationToken ct = default);

        /// <summary>
        /// Closes the given time log segment, recording why it stopped.
        /// </summary>
        Task CloseTimeLogAsync(int timeLogId, DateTime endTime, TimeLogCloseReason reason, CancellationToken ct = default);

        #endregion

        #region Reporting

        /// <summary>
        /// Returns tracked time per project per calendar day in <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>
        /// (inclusive), with lunch deducted per <paramref name="settings"/>.
        /// </summary>
        Task<List<ProjectTimeDailyEntry>> GetProjectTimeDailyAsync(DateOnly rangeStart, DateOnly rangeEnd, AppSettings settings, CancellationToken ct = default);

        /// <summary>
        /// Returns tracked time per project per calendar month in <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>
        /// (inclusive), with lunch deducted per <paramref name="settings"/>.
        /// </summary>
        Task<List<ProjectTimeMonthlyEntry>> GetProjectTimeMonthlyAsync(DateOnly rangeStart, DateOnly rangeEnd, AppSettings settings, CancellationToken ct = default);

        /// <summary>
        /// Returns tracked time across all projects per calendar month in <paramref name="rangeStart"/>..<paramref name="rangeEnd"/>
        /// (inclusive), with lunch deducted per <paramref name="settings"/>.
        /// </summary>
        Task<List<ProjectTimeMonthlyTotalEntry>> GetProjectTimeMonthlyTotalAsync(DateOnly rangeStart, DateOnly rangeEnd, AppSettings settings, CancellationToken ct = default);

        #endregion
    }
}
