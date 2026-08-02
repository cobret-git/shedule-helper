using Microsoft.EntityFrameworkCore;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using System.Linq;
using MSG = SheduleHelper.Core.Resources.Strings.Messages;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Default <see cref="IAttendanceService"/> implementation, backed directly by
    /// <see cref="LocalDbContext"/> and <see cref="TimeBudgetCalculator"/>.
    /// </summary>
    public class AttendanceService : IAttendanceService
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AttendanceService"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Creates the <see cref="LocalDbContext"/> used for every operation.</param>
        public AttendanceService(ILocalDbContextFactory dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public async Task<AttendanceDaySnapshot> GetDaySnapshotAsync(int userId, CancellationToken cancellationToken)
        {
            await using var db = _dbContextFactory.CreateDbContext();
            var userSetting = await GetOrCreateUserSettingAsync(db, userId, cancellationToken);

            return await BuildSnapshotAsync(db, userId, userSetting, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<AttendanceDaySnapshot> ClockInAsync(int userId, DateTime clockInTime, CancellationToken cancellationToken)
        {
            if (clockInTime > DateTime.Now)
            {
                throw new AttendanceOperationException(MSG.error_clockInTimeInFuture);
            }

            await using var db = _dbContextFactory.CreateDbContext();
            var userSetting = await GetOrCreateUserSettingAsync(db, userId, cancellationToken);

            try
            {
                await db.ClockInAsync(userId, clockInTime, cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw new AttendanceOperationException(MSG.error_alreadyClockedInToday);
            }

            return await BuildSnapshotAsync(db, userId, userSetting, cancellationToken);
        }

        /// <inheritdoc/>
        public async Task<AttendanceDaySnapshot> ClockOutAsync(int userId, DateTime clockOutTime, CancellationToken cancellationToken)
        {
            await using var db = _dbContextFactory.CreateDbContext();
            var userSetting = await GetOrCreateUserSettingAsync(db, userId, cancellationToken);

            var openLog = await db.GetActiveAttendanceLogAsync(userId, cancellationToken);
            if (openLog is null)
            {
                throw new AttendanceOperationException(MSG.error_notClockedIn);
            }

            if (clockOutTime <= openLog.ClockIn)
            {
                throw new AttendanceOperationException(MSG.error_clockOutBeforeClockIn);
            }

            if (clockOutTime > DateTime.Now)
            {
                throw new AttendanceOperationException(MSG.error_clockOutTimeInFuture);
            }

            try
            {
                await db.ClockOutAsync(userId, clockOutTime, cancellationToken);
            }
            catch (InvalidOperationException)
            {
                throw new AttendanceOperationException(MSG.error_notClockedIn);
            }

            return await BuildSnapshotAsync(db, userId, userSetting, cancellationToken);
        }

        #endregion

        #region Helpers

        private static async Task<UserSetting> GetOrCreateUserSettingAsync(LocalDbContext db, int userId, CancellationToken cancellationToken)
        {
            return await db.GetUserSettingAsync(userId, cancellationToken)
                ?? await db.CreateUserSettingAsync(userId, cancellationToken);
        }

        /// <summary>
        /// Re-derives the full day snapshot from scratch - called after every mutation as well as
        /// for a plain read, so the state machine has exactly one place it's computed.
        /// </summary>
        private static async Task<AttendanceDaySnapshot> BuildSnapshotAsync(LocalDbContext db, int userId, UserSetting userSetting, CancellationToken cancellationToken)
        {
            var openLog = await db.GetActiveAttendanceLogAsync(userId, cancellationToken);
            AttendanceLog? todayClosedLog = null;
            AttendanceDayState dayState;
            TimeSpan workedToday;

            if (openLog is not null)
            {
                var isToday = openLog.WorkDate == DateTime.Today.ToString("yyyy-MM-dd");
                dayState = isToday ? AttendanceDayState.ClockedIn : AttendanceDayState.ForgottenSession;
                workedToday = isToday ? TimeBudgetCalculator.CalculateNetWorkedTime(openLog, userSetting, DateTime.Now) : TimeSpan.Zero;
            }
            else
            {
                var todayStart = DateTime.Today;
                var todayEnd = todayStart.AddDays(1).AddTicks(-1);
                todayClosedLog = (await db.GetAttendanceLogsAsync(userId, todayStart, todayEnd, cancellationToken)).FirstOrDefault();

                dayState = todayClosedLog is not null ? AttendanceDayState.DayComplete : AttendanceDayState.NotClockedIn;
                workedToday = todayClosedLog is not null
                    ? TimeBudgetCalculator.CalculateNetWorkedTime(todayClosedLog, userSetting, todayClosedLog.ClockOut!.Value)
                    : TimeSpan.Zero;
            }

            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthLogs = await db.GetAttendanceLogsAsync(userId, monthStart, DateTime.Now, cancellationToken);
            var rollingMonthlyBalance = TimeBudgetCalculator.CalculateRollingBudget(monthLogs, userSetting);

            return new AttendanceDaySnapshot(dayState, openLog, todayClosedLog, workedToday, rollingMonthlyBalance, userSetting);
        }

        #endregion
    }
}
