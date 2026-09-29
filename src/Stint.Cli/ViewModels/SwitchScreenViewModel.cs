using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Models;
using Stint.Cli.Services;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The Switch screen: pick a different active project/task to track time against. See
    /// <c>docs/update-v1.1.0/switch-render-48__*.txt</c> for the render mockups this takes its
    /// layout/labels from.
    /// </summary>
    /// <remarks>
    /// Reached from <see cref="HomeScreenViewModel.Switch"/> while clocked in. Loads today's
    /// attendance/open segment itself in <see cref="OnActivated"/> rather than taking it as a
    /// navigation context - same parameterless/root-style shape as <see cref="HomeScreenViewModel"/>.
    /// </remarks>
    public sealed partial class SwitchScreenViewModel : ScreenViewModelBase
    {
        #region Fields

        // Body rows available for the project/task tree: rows 5-18 of the 48x23 grid, per
        // docs/update-v1.1.0/switch-render-48__idle.txt (row 4 is the rule under the header, row
        // 19 is the pager line). A rough layout guess, same caveat as Home's ProjectRowsPerPage.
        private const int TreeRowBudget = 14;

        private readonly IStintDataGateway _gateway;
        private AttendanceLog? _attendanceLog;
        private ProjectTimeLog? _openLog;
        private IReadOnlyList<SwitchProjectRow> _projects = [];
        private List<SwitchFlatRow> _flatRows = [];
        private int _selectedIndex;
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
                new KeyHint("select", SelectCommand, ConsoleKey.Enter),
                new KeyHint("pause", PauseCommand, ConsoleKey.P),
                new KeyHint("done", DoneCommand, ConsoleKey.D),
                new KeyHint("new", BeginCreateProjectCommand, ConsoleKey.N),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape),
                new KeyHint("quit", QuitCommand, ConsoleKey.Q)
            ];
        }

        #endregion

        #region Properties

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
        // convention as Project's MoveSelectionUp/Down over CurrentPageTasks.
        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionUp() => MoveSelection(-1);

        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionDown() => MoveSelection(1);

        // Jumps to the adjacent page, wrapping at both ends, and always lands on that page's
        // first project row - simpler than Project's/Projects' "preserve relative row" since
        // Switch's pages have irregular, unrelated shapes (whole project groups, not fixed rows).
        [RelayCommand(CanExecute = nameof(CanChangePage))] private void PreviousPage() => ChangePage(-1);

        [RelayCommand(CanExecute = nameof(CanChangePage))] private void NextPage() => ChangePage(1);

        // Switches tracking to whatever's selected. CanSelect already rules out the row that's
        // already the running target, so getting here always means there's a real segment to
        // close (if anything's open) and a new one to start.
        [RelayCommand(CanExecute = nameof(CanSelect))] private async Task SelectAsync()
        {
            var selected = _flatRows[_selectedIndex];

            if (_attendanceLog is not null)
            {
                if (_openLog is not null)
                {
                    await _gateway.CloseTimeLogAsync(_openLog.Id, DateTime.Now, TimeLogCloseReason.Switched);
                }

                await _gateway.StartTimeLogAsync(_attendanceLog.Id, selected.ProjectId, selected.TaskId, DateTime.Now);
            }

            Navigation.GoBack();
        }

        // Stops tracking outright - closes whatever's open, starts nothing new, and leaves.
        [RelayCommand(CanExecute = nameof(CanPause))] private async Task PauseAsync()
        {
            if (_openLog is not null)
            {
                await _gateway.CloseTimeLogAsync(_openLog.Id, DateTime.Now, TimeLogCloseReason.Stopped);
            }

            Navigation.GoBack();
        }

        // Marks the selected task complete without leaving the screen, so a new target can be
        // picked right after. Only stops tracking if that task happened to be the running one -
        // marking some other task done shouldn't interrupt whatever's currently active.
        [RelayCommand(CanExecute = nameof(CanDone))] private async Task DoneAsync()
        {
            var selected = _flatRows[_selectedIndex];
            if (selected.TaskId is not int taskId)
            {
                return;
            }

            await _gateway.UpdateTaskStatusAsync(taskId, TaskItemStatus.Done);

            if (_openLog is not null && _openLog.TaskId == taskId)
            {
                await _gateway.CloseTimeLogAsync(_openLog.Id, DateTime.Now, TimeLogCloseReason.Stopped);
            }

            await RefreshAsync();
        }

        // Empty-state escape hatch, same idea as Home's own "[p] projects" when there's nothing
        // to show yet.
        [RelayCommand(CanExecute = nameof(CanBeginCreateProject))] private void BeginCreateProject()
            => Navigation.NavigateTo<ProjectsScreenViewModel>();

        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        [RelayCommand] private void Quit()
        {
            // TODO: replace with a proper shutdown hook (flush logs, dispose the DI container)
            // once the render pipeline/host loop exists - same TODO as every other screen's Quit.
            Environment.Exit(0);
        }

        #endregion

        #region CanExecute

        private bool CanMoveSelection() => _flatRows.Count > 0;

        private bool CanChangePage() => TotalPages > 1;

        // Blocks re-selecting the exact row that's already the running target (project-only
        // active, or the specific active task) - every sibling row (the active project's own
        // tasks, or the project row while one of its tasks is active) still has IsActive false
        // and stays selectable, so switching between a project and its own tasks always works.
        private bool CanSelect() => _selectedIndex >= 0 && _selectedIndex < _flatRows.Count && !_flatRows[_selectedIndex].IsActive;

        private bool CanPause() => _openLog is not null;

        private bool CanDone() => SelectedTaskId is not null;

        private bool CanBeginCreateProject() => Projects.Count == 0;

        private bool CanGoBack() => Navigation.CanGoBack;

        #endregion

        #region Helpers

        private void MoveSelection(int direction)
        {
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
