using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// A small modal that prompts for a time of day (HH:mm), then hands it to
    /// <paramref name="onConfirm"/>-equivalent callback supplied by the constructor. Generic across
    /// callers - clock-in and clock-out both push one of these rather than each solving text entry
    /// and validation themselves.
    /// </summary>
    /// <remarks>
    /// The callback receives the <see cref="ScreenStack"/> itself and is responsible for navigating
    /// away on success (e.g. popping back to Home) - how many levels that is differs by caller, so
    /// this screen can't decide it generically. Returning <see langword="null"/> means "handled,
    /// don't do anything else here"; returning a message means "show this and let the user retry".
    /// </remarks>
    public sealed class TimeEntryScreen : IScreen
    {
        #region Fields

        private readonly string _title;
        private readonly string _label;
        private readonly TextField _field;
        private readonly Func<TimeOnly, ScreenStack, Task<string?>> _onConfirm;
        private string? _message;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TimeEntryScreen"/> class.
        /// </summary>
        /// <param name="title">The header title, e.g. "CLOCK IN".</param>
        /// <param name="label">The prompt shown above the field, e.g. "Clock in at".</param>
        /// <param name="initialValue">The time the field starts pre-filled with.</param>
        /// <param name="onConfirm">
        /// Called with the parsed time and the active <see cref="ScreenStack"/> once the user
        /// confirms a validly-formatted time. Return <see langword="null"/> on success (having
        /// already navigated away as needed), or an error message to display and let the user retry.
        /// </param>
        public TimeEntryScreen(string title, string label, TimeOnly initialValue, Func<TimeOnly, ScreenStack, Task<string?>> onConfirm)
        {
            _title = title;
            _label = label;
            _field = new TextField(Formatting.Time(initialValue));
            _onConfirm = onConfirm;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, _title, "Esc Cancel");

            frame.Write(1, 3, _label);
            _field.Draw(frame, 1, 4, 10, editing: true);
            frame.Write(1, 6, "Format: HH:mm (24-hour)", ColorToken.Dim);

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("Enter/F10", "Confirm"), ("Esc", "Cancel"));
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    await screens.Pop();
                    return;
                case ConsoleKey.Enter:
                case ConsoleKey.F10:
                    await ConfirmAsync(screens);
                    return;
            }

            _field.HandleKey(key);
        }

        #endregion

        #region Helpers

        private async Task ConfirmAsync(ScreenStack screens)
        {
            if (!TryParseTime(_field.Value, out var time))
            {
                _message = "Enter a time as HH:mm, e.g. 08:30.";
                return;
            }

            var error = await _onConfirm(time, screens);
            if (error is not null)
            {
                _message = error;
            }
        }

        private static bool TryParseTime(string input, out TimeOnly time)
        {
            var trimmed = input.Trim();

            if (TimeOnly.TryParseExact(trimmed, new[] { "H:mm", "HH:mm" }, out time))
            {
                return true;
            }

            if (trimmed.Length == 4 && trimmed.All(char.IsDigit) && TimeOnly.TryParseExact(trimmed, "HHmm", out time))
            {
                return true;
            }

            return TimeOnly.TryParse(trimmed, out time);
        }

        #endregion
    }
}
