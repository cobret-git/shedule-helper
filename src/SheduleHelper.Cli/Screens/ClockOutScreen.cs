using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Pushed from <see cref="HomeScreen"/> when the user presses <c>O</c> while clocked in. Offers
    /// a short list of clock-out times, plus a custom one via <see cref="TimeEntryScreen"/>.
    /// </summary>
    public sealed class ClockOutScreen : IScreen
    {
        #region Fields

        private readonly IAttendanceService _attendanceService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly AttendanceDaySnapshot _snapshot;
        private readonly SelectList _options = new(3);
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

            DrawRow(frame, 5, 0, "Now", Formatting.Time(DateTime.Now));
            DrawRow(frame, 6, 1, "Default clock-out", Formatting.Time(DateTime.Today + _snapshot.UserSetting.DefaultClockOutTime.ToTimeSpan()));
            DrawRow(frame, 7, 2, "Custom time...", string.Empty);

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

        private void DrawRow(Frame frame, int y, int index, string label, string value)
        {
            var selected = _options.SelectedIndex == index;
            var marker = selected ? "►" : " ";
            frame.Write(1, y, $"{marker} {label}", selected ? ColorToken.Accent : ColorToken.Default);
            frame.Write(32, y, value);
        }

        private async Task ConfirmAsync(ScreenStack screens)
        {
            if (_options.SelectedIndex == 2)
            {
                await screens.Push(new TimeEntryScreen("CLOCK OUT", "Clock out at", TimeOnly.FromDateTime(DateTime.Now), async (time, s) =>
                {
                    var error = await TryClockOutAsync(DateTime.Today + time.ToTimeSpan());
                    if (error is null)
                    {
                        await s.Pop(); // TimeEntryScreen
                        await s.Pop(); // ClockOutScreen, back to Home
                    }

                    return error;
                }));
                return;
            }

            var time = _options.SelectedIndex == 0
                ? DateTime.Now
                : DateTime.Today + _snapshot.UserSetting.DefaultClockOutTime.ToTimeSpan();

            var directError = await TryClockOutAsync(time);
            if (directError is null)
            {
                await screens.Pop();
            }
            else
            {
                _message = directError;
            }
        }

        /// <summary>
        /// Clocks out and returns an error message on failure instead of setting <see cref="_message"/>
        /// directly - shared by the direct Now/Default rows above and <see cref="TimeEntryScreen"/>'s
        /// callback, which shows the error on itself so the user can correct the time and retry.
        /// </summary>
        private async Task<string?> TryClockOutAsync(DateTime time)
        {
            try
            {
                await _attendanceService.ClockOutAsync(_currentUserContext.UserId, time, CancellationToken.None);
                return null;
            }
            catch (AttendanceOperationException ex)
            {
                return ex.Message;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clock out user {UserId}.", _currentUserContext.UserId);
                return "Something went wrong clocking out.";
            }
        }

        #endregion
    }
}
