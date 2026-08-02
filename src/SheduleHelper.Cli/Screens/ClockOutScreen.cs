using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Pushed from <see cref="HomeScreen"/> when the user presses <c>O</c> while clocked in. Offers
    /// a short list of clock-out times; a custom time will land alongside the general
    /// <c>TextField</c> widget later, so for now this only offers "now" and the default.
    /// </summary>
    public sealed class ClockOutScreen : IScreen
    {
        #region Fields

        private readonly IAttendanceService _attendanceService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AttendanceDaySnapshot _snapshot;
        private readonly SelectList _options = new(2);
        private readonly ILogger _logger = Log.ForContext<ClockOutScreen>();

        private string? _message;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ClockOutScreen"/> class.
        /// </summary>
        /// <param name="snapshot">The day's state as already known by <see cref="HomeScreen"/> - avoids a redundant fetch just to show the clock-in time and target.</param>
        public ClockOutScreen(IAttendanceService attendanceService, ICurrentUserContext currentUserContext, AttendanceDaySnapshot snapshot)
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
            Header.Draw(frame, "CLOCK OUT", "Esc Cancel");

            var clockIn = _snapshot.OpenAttendanceLog!.ClockIn;
            var target = TimeSpan.FromHours((double)_snapshot.UserSetting.TargetShiftHours);
            frame.Write(1, 3, $"Clocked in {Formatting.Time(clockIn)} - worked {Formatting.Duration(DateTime.Now - clockIn)} - target {Formatting.Duration(target)}", ColorToken.Dim);

            DrawRow(frame, 5, 0, "Now", DateTime.Now);
            DrawRow(frame, 6, 1, "Default clock-out", DateTime.Today + _snapshot.UserSetting.DefaultClockOutTime.ToTimeSpan());

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("up/down", "Select"), ("Enter", "Confirm"), ("Esc", "Cancel"));
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            if (_options.HandleKey(key))
            {
                return;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    await screens.Pop();
                    break;
                case ConsoleKey.Enter:
                    await ConfirmAsync(screens);
                    break;
            }
        }

        #endregion

        #region Helpers

        private void DrawRow(Frame frame, int y, int index, string label, DateTime time)
        {
            var selected = _options.SelectedIndex == index;
            var marker = selected ? "►" : " ";
            frame.Write(1, y, $"{marker} {label}", selected ? ColorToken.Accent : ColorToken.Default);
            frame.Write(32, y, Formatting.Time(time));
        }

        private async Task ConfirmAsync(ScreenStack screens)
        {
            var time = _options.SelectedIndex == 0
                ? DateTime.Now
                : DateTime.Today + _snapshot.UserSetting.DefaultClockOutTime.ToTimeSpan();

            try
            {
                await _attendanceService.ClockOutAsync(_currentUserContext.UserId, time, CancellationToken.None);
                await screens.Pop();
            }
            catch (AttendanceOperationException ex)
            {
                _message = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clock out user {UserId}.", _currentUserContext.UserId);
                _message = "Something went wrong clocking out.";
            }
        }

        #endregion
    }
}
