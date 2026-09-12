using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Models;
using Stint.Cli.Services;
using Stint.Cli.Components;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The Home screen: today's clock-in state, live progress toward the daily target, and the
    /// project/task tree worked today. See <c>docs/update-v1.1.0/home-screen-spec.md</c> for the
    /// full render spec this implements.
    /// </summary>
    /// <remarks>
    /// Layout/formatting (dot leaders, the bar's actual characters/colors, how many rows fit
    /// before collapsing or paging) is entirely the render pipeline's job - this class only
    /// exposes the state and computed values behind it. Same for raw console input: while
    /// <see cref="IsEditingCustomTime"/> is set, the future Home renderer is what decides which
    /// physical key is a digit or a backspace and calls <see cref="AppendCustomTimeDigit"/>/
    /// <see cref="RemoveCustomTimeDigit"/> accordingly - this class never sees a
    /// <see cref="ConsoleKey"/> or <see cref="ConsoleKeyInfo"/>.
    /// </remarks>
    public sealed partial class HomeScreenViewModel : ScreenViewModelBase
    {
        #region Fields

        // The progress bar's scale always keeps this much empty space past whatever the current
        // furthest edge (target, or target+overtime) is - so it never reads as "full" the instant
        // the target is reached, and doesn't jump/rescale as overtime starts accruing. Matches the
        // previous CLI's ProgressBar headroom. Not a setting yet; revisit if that ever needs to be
        // user-configurable.
        private static readonly TimeSpan BarHeadroom = TimeSpan.FromMinutes(45);
        private static readonly IReadOnlyList<ClockInOption> AllClockInOptions =
        [
            ClockInOption.Now,
            ClockInOption.Default,
            ClockInOption.Custom
        ];
        // How many project rows the clocked-out pager shows per page. A rendering-layout
        // assumption, not a setting - keep in sync with the actual renderer once it exists.
        private const int ProjectRowsPerPage = 5;
        private readonly IStintDataGateway _gateway;
        private readonly ISettingsService<AppSettings> _settingsService;
        private AttendanceLog? _attendanceLog;
        private string _customTimeDigits = string.Empty;
        private HomeState _state = HomeState.NotClockedIn;
        private int _selectedClockInOptionIndex;
        private bool _isEditingCustomTime;
        private IReadOnlyList<HomeProjectRow> _projectRows = [];
        private int _currentPageIndex;
        private string? _loadError;
        #endregion

        #region Constructors

        public HomeScreenViewModel(
            INavigationService navigation,
            IStintDataGateway gateway,
            ISettingsService<AppSettings> settingsService)
            : base(navigation)
        {
            ArgumentNullException.ThrowIfNull(gateway);
            ArgumentNullException.ThrowIfNull(settingsService);

            _gateway = gateway;
            _settingsService = settingsService;

            Title = "HOME";

            KeyHints =
            [
                new KeyHint("select", MoveClockInSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("select", MoveClockInSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("confirm", ConfirmClockInCommand, ConsoleKey.Enter),
                new KeyHint("cancel", CancelCustomTimeEditCommand, ConsoleKey.Escape),
                new KeyHint("in", BeginClockInCommand, ConsoleKey.I),
                new KeyHint("out", ClockOutCommand, ConsoleKey.O),
                new KeyHint("switch", SwitchCommand, ConsoleKey.S),
                new KeyHint("projects", OpenProjectsCommand, ConsoleKey.P),
                new KeyHint("page", PreviousPageCommand, ConsoleKey.LeftArrow),
                new KeyHint("page", NextPageCommand, ConsoleKey.RightArrow),
                new KeyHint("quit", QuitCommand, ConsoleKey.Q)
            ];
        }

        #endregion

        #region Properties

        /// <summary>Which of Home's three renders is current.</summary>
        public HomeState State { get => _state; private set => SetProperty(ref _state, value); }

        /// <summary>The currently highlighted row index of the clock-in time picker.</summary>
        public int SelectedClockInOptionIndex { get => _selectedClockInOptionIndex; private set => SetProperty(ref _selectedClockInOptionIndex, value); }

        /// <summary>Whether the Custom row is currently being typed into.</summary>
        public bool IsEditingCustomTime { get => _isEditingCustomTime; private set => SetProperty(ref _isEditingCustomTime, value); }

        /// <summary>Today's project/task tree, most recently active first.</summary>
        public IReadOnlyList<HomeProjectRow> ProjectRows { get => _projectRows; private set => SetProperty(ref _projectRows, value); }

        /// <summary>Current page index into <see cref="CurrentPageProjectRows"/>, for the clocked-out pager.</summary>
        public int CurrentPageIndex { get => _currentPageIndex; private set => SetProperty(ref _currentPageIndex, value); }

        /// <summary>The error from the last failed <see cref="RefreshAsync"/>, if any.</summary>
        public string? LoadError { get => _loadError; private set => SetProperty(ref _loadError, value); }

        /// <summary>The rows of Home's clock-in time picker, in display order.</summary>
        public IReadOnlyList<ClockInOption> ClockInOptions => AllClockInOptions;

        /// <summary>The currently highlighted row of the clock-in time picker.</summary>
        public ClockInOption SelectedClockInOption => ClockInOptions[SelectedClockInOptionIndex];

        /// <summary>Today's clock-in time, or null before clocking in.</summary>
        public DateTime? ClockInTime => _attendanceLog?.ClockIn;

        /// <summary>Today's clock-out time, or null before clocking out.</summary>
        public DateTime? ClockOutTime => _attendanceLog?.ClockOut;

        /// <summary>The daily target shift duration.</summary>
        public TimeSpan Target => _settingsService.Settings.TargetShiftDuration;

        /// <summary>
        /// Time worked so far today: elapsed-since-clock-in while <see cref="HomeState.ClockedIn"/>,
        /// the final clock-in-to-clock-out span while <see cref="HomeState.ClockedOut"/>, otherwise zero.
        /// </summary>
        public TimeSpan Elapsed => State switch
        {
            HomeState.ClockedIn => DateTime.Now - (_attendanceLog?.ClockIn ?? DateTime.Now),
            HomeState.ClockedOut => (_attendanceLog?.ClockOut ?? DateTime.Now) - (_attendanceLog?.ClockIn ?? DateTime.Now),
            _ => TimeSpan.Zero
        };

        /// <summary>Whether <see cref="Elapsed"/> has run past <see cref="Target"/>.</summary>
        public bool IsOvertime => Elapsed > Target;

        /// <summary>How far <see cref="Elapsed"/> has run past <see cref="Target"/>, or zero.</summary>
        public TimeSpan Overtime => IsOvertime ? Elapsed - Target : TimeSpan.Zero;

        /// <summary>
        /// The time span the progress bar's full width represents: <see cref="Target"/> plus
        /// whatever <see cref="Overtime"/> has accrued so far, plus a fixed <see cref="BarHeadroom"/>
        /// buffer past that - grows continuously with <see cref="Overtime"/> rather than jumping to
        /// a bigger fixed range the instant overtime starts.
        /// </summary>
        public TimeSpan BarRange => Target + Overtime + BarHeadroom;

        /// <summary>Fraction of the bar (0-1) filled by ordinary, within-target time.</summary>
        public double NormalFillFraction => BarRange > TimeSpan.Zero
            ? (Elapsed < Target ? Elapsed : Target) / BarRange
            : 0;

        /// <summary>Fraction of the bar (0-1) filled by overtime, drawn as a distinct segment.</summary>
        public double OvertimeFillFraction => BarRange > TimeSpan.Zero ? Overtime / BarRange : 0;

        /// <summary><see cref="Elapsed"/> as a percentage of <see cref="Target"/> - can exceed 100.</summary>
        public int PercentComplete => Target > TimeSpan.Zero ? (int)Math.Round(Elapsed / Target * 100) : 0;

        /// <summary>
        /// Today's balance against target (<see cref="Elapsed"/> minus <see cref="Target"/>).
        /// Today only - there's no carried-over balance from other days yet.
        /// </summary>
        public TimeSpan Balance => Elapsed - Target;

        /// <summary>Masked display for the custom clock-in time being typed, e.g. "08:3-".</summary>
        public string CustomTimeDisplay
        {
            get
            {
                var padded = _customTimeDigits.PadRight(4, '-');
                return $"{padded[0]}{padded[1]}:{padded[2]}{padded[3]}";
            }
        }

        /// <summary>
        /// Index into <see cref="CustomTimeDisplay"/> of the next digit to be typed - the render
        /// pipeline's cue for where to draw the "cursor" - or null once all four digits are in and
        /// there's nothing left to fill. Skips over the colon <see cref="CustomTimeDisplay"/>
        /// inserts between the hour and minute pairs.
        /// </summary>
        public int? CustomTimeCursorIndex => _customTimeDigits.Length switch
        {
            0 => 0,
            1 => 1,
            2 => 3,
            3 => 4,
            _ => null
        };

        /// <summary>Number of pages <see cref="CurrentPageProjectRows"/> paginates over.</summary>
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(ProjectRows.Count / (double)ProjectRowsPerPage));

        /// <summary>The slice of <see cref="ProjectRows"/> for <see cref="CurrentPageIndex"/>.</summary>
        public IReadOnlyList<HomeProjectRow> CurrentPageProjectRows => ProjectRows
            .Skip(CurrentPageIndex * ProjectRowsPerPage)
            .Take(ProjectRowsPerPage)
            .ToList();

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void OnActivated()
        {
            // OnActivated is synchronous (see IScreenViewModel), but populating Home needs async
            // gateway calls - fire-and-forget rather than making the whole navigation pipeline
            // async for it. A local SQLite read is fast enough that the one-frame stale/loading
            // gap is harmless; errors are kept on LoadError rather than swallowed silently.
            _ = RefreshAsync();
        }

        /// <summary>
        /// Appends one digit to the custom clock-in time being typed, while <see cref="IsEditingCustomTime"/>.
        /// The render pipeline is what decides which physical key counts as a digit - this
        /// method takes the digit itself, never a <see cref="ConsoleKey"/>/<see cref="ConsoleKeyInfo"/>.
        /// </summary>
        public void AppendCustomTimeDigit(char digit)
        {
            if (!IsEditingCustomTime || !char.IsAsciiDigit(digit) || _customTimeDigits.Length >= 4)
            {
                return;
            }

            _customTimeDigits += digit;
            OnPropertyChanged(nameof(CustomTimeDisplay));
        }

        /// <summary>
        /// Removes the last typed digit of the custom clock-in time, while <see cref="IsEditingCustomTime"/>.
        /// If there's nothing left to remove, exits editing mode instead (back to the picker).
        /// </summary>
        public void RemoveCustomTimeDigit()
        {
            if (!IsEditingCustomTime)
            {
                return;
            }

            if (_customTimeDigits.Length > 0)
            {
                _customTimeDigits = _customTimeDigits[..^1];
            }
            else
            {
                IsEditingCustomTime = false;
            }

            OnPropertyChanged(nameof(CustomTimeDisplay));
        }

        /// <summary>The value that would be used to clock in via <paramref name="option"/> right now.</summary>
        public string GetClockInPreview(ClockInOption option) => option switch
        {
            ClockInOption.Now => DateTime.Now.ToString("HH:mm"),
            ClockInOption.Default => _settingsService.Settings.DefaultClockInTime.ToString("HH:mm"),
            ClockInOption.Custom => CustomTimeDisplay,
            _ => string.Empty
        };

        #endregion

        #region Commands

        // Wraps around at both ends (Now -> up -> Custom, Custom -> down -> Now) rather than
        // stopping at the first/last option.
        [RelayCommand(CanExecute = nameof(CanMoveClockInSelection))] private void MoveClockInSelectionUp()
            => SelectedClockInOptionIndex = (SelectedClockInOptionIndex - 1 + ClockInOptions.Count) % ClockInOptions.Count;

        [RelayCommand(CanExecute = nameof(CanMoveClockInSelection))] private void MoveClockInSelectionDown()
            => SelectedClockInOptionIndex = (SelectedClockInOptionIndex + 1) % ClockInOptions.Count;

        // Enter: while Custom is highlighted but not yet being edited, starts editing instead of
        // clocking in - the second Enter (once a full, valid time is typed) actually clocks in.
        [RelayCommand(CanExecute = nameof(CanConfirmClockIn))] private async Task ConfirmClockInAsync()
        {
            if (SelectedClockInOption == ClockInOption.Custom && !IsEditingCustomTime)
            {
                IsEditingCustomTime = true;
                return;
            }

            var clockInTime = SelectedClockInOption switch
            {
                ClockInOption.Now => DateTime.Now,
                ClockInOption.Default => DateTime.Today + _settingsService.Settings.DefaultClockInTime.ToTimeSpan(),
                ClockInOption.Custom => DateTime.Today + ParseCustomTime().ToTimeSpan(),
                _ => DateTime.Now
            };

            _attendanceLog = await _gateway.ClockInAsync(ToWorkDate(DateTime.Today), clockInTime);

            IsEditingCustomTime = false;
            _customTimeDigits = string.Empty;

            await RefreshAsync();
        }

        // Esc while typing a custom time: discards whatever digits were entered and drops back to
        // the picker with Custom still highlighted, rather than committing a half-typed time.
        [RelayCommand(CanExecute = nameof(CanCancelCustomTimeEdit))] private void CancelCustomTimeEdit()
        {
            IsEditingCustomTime = false;
            _customTimeDigits = string.Empty;
            OnPropertyChanged(nameof(CustomTimeDisplay));
        }

        // The clocked-out screen's "[i] in": re-opens the picker rather than instantly
        // re-clocking-in, so resuming later the same day goes through the same Now/Default/Custom
        // choice as the first clock-in of the day.
        [RelayCommand(CanExecute = nameof(CanBeginClockIn))] private void BeginClockIn()
        {
            State = HomeState.NotClockedIn;
            SelectedClockInOptionIndex = 0;
        }

        [RelayCommand(CanExecute = nameof(CanClockOut))] private async Task ClockOutAsync()
        {
            if (_attendanceLog is null)
            {
                return;
            }

            var clockOutTime = DateTime.Now;
            var openLog = await _gateway.GetOpenTimeLogAsync(_attendanceLog.Id);
            if (openLog is not null)
            {
                await _gateway.CloseTimeLogAsync(openLog.Id, clockOutTime, TimeLogCloseReason.ClockedOut);
            }

            await _gateway.ClockOutAsync(_attendanceLog.Id, clockOutTime);

            await RefreshAsync();
        }

        [RelayCommand(CanExecute = nameof(CanSwitch))] private void Switch()
        {
            // TODO: Navigation.NavigateTo<SwitchScreenViewModel>() once that screen exists.
        }

        [RelayCommand(CanExecute = nameof(CanOpenProjects))] private void OpenProjects()
            => Navigation.NavigateTo<ProjectsScreenViewModel>();

        [RelayCommand(CanExecute = nameof(CanGoToPreviousPage))] private void PreviousPage() => CurrentPageIndex--;

        [RelayCommand(CanExecute = nameof(CanGoToNextPage))] private void NextPage() => CurrentPageIndex++;

        [RelayCommand] private void Quit()
        {
            // TODO: replace with a proper shutdown hook (flush logs, dispose the DI container)
            // once the render pipeline/host loop exists.
            Environment.Exit(0);
        }

        #endregion

        #region CanExecute

        private bool CanMoveClockInSelection() => State == HomeState.NotClockedIn && !IsEditingCustomTime;

        private bool CanConfirmClockIn()
            => State == HomeState.NotClockedIn
               && (SelectedClockInOption != ClockInOption.Custom || !IsEditingCustomTime || IsCustomTimeComplete);

        private bool CanCancelCustomTimeEdit() => IsEditingCustomTime;

        private bool CanBeginClockIn() => State == HomeState.ClockedOut;

        private bool CanClockOut() => State == HomeState.ClockedIn;

        private bool CanSwitch() => State == HomeState.ClockedIn && ProjectRows.Count > 0;

        private bool CanOpenProjects() => State == HomeState.ClockedIn && ProjectRows.Count == 0;

        private bool CanGoToPreviousPage() => State == HomeState.ClockedOut && CurrentPageIndex > 0;

        private bool CanGoToNextPage() => State == HomeState.ClockedOut && CurrentPageIndex < TotalPages - 1;

        private bool IsCustomTimeComplete => TryParseCustomTime(out _);

        #endregion

        #region Helpers

        private async Task RefreshAsync()
        {
            try
            {
                _attendanceLog = await _gateway.GetAttendanceForDateAsync(ToWorkDate(DateTime.Today));

                State = _attendanceLog switch
                {
                    null => HomeState.NotClockedIn,
                    { ClockOut: not null } => HomeState.ClockedOut,
                    _ => HomeState.ClockedIn
                };

                ProjectRows = _attendanceLog is null
                    ? []
                    : HomeProjectRowBuilder.Build(await _gateway.GetTimeLogsForAttendanceAsync(_attendanceLog.Id));

                CurrentPageIndex = 0;
                LoadError = null;
            }
            catch (Exception ex)
            {
                // No logging pipeline exists yet - keep the error on the VM itself rather than
                // losing it silently. The renderer can surface LoadError once it exists.
                LoadError = ex.Message;
            }
        }

        private static string ToWorkDate(DateTime date) => date.ToString("yyyy-MM-dd");

        private TimeOnly ParseCustomTime() => TryParseCustomTime(out var time)
            ? time
            : throw new InvalidOperationException("Custom clock-in time isn't complete yet.");

        private bool TryParseCustomTime(out TimeOnly time)
        {
            time = default;
            if (_customTimeDigits.Length != 4)
            {
                return false;
            }

            var hour = int.Parse(_customTimeDigits[..2]);
            var minute = int.Parse(_customTimeDigits[2..]);
            if (hour is < 0 or > 23 || minute is < 0 or > 59)
            {
                return false;
            }

            time = new TimeOnly(hour, minute);
            return true;
        }

        #endregion
    }
}
