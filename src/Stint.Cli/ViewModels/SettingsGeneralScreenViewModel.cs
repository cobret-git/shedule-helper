using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Cli.Services;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// Settings &gt; General: default clock-in/clock-out times, how "Now" times are rounded, and
    /// the daily target. See <c>docs/update-v1.1.0/settings-render-48__general-*.txt</c>.
    /// </summary>
    /// <remarks>
    /// The whole page is one transaction: editing a row only changes an in-memory draft, nothing
    /// reaches <see cref="ISettingsService{TSettings}"/> until <see cref="SaveAsync"/> writes the
    /// lot at once, and the page can't be left while the draft differs from what's saved - Esc
    /// discards it first (same two-presses-to-back-out shape as Switch's pending marks). Time
    /// rows are typed in with the same HH:mm mask as Home's custom clock-in; the rounding row is a
    /// list picked from with Left/Right.
    /// </remarks>
    public sealed partial class SettingsGeneralScreenViewModel : ScreenViewModelBase
    {
        #region Fields

        private static readonly IReadOnlyList<GeneralSettingsRow> AllRows = Enum.GetValues<GeneralSettingsRow>();
        private static readonly IReadOnlyList<ClockRoundingInterval> RoundingOptions = Enum.GetValues<ClockRoundingInterval>();

        private readonly ISettingsService<AppSettings> _settingsService;
        private readonly TimeDigitsInput _timeInput = new();

        // The draft being edited - starts as a copy of the saved settings, and is only copied back
        // by SaveAsync.
        private TimeOnly _clockIn;
        private TimeOnly _clockOut;
        private TimeSpan _dailyTarget;
        private ClockRoundingInterval _rounding;

        private int _selectedIndex;
        private bool _isEditing;
        private int _selectedOptionIndex;
        private string? _message;
        private bool _isError;
        #endregion

        #region Constructors

        public SettingsGeneralScreenViewModel(INavigationService navigation, ISettingsService<AppSettings> settingsService)
            : base(navigation)
        {
            ArgumentNullException.ThrowIfNull(settingsService);

            _settingsService = settingsService;

            Title = "SETTINGS > GENERAL";

            LoadDraft();

            KeyHints =
            [
                new KeyHint("move", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("move", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("change", PreviousOptionCommand, ConsoleKey.LeftArrow),
                new KeyHint("change", NextOptionCommand, ConsoleKey.RightArrow),
                new KeyHint("edit", BeginEditCommand, ConsoleKey.Enter),
                new KeyHint("confirm", ConfirmEditCommand, ConsoleKey.Enter),
                new KeyHint("save", SaveCommand, ConsoleKey.S),
                new KeyHint("cancel", CancelEditCommand, ConsoleKey.Escape),
                new KeyHint("discard", DiscardCommand, ConsoleKey.Escape),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape)
            ];
        }

        #endregion

        #region Properties

        /// <summary>Every row on the page, in display order.</summary>
        public IReadOnlyList<GeneralSettingsRow> Rows => AllRows;

        /// <summary>The currently highlighted row index of <see cref="Rows"/>.</summary>
        public int SelectedIndex { get => _selectedIndex; private set => SetProperty(ref _selectedIndex, value); }

        /// <summary>The currently highlighted row.</summary>
        public GeneralSettingsRow SelectedRow => Rows[SelectedIndex];

        /// <summary>Whether the highlighted row is being edited right now.</summary>
        public bool IsEditing { get => _isEditing; private set => SetProperty(ref _isEditing, value); }

        /// <summary>Whether anything in the draft differs from what's saved.</summary>
        public bool HasChanges
        {
            get
            {
                var saved = _settingsService.Settings;
                return _clockIn != saved.DefaultClockInTime
                    || _clockOut != saved.DefaultClockOutTime
                    || _dailyTarget != saved.TargetShiftDuration
                    || _rounding != saved.ClockRounding;
            }
        }

        /// <summary>The last save/validation outcome to show at the bottom of the page, if any.</summary>
        public string? Message { get => _message; private set => SetProperty(ref _message, value); }

        /// <summary>Whether <see cref="Message"/> is an error rather than a confirmation.</summary>
        public bool IsError { get => _isError; private set => SetProperty(ref _isError, value); }

        /// <summary>Index into the mask text of the next digit to be typed, for the renderer's cursor.</summary>
        public int? TimeCursorIndex => _timeInput.CursorIndex;

        #endregion

        #region Methods

        /// <summary>Whether <paramref name="row"/> is typed in as a time (as opposed to picked from a list).</summary>
        public static bool IsTimeRow(GeneralSettingsRow row) => row != GeneralSettingsRow.RoundNowTimes;

        /// <summary>
        /// What <paramref name="row"/> shows on its right-hand side: its draft value as plain text
        /// (<c>08:00</c>, <c>[ 5 min ]</c>), or - while it's the row being edited - the mask being
        /// typed into or the list's <c>&lt; 5 min &gt;</c>.
        /// </summary>
        public string GetValueText(GeneralSettingsRow row)
        {
            var isEditingThisRow = IsEditing && row == SelectedRow;

            if (!IsTimeRow(row))
            {
                var option = isEditingThisRow ? RoundingOptions[_selectedOptionIndex] : _rounding;
                return isEditingThisRow ? $"< {FormatRounding(option)} >" : $"[ {FormatRounding(option)} ]";
            }

            return isEditingThisRow ? _timeInput.Display : FormatTime(row switch
            {
                GeneralSettingsRow.DefaultClockIn => _clockIn.ToTimeSpan(),
                GeneralSettingsRow.DefaultClockOut => _clockOut.ToTimeSpan(),
                _ => _dailyTarget
            });
        }

        /// <summary>Appends one digit to the time being typed, while editing a time row.</summary>
        public void AppendTimeDigit(char digit)
        {
            if (IsEditing && IsTimeRow(SelectedRow))
            {
                _timeInput.Append(digit);
                OnPropertyChanged(nameof(TimeCursorIndex));
            }
        }

        /// <summary>
        /// Removes the last typed digit, while editing a time row. With nothing left to remove,
        /// stops editing instead (back to browsing), same as Home's custom time.
        /// </summary>
        public void RemoveTimeDigit()
        {
            if (!IsEditing || !IsTimeRow(SelectedRow))
            {
                return;
            }

            if (!_timeInput.Backspace())
            {
                IsEditing = false;
            }

            OnPropertyChanged(nameof(TimeCursorIndex));
        }

        #endregion

        #region Commands

        // Wraps around at both ends; not while a row is being edited (Up/Down mean nothing there).
        [RelayCommand(CanExecute = nameof(CanBrowse))] private void MoveSelectionUp()
        {
            Message = null;
            SelectedIndex = (SelectedIndex - 1 + Rows.Count) % Rows.Count;
        }

        [RelayCommand(CanExecute = nameof(CanBrowse))] private void MoveSelectionDown()
        {
            Message = null;
            SelectedIndex = (SelectedIndex + 1) % Rows.Count;
        }

        // Left/Right while editing the rounding list: wraps around, like every other picker.
        [RelayCommand(CanExecute = nameof(CanChangeOption))] private void PreviousOption()
            => _selectedOptionIndex = (_selectedOptionIndex - 1 + RoundingOptions.Count) % RoundingOptions.Count;

        [RelayCommand(CanExecute = nameof(CanChangeOption))] private void NextOption()
            => _selectedOptionIndex = (_selectedOptionIndex + 1) % RoundingOptions.Count;

        [RelayCommand(CanExecute = nameof(CanBrowse))] private void BeginEdit()
        {
            Message = null;

            if (IsTimeRow(SelectedRow))
            {
                _timeInput.Clear();
            }
            else
            {
                _selectedOptionIndex = RoundingOptions.ToList().IndexOf(_rounding);
            }

            IsEditing = true;
            OnPropertyChanged(nameof(TimeCursorIndex));
        }

        // Applies the edited value to the draft only - nothing is saved until SaveAsync.
        [RelayCommand(CanExecute = nameof(CanConfirmEdit))] private void ConfirmEdit()
        {
            if (!IsTimeRow(SelectedRow))
            {
                _rounding = RoundingOptions[_selectedOptionIndex];
            }
            else if (_timeInput.TryParse(out var time))
            {
                switch (SelectedRow)
                {
                    case GeneralSettingsRow.DefaultClockIn:
                        _clockIn = time;
                        break;
                    case GeneralSettingsRow.DefaultClockOut:
                        _clockOut = time;
                        break;
                    case GeneralSettingsRow.DailyTarget:
                        _dailyTarget = time.ToTimeSpan();
                        break;
                }
            }

            IsEditing = false;
            OnPropertyChanged(nameof(HasChanges));
        }

        [RelayCommand(CanExecute = nameof(CanCancelEdit))] private void CancelEdit()
        {
            IsEditing = false;
            _timeInput.Clear();
        }

        // Validates the whole draft, then writes it to the settings file in one go. A failed
        // write puts the previous values back, so the in-memory settings never claim something
        // that isn't on disk; the draft stays as it was so nothing typed is lost.
        [RelayCommand(CanExecute = nameof(CanSave))] private async Task SaveAsync()
        {
            if (_dailyTarget <= TimeSpan.Zero)
            {
                ShowMessage("Daily target must be above 00:00", isError: true);
                return;
            }

            if (_clockOut <= _clockIn)
            {
                ShowMessage("Default clock-out must be after clock-in", isError: true);
                return;
            }

            var settings = _settingsService.Settings;
            var previous = (settings.DefaultClockInTime, settings.DefaultClockOutTime, settings.TargetShiftDuration, settings.ClockRounding);

            settings.DefaultClockInTime = _clockIn;
            settings.DefaultClockOutTime = _clockOut;
            settings.TargetShiftDuration = _dailyTarget;
            settings.ClockRounding = _rounding;

            try
            {
                await _settingsService.SaveAsync();
                ShowMessage("Saved", isError: false);
            }
            catch (Exception ex)
            {
                (settings.DefaultClockInTime, settings.DefaultClockOutTime, settings.TargetShiftDuration, settings.ClockRounding) = previous;
                ShowMessage($"Couldn't save: {ex.Message}", isError: true);
            }

            OnPropertyChanged(nameof(HasChanges));
        }

        // Esc with unsaved changes: throws the draft away and stays; Esc again (nothing left to
        // discard) is what leaves.
        [RelayCommand(CanExecute = nameof(CanDiscard))] private void Discard()
        {
            LoadDraft();
            Message = null;
            OnPropertyChanged(nameof(HasChanges));
        }

        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        #endregion

        #region CanExecute

        private bool CanBrowse() => !IsEditing;

        private bool CanChangeOption() => IsEditing && !IsTimeRow(SelectedRow);

        private bool CanConfirmEdit() => IsEditing && (!IsTimeRow(SelectedRow) || _timeInput.IsComplete);

        private bool CanCancelEdit() => IsEditing;

        private bool CanSave() => !IsEditing && HasChanges;

        private bool CanDiscard() => !IsEditing && HasChanges;

        private bool CanGoBack() => !IsEditing && !HasChanges && Navigation.CanGoBack;

        #endregion

        #region Helpers

        private void LoadDraft()
        {
            var settings = _settingsService.Settings;

            _clockIn = settings.DefaultClockInTime;
            _clockOut = settings.DefaultClockOutTime;
            _dailyTarget = settings.TargetShiftDuration;
            _rounding = settings.ClockRounding;
        }

        private void ShowMessage(string message, bool isError)
        {
            Message = message;
            IsError = isError;
        }

        private static string FormatTime(TimeSpan value) => $"{(int)value.TotalHours:00}:{value.Minutes:00}";

        private static string FormatRounding(ClockRoundingInterval interval) => interval switch
        {
            ClockRoundingInterval.Off => "Off",
            ClockRoundingInterval.OneHour => "1 hour",
            _ => $"{(int)interval} min"
        };

        #endregion
    }
}
