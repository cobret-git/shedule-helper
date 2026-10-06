using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Models;
using Stint.Cli.Services;
using Stint.Cli.Components;
using Stint.Cli.Components.Extensions;
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
    /// Clocking in and out are a dialog of their own (<see cref="ClockTimePickerScreenViewModel"/>): clocking
    /// in opens as soon as Home finds there is no attendance for today, clocking out on <c>[o]</c>. Only
    /// "Now" is ever rounded (<see cref="AppSettings.ClockRounding"/>); Default and Custom are stored as typed.
    /// Coming back from an absence still uses Home's own Now/Custom picker (see <see cref="HomeState.Returning"/>).
    /// A day is clocked in once and out once - there is no second clock-in after clocking out.
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
        // Coming back from an absence has no "Default" - there is no usual time to return at - so
        // its picker is just Now and Custom.
        private static readonly IReadOnlyList<ClockTimeOption> ReturnClockOptions =
        [
            ClockTimeOption.Now,
            ClockTimeOption.Custom
        ];
        // How many project rows the clocked-out pager shows per page. A rendering-layout
        // assumption, not a setting - keep in sync with the actual renderer once it exists.
        private const int ProjectRowsPerPage = 5;
        private readonly IStintDataGateway _gateway;
        private readonly ISettingsService<AppSettings> _settingsService;
        private readonly TimeDigitsInput _customTime = new();
        private AttendanceLog? _attendanceLog;
        // Set from opening the clock-in/clock-out dialog until the time is saved. Closing the dialog
        // re-activates Home, and that refresh must not look at the db (it would still find no
        // attendance and open the clock-in dialog again) - the save's own refresh follows.
        private bool _isPickingClockTime;
        // The part-day absence the user is away for right now, if any - loaded alongside the
        // attendance log. Its paused segment (project/task) is what coming back resumes.
        private AwayLog? _openAway;
        private HomeState _state = HomeState.NotClockedIn;
        private int _selectedClockOptionIndex;
        private bool _isEditingCustomTime;
        private string? _clockError;
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
                new KeyHint("select", MoveClockSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("select", MoveClockSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("confirm", ConfirmClockCommand, ConsoleKey.Enter),
                new KeyHint("cancel", CancelClockPickerCommand, ConsoleKey.Escape),
                new KeyHint("out", ClockOutCommand, ConsoleKey.O),
                new KeyHint("back", BeginReturnCommand, ConsoleKey.R),
                new KeyHint("menu", OpenMenuCommand, ConsoleKey.Tab),
                new KeyHint("page", PreviousPageCommand, ConsoleKey.LeftArrow),
                new KeyHint("page", NextPageCommand, ConsoleKey.RightArrow),
                new KeyHint("quit", QuitCommand, ConsoleKey.Q)
            ];
        }

        #endregion

        #region Properties

        /// <summary>Which of Home's renders is current.</summary>
        public HomeState State { get => _state; private set => SetProperty(ref _state, value); }

        /// <summary>The currently highlighted row index of the "back at" time picker.</summary>
        public int SelectedClockOptionIndex { get => _selectedClockOptionIndex; private set => SetProperty(ref _selectedClockOptionIndex, value); }

        /// <summary>Whether the Custom row is currently being typed into.</summary>
        public bool IsEditingCustomTime { get => _isEditingCustomTime; private set => SetProperty(ref _isEditingCustomTime, value); }

        /// <summary>Why the last "back at" attempt was refused (a Custom time that can't be used), if it was.</summary>
        public string? ClockError { get => _clockError; private set => SetProperty(ref _clockError, value); }

        /// <summary>Today's project/task tree, most recently active first.</summary>
        public IReadOnlyList<HomeProjectRow> ProjectRows { get => _projectRows; private set => SetProperty(ref _projectRows, value); }

        /// <summary>Current page index into <see cref="CurrentPageProjectRows"/>, for the clocked-out pager.</summary>
        public int CurrentPageIndex { get => _currentPageIndex; private set => SetProperty(ref _currentPageIndex, value); }

        /// <summary>The error from the last failed <see cref="RefreshAsync"/>, if any.</summary>
        public string? LoadError { get => _loadError; private set => SetProperty(ref _loadError, value); }

        /// <summary>Whether the "back at" time picker is what Home is showing.</summary>
        public bool IsPickerOpen => State == HomeState.Returning;

        /// <summary>Whether the user is away right now (a part-day absence is open).</summary>
        public bool IsAway => _openAway is not null;

        /// <summary>What the open absence is for, e.g. "Doctor / dental" - empty when not away.</summary>
        public string AwayName => _openAway is { } away ? AwayKindNames.GetName(away.Kind) : string.Empty;

        /// <summary>When the open absence started, or null when not away.</summary>
        public DateTime? AwayStart => _openAway?.StartTime;

        /// <summary>
        /// The project (and task) that was running when the user went away - "PROJECT / TASK" - and will
        /// resume on return, or null when nothing was being tracked (or not away).
        /// </summary>
        public string? PausedLabel => _openAway?.PausedTimeLog is { } paused
            ? (paused.Task is { } task ? $"{paused.Project.Name} / {task.Title}" : paused.Project.Name)
            : null;

        /// <summary>The rows of Home's "back at" time picker, in display order.</summary>
        public IReadOnlyList<ClockTimeOption> ClockOptions => ReturnClockOptions;

        /// <summary>The currently highlighted row of the time picker.</summary>
        public ClockTimeOption SelectedClockOption => ClockOptions[SelectedClockOptionIndex];

        /// <summary>Today's clock-in time, or null before clocking in.</summary>
        public DateTime? ClockInTime => _attendanceLog?.ClockIn;

        /// <summary>Today's clock-out time, or null before clocking out.</summary>
        public DateTime? ClockOutTime => _attendanceLog?.ClockOut;

        /// <summary>The daily target shift duration.</summary>
        public TimeSpan Target => _settingsService.Settings.TargetShiftDuration;

        /// <summary>
        /// Time worked so far today: elapsed-since-clock-in while clocked in (never negative - a
        /// "Now" clock-in rounded forward is briefly still in the future), the final
        /// clock-in-to-clock-out span while <see cref="HomeState.ClockedOut"/>, otherwise zero.
        /// </summary>
        public TimeSpan Elapsed => State switch
        {
            HomeState.ClockedIn or HomeState.Returning => Max(DateTime.Now - (_attendanceLog?.ClockIn ?? DateTime.Now), TimeSpan.Zero),
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

        /// <summary>Masked display for the custom "back at" time being typed, e.g. "08:3-".</summary>
        public string CustomTimeDisplay => _customTime.Display;

        /// <summary>
        /// Index into <see cref="CustomTimeDisplay"/> of the next digit to be typed - the render
        /// pipeline's cue for where to draw the "cursor" - or null once all four digits are in and
        /// there's nothing left to fill. Skips over the colon <see cref="CustomTimeDisplay"/>
        /// inserts between the hour and minute pairs.
        /// </summary>
        public int? CustomTimeCursorIndex => _customTime.CursorIndex;

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
        /// Appends one digit to the custom "back at" time being typed, while <see cref="IsEditingCustomTime"/>.
        /// The render pipeline is what decides which physical key counts as a digit - this
        /// method takes the digit itself, never a <see cref="ConsoleKey"/>/<see cref="ConsoleKeyInfo"/>.
        /// </summary>
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
        /// Removes the last typed digit of the custom "back at" time, while <see cref="IsEditingCustomTime"/>.
        /// If there's nothing left to remove, exits editing mode instead (back to the picker).
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
        /// The value that would be used to come back via <paramref name="option"/> right now.
        /// </summary>
        public string GetClockPreview(ClockTimeOption option)
        {
            return option switch
            {
                // Coming back isn't rounded: like a Switch, it simply happens when it happens.
                ClockTimeOption.Now => DateTime.Now.ToString("HH:mm"),
                ClockTimeOption.Custom => CustomTimeDisplay,
                _ => string.Empty
            };
        }

        #endregion

        #region Commands

        // Wraps around at both ends (Now -> up -> Custom, Custom -> down -> Now) rather than
        // stopping at the first/last option.
        [RelayCommand(CanExecute = nameof(CanMoveClockSelection))] private void MoveClockSelectionUp()
        {
            ClockError = null;
            SelectedClockOptionIndex = (SelectedClockOptionIndex - 1 + ClockOptions.Count) % ClockOptions.Count;
        }

        [RelayCommand(CanExecute = nameof(CanMoveClockSelection))] private void MoveClockSelectionDown()
        {
            ClockError = null;
            SelectedClockOptionIndex = (SelectedClockOptionIndex + 1) % ClockOptions.Count;
        }

        // Enter: while Custom is highlighted but not yet being edited, starts editing instead of
        // returning - the second Enter (once a full, valid time is typed) actually does it.
        [RelayCommand(CanExecute = nameof(CanConfirmClock))] private async Task ConfirmClockAsync()
        {
            if (SelectedClockOption == ClockTimeOption.Custom && !IsEditingCustomTime)
            {
                ClockError = null;
                IsEditingCustomTime = true;
                return;
            }

            await ReturnAsync();
        }

        // Esc in the picker: first discards a half-typed custom time and drops back to the picker
        // with Custom still highlighted; with nothing being typed, the picker closes back to the
        // live shift.
        [RelayCommand(CanExecute = nameof(CanCancelClockPicker))] private void CancelClockPicker()
        {
            ClockError = null;

            if (IsEditingCustomTime)
            {
                IsEditingCustomTime = false;
                _customTime.Clear();
                OnPropertyChanged(nameof(CustomTimeDisplay));
                return;
            }

            State = HomeState.ClockedIn;
        }

        // Asks for the clock-out time in the picker dialog, then closes the open project segment and
        // the day at that time.
        [RelayCommand(CanExecute = nameof(CanClockOut))] private async Task ClockOutAsync()
        {
            if (_attendanceLog is null)
            {
                return;
            }

            try
            {
                _isPickingClockTime = true;

                var result = await Navigation.PickClockTimeAsync(new ClockTimePickerRequest(ClockTimePickerAction.ClockOut));
                if (result.IsPicked)
                {
                    var clockOutTime = DateTime.Today + result.Time.ToTimeSpan();

                    var openLog = await _gateway.GetOpenTimeLogAsync(_attendanceLog.Id);
                    if (openLog is not null)
                    {
                        await _gateway.CloseTimeLogAsync(openLog.Id, clockOutTime, TimeLogCloseReason.ClockedOut);
                    }

                    await _gateway.ClockOutAsync(_attendanceLog.Id, clockOutTime);
                }
            }
            finally
            {
                _isPickingClockTime = false;
            }

            await RefreshAsync();
        }

        // Opens the Now/Custom picker for the time the user is back at, rather than ending the
        // absence instantly - nobody knows in advance how long an appointment takes, so the time
        // is given afterwards.
        [RelayCommand(CanExecute = nameof(CanBeginReturn))] private void BeginReturn()
        {
            SelectedClockOptionIndex = 0;
            ClockError = null;
            IsEditingCustomTime = false;
            _customTime.Clear();
            State = HomeState.Returning;
        }

        // The way to every other screen - the menu itself decides which of them are open right now.
        [RelayCommand(CanExecute = nameof(CanOpenMenu))] private Task OpenMenuAsync()
            => Navigation.OpenMenuAsync(MenuDestination.Home);

        // Wraps around at both ends (last page -> right -> first page, first page -> left ->
        // last page), same as the clock-in picker's own Up/Down above.
        [RelayCommand(CanExecute = nameof(CanChangePage))] private void PreviousPage()
            => CurrentPageIndex = (CurrentPageIndex - 1 + TotalPages) % TotalPages;

        [RelayCommand(CanExecute = nameof(CanChangePage))] private void NextPage()
            => CurrentPageIndex = (CurrentPageIndex + 1) % TotalPages;

        [RelayCommand] private void Quit()
        {
            // TODO: replace with a proper shutdown hook (flush logs, dispose the DI container)
            // once the render pipeline/host loop exists.
            Environment.Exit(0);
        }

        #endregion

        #region CanExecute

        private bool CanMoveClockSelection() => IsPickerOpen && !IsEditingCustomTime;

        private bool CanConfirmClock()
            => IsPickerOpen
               && (SelectedClockOption != ClockTimeOption.Custom || !IsEditingCustomTime || _customTime.IsComplete);

        private bool CanCancelClockPicker() => IsEditingCustomTime || State == HomeState.Returning;

        // Clocking out or switching while away would leave the absence dangling - coming back
        // ([r]) is what ends it, so both wait until the user has.
        private bool CanClockOut() => State == HomeState.ClockedIn && !IsAway;

        private bool CanBeginReturn() => State == HomeState.ClockedIn && IsAway;

        // Nothing to navigate to before the day has started: the clock-in picker is all Home offers
        // until then. Once clocked in (or out again) the menu is open, but not mid-way through a
        // time picker - which sections it then allows (Switch and Projects need a live shift) is the
        // menu's own business.
        private bool CanOpenMenu() => State is HomeState.ClockedIn or HomeState.ClockedOut;

        private bool CanChangePage() => State == HomeState.ClockedOut && TotalPages > 1;

        #endregion

        #region Helpers

        private async Task RefreshAsync()
        {
            if (_isPickingClockTime)
            {
                return;
            }

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

                // An absence can only be open on a live shift - clocking out is refused while away.
                _openAway = State == HomeState.ClockedIn && _attendanceLog is not null
                    ? await _gateway.GetOpenAwayLogAsync(_attendanceLog.Id)
                    : null;
                OnPropertyChanged(nameof(IsAway));
                OnPropertyChanged(nameof(AwayName));
                OnPropertyChanged(nameof(AwayStart));
                OnPropertyChanged(nameof(PausedLabel));

                CurrentPageIndex = 0;
                LoadError = null;

                if (State == HomeState.NotClockedIn)
                {
                    await ClockInAsync();
                }
            }
            catch (Exception ex)
            {
                // No logging pipeline exists yet - keep the error on the VM itself rather than
                // losing it silently. The renderer can surface LoadError once it exists.
                LoadError = ex.Message;
            }
        }

        private static string ToWorkDate(DateTime date) => date.ToString("yyyy-MM-dd");

        private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

        // Asks for the clock-in time in the picker dialog, saves it, and refreshes into the live shift.
        private async Task ClockInAsync()
        {
            try
            {
                _isPickingClockTime = true;

                var result = await Navigation.PickClockTimeAsync(new ClockTimePickerRequest(ClockTimePickerAction.ClockIn));
                _attendanceLog = await _gateway.ClockInAsync(ToWorkDate(DateTime.Today), DateTime.Today + result.Time.ToTimeSpan());
            }
            finally
            {
                _isPickingClockTime = false;
            }

            await RefreshAsync();
        }

        // Ends the open absence at the chosen time; the gateway reopens the paused project/task at that
        // same moment. Custom is the user's own explicit choice, so a time that can't work is refused
        // with a reason; Now just never lands before the absence began.
        private async Task ReturnAsync()
        {
            if (_openAway is not { } away)
            {
                return;
            }

            var returnTime = SelectedClockOption == ClockTimeOption.Custom
                ? DateTime.Today + ParseCustomTime().ToTimeSpan()
                : (DateTime.Now > away.StartTime ? DateTime.Now : away.StartTime);

            if (returnTime > DateTime.Now)
            {
                ClockError = "Back time can't be in the future";
                return;
            }

            if (returnTime < away.StartTime)
            {
                ClockError = $"Back time can't be before the start ({away.StartTime:HH:mm})";
                return;
            }

            await _gateway.EndAwayAsync(away.Id, returnTime);

            IsEditingCustomTime = false;
            _customTime.Clear();
            ClockError = null;

            await RefreshAsync();
        }

        private TimeOnly ParseCustomTime() => _customTime.TryParse(out var time)
            ? time
            : throw new InvalidOperationException("Custom clock time isn't complete yet.");

        #endregion
    }
}
