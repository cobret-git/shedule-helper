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
    /// Owns the start-of-day resolution (<see cref="IAttendanceService.ResolveDayStartAsync"/>):
    /// it runs on the first <see cref="OnEnter"/>, before the first frame is drawn, and again from
    /// <see cref="OnTick"/> if the date rolls over while the app is left running - so "open the app
    /// in the morning" and "never close the app" behave the same way. Whatever it changed is
    /// reported in a banner rather than applied silently.
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

        private DateTime _resolvedDate = DateTime.MinValue;
        private DayStartResolution? _resolution;
        private bool _bannerDismissed;

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
        public Task OnEnter()
        {
            // Popping back from another screen re-enters Home, so the day is resolved per calendar
            // date rather than per activation - otherwise a trip to Settings would re-run it.
            return _resolvedDate == DateTime.Today ? RefreshAsync() : ResolveDayStartAsync();
        }

        /// <inheritdoc/>
        public Task OnTick()
        {
            return _resolvedDate == DateTime.Today ? Task.CompletedTask : ResolveDayStartAsync();
        }

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
            RenderBalance(frame, snapshot);

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

            RenderBanner(frame);

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
                case ConsoleKey.Escape when _resolution is not null && !_bannerDismissed:
                    _bannerDismissed = true;
                    break;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Draws the monthly balance, plus today's still-moving contribution to it as a separate
        /// figure. The two are kept apart rather than summed because they answer different
        /// questions - the first is banked, the second is a projection that a late clock-out will
        /// still change - and because <see cref="AttendanceDaySnapshot.RollingMonthlyBalance"/>
        /// counts completed days only, so on its own it looks frozen all day.
        /// </summary>
        private static void RenderBalance(Frame frame, AttendanceDaySnapshot snapshot)
        {
            var balanceColor = snapshot.RollingMonthlyBalance >= TimeSpan.Zero ? ColorToken.Positive : ColorToken.Negative;
            var openDayBalance = snapshot.OpenDayBalanceAsOf(DateTime.Now);

            if (openDayBalance is not { } today)
            {
                frame.WriteRight(frame.Width - 1, 3, $"Balance  {Formatting.Balance(snapshot.RollingMonthlyBalance)}", balanceColor);
                return;
            }

            var todayText = $" · today {Formatting.Balance(today)}";
            frame.WriteRight(frame.Width - 1, 3, todayText, ColorToken.Dim);
            frame.WriteRight(frame.Width - 1 - todayText.Length, 3, $"Balance  {Formatting.Balance(snapshot.RollingMonthlyBalance)}", balanceColor);
        }

        private void RenderClockedIn(Frame frame, AttendanceDaySnapshot snapshot)
        {
            var openLog = snapshot.OpenAttendanceLog!;
            frame.Write(1, 3, "● CLOCKED IN", ColorToken.Positive);
            frame.Write(14, 3, $"since {Formatting.Time(openLog.ClockIn)}", ColorToken.Dim);

            // Recomputed per frame rather than read off the snapshot: the loop redraws every second,
            // so this is what makes the bar and the worked figure tick along with the clock instead
            // of freezing at whatever they were when the screen was last loaded. Pure arithmetic
            // over data already in hand - no database access from a render.
            var target = snapshot.DailyTarget;
            var worked = snapshot.WorkedAsOf(DateTime.Now);
            var overtime = worked > target ? worked - target : TimeSpan.Zero;

            // The bar's scale grows to keep a fixed ~45-minute buffer visible past the current
            // position, so it never looks saturated the moment the target - or any amount of
            // overtime - is reached; the buffer is what renders as empty/dim past the fill.
            var headroom = TimeSpan.FromMinutes(45);
            var scale = target + overtime + headroom;
            var normalWorked = worked < target ? worked : target;
            var normalRatio = scale > TimeSpan.Zero ? normalWorked.TotalSeconds / scale.TotalSeconds : 0;
            var overtimeRatio = scale > TimeSpan.Zero ? overtime.TotalSeconds / scale.TotalSeconds : 0;

            var caption = overtime > TimeSpan.Zero
                ? $"{Formatting.Duration(worked)} / {Formatting.Duration(target)} ({Formatting.Balance(overtime)})"
                : $"{Formatting.Duration(worked)} / {Formatting.Duration(target)}";
            ProgressBar.Draw(frame, 1, 4, 40, normalRatio, overtimeRatio, caption);

            var nextRow = 9;

            if (_activeTracking is { } tracking)
            {
                // Project and task each get their own line and their own truncation budget -
                // joining them with " / " on one line (the old behaviour) meant a long project name
                // could push the task title, or even the elapsed-time readout, clean off the edge.
                const string prefix = "Active   ▶ ";
                var elapsed = Formatting.Duration(DateTime.Now - tracking.StartTime);
                var projectAvailable = Math.Max(1, frame.Width - 1 - prefix.Length - elapsed.Length - 2);
                frame.Write(1, 6, $"{prefix}{Formatting.Truncate(tracking.Project.Name, projectAvailable)}");
                frame.WriteRight(frame.Width - 1, 6, elapsed);

                var detailRow = 7;
                if (tracking.Task is not null)
                {
                    var taskAvailable = Math.Max(1, frame.Width - 1 - 3);
                    frame.Write(3, detailRow, Formatting.Truncate(tracking.Task.Title, taskAvailable), ColorToken.Dim);
                    detailRow++;
                }

                frame.Write(3, detailRow, $"started {Formatting.Time(tracking.StartTime)}", ColorToken.Dim);
                nextRow = detailRow + 2;
            }
            else
            {
                frame.Write(1, 6, "Active   no project - press S to start tracking", ColorToken.Dim);
            }

            RenderToday(frame, nextRow);

            KeyBar.Draw(frame, ("S", "Switch"), ("O", "Clock out"), ("P", "Projects"), ("R", "Reports"), ("F10", "Settings"), ("Q", "Quit"));
        }

        private void RenderNotClockedIn(Frame frame, AttendanceDaySnapshot snapshot)
        {
            frame.Write(1, 3, "○ NOT CLOCKED IN", ColorToken.Dim);

            ProgressBar.Draw(frame, 1, 4, 40, 0, $"0h 00m / {Formatting.Duration(snapshot.DailyTarget)}");

            frame.Write(1, 6, "Good day. Ready when you are.");
            frame.Write(3, 8, $"I   Clock in now                     {Formatting.Time(DateTime.Now)}");
            frame.Write(3, 9, $"D   Clock in at default              {Formatting.Time(snapshot.UserSetting.DefaultClockInTime)}");
            frame.Write(3, 10, "M   Clock in at custom time...");

            RenderToday(frame, 12);

            KeyBar.Draw(frame, ("I", "Now"), ("D", "Default"), ("M", "Custom"), ("P", "Projects"), ("R", "Reports"), ("F10", "Settings"), ("Q", "Quit"));
        }

        private void RenderDayComplete(Frame frame, AttendanceDaySnapshot snapshot)
        {
            var log = snapshot.TodayClosedAttendanceLog!;
            frame.Write(1, 3, "✓ DAY COMPLETE", ColorToken.Dim);
            frame.Write(1, 5, $"{Formatting.Time(log.ClockIn)} -> {Formatting.Time(log.ClockOut!.Value)}    worked {Formatting.Duration(snapshot.WorkedToday)}");

            RenderToday(frame, 7);

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

        /// <summary>
        /// Draws what the start-of-day resolution changed, so automation is never invisible - the
        /// user sees which timestamps were written on their behalf and can correct any of them with
        /// the ordinary clock-out and switch commands. Dismissed with <c>Esc</c>, and gone by the
        /// next launch regardless.
        /// </summary>
        private void RenderBanner(Frame frame)
        {
            if (_bannerDismissed || _resolution is not { DidSomething: true } resolution)
            {
                return;
            }

            // Two lines rather than one: the attendance edits and the tracking change are separate
            // facts, and a project/task pair is long enough that joining all three overruns 80
            // columns and collides with the dismiss hint.
            var attendance = new List<string>();

            if (resolution.ClosedForgottenDay is { } closed)
            {
                attendance.Add($"closed {closed:ddd d MMM} at {Formatting.Time(closed)}");
            }

            if (resolution.ClockedIn is { } clockedIn)
            {
                attendance.Add($"clocked in {Formatting.Time(clockedIn)}");
            }

            var tracking = resolution.Resume?.Outcome switch
            {
                TrackingResumeOutcome.Resumed when resolution.Resume.TaskTitle is not null => $"resumed {resolution.Resume.ProjectName} / {resolution.Resume.TaskTitle}",
                TrackingResumeOutcome.Resumed => $"resumed {resolution.Resume.ProjectName}",
                TrackingResumeOutcome.ResumedWithoutTask => $"resumed {resolution.Resume.ProjectName} - its task is done, pick the next with S",
                TrackingResumeOutcome.ProjectUnavailable => $"{resolution.Resume.ProjectName} is archived, nothing resumed",
                _ => null,
            };

            const string dismiss = "Esc dismiss";
            var firstRow = frame.Height - 5;
            var available = frame.Width - 2 - dismiss.Length - 2;

            frame.Write(1, firstRow, Formatting.Truncate($"⚙ Auto · {string.Join(" · ", attendance)}", available), ColorToken.Accent);
            frame.WriteRight(frame.Width - 1, firstRow, dismiss, ColorToken.Dim);

            if (tracking is not null)
            {
                frame.Write(9, firstRow + 1, Formatting.Truncate(tracking, frame.Width - 10), ColorToken.Accent);
            }
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
                var snapshot = await _attendanceService.ClockInAsync(_currentUserContext.UserId, time, CancellationToken.None);
                _snapshot = snapshot;
                _message = null;

                // Resuming is tied to the clock-in itself, not to how it was triggered, so a manual
                // clock-in continues yesterday's work exactly as an automatic one does.
                if (snapshot.UserSetting.ResumeTrackingOnClockIn && snapshot.OpenAttendanceLog is { } openLog)
                {
                    await ResumeTrackingAsync(openLog.Id, time);
                }

                await RefreshAsync();
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

        /// <summary>
        /// Continues the previously tracked project after a manual clock-in, reusing the banner to
        /// report it. A failure here is deliberately not surfaced as a clock-in error: the clock-in
        /// itself succeeded, and the user can always pick a project with <c>S</c>.
        /// </summary>
        private async Task ResumeTrackingAsync(int attendanceLogId, DateTime startTime)
        {
            try
            {
                var resume = await _trackingService.ResumeLastAsync(_currentUserContext.UserId, attendanceLogId, startTime, CancellationToken.None);
                if (resume.Outcome is TrackingResumeOutcome.Resumed or TrackingResumeOutcome.ResumedWithoutTask or TrackingResumeOutcome.ProjectUnavailable)
                {
                    _resolution = new DayStartResolution(_snapshot!, _snapshot!.UserSetting.DayStartAutomation, null, startTime, resume, false);
                    _bannerDismissed = false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to resume tracking for user {UserId}.", _currentUserContext.UserId);
            }
        }

        /// <summary>
        /// Applies the user's day-start automation, then loads the rest of Home's state. Records the
        /// date it ran for so re-entering Home doesn't repeat it, and so a date rollover under a
        /// running app does - the date is stamped before the work, so a failure surfaces as a
        /// message rather than retrying every tick.
        /// </summary>
        private async Task ResolveDayStartAsync()
        {
            _resolvedDate = DateTime.Today;

            try
            {
                var resolution = await _attendanceService.ResolveDayStartAsync(_currentUserContext.UserId, DateTime.Now, CancellationToken.None);
                _snapshot = resolution.Snapshot;

                if (resolution.DidSomething)
                {
                    _resolution = resolution;
                    _bannerDismissed = false;
                    _logger.Information(
                        "Day-start automation for user {UserId}: closed {ClosedForgottenDay}, clocked in {ClockedIn}, resume {ResumeOutcome}.",
                        _currentUserContext.UserId,
                        resolution.ClosedForgottenDay,
                        resolution.ClockedIn,
                        resolution.Resume?.Outcome);
                }
            }
            catch (AttendanceOperationException ex)
            {
                _message = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to resolve the day start for user {UserId}.", _currentUserContext.UserId);
                _message = "Failed to resolve today's state automatically.";
            }

            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            try
            {
                var snapshot = await _attendanceService.GetDaySnapshotAsync(_currentUserContext.UserId, CancellationToken.None);
                _snapshot = snapshot;

                await using var db = _dbContextFactory.CreateDbContext();

                if (snapshot.DayState == AttendanceDayState.ClockedIn)
                {
                    var openLogId = snapshot.OpenAttendanceLog!.Id;
                    _activeTracking = await _trackingService.GetActiveTrackingAsync(openLogId, CancellationToken.None);
                }
                else
                {
                    _activeTracking = null;
                }

                _todaysLogs = await db.GetProjectTimeLogsAsync(_currentUserContext.UserId, DateTime.Today, DateTime.Now, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load home state for user {UserId}.", _currentUserContext.UserId);
                _message = "Failed to load today's state.";
            }
        }

        /// <summary>
        /// Draws today's finished project/task sessions, most recent first - what came before the
        /// currently active one (if any), so Home stays informative beyond just "what's active now".
        /// The still-open segment (if any) is excluded here since it's already shown as "Active".
        /// </summary>
        private void RenderToday(Frame frame, int y)
        {
            var finished = _todaysLogs.Where(l => l.EndTime is not null).OrderByDescending(l => l.StartTime).ToList();
            if (finished.Count == 0)
            {
                return;
            }

            frame.Write(1, y, "Today", ColorToken.Accent);
            frame.Rule(y + 1);

            for (var i = 0; i < finished.Count; i++)
            {
                var log = finished[i];
                var label = log.Task is not null ? $"{log.Project.Name} / {log.Task.Title}" : log.Project.Name;
                var row = y + 2 + i;

                var prefix = $"{Formatting.Time(log.StartTime)}-{Formatting.Time(log.EndTime!.Value)}  ";
                var duration = Formatting.Duration(log.EndTime.Value - log.StartTime);
                var labelAvailable = Math.Max(1, frame.Width - 1 - prefix.Length - duration.Length - 2);

                frame.Write(1, row, $"{prefix}{Formatting.Truncate(label, labelAvailable)}", ColorToken.Dim);
                frame.WriteRight(frame.Width - 1, row, duration, ColorToken.Dim);
            }
        }

        #endregion
    }
}
