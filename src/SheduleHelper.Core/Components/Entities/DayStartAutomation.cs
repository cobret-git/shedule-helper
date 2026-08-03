namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// How much of the start-of-day ritual the app performs by itself when it launches (or when the
    /// date rolls over under a running app), per
    /// <see cref="Services.IAttendanceService.ResolveDayStartAsync"/>. The levels are cumulative
    /// rather than independent flags: clocking in today while a previous day is still open is not a
    /// state worth offering, so <see cref="CloseAndClockIn"/> implies
    /// <see cref="CloseForgottenDays"/>.
    /// </summary>
    public enum DayStartAutomation
    {
        /// <summary>
        /// Nothing happens automatically - every clock-in and clock-out is an explicit keystroke.
        /// </summary>
        Off = 0,

        /// <summary>
        /// A previous day left open (<see cref="AttendanceDayState.ForgottenSession"/>) is closed at
        /// that day's <see cref="UserSetting.DefaultClockOutTime"/>, but today is left for the user
        /// to start.
        /// </summary>
        CloseForgottenDays = 1,

        /// <summary>
        /// As <see cref="CloseForgottenDays"/>, and today is then clocked in at
        /// <see cref="UserSetting.DefaultClockInTime"/> (or the current time, if the default is
        /// still in the future). Weekends are skipped - opening the app on a Saturday to read a
        /// report should not start a shift.
        /// </summary>
        CloseAndClockIn = 2
    }
}
