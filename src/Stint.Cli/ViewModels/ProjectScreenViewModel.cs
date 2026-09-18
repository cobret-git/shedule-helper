using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Models;
using Stint.Cli.Services;
using Stint.Cli.Components;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The Project screen: one project's task list, with inline create/rename and a
    /// mark-then-confirm batch delete. See <c>docs/update-v1.1.0/project-render-48__*.txt</c>
    /// for the render mockups this takes its layout/labels from, same caveat as
    /// <see cref="ProjectsScreenViewModel"/> about row positions being a rough guide.
    /// </summary>
    /// <remarks>
    /// Reached from <see cref="ProjectsScreenViewModel"/> via <see cref="Initialize"/>, which
    /// hands over the selected <see cref="ProjectListRow"/> - just enough (id + name) to load
    /// this project's tasks and label the header, without needing the full <see cref="Project"/>
    /// entity. Same split of responsibility as Projects: this class only exposes state and
    /// behavior, <see cref="Stint.Cli.Views.ProjectScreen"/> decides how <see cref="Mode"/> maps
    /// to rows/colors, and is also the only thing that ever sees a raw <see cref="ConsoleKey"/> -
    /// free-form title entry reaches this class as plain characters via
    /// <see cref="AppendTitleCharacter"/>/<see cref="RemoveTitleCharacter"/>, never as a key.
    /// </remarks>
    public sealed partial class ProjectScreenViewModel : ScreenViewModelBase, IScreenViewModel<ProjectListRow>
    {
        #region Fields

        // How many task rows the list shows per page - one less than Projects' own PageSize
        // since row 3 here is spent on the project-name header instead of the first task.
        private const int PageSize = 13;

        // Matches TaskItem.Title's [MaxLength(16)] - stops a title from being typed past what
        // the gateway would reject on commit anyway.
        private const int MaxTitleLength = 16;

        private readonly IStintDataGateway _gateway;

        // Ids currently marked for deletion while Mode is ConfirmingDelete - never touched
        // outside that mode, and always empty on the way back out of it (whether that's from
        // Confirm, which clears it after the gateway calls, or Cancel, which clears it and
        // discards it instead).
        private readonly HashSet<int> _pendingDeleteIds = [];

        private ProjectListRow _project = null!;
        private ProjectMode _mode = ProjectMode.Idle;
        private IReadOnlyList<TaskItem> _tasks = [];
        private int _selectedIndex;
        private string _titleInput = string.Empty;
        private int? _editingTaskId;
        private TaskItemStatus _editingTaskStatus;
        private string? _loadError;
        #endregion

        #region Constructors

        public ProjectScreenViewModel(INavigationService navigation, IStintDataGateway gateway)
            : base(navigation)
        {
            ArgumentNullException.ThrowIfNull(gateway);

            _gateway = gateway;

            Title = "PROJECT";

            KeyHints =
            [
                new KeyHint("select", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("select", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("confirm", ConfirmCommand, ConsoleKey.Enter),
                new KeyHint("cancel", CancelCommand, ConsoleKey.Escape),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape),
                new KeyHint("edit", BeginEditCommand, ConsoleKey.E),
                new KeyHint("delete", BeginDeleteCommand, ConsoleKey.D),
                new KeyHint("mark", ToggleMarkForDeleteCommand, ConsoleKey.D),
                new KeyHint("new", BeginCreateCommand, ConsoleKey.N),
                new KeyHint("quit", QuitCommand, ConsoleKey.Q)
            ];
        }

        #endregion

        #region Properties

        /// <summary>Which of Project's four renders is current.</summary>
        public ProjectMode Mode
        {
            get => _mode;
            private set
            {
                if (SetProperty(ref _mode, value))
                {
                    Title = value switch
                    {
                        ProjectMode.Creating => "PROJECT - NEW",
                        ProjectMode.Editing => "PROJECT - EDIT",
                        ProjectMode.ConfirmingDelete => "PROJECT - DELETE",
                        _ => "PROJECT"
                    };
                }
            }
        }

        /// <summary>The project this screen is showing the tasks of.</summary>
        public string ProjectName => _project.Name;

        /// <summary>This project's active tasks, in creation order.</summary>
        public IReadOnlyList<TaskItem> Tasks { get => _tasks; private set => SetProperty(ref _tasks, value); }

        /// <summary>The currently highlighted row's index into <see cref="Tasks"/>.</summary>
        public int SelectedIndex { get => _selectedIndex; private set => SetProperty(ref _selectedIndex, value); }

        /// <summary>The title being typed while <see cref="Mode"/> is Creating or Editing.</summary>
        public string TitleInput { get => _titleInput; private set => SetProperty(ref _titleInput, value); }

        /// <summary>The error from the last failed refresh, if any.</summary>
        public string? LoadError { get => _loadError; private set => SetProperty(ref _loadError, value); }

        /// <summary>The row at <see cref="SelectedIndex"/>, or null if <see cref="Tasks"/> is empty.</summary>
        public TaskItem? SelectedTask => SelectedIndex >= 0 && SelectedIndex < Tasks.Count ? Tasks[SelectedIndex] : null;

        /// <summary>Whether <see cref="TitleInput"/> is currently being typed into.</summary>
        public bool IsEditingTitle => Mode is ProjectMode.Creating or ProjectMode.Editing;

        /// <summary>The ids currently marked for deletion - only meaningful while <see cref="Mode"/> is ConfirmingDelete.</summary>
        public IReadOnlySet<int> PendingDeleteIds => _pendingDeleteIds;

        /// <summary>Number of pages <see cref="CurrentPageTasks"/> paginates over.</summary>
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Tasks.Count / (double)PageSize));

        /// <summary>Which page <see cref="SelectedIndex"/> currently falls on.</summary>
        public int CurrentPageIndex => Tasks.Count == 0 ? 0 : SelectedIndex / PageSize;

        /// <summary><see cref="SelectedIndex"/> relative to the start of <see cref="CurrentPageTasks"/>.</summary>
        public int SelectedIndexOnPage => SelectedIndex - CurrentPageIndex * PageSize;

        /// <summary>The slice of <see cref="Tasks"/> for <see cref="CurrentPageIndex"/>.</summary>
        public IReadOnlyList<TaskItem> CurrentPageTasks => Tasks
            .Skip(CurrentPageIndex * PageSize)
            .Take(PageSize)
            .ToList();

        #endregion

        #region Methods

        /// <inheritdoc />
        public void Initialize(ProjectListRow context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _project = context;
        }

        /// <inheritdoc />
        public override void OnActivated()
        {
            // Fire-and-forget for the same reason as Projects' OnActivated: a local SQLite read
            // is fast enough that the one-frame stale gap is harmless.
            _ = RefreshAsync();
        }

        /// <summary>
        /// Appends one character to <see cref="TitleInput"/>, while <see cref="IsEditingTitle"/>.
        /// The render pipeline decides which physical key counts as printable text - this method
        /// takes the character itself, never a <see cref="ConsoleKey"/>/<see cref="ConsoleKeyInfo"/>.
        /// </summary>
        public void AppendTitleCharacter(char character)
        {
            if (!IsEditingTitle || char.IsControl(character) || TitleInput.Length >= MaxTitleLength)
            {
                return;
            }

            TitleInput += character;
        }

        /// <summary>
        /// Removes the last typed character of <see cref="TitleInput"/>, while <see cref="IsEditingTitle"/>.
        /// If there's nothing left to remove, cancels back to <see cref="ProjectMode.Idle"/> instead
        /// of leaving an empty title field sitting open.
        /// </summary>
        public void RemoveTitleCharacter()
        {
            if (!IsEditingTitle)
            {
                return;
            }

            if (TitleInput.Length > 0)
            {
                TitleInput = TitleInput[..^1];
            }
            else
            {
                Cancel();
            }
        }

        #endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionUp()
            => SelectedIndex = Math.Max(0, SelectedIndex - 1);

        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionDown()
            => SelectedIndex = Math.Min(Tasks.Count - 1, SelectedIndex + 1);

        // Enter's behavior depends on Mode: commits the typed title while Creating/Editing, or
        // commits the batch delete while ConfirmingDelete. CanConfirm keeps it inert while Idle -
        // there's nothing further to open from here, unlike Projects' own Enter.
        [RelayCommand(CanExecute = nameof(CanConfirm))] private async Task ConfirmAsync()
        {
            switch (Mode)
            {
                case ProjectMode.Creating:
                    var created = await _gateway.AddTaskAsync(new TaskItem { ProjectId = _project.Id, Title = TitleInput.Trim() });
                    Mode = ProjectMode.Idle;
                    TitleInput = string.Empty;
                    await RefreshAsync(created.Id);
                    break;

                case ProjectMode.Editing:
                    if (_editingTaskId is int id)
                    {
                        await _gateway.UpdateTaskAsync(new TaskItem
                        {
                            Id = id,
                            ProjectId = _project.Id,
                            Title = TitleInput.Trim(),
                            Status = _editingTaskStatus,
                            IsActive = true
                        });

                        Mode = ProjectMode.Idle;
                        _editingTaskId = null;
                        TitleInput = string.Empty;
                        await RefreshAsync(id);
                    }
                    break;

                case ProjectMode.ConfirmingDelete:
                    foreach (var taskId in _pendingDeleteIds)
                    {
                        await _gateway.SetTaskActiveAsync(taskId, false);
                    }

                    _pendingDeleteIds.Clear();
                    Mode = ProjectMode.Idle;
                    await RefreshAsync();
                    break;

                case ProjectMode.Idle:
                    break;
            }
        }

        // Esc while Creating/Editing/ConfirmingDelete: discards whatever was in progress (typed
        // title, or every pending delete mark - nothing is written to the gateway until Confirm)
        // and drops back to Idle, staying on this screen. Esc again from Idle is what actually
        // leaves - see GoBack - so backing all the way out to Projects always takes two presses
        // once you're mid-edit/mid-delete, never one.
        [RelayCommand(CanExecute = nameof(CanCancel))] private void Cancel()
        {
            Mode = ProjectMode.Idle;
            _editingTaskId = null;
            TitleInput = string.Empty;
            _pendingDeleteIds.Clear();
        }

        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        [RelayCommand(CanExecute = nameof(CanBeginCreate))] private void BeginCreate()
        {
            Mode = ProjectMode.Creating;
            _editingTaskId = null;
            TitleInput = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanBeginEdit))] private void BeginEdit()
        {
            var selected = SelectedTask;
            if (selected is null)
            {
                return;
            }

            Mode = ProjectMode.Editing;
            _editingTaskId = selected.Id;
            _editingTaskStatus = selected.Status;
            TitleInput = selected.Title;
        }

        // Enters the mark-then-confirm delete flow, immediately marking whatever was selected
        // when delete was pressed - the "red indicator" shows up on it right away rather than
        // requiring a separate first toggle just to mark the obvious one.
        [RelayCommand(CanExecute = nameof(CanBeginDelete))] private void BeginDelete()
        {
            var selected = SelectedTask;
            if (selected is null)
            {
                return;
            }

            Mode = ProjectMode.ConfirmingDelete;
            _pendingDeleteIds.Clear();
            _pendingDeleteIds.Add(selected.Id);
            OnPropertyChanged(nameof(PendingDeleteIds));
        }

        // D while ConfirmingDelete: adds or removes the currently selected task from the
        // pending-delete set, without touching the gateway - only Confirm actually deletes. Bound
        // to the same key as BeginDelete above - CanToggleMarkForDelete/CanBeginDelete are never
        // true at the same time, so exactly one of the two ever fires.
        // Unmarking the last remaining one drops straight back to Idle, same as Esc would, since
        // an empty mark set means there's nothing left to confirm.
        [RelayCommand(CanExecute = nameof(CanToggleMarkForDelete))] private void ToggleMarkForDelete()
        {
            var selected = SelectedTask;
            if (selected is null)
            {
                return;
            }

            if (!_pendingDeleteIds.Remove(selected.Id))
            {
                _pendingDeleteIds.Add(selected.Id);
                OnPropertyChanged(nameof(PendingDeleteIds));
                return;
            }

            if (_pendingDeleteIds.Count == 0)
            {
                Mode = ProjectMode.Idle;
            }

            OnPropertyChanged(nameof(PendingDeleteIds));
        }

        [RelayCommand] private void Quit()
        {
            // TODO: replace with a proper shutdown hook (flush logs, dispose the DI container)
            // once the render pipeline/host loop exists - same TODO as Projects' Quit.
            Environment.Exit(0);
        }

        #endregion

        #region CanExecute

        private bool CanMoveSelection() => Mode is ProjectMode.Idle or ProjectMode.ConfirmingDelete && Tasks.Count > 0;

        private bool CanConfirm() => Mode switch
        {
            ProjectMode.Creating or ProjectMode.Editing => IsTitleValid,
            ProjectMode.ConfirmingDelete => _pendingDeleteIds.Count > 0,
            _ => false
        };

        private bool CanCancel() => Mode != ProjectMode.Idle;

        private bool CanGoBack() => Mode == ProjectMode.Idle && Navigation.CanGoBack;

        private bool CanBeginCreate() => Mode == ProjectMode.Idle;

        private bool CanBeginEdit() => Mode == ProjectMode.Idle && Tasks.Count > 0;

        private bool CanBeginDelete() => Mode == ProjectMode.Idle && Tasks.Count > 0;

        private bool CanToggleMarkForDelete() => Mode == ProjectMode.ConfirmingDelete && Tasks.Count > 0;

        private bool IsTitleValid => !string.IsNullOrWhiteSpace(TitleInput);

        #endregion

        #region Helpers

        private async Task RefreshAsync(int? selectTaskId = null)
        {
            try
            {
                var tasks = await _gateway.GetTasksForProjectAsync(_project.Id);
                Tasks = tasks;

                var targetIndex = selectTaskId is int id
                    ? tasks.FindIndex(t => t.Id == id)
                    : SelectedIndex;

                SelectedIndex = Math.Clamp(targetIndex < 0 ? 0 : targetIndex, 0, Math.Max(0, tasks.Count - 1));
                LoadError = null;
            }
            catch (Exception ex)
            {
                // No logging pipeline exists yet - keep the error on the VM itself rather than
                // losing it silently, same approach as Projects' RefreshAsync.
                LoadError = ex.Message;
            }
        }

        #endregion
    }
}
