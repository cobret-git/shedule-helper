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
    /// <param name="WorkedToday">Net worked time so far today (or for the day just closed), per <see cref="TimeBudgetCalculator.CalculateNetWorkedTime"/>.</param>
    /// <param name="RollingMonthlyBalance">The rolling balance across the current calendar month to date, per <see cref="TimeBudgetCalculator.CalculateRollingBudget"/>.</param>
    /// <param name="UserSetting">The user's settings in effect when this snapshot was built - target hours, default times, lunch strategy.</param>
    public sealed record AttendanceDaySnapshot(
        AttendanceDayState DayState,
        AttendanceLog? OpenAttendanceLog,
        AttendanceLog? TodayClosedAttendanceLog,
        TimeSpan WorkedToday,
        TimeSpan RollingMonthlyBalance,
        UserSetting UserSetting);
}
