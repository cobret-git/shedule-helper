using SheduleHelper.Core.Components.Entities;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// A user-facing project-tracking failure (starting in the future, nothing currently being
    /// tracked). <see cref="Exception.Message"/> is already a localized, display-ready string
    /// (from <c>Resources.Strings.Messages</c>) - callers show it as-is.
    /// </summary>
    public sealed class TrackingOperationException : Exception
    {
        public TrackingOperationException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Owns switching which project/task is being tracked within an attendance session: at any
    /// time there is at most one open <see cref="ProjectTimeLog"/> per session, so "switch to
    /// project B" is just "close whatever's open, open a new segment for B" -
    /// <see cref="Models.LocalDbContext.StartProjectTimeLogAsync"/> already does exactly that.
    /// This wraps it with the same user-facing validation style as <see cref="IAttendanceService"/>.
    /// </summary>
    public interface ITrackingService
    {
        #region Methods

        /// <summary>
        /// Retrieves the project/task currently being tracked within the given attendance session, if any.
        /// </summary>
        Task<ProjectTimeLog?> GetActiveTrackingAsync(int attendanceLogId, CancellationToken cancellationToken);

        /// <summary>
        /// Switches tracking to <paramref name="projectId"/> (and optionally <paramref name="taskId"/>),
        /// closing whatever segment was previously open at <paramref name="startTime"/> and opening a new one.
        /// </summary>
        /// <exception cref="TrackingOperationException">The time is in the future, or <paramref name="projectId"/>/<paramref name="taskId"/> no longer exists.</exception>
        Task<ProjectTimeLog> SwitchAsync(int attendanceLogId, int projectId, int? taskId, DateTime startTime, CancellationToken cancellationToken);

        /// <summary>
        /// Stops tracking - closes the currently open segment at <paramref name="endTime"/> without opening a new one.
        /// </summary>
        /// <exception cref="TrackingOperationException">Nothing is currently being tracked for this attendance session.</exception>
        Task StopTrackingAsync(int attendanceLogId, DateTime endTime, CancellationToken cancellationToken);

        #endregion
    }
}
