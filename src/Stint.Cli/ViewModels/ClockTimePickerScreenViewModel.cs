using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Cli.Services;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// A time picker dialog: Now / Default / Custom rows, opened with a
    /// <see cref="ClockTimePickerRequest"/> and closed with a <see cref="ClockTimePickerResult"/> - see
    /// <c>NavigationDialogExtensions.PickClockTimeAsync</c>. For now it only picks the clock-in time.
    /// </summary>
    /// <remarks>
    /// It cannot be cancelled: a day has to start somewhere, so the only way out is picking a time.
    /// Only "Now" is ever rounded (<see cref="AppSettings.ClockRounding"/>); Default and Custom are
    /// returned as set or typed. As on Home, raw key handling for the custom time is the view's job -
    /// it calls <see cref="AppendCustomTimeDigit"/>/<see cref="RemoveCustomTimeDigit"/>.
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
        private readonly TimeDigitsInput _customTime = new();
        private int _selectedOptionIndex;
        private bool _isEditingCustomTime;
        #endregion

        #region Constructors

        public ClockTimePickerScreenViewModel(ISettingsService<AppSettings> settingsService)
        {
            ArgumentNullException.ThrowIfNull(settingsService);

            _settingsService = settingsService;

            Title = "CLOCK IN";

            KeyHints =
            [
                new KeyHint("select", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("select", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("confirm", ConfirmCommand, ConsoleKey.Enter),
                new KeyHint("cancel", CancelCustomTimeCommand, ConsoleKey.Escape)
            ];
        }
        #endregion

        #region Properties

        /// <summary>The picker's rows, in display order.</summary>
        public IReadOnlyList<ClockTimeOption> Options => AllOptions;

        /// <summary>The currently highlighted row.</summary>
        public ClockTimeOption SelectedOption => AllOptions[_selectedOptionIndex];

        /// <summary>Whether the Custom row is currently being typed into.</summary>
        public bool IsEditingCustomTime { get => _isEditingCustomTime; private set => SetProperty(ref _isEditingCustomTime, value); }

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
        }

        /// <summary>Appends one digit to the custom time being typed, while <see cref="IsEditingCustomTime"/>.</summary>
        public void AppendCustomTimeDigit(char digit)
        {
            if (!IsEditingCustomTime)
            {
                return;
            }

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
                ClockTimeOption.Default => settings.DefaultClockInTime.ToString("HH:mm"),
                ClockTimeOption.Custom => CustomTimeDisplay,
                _ => string.Empty
            };
        }
        #endregion

        #region Commands

        // Wraps around at both ends, like every other picker.
        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionUp()
            => _selectedOptionIndex = (_selectedOptionIndex - 1 + AllOptions.Count) % AllOptions.Count;

        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionDown()
            => _selectedOptionIndex = (_selectedOptionIndex + 1) % AllOptions.Count;

        // Enter: while Custom is highlighted but not yet being edited, starts editing instead of
        // confirming - the second Enter (once a full, valid time is typed) closes the dialog.
        [RelayCommand(CanExecute = nameof(CanConfirm))] private void Confirm()
        {
            if (SelectedOption == ClockTimeOption.Custom && !IsEditingCustomTime)
            {
                IsEditingCustomTime = true;
                return;
            }

            Close(new ClockTimePickerResult(SelectedOption, GetTime(SelectedOption)));
        }

        // Esc discards a half-typed custom time and drops back to the rows. With nothing being
        // typed there is nothing to cancel - the dialog has no way out but a time.
        [RelayCommand(CanExecute = nameof(CanCancelCustomTime))] private void CancelCustomTime()
        {
            IsEditingCustomTime = false;
            _customTime.Clear();
            OnPropertyChanged(nameof(CustomTimeDisplay));
        }
        #endregion

        #region CanExecute

        private bool CanMoveSelection() => !IsEditingCustomTime;

        private bool CanConfirm() => SelectedOption != ClockTimeOption.Custom || !IsEditingCustomTime || _customTime.IsComplete;

        private bool CanCancelCustomTime() => IsEditingCustomTime;
        #endregion

        #region Helpers

        private TimeOnly GetTime(ClockTimeOption option)
        {
            var settings = _settingsService.Settings;

            return option switch
            {
                ClockTimeOption.Now => TimeOnly.FromDateTime(ClockRounder.RoundClockIn(DateTime.Now, settings.ClockRounding)),
                ClockTimeOption.Default => settings.DefaultClockInTime,
                _ => _customTime.TryParse(out var time)
                    ? time
                    : throw new InvalidOperationException("Custom clock time isn't complete yet.")
            };
        }

        private string FormatNowPreview()
        {
            var now = DateTime.Now;
            var stored = ClockRounder.RoundClockIn(now, _settingsService.Settings.ClockRounding);

            return stored.ToString("HH:mm") == now.ToString("HH:mm")
                ? now.ToString("HH:mm")
                : $"{now:HH:mm} ({stored:HH:mm})";
        }
        #endregion
    }

    /// <summary>
    /// What the clock time picker dialog is opened with. Nothing yet - it exists so the dialog fits
    /// the context/result shape of every other dialog and can grow a context later.
    /// </summary>
    public sealed record ClockTimePickerRequest;

    /// <summary>
    /// What the clock time picker dialog hands back when it closes.
    /// </summary>
    /// <param name="Option">Which row the user confirmed: Now, Default or Custom.</param>
    /// <param name="Time">
    /// The time of day picked. For Now it is already rounded per
    /// <see cref="AppSettings.ClockRounding"/>; Default and Custom are as set or typed.
    /// </param>
    public readonly record struct ClockTimePickerResult(ClockTimeOption Option, TimeOnly Time);
}
