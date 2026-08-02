using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Pushed from <see cref="HomeScreen"/> when the user presses <c>R</c> during a
    /// <see cref="AttendanceDayState.ForgottenSession"/> - a previous day's attendance log was
    /// never closed. <c>Esc</c> backs out without resolving anything, so a user who isn't ready to
    /// deal with it yet isn't forced to; <see cref="HomeScreen"/> keeps showing the warning (and the
    /// <c>R</c> shortcut) either way.
    /// </summary>
    public sealed class ResolveForgottenScreen : IScreen
    {
        #region Fields

        private readonly IAttendanceService _attendanceService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AttendanceDaySnapshot _snapshot;
        private readonly ILogger _logger = Log.ForContext<ResolveForgottenScreen>();

        private string? _message;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ResolveForgottenScreen"/> class.
        /// </summary>
        public ResolveForgottenScreen(IAttendanceService attendanceService, ICurrentUserContext currentUserContext, AttendanceDaySnapshot snapshot)
        {
            _attendanceService = attendanceService;
            _currentUserContext = currentUserContext;
            _snapshot = snapshot;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, "SCHEDULE HELPER", DateTime.Now.ToString("ddd d MMM   HH:mm"));

            var openLog = _snapshot.OpenAttendanceLog!;
            var defaultClockOut = openLog.ClockIn.Date + _snapshot.UserSetting.DefaultClockOutTime.ToTimeSpan();

            frame.Write(1, 3, "⚠ UNFINISHED DAY", ColorToken.Warning);
            frame.Write(3, 5, $"{openLog.ClockIn:ddd d MMM} - clocked in at {Formatting.Time(openLog.ClockIn)}, never clocked out.");
            frame.Write(3, 6, "Close it before starting today.");

            frame.Write(3, 8, $"D   Clock out at default          {openLog.ClockIn:ddd d MMM}  {Formatting.Time(defaultClockOut)}");

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("D", "Clock out at default"), ("Esc", "Decide later"), ("Q", "Quit"));
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            switch (key.Key)
            {
                case ConsoleKey.D:
                    await ResolveAsync(screens);
                    break;
                case ConsoleKey.Escape:
                    await screens.Pop();
                    break;
                case ConsoleKey.Q:
                    await screens.Quit();
                    break;
            }
        }

        #endregion

        #region Helpers

        private async Task ResolveAsync(ScreenStack screens)
        {
            var openLog = _snapshot.OpenAttendanceLog!;
            var clockOutTime = openLog.ClockIn.Date + _snapshot.UserSetting.DefaultClockOutTime.ToTimeSpan();

            try
            {
                await _attendanceService.ClockOutAsync(_currentUserContext.UserId, clockOutTime, CancellationToken.None);
                await screens.Pop();
            }
            catch (AttendanceOperationException ex)
            {
                _message = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to resolve forgotten session for user {UserId}.", _currentUserContext.UserId);
                _message = "Something went wrong closing that day.";
            }
        }

        #endregion
    }
}
