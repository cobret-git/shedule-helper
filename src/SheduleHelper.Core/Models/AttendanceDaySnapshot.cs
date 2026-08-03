using SheduleHelper.Core.Components.Entities;

namespace SheduleHelper.Core.Models
{
    /// <summary>
    /// The current day's attendance state, as resolved by <see cref="Services.IAttendanceService"/>.
    /// Carries the entities involved directly (rather than pre-extracting every field a caller
    /// might want) so a host only reads what its particular screen/page actually needs.
    /// </summary>
    /// <param name="DayState">The derived state - see <see cref="AttendanceDayState"/> for what each value means.</param>
    /// <param name="OpenAttendanceLog">The still-open attendance log, when <paramref name="DayState"/> is <see cref="AttendanceDayState.ClockedIn"/> or <see cref="AttendanceDayState.ForgottenSession"/>; otherwise <see langword="null"/>.</param>
    /// <param name="TodayClosedAttendanceLog">Today's already-closed attendance log, when <paramref name="DayState"/> is <see cref="AttendanceDayState.DayComplete"/>; otherwise <see langword="null"/>.</param>
    /// <param name="WorkedToday">Net worked time so far today (or for the day just closed), per <see cref="TimeBudgetCalculator.CalculateNetWorkedTime"/>, measured at the instant this snapshot was built. For a live figure use <see cref="WorkedAsOf"/>.</param>
    /// <param name="RollingMonthlyBalance">The rolling balance across the current calendar month to date, per <see cref="TimeBudgetCalculator.CalculateRollingBudget"/>. Counts completed days only, so it does not move while today is still open - see <see cref="OpenDayBalanceAsOf"/>.</param>
    /// <param name="UserSetting">The user's settings in effect when this snapshot was built - target hours, default times, lunch strategy.</param>
    public sealed record AttendanceDaySnapshot(
        AttendanceDayState DayState,
        AttendanceLog? OpenAttendanceLog,
        AttendanceLog? TodayClosedAttendanceLog,
        TimeSpan WorkedToday,
        TimeSpan RollingMonthlyBalance,
        UserSetting UserSetting)
    {
        #region Properties

        /// <summary>
        /// The day's target as a <see cref="TimeSpan"/> - <see cref="UserSetting.TargetShiftHours"/>
        /// is a <see cref="decimal"/> of hours, and every host was converting it identically.
        /// </summary>
        public TimeSpan DailyTarget => TimeSpan.FromHours((double)UserSetting.TargetShiftHours);

        #endregion

        #region Methods

        /// <summary>
        /// Net worked time as of <paramref name="asOf"/> rather than as of when this snapshot was
        /// built - the same figure as <see cref="WorkedToday"/>, recomputed against a later clock.
        /// Pure arithmetic over the data already carried here (no database access), so a host can
        /// safely call it once per rendered frame to keep a progress bar and a worked-versus-target
        /// readout moving in real time. For any state other than
        /// <see cref="AttendanceDayState.ClockedIn"/> there is no open session to extend, so
        /// <see cref="WorkedToday"/> is returned unchanged.
        /// </summary>
        public TimeSpan WorkedAsOf(DateTime asOf)
        {
            return DayState == AttendanceDayState.ClockedIn && OpenAttendanceLog is { } openLog
                ? TimeBudgetCalculator.CalculateNetWorkedTime(openLog, UserSetting, asOf)
                : WorkedToday;
        }

        /// <summary>
        /// Today's own contribution to the monthly balance as of <paramref name="asOf"/>, or
        /// <see langword="null"/> when there is none to report separately. Exists because
        /// <see cref="RollingMonthlyBalance"/> deliberately counts completed days only: while a
        /// session is open, this is the part that is still moving, and adding the two gives the
        /// projected month-to-date balance. Returns <see langword="null"/> once the day is complete -
        /// by then <see cref="RollingMonthlyBalance"/> already includes it, and adding this again
        /// would double-count.
        /// </summary>
        public TimeSpan? OpenDayBalanceAsOf(DateTime asOf)
        {
            return DayState == AttendanceDayState.ClockedIn ? WorkedAsOf(asOf) - DailyTarget : null;
        }

        #endregion
    }
}
