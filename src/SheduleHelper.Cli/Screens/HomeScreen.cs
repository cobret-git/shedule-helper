using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// The Home screen (The Daily Control Center) - the app's landing screen. Shows today's
    /// attendance state and the rolling monthly balance, and is where clock-in/out starts.
    /// </summary>
    public sealed class HomeScreen : IScreen
    {
        #region Fields

        private readonly IAttendanceService _attendanceService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ILogger _logger = Log.ForContext<HomeScreen>();

        private AttendanceDaySnapshot? _snapshot;
        private string? _message;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeScreen"/> class.
        /// </summary>
        public HomeScreen(IAttendanceService attendanceService, ICurrentUserContext currentUserContext)
        {
            _attendanceService = attendanceService;
            _currentUserContext = currentUserContext;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public Task OnEnter() => RefreshAsync();

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, "SCHEDULE HELPER", DateTime.Now.ToString("ddd d MMM   HH:mm"));

            if (_snapshot is null)
            {
                frame.Write(1, 3, "Loading...", ColorToken.Dim);
                return;
            }

            var snapshot = _snapshot;
            var balanceColor = snapshot.RollingMonthlyBalance >= TimeSpan.Zero ? ColorToken.Positive : ColorToken.Negative;
            frame.WriteRight(frame.Width - 1, 3, $"Balance  {Formatting.Balance(snapshot.RollingMonthlyBalance)}", balanceColor);

            switch (snapshot.DayState)
            {
                case AttendanceDayState.ClockedIn:
                    RenderClockedIn(frame, snapshot);
                    break;
                case AttendanceDayState.NotClockedIn:
                    RenderNotClockedIn(frame, snapshot);
                    break;
                case AttendanceDayState.DayComplete:
                    RenderDayComplete(frame, snapshot);
                    break;
                case AttendanceDayState.ForgottenSession:
                    RenderForgottenSession(frame, snapshot);
                    break;
            }

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 3, _message, ColorToken.Negative);
            }
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            if (_snapshot is not { } snapshot)
            {
                return;
            }

            switch (key.Key)
            {
                case ConsoleKey.F1:
                    await screens.Push(new HelpScreen());
                    break;
                case ConsoleKey.Q:
                    await screens.Quit();
                    break;
                case ConsoleKey.I when snapshot.DayState == AttendanceDayState.NotClockedIn:
                    await ClockInAsync(DateTime.Now);
                    break;
                case ConsoleKey.D when snapshot.DayState == AttendanceDayState.NotClockedIn:
                    await ClockInAsync(DateTime.Today + snapshot.UserSetting.DefaultClockInTime.ToTimeSpan());
                    break;
                case ConsoleKey.O when snapshot.DayState == AttendanceDayState.ClockedIn:
                    await screens.Push(new ClockOutScreen(_attendanceService, _currentUserContext, snapshot));
                    break;
                case ConsoleKey.R when snapshot.DayState == AttendanceDayState.ForgottenSession:
                    await screens.Push(new ResolveForgottenScreen(_attendanceService, _currentUserContext, snapshot));
                    break;
            }
        }

        #endregion

        #region Helpers

        private static void RenderClockedIn(Frame frame, AttendanceDaySnapshot snapshot)
        {
            var openLog = snapshot.OpenAttendanceLog!;
            frame.Write(1, 3, "● CLOCKED IN", ColorToken.Positive);
            frame.Write(14, 3, $"since {Formatting.Time(openLog.ClockIn)}", ColorToken.Dim);

            var target = TimeSpan.FromHours((double)snapshot.UserSetting.TargetShiftHours);
            var ratio = target > TimeSpan.Zero ? snapshot.WorkedToday.TotalSeconds / target.TotalSeconds : 0;
            ProgressBar.Draw(frame, 1, 4, 40, ratio, $"{Formatting.Duration(snapshot.WorkedToday)} / {Formatting.Duration(target)}");

            KeyBar.Draw(frame, ("O", "Clock out"), ("F1", "Help"), ("Q", "Quit"));
        }

        private static void RenderNotClockedIn(Frame frame, AttendanceDaySnapshot snapshot)
        {
            frame.Write(1, 3, "○ NOT CLOCKED IN", ColorToken.Dim);

            var target = TimeSpan.FromHours((double)snapshot.UserSetting.TargetShiftHours);
            ProgressBar.Draw(frame, 1, 4, 40, 0, $"0h 00m / {Formatting.Duration(target)}");

            frame.Write(1, 6, "Good day. Ready when you are.");
            frame.Write(3, 8, $"I   Clock in now                     {Formatting.Time(DateTime.Now)}");
            frame.Write(3, 9, $"D   Clock in at default              {Formatting.Time(snapshot.UserSetting.DefaultClockInTime)}");

            KeyBar.Draw(frame, ("I", "Clock in now"), ("D", "Clock in at default"), ("F1", "Help"), ("Q", "Quit"));
        }

        private static void RenderDayComplete(Frame frame, AttendanceDaySnapshot snapshot)
        {
            var log = snapshot.TodayClosedAttendanceLog!;
            frame.Write(1, 3, "✓ DAY COMPLETE", ColorToken.Dim);
            frame.Write(1, 5, $"{Formatting.Time(log.ClockIn)} -> {Formatting.Time(log.ClockOut!.Value)}    worked {Formatting.Duration(snapshot.WorkedToday)}");

            KeyBar.Draw(frame, ("F1", "Help"), ("Q", "Quit"));
        }

        private static void RenderForgottenSession(Frame frame, AttendanceDaySnapshot snapshot)
        {
            var openLog = snapshot.OpenAttendanceLog!;
            frame.Write(1, 3, "⚠ UNFINISHED DAY", ColorToken.Warning);
            frame.Write(1, 5, $"{openLog.ClockIn:ddd d MMM} - clocked in at {Formatting.Time(openLog.ClockIn)}, never clocked out.", ColorToken.Dim);
            frame.Write(1, 6, "Close it before starting today's clock-in.", ColorToken.Dim);

            KeyBar.Draw(frame, ("R", "Resolve"), ("F1", "Help"), ("Q", "Quit"));
        }

        private async Task ClockInAsync(DateTime time)
        {
            try
            {
                _snapshot = await _attendanceService.ClockInAsync(_currentUserContext.UserId, time, CancellationToken.None);
                _message = null;
            }
            catch (AttendanceOperationException ex)
            {
                _message = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clock in user {UserId}.", _currentUserContext.UserId);
                _message = "Something went wrong clocking in.";
            }
        }

        private async Task RefreshAsync()
        {
            try
            {
                _snapshot = await _attendanceService.GetDaySnapshotAsync(_currentUserContext.UserId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load home state for user {UserId}.", _currentUserContext.UserId);
                _message = "Failed to load today's state.";
            }
        }

        #endregion
    }
}
