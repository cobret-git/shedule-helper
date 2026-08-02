using SheduleHelper.Core.Models;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// A user-facing attendance failure (already clocked in today, clock-out before clock-in,
    /// a time in the future, no open session). <see cref="Exception.Message"/> is already a
    /// localized, display-ready string (from <c>Resources.Strings.Messages</c>) - callers show it
    /// as-is rather than switching on an error code.
    /// </summary>
    public sealed class AttendanceOperationException : Exception
    {
        public AttendanceOperationException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Owns the clock-in/clock-out state machine: at any time there is at most one open
    /// <see cref="Components.Entities.AttendanceLog"/> for a user, so the whole day's state can be
    /// derived from whether it exists and whether it belongs to today. Extracted from what used to
    /// be <c>HomeViewModel</c>'s own private logic, so every host application shares one
    /// implementation instead of re-deriving <see cref="Components.Entities.AttendanceDayState"/> itself.
    /// </summary>
    public interface IAttendanceService
    {
        #region Methods

        /// <summary>
        /// Resolves the current day's attendance state for the given user, without changing anything.
        /// </summary>
        Task<AttendanceDaySnapshot> GetDaySnapshotAsync(int userId, CancellationToken cancellationToken);

        /// <summary>
        /// Opens a new attendance log at <paramref name="clockInTime"/> and returns the resulting snapshot.
        /// </summary>
        /// <exception cref="AttendanceOperationException">The time is in the future, or the user already has an attendance log for today.</exception>
        Task<AttendanceDaySnapshot> ClockInAsync(int userId, DateTime clockInTime, CancellationToken cancellationToken);

        /// <summary>
        /// Closes the user's currently open attendance log at <paramref name="clockOutTime"/> and
        /// returns the resulting snapshot. Works equally for today's session and a forgotten
        /// (previous-day) one - the open log's own date determines which.
        /// </summary>
        /// <exception cref="AttendanceOperationException">There is no open session, the time is before its clock-in, or the time is in the future.</exception>
        Task<AttendanceDaySnapshot> ClockOutAsync(int userId, DateTime clockOutTime, CancellationToken cancellationToken);

        #endregion
    }
}
