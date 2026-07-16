using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;
using System;
using System.Linq;
using MSG = SheduleHelper.Core.Resources.Strings.Messages;

namespace SheduleHelper.Core.ViewModels
{
    /// <summary>
    /// ViewModel for the Home page (The Daily Control Center). Owns the clock-in/clock-out state
    /// machine: at any time there is at most one open <see cref="AttendanceLog"/> for the current
    /// user, so the whole day's state can be derived from whether it exists and whether it belongs
    /// to today.
    /// </summary>
    public partial class HomeViewModel : PageViewModelBase
    {
        #region Fields
        private readonly ILocalDbContextFactory _localDbFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly IDispatcherService _dispatcherService;
        private readonly ILogger _logger = Serilog.Log.ForContext<HomeViewModel>();
        private CancellationTokenSource? _cts;
        private UserSetting _userSetting = null!;
        private AttendanceLog? _openAttendanceLog;
        private AttendanceLog? _todayClosedAttendanceLog;
        private bool _isBusy;
        [ObservableProperty] private AttendanceDayState _dayState;
        [ObservableProperty] private DateTime? _clockInTime;
        [ObservableProperty] private DateTime? _clockOutTime;
        [ObservableProperty] private TimeOnly _selectedCustomTime;
        [ObservableProperty] private DateTime _forgottenSessionDate;
        [ObservableProperty] private TimeOnly _forgottenSessionClockOutTimeOfDay;
        [ObservableProperty] private TimeOnly _defaultClockInTimeOfDay;
        [ObservableProperty] private TimeOnly _defaultClockOutTimeOfDay;
        [ObservableProperty] private TimeSpan _workedToday;
        [ObservableProperty] private TimeSpan _targetShiftDuration;
        [ObservableProperty] private TimeSpan _rollingMonthlyBalance;
        private string? _message;
        #endregion

        #region Constructors
        public HomeViewModel(ILocalDbContextFactory localDbFactory,
            ICurrentUserContext currentUserContext,
            IDispatcherService dispatcherService)
        {
            _localDbFactory = localDbFactory;
            _currentUserContext = currentUserContext;
            _dispatcherService = dispatcherService;
            _ = InitializeAsync();
        }
        #endregion

        #region Properties
        /// <summary>
        /// Static greeting shown on the Current Session card. Not yet time-of-day or user aware -
        /// a placeholder until greetings are designed properly.
        /// </summary>
        public string Greeting => "Good day! Ready to make progress?";
        public string? Message { get => _message; private set { if (SetProperty(ref _message, value)) OnPropertyChanged(nameof(MessageVisible)); } }
        public bool MessageVisible { get => !string.IsNullOrWhiteSpace(Message); }
        public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) NotifyCanExecuteChanged(); } }
        #endregion

        #region Commands
        [RelayCommand(CanExecute = nameof(CanClockIn))] private Task ClockInNowAsync() => PerformClockInAsync(DateTime.Now);
        [RelayCommand(CanExecute = nameof(CanClockIn))] private Task ClockInAtDefaultTimeAsync() => PerformClockInAsync(DateTime.Today + _userSetting.DefaultClockInTime.ToTimeSpan());
        [RelayCommand(CanExecute = nameof(CanClockIn))] private Task ClockInAtCustomTimeAsync() => PerformClockInAsync(DateTime.Today + SelectedCustomTime.ToTimeSpan());
        [RelayCommand(CanExecute = nameof(CanClockOut))] private Task ClockOutNowAsync() => PerformClockOutAsync(DateTime.Now);
        [RelayCommand(CanExecute = nameof(CanClockOut))] private Task ClockOutAtDefaultTimeAsync() => PerformClockOutAsync(DateTime.Today + _userSetting.DefaultClockOutTime.ToTimeSpan());
        [RelayCommand(CanExecute = nameof(CanClockOut))] private Task ClockOutAtCustomTimeAsync() => PerformClockOutAsync(DateTime.Today + SelectedCustomTime.ToTimeSpan());
        [RelayCommand(CanExecute = nameof(CanResolveForgottenSession))] private Task ResolveForgottenSessionAtDefaultTimeAsync() => PerformClockOutAsync(_openAttendanceLog!.ClockIn.Date + _userSetting.DefaultClockOutTime.ToTimeSpan());
        [RelayCommand(CanExecute = nameof(CanResolveForgottenSession))] private Task ResolveForgottenSessionAtCustomTimeAsync() => PerformClockOutAsync(_openAttendanceLog!.ClockIn.Date + ForgottenSessionClockOutTimeOfDay.ToTimeSpan());
        #endregion

        #region Methods

        /// <inheritdoc/>
        public override void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion

        #region CanExecute
        private bool CanClockIn() => !IsBusy && DayState == AttendanceDayState.NotClockedIn;
        private bool CanClockOut() => !IsBusy && DayState == AttendanceDayState.ClockedIn;
        private bool CanResolveForgottenSession() => !IsBusy && DayState == AttendanceDayState.ForgottenSession;
        #endregion

        #region Helpers
        private async Task InitializeAsync()
        {
            try
            {
                IsBusy = true;
                var ct = CreateCancellationToken();
                var userId = _currentUserContext.UserId;

                await using var dbContext = _localDbFactory.CreateDbContext();
                _userSetting = await dbContext.GetUserSettingAsync(userId, ct) ?? await dbContext.CreateUserSettingAsync(userId, ct);
                DefaultClockInTimeOfDay = _userSetting.DefaultClockInTime;
                DefaultClockOutTimeOfDay = _userSetting.DefaultClockOutTime;
                TargetShiftDuration = TimeSpan.FromHours((double)_userSetting.TargetShiftHours);

                await RefreshDayStateAsync(dbContext, ct);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load home state for user {UserId}.", _currentUserContext.UserId);
                Message = MSG.error_homeLoadUnexpected;
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task PerformClockInAsync(DateTime clockInTime)
        {
            if (clockInTime > DateTime.Now)
            {
                Message = MSG.error_clockInTimeInFuture;
                return;
            }
            try
            {
                IsBusy = true;
                var ct = CreateCancellationToken();
                await using var dbContext = _localDbFactory.CreateDbContext();
                await dbContext.ClockInAsync(_currentUserContext.UserId, clockInTime, ct);
                await RefreshDayStateAsync(dbContext, ct);
                Message = null;
            }
            catch (DbUpdateException ex)
            {
                _logger.Error(ex, "Failed to clock in user {UserId}: attendance log already exists for today.", _currentUserContext.UserId);
                Message = MSG.error_alreadyClockedInToday;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error clocking in user {UserId}.", _currentUserContext.UserId);
                Message = MSG.error_clockInUnexpected;
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task PerformClockOutAsync(DateTime clockOutTime)
        {
            if (_openAttendanceLog is null)
            {
                Message = MSG.error_notClockedIn;
                return;
            }
            if (clockOutTime <= _openAttendanceLog.ClockIn)
            {
                Message = MSG.error_clockOutBeforeClockIn;
                return;
            }
            if (clockOutTime > DateTime.Now)
            {
                Message = MSG.error_clockOutTimeInFuture;
                return;
            }
            try
            {
                IsBusy = true;
                var ct = CreateCancellationToken();
                await using var dbContext = _localDbFactory.CreateDbContext();
                await dbContext.ClockOutAsync(_currentUserContext.UserId, clockOutTime, ct);
                await RefreshDayStateAsync(dbContext, ct);
                Message = null;
            }
            catch (InvalidOperationException ex)
            {
                _logger.Error(ex, "Failed to clock out user {UserId}: no open attendance session.", _currentUserContext.UserId);
                Message = MSG.error_notClockedIn;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error clocking out user {UserId}.", _currentUserContext.UserId);
                Message = MSG.error_clockOutUnexpected;
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task RefreshDayStateAsync(LocalDbContext dbContext, CancellationToken ct)
        {
            var userId = _currentUserContext.UserId;
            var openLog = await dbContext.GetActiveAttendanceLogAsync(userId, ct);

            if (openLog is not null)
            {
                _openAttendanceLog = openLog;
                _todayClosedAttendanceLog = null;
                var isToday = openLog.WorkDate == DateTime.Today.ToString("yyyy-MM-dd");
                DayState = isToday ? AttendanceDayState.ClockedIn : AttendanceDayState.ForgottenSession;
                ClockInTime = openLog.ClockIn;
                ClockOutTime = null;
                WorkedToday = isToday ? TimeBudgetCalculator.CalculateNetWorkedTime(openLog, _userSetting, DateTime.Now) : TimeSpan.Zero;
                if (!isToday)
                {
                    ForgottenSessionDate = openLog.ClockIn.Date;
                    ForgottenSessionClockOutTimeOfDay = _userSetting.DefaultClockOutTime;
                }
            }
            else
            {
                var todayStart = DateTime.Today;
                var todayEnd = todayStart.AddDays(1).AddTicks(-1);
                var todayLog = (await dbContext.GetAttendanceLogsAsync(userId, todayStart, todayEnd, ct)).FirstOrDefault();

                _openAttendanceLog = null;
                _todayClosedAttendanceLog = todayLog;
                DayState = todayLog is not null ? AttendanceDayState.DayComplete : AttendanceDayState.NotClockedIn;
                ClockInTime = todayLog?.ClockIn;
                ClockOutTime = todayLog?.ClockOut;
                WorkedToday = todayLog is not null ? TimeBudgetCalculator.CalculateNetWorkedTime(todayLog, _userSetting, todayLog.ClockOut!.Value) : TimeSpan.Zero;
                if (todayLog is null)
                {
                    SelectedCustomTime = TimeOnly.FromDateTime(DateTime.Now);
                }
            }

            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthLogs = await dbContext.GetAttendanceLogsAsync(userId, monthStart, DateTime.Now, ct);
            RollingMonthlyBalance = TimeBudgetCalculator.CalculateRollingBudget(monthLogs, _userSetting);
        }
        private void NotifyCanExecuteChanged()
        {
            _dispatcherService.Run(() =>
            {
                ClockInNowCommand.NotifyCanExecuteChanged();
                ClockInAtDefaultTimeCommand.NotifyCanExecuteChanged();
                ClockInAtCustomTimeCommand.NotifyCanExecuteChanged();
                ClockOutNowCommand.NotifyCanExecuteChanged();
                ClockOutAtDefaultTimeCommand.NotifyCanExecuteChanged();
                ClockOutAtCustomTimeCommand.NotifyCanExecuteChanged();
                ResolveForgottenSessionAtDefaultTimeCommand.NotifyCanExecuteChanged();
                ResolveForgottenSessionAtCustomTimeCommand.NotifyCanExecuteChanged();
            });
        }
        private CancellationToken CreateCancellationToken()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new();
            return _cts.Token;
        }
        #endregion
    }
}
