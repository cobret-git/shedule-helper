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
    /// attendance state, the currently tracked project, and the rolling monthly balance; clock
    /// in/out and project switching all start here.
    /// </summary>
    public sealed class HomeScreen : IScreen
    {
        #region Fields

        private readonly IAttendanceService _attendanceService;
        private readonly ITrackingService _trackingService;
        private readonly IReportingService _reportingService;
        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly IPathProvider _pathProvider;
        private readonly ILogger _logger = Log.ForContext<HomeScreen>();

        private AttendanceDaySnapshot? _snapshot;
        private ProjectTimeLog? _activeTracking;
        private List<ProjectTimeLog> _todaysLogs = new();
        private string? _message;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeScreen"/> class.
        /// </summary>
        public HomeScreen(
            IAttendanceService attendanceService,
            ITrackingService trackingService,
            IReportingService reportingService,
            ILocalDbContextFactory dbContextFactory,
            ICurrentUserContext currentUserContext,
            IPathProvider pathProvider)
        {
            _attendanceService = attendanceService;
            _trackingService = trackingService;
            _reportingService = reportingService;
            _dbContextFactory = dbContextFactory;
            _currentUserContext = currentUserContext;
            _pathProvider = pathProvider;
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
                case ConsoleKey.P:
                    await screens.Push(new ProjectsScreen(_dbContextFactory, _currentUserContext));
                    break;
                case ConsoleKey.F10:
                    await screens.Push(new SettingsScreen(_dbContextFactory, _currentUserContext, _pathProvider));
                    break;
                case ConsoleKey.I when snapshot.DayState == AttendanceDayState.NotClockedIn:
                    await ClockInAsync(DateTime.Now);
                    break;
                case ConsoleKey.D when snapshot.DayState == AttendanceDayState.NotClockedIn:
                    await ClockInAsync(DateTime.Today + snapshot.UserSetting.DefaultClockInTime.ToTimeSpan());
                    break;
                case ConsoleKey.M when snapshot.DayState == AttendanceDayState.NotClockedIn:
                    await screens.Push(new TimeEntryScreen("CLOCK IN", "Clock in at", TimeOnly.FromDateTime(DateTime.Now), async (time, s) =>
                    {
                        var error = await TryClockInAsync(DateTime.Today + time.ToTimeSpan());
                        if (error is null)
                        {
                            await s.Pop();
                        }

                        return error;
                    }));
                    break;
                case ConsoleKey.O when snapshot.DayState == AttendanceDayState.ClockedIn:
                    await screens.Push(new ClockOutScreen(_attendanceService, _currentUserContext, snapshot));
                    break;
                case ConsoleKey.S when snapshot.DayState == AttendanceDayState.ClockedIn:
                    await screens.Push(new SwitchScreen(_trackingService, _dbContextFactory, _currentUserContext, snapshot.OpenAttendanceLog!.Id));
                    break;
                case ConsoleKey.R when snapshot.DayState == AttendanceDayState.ForgottenSession:
                    await screens.Push(new ResolveForgottenScreen(_attendanceService, _currentUserContext, snapshot));
                    break;
                case ConsoleKey.R:
                    await screens.Push(new ReportsScreen(_reportingService, _currentUserContext));
                    break;
            }
        }

        #endregion

        #region Helpers

        private void RenderClockedIn(Frame frame, AttendanceDaySnapshot snapshot)
        {
            var openLog = snapshot.OpenAttendanceLog!;
            frame.Write(1, 3, "● CLOCKED IN", ColorToken.Positive);
            frame.Write(14, 3, $"since {Formatting.Time(openLog.ClockIn)}", ColorToken.Dim);

            var target = TimeSpan.FromHours((double)snapshot.UserSetting.TargetShiftHours);
            var ratio = target > TimeSpan.Zero ? snapshot.WorkedToday.TotalSeconds / target.TotalSeconds : 0;
            ProgressBar.Draw(frame, 1, 4, 40, ratio, $"{Formatting.Duration(snapshot.WorkedToday)} / {Formatting.Duration(target)}");

            if (_activeTracking is { } tracking)
            {
                var label = tracking.Task is not null ? $"{tracking.Project.Name} / {tracking.Task.Title}" : tracking.Project.Name;
                frame.Write(1, 6, $"Active   ▶ {label}");
                frame.WriteRight(frame.Width - 1, 6, Formatting.Duration(DateTime.Now - tracking.StartTime));
                frame.Write(3, 7, $"started {Formatting.Time(tracking.StartTime)}", ColorToken.Dim);
            }
            else
            {
                frame.Write(1, 6, "Active   no project - press S to start tracking", ColorToken.Dim);
            }

            if (_todaysLogs.Count > 0)
            {
                frame.Write(1, 9, "Today", ColorToken.Accent);
                var stripWidth = Math.Min(60, frame.Width - 2);
                var blocks = _todaysLogs.Select(l => new TimelineBlock(l.StartTime, l.EndTime)).ToList();
                TimelineStrip.Draw(frame, 1, 10, stripWidth, openLog.ClockIn, DateTime.Now, blocks);
            }

            KeyBar.Draw(frame, ("S", "Switch"), ("O", "Clock out"), ("P", "Projects"), ("R", "Reports"), ("F10", "Settings"), ("Q", "Quit"));
        }

        private static void RenderNotClockedIn(Frame frame, AttendanceDaySnapshot snapshot)
        {
            frame.Write(1, 3, "○ NOT CLOCKED IN", ColorToken.Dim);

            var target = TimeSpan.FromHours((double)snapshot.UserSetting.TargetShiftHours);
            ProgressBar.Draw(frame, 1, 4, 40, 0, $"0h 00m / {Formatting.Duration(target)}");

            frame.Write(1, 6, "Good day. Ready when you are.");
            frame.Write(3, 8, $"I   Clock in now                     {Formatting.Time(DateTime.Now)}");
            frame.Write(3, 9, $"D   Clock in at default              {Formatting.Time(snapshot.UserSetting.DefaultClockInTime)}");
            frame.Write(3, 10, "M   Clock in at custom time...");

            KeyBar.Draw(frame, ("I", "Now"), ("D", "Default"), ("M", "Custom"), ("P", "Projects"), ("R", "Reports"), ("F10", "Settings"), ("Q", "Quit"));
        }

        private static void RenderDayComplete(Frame frame, AttendanceDaySnapshot snapshot)
        {
            var log = snapshot.TodayClosedAttendanceLog!;
            frame.Write(1, 3, "✓ DAY COMPLETE", ColorToken.Dim);
            frame.Write(1, 5, $"{Formatting.Time(log.ClockIn)} -> {Formatting.Time(log.ClockOut!.Value)}    worked {Formatting.Duration(snapshot.WorkedToday)}");

            KeyBar.Draw(frame, ("P", "Projects"), ("R", "Reports"), ("F10", "Settings"), ("Q", "Quit"));
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
            var error = await TryClockInAsync(time);
            if (error is not null)
            {
                _message = error;
            }
        }

        /// <summary>
        /// Clocks in and returns an error message on failure instead of setting <see cref="_message"/>
        /// directly - shared by the direct <c>I</c>/<c>D</c> key handlers above (which do want it on
        /// <see cref="_message"/>) and <see cref="TimeEntryScreen"/>'s callback (which shows the
        /// error on itself so the user can correct the time and retry, rather than losing that
        /// context by bouncing back to Home).
        /// </summary>
        private async Task<string?> TryClockInAsync(DateTime time)
        {
            try
            {
                _snapshot = await _attendanceService.ClockInAsync(_currentUserContext.UserId, time, CancellationToken.None);
                _message = null;
                return null;
            }
            catch (AttendanceOperationException ex)
            {
                return ex.Message;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clock in user {UserId}.", _currentUserContext.UserId);
                return "Something went wrong clocking in.";
            }
        }

        private async Task RefreshAsync()
        {
            try
            {
                var snapshot = await _attendanceService.GetDaySnapshotAsync(_currentUserContext.UserId, CancellationToken.None);
                _snapshot = snapshot;

                if (snapshot.DayState == AttendanceDayState.ClockedIn)
                {
                    var openLogId = snapshot.OpenAttendanceLog!.Id;
                    _activeTracking = await _trackingService.GetActiveTrackingAsync(openLogId, CancellationToken.None);

                    await using var db = _dbContextFactory.CreateDbContext();
                    _todaysLogs = await db.GetProjectTimeLogsAsync(_currentUserContext.UserId, DateTime.Today, DateTime.Now, CancellationToken.None);
                }
                else
                {
                    _activeTracking = null;
                    _todaysLogs = new List<ProjectTimeLog>();
                }
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
