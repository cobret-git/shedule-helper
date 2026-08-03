using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;

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
        /// Records <see cref="TimeLogCloseReason.Stopped"/>, which is what stops
        /// <see cref="ResumeLastAsync"/> from picking that project back up on the next clock-in.
        /// </summary>
        /// <exception cref="TrackingOperationException">Nothing is currently being tracked for this attendance session.</exception>
        Task StopTrackingAsync(int attendanceLogId, DateTime endTime, CancellationToken cancellationToken);

        /// <summary>
        /// Continues whatever the user was last tracking into <paramref name="attendanceLogId"/>,
        /// starting at <paramref name="startTime"/> - so multi-day work doesn't have to be
        /// re-selected each morning. The candidate is the most recent segment across all sessions,
        /// which means switching projects moves the candidate on its own and no separate "current
        /// project" state has to be kept in sync; an explicit stop
        /// (<see cref="TimeLogCloseReason.Stopped"/>) suppresses it entirely.
        /// Declining to resume is a normal outcome, not a failure - see
        /// <see cref="TrackingResumeOutcome"/> - so this reports rather than throws.
        /// </summary>
        Task<TrackingResumeResult> ResumeLastAsync(int userId, int attendanceLogId, DateTime startTime, CancellationToken cancellationToken);

        #endregion
    }
}
