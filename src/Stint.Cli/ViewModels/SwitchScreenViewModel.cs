using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Models;
using Stint.Cli.Services;
using Stint.Cli.Components;
using Stint.Cli.Components.Extensions;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The Switch screen: pick a different active project/task to track time against. See
    /// <c>docs/update-v1.1.0/switch-render-48__*.txt</c> for the render mockups this takes its
    /// layout/labels from.
    /// </summary>
    /// <remarks>
    /// Toggled with V between two lists: the project tree and the part-day absences (doctor,
    /// errand, ...) - going away is just another thing to switch to, marked and confirmed the same
    /// way, and it pauses whatever is running until the user comes back from Home.
    /// Reached from <see cref="HomeScreenViewModel.Switch"/> while clocked in. Loads today's
    /// attendance/open segment itself in <see cref="OnActivated"/> rather than taking it as a
    /// navigation context - same parameterless/root-style shape as <see cref="HomeScreenViewModel"/>.
    /// Every action (switch target, pause, mark a task done) is only ever *marked* pending -
    /// nothing reaches <see cref="IStintDataGateway"/> until <see cref="ConfirmAsync"/> commits
    /// the whole batch at once, same mark-then-confirm shape as <see cref="ProjectScreenViewModel"/>'s
    /// delete flow. The one added wrinkle here: whichever pending marks touch time logs (closing
    /// the currently open segment, starting a new one) share a single <see cref="DateTime.Now"/>
    /// snapshot, so there's never a gap between "stopped this" and "started that."
    /// </remarks>
    public sealed partial class SwitchScreenViewModel : ScreenViewModelBase
    {
        #region Fields

        // Body rows available for the project/task tree: rows 5-18 of the 48x23 grid, per
        // docs/update-v1.1.0/switch-render-48__idle.txt (row 4 is the rule under the header, row
        // 19 is the pager line). A rough layout guess, same caveat as Home's ProjectRowsPerPage.
        private const int TreeRowBudget = 14;

        // The part-day absences the Away view lists, in display order.
        private static readonly IReadOnlyList<AwayKind> AllAwayKinds = Enum.GetValues<AwayKind>();

        private readonly IStintDataGateway _gateway;

        // Ids/target marked pending while Mode is Reviewing - never touched outside that mode,
        // and always back to empty/null on the way out of it (Confirm clears them after the
        // gateway calls, Cancel clears them and discards instead).
        private readonly HashSet<int> _pendingDoneTaskIds = [];
        private SwitchFlatRow? _pendingSwitchTarget;
        private AwayKind? _pendingAwayKind;
        private bool _pendingPause;

        private AttendanceLog? _attendanceLog;
        private ProjectTimeLog? _openLog;
        private IReadOnlyList<SwitchProjectRow> _projects = [];
        private List<SwitchFlatRow> _flatRows = [];
        private int _selectedIndex;
        private int _selectedAwayIndex;
        private SwitchView _view = SwitchView.Projects;
        private SwitchMode _mode = SwitchMode.Idle;
        private string? _loadError;
        #endregion

        #region Constructors

        public SwitchScreenViewModel(INavigationService navigation, IStintDataGateway gateway)
            : base(navigation)
        {
            ArgumentNullException.ThrowIfNull(gateway);

            _gateway = gateway;

            Title = "SWITCH";

            KeyHints =
            [
                new KeyHint("move", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("move", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("page", PreviousPageCommand, ConsoleKey.LeftArrow),
                new KeyHint("page", NextPageCommand, ConsoleKey.RightArrow),
                new KeyHint("view", ToggleViewCommand, ConsoleKey.V),
                new KeyHint("switch", ToggleSwitchTargetCommand, ConsoleKey.S),
                new KeyHint("pause", TogglePauseCommand, ConsoleKey.P),
                new KeyHint("done", ToggleDoneCommand, ConsoleKey.D),
                new KeyHint("confirm", ConfirmCommand, ConsoleKey.Enter),
                new KeyHint("new", BeginCreateProjectCommand, ConsoleKey.N),
                new KeyHint("cancel", CancelCommand, ConsoleKey.Escape),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape),
                new KeyHint("menu", OpenMenuCommand, ConsoleKey.Tab),
                new KeyHint("quit", QuitCommand, ConsoleKey.Q)
            ];
        }

        #endregion

        #region Properties

        /// <summary>Which of Switch's two renders is current.</summary>
        public SwitchMode Mode
        {
            get => _mode;
            private set
            {
                if (SetProperty(ref _mode, value))
                {
                    Title = value == SwitchMode.Reviewing ? "SWITCH - REVIEW" : "SWITCH";
                }
            }
        }

        /// <summary>What is being listed to switch to - the project tree, or the part-day absences.</summary>
        public SwitchView View { get => _view; private set => SetProperty(ref _view, value); }

        /// <summary>The part-day absences the Away view lists.</summary>
        public IReadOnlyList<AwayKind> AwayKinds => AllAwayKinds;

        /// <summary>The currently highlighted index into <see cref="AwayKinds"/>, in the Away view.</summary>
        public int SelectedAwayIndex { get => _selectedAwayIndex; private set => SetProperty(ref _selectedAwayIndex, value); }

        /// <summary>The absence marked to go away for on confirm, if any - only meaningful while <see cref="Mode"/> is Reviewing.</summary>
        public AwayKind? PendingAwayKind => _pendingAwayKind;

        /// <summary>Every active project/task, switchable target or not.</summary>
        public IReadOnlyList<SwitchProjectRow> Projects { get => _projects; private set => SetProperty(ref _projects, value); }

        /// <summary>The slice of <see cref="Projects"/> for <see cref="CurrentPageIndex"/>.</summary>
        public IReadOnlyList<SwitchProjectRow> CurrentPageProjects
        {
            get
            {
                var starts = BuildPageStartProjectIndices();
                var pageIndex = CurrentPageIndex;
                var start = starts[pageIndex];
                var end = pageIndex + 1 < starts.Count ? starts[pageIndex + 1] : Projects.Count;

                return Projects.Skip(start).Take(end - start).ToList();
            }
        }

        /// <summary>The id of the currently selected project (the selected row's own project, whether it's the project row itself or one of its tasks).</summary>
        public int? SelectedProjectId => _selectedIndex >= 0 && _selectedIndex < _flatRows.Count
            ? _flatRows[_selectedIndex].ProjectId
            : null;

        /// <summary>The id of the currently selected task, or null when the project row itself is selected.</summary>
        public int? SelectedTaskId => _selectedIndex >= 0 && _selectedIndex < _flatRows.Count
            ? _flatRows[_selectedIndex].TaskId
            : null;

        /// <summary>The project id of the pending switch target, if anything is marked - only meaningful while <see cref="Mode"/> is Reviewing.</summary>
        public int? PendingSwitchTargetProjectId => _pendingSwitchTarget?.ProjectId;

        /// <summary>The task id of the pending switch target, or null if it's a project-level (no-task) target - only meaningful when <see cref="PendingSwitchTargetProjectId"/> is set.</summary>
        public int? PendingSwitchTargetTaskId => _pendingSwitchTarget?.TaskId;

        /// <summary>Whether "stop tracking entirely" is the pending resolution for the currently open segment.</summary>
        public bool PendingPause => _pendingPause;

        /// <summary>The task ids currently marked to be set Done on confirm - only meaningful while <see cref="Mode"/> is Reviewing.</summary>
        public IReadOnlySet<int> PendingDoneTaskIds => _pendingDoneTaskIds;

        /// <summary>The error from the last failed <see cref="RefreshAsync"/>, if any.</summary>
        public string? LoadError { get => _loadError; private set => SetProperty(ref _loadError, value); }

        /// <summary>Number of pages <see cref="CurrentPageProjects"/> paginates over.</summary>
        public int TotalPages => BuildPageStartProjectIndices().Count;

        /// <summary>Which page the current selection falls on.</summary>
        public int CurrentPageIndex
        {
            get
            {
                if (_flatRows.Count == 0)
                {
                    return 0;
                }

                var starts = BuildPageStartProjectIndices();
                var selectedProjectIndex = _flatRows[_selectedIndex].ProjectIndex;

                var page = 0;
                for (var i = 1; i < starts.Count; i++)
                {
                    if (starts[i] > selectedProjectIndex)
                    {
                        break;
                    }

                    page = i;
                }

                return page;
            }
        }

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void OnActivated()
        {
            // Fire-and-forget for the same reason as Home/Project's OnActivated: a local SQLite
            // read is fast enough that the one-frame stale gap is harmless.
            _ = RefreshAsync();
        }

        #endregion

        #region Commands

        // Wraps around within the current page only (last row on the page -> down -> first row
        // of that same page, and back) - Left/Right below is what moves between pages, same
        // convention as Project's MoveSelectionUp/Down over CurrentPageTasks. Available in both
        // Idle and Reviewing - browsing to mark more rows is exactly what Reviewing is for.
        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionUp() => MoveSelection(-1);

        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionDown() => MoveSelection(1);

        // Jumps to the adjacent page, wrapping at both ends, and always lands on that page's
        // first project row - simpler than Project's/Projects' "preserve relative row" since
        // Switch's pages have irregular, unrelated shapes (whole project groups, not fixed rows).
        [RelayCommand(CanExecute = nameof(CanChangePage))] private void PreviousPage() => ChangePage(-1);

        [RelayCommand(CanExecute = nameof(CanChangePage))] private void NextPage() => ChangePage(1);

        // Flips between the project tree and the part-day absences. Only while nothing is marked:
        // a pending mark belongs to the list it was made in, so finish or cancel it first.
        [RelayCommand(CanExecute = nameof(CanToggleView))] private void ToggleView()
            => View = View == SwitchView.Projects ? SwitchView.Away : SwitchView.Projects;

        // Marks (or unmarks, pressing it again on the same row) the selected row as the pending
        // switch target - a single slot, not a set, since only one thing can end up running. In
        // the Away view the row is a part-day absence instead of a project/task; either way the
        // slot is shared, so marking one clears the other.
        // Marking a target clears any pending pause - the two are alternate resolutions for the
        // same currently-open segment, never both at once.
        [RelayCommand(CanExecute = nameof(CanToggleSwitchTarget))] private void ToggleSwitchTarget()
        {
            if (View == SwitchView.Away)
            {
                var kind = AwayKinds[SelectedAwayIndex];

                _pendingAwayKind = _pendingAwayKind == kind ? null : kind;
                _pendingSwitchTarget = null;
                _pendingPause = false;

                OnPendingChanged();
                return;
            }

            var selected = _flatRows[_selectedIndex];

            _pendingSwitchTarget = _pendingSwitchTarget == selected ? null : selected;
            if (_pendingSwitchTarget is not null)
            {
                _pendingPause = false;
                _pendingAwayKind = null;
            }

            OnPendingChanged();
        }

        // Toggles "stop tracking entirely" as the pending resolution - mutually exclusive with a
        // pending switch target for the same reason as above.
        [RelayCommand(CanExecute = nameof(CanTogglePause))] private void TogglePause()
        {
            _pendingPause = !_pendingPause;
            if (_pendingPause)
            {
                _pendingSwitchTarget = null;
                _pendingAwayKind = null;
            }

            OnPendingChanged();
        }

        // Marks (or unmarks) the selected task to be set Done on confirm - a set, not a single
        // slot, since several tasks can be wrapped up in the same batch. Independent of the
        // switch-target/pause slot above: marking a task done doesn't by itself say anything
        // about what should be tracked next.
        [RelayCommand(CanExecute = nameof(CanToggleDone))] private void ToggleDone()
        {
            if (SelectedTaskId is not int taskId)
            {
                return;
            }

            if (!_pendingDoneTaskIds.Remove(taskId))
            {
                _pendingDoneTaskIds.Add(taskId);
            }

            OnPendingChanged();
        }

        // Commits every pending mark in one atomic batch. Every time-log operation this implies -
        // closing whatever was open, starting whatever's newly targeted - shares one `now`
        // snapshot, so a switch (or a pause, or marking the running task done) never leaves a
        // gap between the old segment's end and the new one's start.
        [RelayCommand(CanExecute = nameof(CanConfirm))] private async Task ConfirmAsync()
        {
            // Never earlier than the clock-in: a "Now" clock-in rounded forward can still be in the
            // future for a few minutes, and no segment may start (or end) before the day did.
            var now = DateTime.Now;
            if (_attendanceLog?.ClockIn is DateTime clockIn && clockIn > now)
            {
                now = clockIn;
            }

            // Going away is its own batch - Switch's other marks can't be pending alongside it - and
            // the gateway closes whatever is running (to be resumed on return) in the same save.
            if (_pendingAwayKind is AwayKind awayKind && _attendanceLog is not null)
            {
                try
                {
                    await _gateway.StartAwayAsync(_attendanceLog.Id, awayKind, now);
                }
                catch (InvalidOperationException ex)
                {
                    LoadError = ex.Message;
                    return;
                }

                ClearPending();
                Navigation.GoBack();
                return;
            }

            foreach (var taskId in _pendingDoneTaskIds)
            {
                await _gateway.UpdateTaskStatusAsync(taskId, TaskItemStatus.Done);
            }

            var activeTaskMarkedDone = _openLog?.TaskId is int activeTaskId && _pendingDoneTaskIds.Contains(activeTaskId);
            var changesTracking = _pendingSwitchTarget is not null || _pendingPause;

            if (_openLog is not null && (changesTracking || activeTaskMarkedDone))
            {
                var reason = _pendingSwitchTarget is not null ? TimeLogCloseReason.Switched : TimeLogCloseReason.Stopped;
                await _gateway.CloseTimeLogAsync(_openLog.Id, now, reason);
            }

            if (_pendingSwitchTarget is SwitchFlatRow target && _attendanceLog is not null)
            {
                await _gateway.StartTimeLogAsync(_attendanceLog.Id, target.ProjectId, target.TaskId, now);
            }

            ClearPending();
            await RefreshAsync();

            // Only leave for Home if tracking itself actually changed - marking tasks done on
            // their own (without a switch/pause alongside) stays on Switch, same as before this
            // was folded into the batch, so another target can be picked right after.
            if (changesTracking)
            {
                Navigation.GoBack();
            }
        }

        // Esc while Reviewing: discards every pending mark (nothing was ever sent to the gateway)
        // and drops back to Idle, staying on this screen. Esc again from Idle is what actually
        // leaves - see GoBack - same two-presses-to-back-out shape as Project's Cancel/GoBack.
        [RelayCommand(CanExecute = nameof(CanCancel))] private void Cancel() => ClearPending();

        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        [RelayCommand(CanExecute = nameof(CanOpenMenu))] private Task OpenMenuAsync()
            => Navigation.OpenMenuAsync(MenuDestination.Switch);

        // Empty-state escape hatch, same idea as Home's own "[p] projects" when there's nothing
        // to show yet.
        [RelayCommand(CanExecute = nameof(CanBeginCreateProject))] private void BeginCreateProject()
            => Navigation.NavigateTo<ProjectsScreenViewModel>();

        [RelayCommand] private void Quit()
        {
            // TODO: replace with a proper shutdown hook (flush logs, dispose the DI container)
            // once the render pipeline/host loop exists - same TODO as every other screen's Quit.
            Environment.Exit(0);
        }

        #endregion

        #region CanExecute

        private bool CanMoveSelection() => View == SwitchView.Away || _flatRows.Count > 0;

        private bool CanChangePage() => View == SwitchView.Projects && TotalPages > 1;

        private bool CanToggleView() => Mode == SwitchMode.Idle;

        // Can't mark the row that's already the exact running target - nothing to switch to. Going
        // away needs a clocked-in day to go away from.
        private bool CanToggleSwitchTarget() => View == SwitchView.Away
            ? _attendanceLog is not null
            : _flatRows.Count > 0 && !_flatRows[_selectedIndex].IsActive;

        private bool CanTogglePause() => View == SwitchView.Projects && _openLog is not null;

        private bool CanToggleDone() => View == SwitchView.Projects && SelectedTaskId is not null;

        private bool CanConfirm() => _pendingSwitchTarget is not null || _pendingAwayKind is not null || _pendingPause || _pendingDoneTaskIds.Count > 0;

        private bool CanCancel() => Mode == SwitchMode.Reviewing;

        private bool CanGoBack() => Mode == SwitchMode.Idle && Navigation.CanGoBack;

        private bool CanOpenMenu() => Mode == SwitchMode.Idle;

        private bool CanBeginCreateProject() => Mode == SwitchMode.Idle && View == SwitchView.Projects && Projects.Count == 0;

        #endregion

        #region Helpers

        private void MoveSelection(int direction)
        {
            if (View == SwitchView.Away)
            {
                SelectedAwayIndex = (SelectedAwayIndex + direction + AwayKinds.Count) % AwayKinds.Count;
                return;
            }

            var starts = BuildPageStartProjectIndices();
            var pageIndex = CurrentPageIndex;
            var pageStartProjectIndex = starts[pageIndex];
            var pageEndProjectIndex = pageIndex + 1 < starts.Count ? starts[pageIndex + 1] : Projects.Count;

            var pageStartFlatIndex = _flatRows.FindIndex(r => r.ProjectIndex >= pageStartProjectIndex);
            var pageEndFlatIndex = _flatRows.FindIndex(r => r.ProjectIndex >= pageEndProjectIndex);
            if (pageEndFlatIndex < 0)
            {
                pageEndFlatIndex = _flatRows.Count;
            }

            var pageLength = pageEndFlatIndex - pageStartFlatIndex;
            var offsetOnPage = _selectedIndex - pageStartFlatIndex;

            _selectedIndex = pageStartFlatIndex + (offsetOnPage + direction + pageLength) % pageLength;
            OnPropertyChanged(nameof(SelectedProjectId));
            OnPropertyChanged(nameof(SelectedTaskId));
        }

        private void ChangePage(int direction)
        {
            var starts = BuildPageStartProjectIndices();
            var targetPage = (CurrentPageIndex + direction + starts.Count) % starts.Count;
            var targetProjectIndex = starts[targetPage];

            _selectedIndex = Math.Max(0, _flatRows.FindIndex(r => r.ProjectIndex == targetProjectIndex));
            OnPropertyChanged(nameof(SelectedProjectId));
            OnPropertyChanged(nameof(SelectedTaskId));
        }

        // Greedily packs whole project groups (header + tasks + one blank separator row) into
        // pages no taller than TreeRowBudget, never splitting a project's tasks across pages.
        // Returns the project index each page starts at - always at least [0].
        private List<int> BuildPageStartProjectIndices()
        {
            var starts = new List<int> { 0 };

            if (Projects.Count == 0)
            {
                return starts;
            }

            var rowsUsedOnPage = 0;
            for (var i = 0; i < Projects.Count; i++)
            {
                var groupHeight = 1 + Projects[i].Tasks.Count + 1;

                if (rowsUsedOnPage > 0 && rowsUsedOnPage + groupHeight > TreeRowBudget)
                {
                    starts.Add(i);
                    rowsUsedOnPage = 0;
                }

                rowsUsedOnPage += groupHeight;
            }

            return starts;
        }

        private void ClearPending()
        {
            _pendingSwitchTarget = null;
            _pendingAwayKind = null;
            _pendingPause = false;
            _pendingDoneTaskIds.Clear();
            OnPendingChanged();
        }

        private void OnPendingChanged()
        {
            Mode = _pendingSwitchTarget is not null || _pendingAwayKind is not null || _pendingPause || _pendingDoneTaskIds.Count > 0
                ? SwitchMode.Reviewing
                : SwitchMode.Idle;

            OnPropertyChanged(nameof(PendingAwayKind));
            OnPropertyChanged(nameof(PendingSwitchTargetProjectId));
            OnPropertyChanged(nameof(PendingSwitchTargetTaskId));
            OnPropertyChanged(nameof(PendingPause));
            OnPropertyChanged(nameof(PendingDoneTaskIds));
        }

        private async Task RefreshAsync()
        {
            try
            {
                _attendanceLog = await _gateway.GetAttendanceForDateAsync(ToWorkDate(DateTime.Today));
                _openLog = _attendanceLog is not null
                    ? await _gateway.GetOpenTimeLogAsync(_attendanceLog.Id)
                    : null;

                var activeProjects = await _gateway.GetActiveProjectsAsync();
                var projectRows = new List<SwitchProjectRow>();

                foreach (var project in activeProjects)
                {
                    var tasks = await _gateway.GetTasksForProjectAsync(project.Id);
                    var taskRows = tasks
                        // Done tasks aren't switch targets any more - Project's own screen is
                        // still where they're managed/reopened, GetTasksForProjectAsync keeps
                        // returning them for that, this is just Switch's own display filter.
                        .Where(t => t.Status != TaskItemStatus.Done)
                        .Select(t => new SwitchTaskRow
                        {
                            Id = t.Id,
                            Name = t.Title,
                            IsActive = _openLog is not null && _openLog.ProjectId == project.Id && _openLog.TaskId == t.Id
                        })
                        .ToList();

                    projectRows.Add(new SwitchProjectRow
                    {
                        Id = project.Id,
                        Name = project.Name,
                        IsActive = _openLog is not null && _openLog.ProjectId == project.Id && _openLog.TaskId is null,
                        Tasks = taskRows
                    });
                }

                Projects = projectRows;
                _flatRows = BuildFlatRows(projectRows);
                _selectedIndex = Math.Clamp(_selectedIndex, 0, Math.Max(0, _flatRows.Count - 1));

                LoadError = null;
            }
            catch (Exception ex)
            {
                // No logging pipeline exists yet - keep the error on the VM itself rather than
                // losing it silently, same approach as every other screen's RefreshAsync.
                LoadError = ex.Message;
            }

            OnPropertyChanged(nameof(CurrentPageProjects));
            OnPropertyChanged(nameof(SelectedProjectId));
            OnPropertyChanged(nameof(SelectedTaskId));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(CurrentPageIndex));
        }

        private static List<SwitchFlatRow> BuildFlatRows(IReadOnlyList<SwitchProjectRow> projects)
        {
            var rows = new List<SwitchFlatRow>();

            for (var projectIndex = 0; projectIndex < projects.Count; projectIndex++)
            {
                var project = projects[projectIndex];
                rows.Add(new SwitchFlatRow(projectIndex, project.Id, null, project.IsActive));

                foreach (var task in project.Tasks)
                {
                    rows.Add(new SwitchFlatRow(projectIndex, project.Id, task.Id, task.IsActive));
                }
            }

            return rows;
        }

        private static string ToWorkDate(DateTime date) => date.ToString("yyyy-MM-dd");

        #endregion

        #region Structures

        private readonly record struct SwitchFlatRow(int ProjectIndex, int ProjectId, int? TaskId, bool IsActive);

        #endregion
    }
}
