using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Cli.Services;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// A time picker dialog: Now / Default / Custom rows, opened with a
    /// <see cref="ClockTimePickerRequest"/> and closed with a <see cref="ClockTimePickerResult"/> - see
    /// <c>NavigationDialogExtensions.PickClockTimeAsync</c>. It picks the time for either clocking in
    /// or clocking out, as the request says.
    /// </summary>
    /// <remarks>
    /// Clocking in cannot be cancelled: a day has to start somewhere, so the only way out is picking a
    /// time. Clocking out can (Esc). Only "Now" is ever rounded (<see cref="AppSettings.ClockRounding"/>);
    /// Default and Custom are returned as set or typed. As on Home, raw key handling for the custom time
    /// is the view's job - it calls <see cref="AppendCustomTimeDigit"/>/<see cref="RemoveCustomTimeDigit"/>.
    /// </remarks>
    public sealed partial class ClockTimePickerScreenViewModel : DialogScreenViewModelBase<ClockTimePickerRequest, ClockTimePickerResult>
    {
        #region Fields

        private static readonly IReadOnlyList<ClockTimeOption> AllOptions =
        [
            ClockTimeOption.Now,
            ClockTimeOption.Default,
            ClockTimeOption.Custom
        ];
        private readonly ISettingsService<AppSettings> _settingsService;
        private readonly IStintDataGateway _gateway;
        private readonly TimeDigitsInput _customTime = new();
        private ClockTimePickerRequest _request = new(ClockTimePickerAction.ClockIn);
        // A clock-out is bounded by today's clock-in and the day's latest project segment, loaded
        // from the db when the dialog opens - see LoadClockOutBoundsAsync.
        private DateTime? _clockIn;
        private DateTime? _lastSegmentStart;
        private bool _areBoundsLoaded = true;
        private int _selectedOptionIndex;
        private bool _isEditingCustomTime;
        private string? _clockError;
        private string? _loadError;
        #endregion

        #region Constructors

        public ClockTimePickerScreenViewModel(ISettingsService<AppSettings> settingsService, IStintDataGateway gateway)
        {
            ArgumentNullException.ThrowIfNull(settingsService);
            ArgumentNullException.ThrowIfNull(gateway);

            _settingsService = settingsService;
            _gateway = gateway;

            KeyHints =
            [
                new KeyHint("select", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("select", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("confirm", ConfirmCommand, ConsoleKey.Enter),
                new KeyHint("cancel", CancelCommand, ConsoleKey.Escape)
            ];
        }
        #endregion

        #region Properties

        /// <summary>Whether this dialog is picking the clock-in or the clock-out time.</summary>
        public ClockTimePickerAction Action => _request.Action;

        /// <summary>Today's clock-in time when clocking out, otherwise null.</summary>
        public DateTime? ClockInTime => _clockIn;

        /// <summary>The picker's rows, in display order.</summary>
        public IReadOnlyList<ClockTimeOption> Options => AllOptions;

        /// <summary>The currently highlighted row.</summary>
        public ClockTimeOption SelectedOption => AllOptions[_selectedOptionIndex];

        /// <summary>Whether the Custom row is currently being typed into.</summary>
        public bool IsEditingCustomTime { get => _isEditingCustomTime; private set => SetProperty(ref _isEditingCustomTime, value); }

        /// <summary>Why the last confirm was refused (a Default/Custom clock-out time that can't be used), if it was.</summary>
        public string? ClockError { get => _clockError; private set => SetProperty(ref _clockError, value); }

        /// <summary>Why the clock-out bounds couldn't be loaded, if they couldn't - the dialog then can't confirm.</summary>
        public string? LoadError { get => _loadError; private set => SetProperty(ref _loadError, value); }

        /// <summary>Masked display for the custom time being typed, e.g. "08:3-".</summary>
        public string CustomTimeDisplay => _customTime.Display;

        /// <summary>Index into <see cref="CustomTimeDisplay"/> of the next digit to be typed, or null once all four are in.</summary>
        public int? CustomTimeCursorIndex => _customTime.CursorIndex;
        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Initialize(ClockTimePickerRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            _request = request;
            Title = request.Action == ClockTimePickerAction.ClockOut ? "CLOCK OUT" : "CLOCK IN";

            if (request.Action == ClockTimePickerAction.ClockOut)
            {
                // Initialize is synchronous (see IScreenViewModel), so the bounds load on their own;
                // confirming waits for them. A local SQLite read is done before the first frame.
                _areBoundsLoaded = false;
                _ = LoadClockOutBoundsAsync();
            }
        }

        /// <summary>Appends one digit to the custom time being typed, while <see cref="IsEditingCustomTime"/>.</summary>
        public void AppendCustomTimeDigit(char digit)
        {
            if (!IsEditingCustomTime)
            {
                return;
            }

            ClockError = null;
            _customTime.Append(digit);
            OnPropertyChanged(nameof(CustomTimeDisplay));
        }

        /// <summary>
        /// Removes the last typed digit, while <see cref="IsEditingCustomTime"/>. With nothing left
        /// to remove, exits editing mode instead (back to the rows).
        /// </summary>
        public void RemoveCustomTimeDigit()
        {
            if (!IsEditingCustomTime)
            {
                return;
            }

            ClockError = null;
            if (!_customTime.Backspace())
            {
                IsEditingCustomTime = false;
            }

            OnPropertyChanged(nameof(CustomTimeDisplay));
        }

        /// <summary>
        /// The value that would be picked via <paramref name="option"/> right now. A "Now" that
        /// rounding would move shows both, the real time first - e.g. <c>07:38 (07:40)</c>.
        /// </summary>
        public string GetPreview(ClockTimeOption option)
        {
            var settings = _settingsService.Settings;

            return option switch
            {
                ClockTimeOption.Now => FormatNowPreview(),
                ClockTimeOption.Default => GetDefaultTime(settings).ToString("HH:mm"),
                ClockTimeOption.Custom => CustomTimeDisplay,
                _ => string.Empty
            };
        }
        #endregion

        #region Commands

        // Wraps around at both ends, like every other picker.
        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionUp()
        {
            ClockError = null;
            _selectedOptionIndex = (_selectedOptionIndex - 1 + AllOptions.Count) % AllOptions.Count;
        }

        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionDown()
        {
            ClockError = null;
            _selectedOptionIndex = (_selectedOptionIndex + 1) % AllOptions.Count;
        }

        // Enter: while Custom is highlighted but not yet being edited, starts editing instead of
        // confirming - the second Enter (once a full, valid time is typed) closes the dialog.
        [RelayCommand(CanExecute = nameof(CanConfirm))] private void Confirm()
        {
            if (SelectedOption == ClockTimeOption.Custom && !IsEditingCustomTime)
            {
                ClockError = null;
                IsEditingCustomTime = true;
                return;
            }

            var time = GetTime(SelectedOption);

            // A Now clock-out is already clamped into range by RoundNow. Default/Custom are
            // the user's own explicit choice, so a time that can't work is refused with a reason
            // rather than silently moved somewhere else in their timesheet.
            if (Action == ClockTimePickerAction.ClockOut && SelectedOption != ClockTimeOption.Now
                && GetClockOutError(DateTime.Today + time.ToTimeSpan()) is { } error)
            {
                ClockError = error;
                return;
            }

            Close(ClockTimePickerResult.Pick(Action, SelectedOption, time));
        }

        // Esc first discards a half-typed custom time and drops back to the rows. With nothing being
        // typed, a clock-out closes without a time; a clock-in has no way out but a time.
        [RelayCommand(CanExecute = nameof(CanCancel))] private void Cancel()
        {
            ClockError = null;

            if (IsEditingCustomTime)
            {
                IsEditingCustomTime = false;
                _customTime.Clear();
                OnPropertyChanged(nameof(CustomTimeDisplay));
                return;
            }

            Close(ClockTimePickerResult.Cancelled(Action));
        }
        #endregion

        #region CanExecute

        private bool CanMoveSelection() => !IsEditingCustomTime;

        private bool CanConfirm() => _areBoundsLoaded && (SelectedOption != ClockTimeOption.Custom || !IsEditingCustomTime || _customTime.IsComplete);

        private bool CanCancel() => IsEditingCustomTime || Action == ClockTimePickerAction.ClockOut;
        #endregion

        #region Helpers

        private async Task LoadClockOutBoundsAsync()
        {
            try
            {
                var attendance = await _gateway.GetAttendanceForDateAsync(DateTime.Today.ToString("yyyy-MM-dd"))
                    ?? throw new InvalidOperationException("Not clocked in today.");
                var timeLogs = await _gateway.GetTimeLogsForAttendanceAsync(attendance.Id);

                _clockIn = attendance.ClockIn;
                _lastSegmentStart = timeLogs.Count == 0 ? null : timeLogs.Max(l => l.StartTime);
                _areBoundsLoaded = true;
                OnPropertyChanged(nameof(ClockInTime));
            }
            catch (Exception ex)
            {
                LoadError = ex.Message;
            }
        }

        private TimeOnly GetTime(ClockTimeOption option)
        {
            var settings = _settingsService.Settings;

            return option switch
            {
                ClockTimeOption.Now => TimeOnly.FromDateTime(RoundNow(DateTime.Now)),
                ClockTimeOption.Default => GetDefaultTime(settings),
                _ => _customTime.TryParse(out var time)
                    ? time
                    : throw new InvalidOperationException("Custom clock time isn't complete yet.")
            };
        }

        private TimeOnly GetDefaultTime(AppSettings settings)
            => Action == ClockTimePickerAction.ClockOut ? settings.DefaultClockOutTime : settings.DefaultClockInTime;

        private string? GetClockOutError(DateTime clockOutTime)
        {
            if (clockOutTime < ClockOutFloor)
            {
                return clockOutTime < _clockIn
                    ? "Clock-out can't be before clock-in"
                    : $"Clock-out can't be before the last switch ({_lastSegmentStart:HH:mm})";
            }

            return null;
        }

        // The earliest a clock-out may be: not before clocking in, and not before the latest
        // project segment began.
        private DateTime ClockOutFloor
        {
            get
            {
                var clockIn = _clockIn ?? DateTime.MinValue;
                return _lastSegmentStart is DateTime lastStart && lastStart > clockIn ? lastStart : clockIn;
            }
        }

        // Rounds "now" to the grid for the action, but a clock-out never goes past ClockOutFloor - a
        // short shift (or a switch a moment ago) mustn't end before it started; it just ends there.
        private DateTime RoundNow(DateTime now)
        {
            var rounding = _settingsService.Settings.ClockRounding;

            if (Action == ClockTimePickerAction.ClockIn)
            {
                return ClockRounder.RoundClockIn(now, rounding);
            }

            var rounded = ClockRounder.RoundClockOut(now, rounding);
            return rounded < ClockOutFloor ? ClockOutFloor : rounded;
        }

        private string FormatNowPreview()
        {
            var now = DateTime.Now;
            var stored = RoundNow(now);

            return stored.ToString("HH:mm") == now.ToString("HH:mm")
                ? now.ToString("HH:mm")
                : $"{now:HH:mm} ({stored:HH:mm})";
        }
        #endregion
    }

    /// <summary>
    /// Which time the clock time picker dialog is picking.
    /// </summary>
    public enum ClockTimePickerAction
    {
        /// <summary>Starting the day.</summary>
        ClockIn,

        /// <summary>Ending the day.</summary>
        ClockOut
    }

    /// <summary>
    /// What the clock time picker dialog is opened with. Everything else it needs (today's clock-in,
    /// the latest project segment) it reads from the db itself.
    /// </summary>
    /// <param name="Action">Whether the clock-in or the clock-out time is being picked.</param>
    public sealed record ClockTimePickerRequest(ClockTimePickerAction Action);

    /// <summary>
    /// What the clock time picker dialog hands back when it closes.
    /// </summary>
    /// <param name="IsPicked">True when the user confirmed a time, false when they cancelled (clock-out only).</param>
    /// <param name="Action">What the dialog was picking, as requested.</param>
    /// <param name="Option">Which row the user confirmed: Now, Default or Custom - meaningless when not picked.</param>
    /// <param name="Time">
    /// The time of day picked - meaningless (default) when not picked. For Now it is already rounded per
    /// <see cref="AppSettings.ClockRounding"/>; Default and Custom are as set or typed.
    /// </param>
    public readonly record struct ClockTimePickerResult(bool IsPicked, ClockTimePickerAction Action, ClockTimeOption Option, TimeOnly Time)
    {
        #region Methods

        /// <summary>The result of confirming <paramref name="time"/> via <paramref name="option"/>.</summary>
        public static ClockTimePickerResult Pick(ClockTimePickerAction action, ClockTimeOption option, TimeOnly time) => new(true, action, option, time);

        /// <summary>The result of backing out of the dialog without picking anything.</summary>
        public static ClockTimePickerResult Cancelled(ClockTimePickerAction action) => new(false, action, default, default);

        #endregion
    }
}
