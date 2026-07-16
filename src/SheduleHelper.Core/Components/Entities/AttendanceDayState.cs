namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Describes the current day's attendance state, derived from whether an open
    /// <see cref="AttendanceLog"/> exists and whether today already has one.
    /// </summary>
    public enum AttendanceDayState
    {
        /// <summary>
        /// No open attendance log, and no log at all for today yet.
        /// </summary>
        NotClockedIn = 0,

        /// <summary>
        /// An attendance log is open and its <see cref="AttendanceLog.WorkDate"/> is today.
        /// </summary>
        ClockedIn = 1,

        /// <summary>
        /// An attendance log is open but its <see cref="AttendanceLog.WorkDate"/> is before today -
        /// the user forgot to clock out before the day rolled over. Must be resolved before a new
        /// day's clock-in becomes available.
        /// </summary>
        ForgottenSession = 2,

        /// <summary>
        /// No open attendance log, but today's log already exists and is closed.
        /// </summary>
        DayComplete = 3
    }
}
