using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Services;
using MSG = SheduleHelper.Core.Resources.Strings.Messages;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// The Settings screen (The Rule Setter) - a direct transcription of <see cref="UserSetting"/>.
    /// Every value here is a stepper or a cycle, adjusted with left/right rather than typed -
    /// there's nothing here that benefits from free text, so <c>TextField</c> stays unused and
    /// there's no input to validate. <see cref="Components.Settings.AppSettingsData"/>'s Theme/
    /// Culture are deliberately not shown - the CLI doesn't yet route its own UI text through them
    /// (colour capability is auto-detected, see <see cref="Theme"/>; no screen is localized), so
    /// exposing them here would be a setting that visibly does nothing.
    /// </summary>
    public sealed class SettingsScreen : IScreen
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ILogger _logger = Log.ForContext<SettingsScreen>();

        private readonly SelectList _rows = new(7);
        private UserSetting? _userSetting;
        private string? _message;

        private decimal _targetShiftHours;
        private TimeOnly _defaultClockIn;
        private TimeOnly _defaultClockOut;
        private LunchStrategy _lunchStrategy;
        private TimeOnly _lunchStart;
        private TimeOnly _lunchEnd;
        private int _lunchDurationMinutes;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsScreen"/> class.
        /// </summary>
        public SettingsScreen(ILocalDbContextFactory dbContextFactory, ICurrentUserContext currentUserContext)
        {
            _dbContextFactory = dbContextFactory;
            _currentUserContext = currentUserContext;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public Task OnEnter() => LoadAsync();

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, "SETTINGS", "Esc Back");

            if (_userSetting is null)
            {
                frame.Write(1, 3, _message ?? "Loading...", ColorToken.Dim);
                return;
            }

            frame.Write(1, 3, "Shift", ColorToken.Accent);
            DrawRow(frame, 4, 0, "Target shift hours", $"[ {_targetShiftHours:0.00} ]");
            DrawRow(frame, 5, 1, "Default clock-in", $"[ {Formatting.Time(_defaultClockIn)} ]");
            DrawRow(frame, 6, 2, "Default clock-out", $"[ {Formatting.Time(_defaultClockOut)} ]");

            frame.Write(1, 8, "Lunch", ColorToken.Accent);
            DrawRow(frame, 9, 3, "Strategy", StrategyDisplay(_lunchStrategy));
            DrawRow(frame, 10, 4, "Lunch start time", $"[ {Formatting.Time(_lunchStart)} ]", enabled: _lunchStrategy == LunchStrategy.FixedWindow);
            DrawRow(frame, 11, 5, "Lunch end time", $"[ {Formatting.Time(_lunchEnd)} ]", enabled: _lunchStrategy == LunchStrategy.FixedWindow);
            DrawRow(frame, 12, 6, "Lunch duration", $"[ {_lunchDurationMinutes} min ]", enabled: _lunchStrategy == LunchStrategy.DurationBased);

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("up/down", "Select"), ("left/right", "Change"), ("Enter", "Save"), ("F10", "Save"), ("Esc", "Back"));
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            if (_userSetting is null)
            {
                if (key.Key == ConsoleKey.Escape)
                {
                    await screens.Pop();
                }

                return;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    await screens.Pop();
                    return;
                case ConsoleKey.Enter:
                case ConsoleKey.F10:
                    await SaveAsync(screens);
                    return;
            }

            if (_rows.HandleKey(key))
            {
                return;
            }

            if (key.Key is not (ConsoleKey.LeftArrow or ConsoleKey.RightArrow))
            {
                return;
            }

            var forward = key.Key == ConsoleKey.RightArrow;
            switch (_rows.SelectedIndex)
            {
                case 0:
                    _targetShiftHours = ClampDecimal(_targetShiftHours + (forward ? 0.25m : -0.25m), 1.00m, 16.00m);
                    break;
                case 1:
                    _defaultClockIn = _defaultClockIn.Add(TimeSpan.FromMinutes(forward ? 15 : -15));
                    break;
                case 2:
                    _defaultClockOut = _defaultClockOut.Add(TimeSpan.FromMinutes(forward ? 15 : -15));
                    break;
                case 3:
                    _lunchStrategy = CycleStrategy(_lunchStrategy, forward);
                    break;
                case 4 when _lunchStrategy == LunchStrategy.FixedWindow:
                    _lunchStart = _lunchStart.Add(TimeSpan.FromMinutes(forward ? 15 : -15));
                    break;
                case 5 when _lunchStrategy == LunchStrategy.FixedWindow:
                    _lunchEnd = _lunchEnd.Add(TimeSpan.FromMinutes(forward ? 15 : -15));
                    break;
                case 6 when _lunchStrategy == LunchStrategy.DurationBased:
                    _lunchDurationMinutes = ClampInt(_lunchDurationMinutes + (forward ? 5 : -5), 0, 180);
                    break;
            }
        }

        #endregion

        #region Helpers

        private void DrawRow(Frame frame, int y, int index, string label, string value, bool enabled = true)
        {
            var selected = _rows.SelectedIndex == index;
            var color = !enabled ? ColorToken.Dim : selected ? ColorToken.Accent : ColorToken.Default;
            var marker = selected ? "►" : " ";
            frame.Write(1, y, $"{marker} {label}", color);
            frame.Write(36, y, value, color);
        }

        private static string StrategyDisplay(LunchStrategy strategy) => strategy switch
        {
            LunchStrategy.None => "‹ [ None ] · Fixed window · Duration ›",
            LunchStrategy.FixedWindow => "‹ None · [ Fixed window ] · Duration ›",
            LunchStrategy.DurationBased => "‹ None · Fixed window · [ Duration ] ›",
            _ => strategy.ToString(),
        };

        private static LunchStrategy CycleStrategy(LunchStrategy current, bool forward)
        {
            var values = Enum.GetValues<LunchStrategy>();
            var index = Array.IndexOf(values, current);
            var nextIndex = forward ? (index + 1) % values.Length : (index - 1 + values.Length) % values.Length;
            return values[nextIndex];
        }

        private static decimal ClampDecimal(decimal value, decimal min, decimal max) => Math.Max(min, Math.Min(max, value));

        private static int ClampInt(int value, int min, int max) => Math.Max(min, Math.Min(max, value));

        private async Task LoadAsync()
        {
            try
            {
                await using var db = _dbContextFactory.CreateDbContext();
                var userId = _currentUserContext.UserId;
                var userSetting = await db.GetUserSettingAsync(userId, CancellationToken.None)
                    ?? await db.CreateUserSettingAsync(userId, CancellationToken.None);

                _userSetting = userSetting;
                _targetShiftHours = userSetting.TargetShiftHours;
                _defaultClockIn = userSetting.DefaultClockInTime;
                _defaultClockOut = userSetting.DefaultClockOutTime;
                _lunchStrategy = userSetting.LunchStrategy;
                _lunchStart = userSetting.LunchStartTime;
                _lunchEnd = userSetting.LunchEndTime;
                _lunchDurationMinutes = userSetting.LunchDurationMinutes;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load settings for user {UserId}.", _currentUserContext.UserId);
                _message = MSG.error_settingsLoadUnexpected;
            }
        }

        private async Task SaveAsync(ScreenStack screens)
        {
            if (_userSetting is null)
            {
                return;
            }

            try
            {
                await using var db = _dbContextFactory.CreateDbContext();

                _userSetting.TargetShiftHours = _targetShiftHours;
                _userSetting.DefaultClockInTime = _defaultClockIn;
                _userSetting.DefaultClockOutTime = _defaultClockOut;
                _userSetting.LunchStrategy = _lunchStrategy;
                _userSetting.LunchStartTime = _lunchStart;
                _userSetting.LunchEndTime = _lunchEnd;
                _userSetting.LunchDurationMinutes = _lunchDurationMinutes;

                await db.UpdateUserSettingAsync(_userSetting, CancellationToken.None);
                await screens.Pop();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save settings for user {UserId}.", _currentUserContext.UserId);
                _message = MSG.error_settingsSaveUnexpected;
            }
        }

        #endregion
    }
}
