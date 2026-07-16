using System;
using System.Collections.Generic;
using System.Linq;
using SheduleHelper.Core.Components.Entities;

namespace SheduleHelper.Core.Models
{
    /// <summary>
    /// Computes the rolling time budget and per-project/per-task time summaries described in the
    /// Time &amp; Task Tracker blueprint, from already-loaded attendance and project time log data.
    /// </summary>
    public static class TimeBudgetCalculator
    {
        #region Methods

        /// <summary>
        /// Calculates the net worked time and daily balance for a single completed attendance session,
        /// deducting the break time dictated by the user's <see cref="UserSetting.LunchStrategy"/>.
        /// </summary>
        /// <param name="attendanceLog">A completed attendance log (<see cref="AttendanceLog.ClockOut"/> must be set).</param>
        /// <param name="userSetting">The user's settings, providing the lunch strategy and target shift hours.</param>
        /// <returns>The daily balance ($B_{day} = T_{net} - \text{TargetShiftHours}$).</returns>
        /// <exception cref="InvalidOperationException">Thrown when <paramref name="attendanceLog"/> has no <see cref="AttendanceLog.ClockOut"/> (session still open).</exception>
        public static TimeSpan CalculateDailyBalance(AttendanceLog attendanceLog, UserSetting userSetting)
        {
            if (attendanceLog.ClockOut is null)
            {
                throw new InvalidOperationException($"Attendance log {attendanceLog.Id} has no clock-out time; cannot calculate a daily balance for an open session.");
            }

            var rawTime = attendanceLog.ClockOut.Value - attendanceLog.ClockIn;
            var breakTime = CalculateBreakTime(attendanceLog.ClockIn, attendanceLog.ClockOut.Value, rawTime, userSetting);
            var netTime = rawTime - breakTime;

            return netTime - TimeSpan.FromHours((double)userSetting.TargetShiftHours);
        }

        /// <summary>
        /// Calculates the net worked time so far for an attendance session as of a given instant,
        /// applying the same break-time deduction rules as <see cref="CalculateDailyBalance"/>.
        /// Unlike that method, this does not subtract the target shift hours and works for a still-open
        /// session (pass <see cref="DateTime.Now"/> as <paramref name="asOf"/>) as well as a completed one
        /// (pass <see cref="AttendanceLog.ClockOut"/>).
        /// </summary>
        /// <param name="attendanceLog">The attendance log being evaluated.</param>
        /// <param name="userSetting">The user's settings, providing the lunch strategy and its parameters.</param>
        /// <param name="asOf">The instant to measure worked time up to.</param>
        /// <returns>The net worked time ($T_{net}$) from clock-in up to <paramref name="asOf"/>.</returns>
        public static TimeSpan CalculateNetWorkedTime(AttendanceLog attendanceLog, UserSetting userSetting, DateTime asOf)
        {
            var rawTime = asOf - attendanceLog.ClockIn;
            var breakTime = CalculateBreakTime(attendanceLog.ClockIn, asOf, rawTime, userSetting);

            return rawTime - breakTime;
        }

        /// <summary>
        /// Calculates the rolling budget over a set of attendance logs by summing each completed day's balance.
        /// Attendance logs without a <see cref="AttendanceLog.ClockOut"/> (the currently open session, if any) are ignored.
        /// </summary>
        /// <param name="attendanceLogs">The attendance logs for the period being evaluated.</param>
        /// <param name="userSetting">The user's settings, providing the lunch strategy and target shift hours.</param>
        /// <returns>The rolling balance ($B_{month} = \sum B_{day}$) across all completed days.</returns>
        public static TimeSpan CalculateRollingBudget(IEnumerable<AttendanceLog> attendanceLogs, UserSetting userSetting)
        {
            return attendanceLogs
                .Where(a => a.ClockOut is not null)
                .Aggregate(TimeSpan.Zero, (total, log) => total + CalculateDailyBalance(log, userSetting));
        }

        /// <summary>
        /// Sums tracked time per project across the given project time log segments.
        /// Segments still open (<see cref="ProjectTimeLog.EndTime"/> is <see langword="null"/>) are ignored.
        /// </summary>
        /// <param name="projectTimeLogs">The project time log segments for the period being evaluated.</param>
        /// <returns>A dictionary mapping each project's identifier to its total tracked time.</returns>
        public static Dictionary<int, TimeSpan> SummarizeByProject(IEnumerable<ProjectTimeLog> projectTimeLogs)
        {
            return projectTimeLogs
                .Where(l => l.EndTime is not null)
                .GroupBy(l => l.ProjectId)
                .ToDictionary(g => g.Key, g => g.Aggregate(TimeSpan.Zero, (total, l) => total + (l.EndTime!.Value - l.StartTime)));
        }

        /// <summary>
        /// Sums tracked time per task across the given project time log segments.
        /// Segments still open (<see cref="ProjectTimeLog.EndTime"/> is <see langword="null"/>) or with no
        /// associated task (<see cref="ProjectTimeLog.TaskId"/> is <see langword="null"/>) are ignored.
        /// </summary>
        /// <param name="projectTimeLogs">The project time log segments for the period being evaluated.</param>
        /// <returns>A dictionary mapping each task's identifier to its total tracked time.</returns>
        public static Dictionary<int, TimeSpan> SummarizeByTask(IEnumerable<ProjectTimeLog> projectTimeLogs)
        {
            return projectTimeLogs
                .Where(l => l.EndTime is not null && l.TaskId is not null)
                .GroupBy(l => l.TaskId!.Value)
                .ToDictionary(g => g.Key, g => g.Aggregate(TimeSpan.Zero, (total, l) => total + (l.EndTime!.Value - l.StartTime)));
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Determines the break time to deduct from a day's raw attendance time, based on the user's lunch strategy.
        /// </summary>
        /// <param name="clockIn">The session's clock-in timestamp.</param>
        /// <param name="clockOutOrAsOf">The session's clock-out timestamp, or the instant being measured up to for a still-open session.</param>
        /// <param name="rawTime">The raw attendance time ($T_{raw}$) for the day.</param>
        /// <param name="userSetting">The user's settings, providing the lunch strategy and its parameters.</param>
        /// <returns>The break time to deduct ($T_{break}$).</returns>
        private static TimeSpan CalculateBreakTime(DateTime clockIn, DateTime clockOutOrAsOf, TimeSpan rawTime, UserSetting userSetting)
        {
            var clockInTime = TimeOnly.FromDateTime(clockIn);
            var clockOutTime = TimeOnly.FromDateTime(clockOutOrAsOf);

            return userSetting.LunchStrategy switch
            {
                LunchStrategy.FixedWindow when clockInTime <= userSetting.LunchStartTime && clockOutTime >= userSetting.LunchEndTime
                    => userSetting.LunchEndTime - userSetting.LunchStartTime,
                LunchStrategy.DurationBased when rawTime >= TimeSpan.FromHours(6)
                    => TimeSpan.FromMinutes(userSetting.LunchDurationMinutes),
                _ => TimeSpan.Zero
            };
        }

        #endregion
    }
}
