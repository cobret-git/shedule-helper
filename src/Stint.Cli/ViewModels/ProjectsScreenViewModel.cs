using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Models;
using Stint.Cli.Services;
using Stint.Cli.Components;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The Projects screen: the list of active projects, with inline create/rename and soft
    /// delete (with a same-visit undo). See <c>docs/update-v1.1.0/projects-render-48__*.txt</c>
    /// for the render mockups this takes its layout/labels from - row positions there are a
    /// rough guide, not exact, the same way <see cref="HomeScreenViewModel"/>'s were.
    /// </summary>
    /// <remarks>
    /// Same split of responsibility as Home: this class only exposes state and behavior: the
    /// render pipeline (<see cref="Stint.Cli.Views.ProjectsScreen"/>) decides how <see cref="Mode"/> maps
    /// to rows/colors, and is also the only thing that ever sees a raw <see cref="ConsoleKey"/> -
    /// free-form name entry reaches this class as plain characters via
    /// <see cref="AppendNameCharacter"/>/<see cref="RemoveNameCharacter"/>, never as a key.
    /// </remarks>
    public sealed partial class ProjectsScreenViewModel : ScreenViewModelBase
    {
        #region Fields

        // How many project rows the list shows per page - a rendering-layout assumption, not a
        // setting, matching the projects-render-48 mockups. Keep in sync with the actual
        // renderer once it's been tuned against a real console (see ProjectsScreen's own TODO).
        private const int PageSize = 14;

        // Matches Project.Name's [MaxLength(14)] - stops a name from being typed past what the
        // gateway would reject on commit anyway.
        private const int MaxNameLength = 14;

        private readonly IStintDataGateway _gateway;

        // Ids soft-deleted (SetProjectActiveAsync(id, false)) this screen visit, most recent on
        // top. Ctrl+Z pops one and reactivates it - handled entirely by ProjectsScreen.HandleKey
        // (see its remarks for why this isn't a KeyHint), which calls UndoLastDeleteAsync
        // directly. Resets whenever this screen is recreated, since the VM itself is transient.
        private readonly Stack<int> _undoDeleteStack = new();

        private ProjectsMode _mode = ProjectsMode.Idle;
        private IReadOnlyList<ProjectListRow> _projects = [];
        private int _selectedIndex;
        private string _nameInput = string.Empty;
        private int? _editingProjectId;
        private string? _loadError;
        #endregion

        #region Constructors

        public ProjectsScreenViewModel(INavigationService navigation, IStintDataGateway gateway)
            : base(navigation)
        {
            ArgumentNullException.ThrowIfNull(gateway);

            _gateway = gateway;

            Title = "PROJECTS";

            KeyHints =
            [
                new KeyHint("select", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("select", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("open", OpenCommand, ConsoleKey.Enter),
                new KeyHint("confirm", ConfirmCommand, ConsoleKey.Enter),
                new KeyHint("cancel", CancelCommand, ConsoleKey.Escape),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape),
                new KeyHint("edit", BeginEditCommand, ConsoleKey.E),
                new KeyHint("delete", DeleteCommand, ConsoleKey.D),
                new KeyHint("new", BeginCreateCommand, ConsoleKey.N),
                new KeyHint("quit", QuitCommand, ConsoleKey.Q)
            ];
        }

        #endregion

        #region Properties

        /// <summary>
        /// Which of Projects' three renders is current. Setting this also updates <see cref="Title"/>,
        /// so the title bar always shows what's actually being done (e.g. "PROJECTS - NEW") rather
        /// than a static "PROJECTS" no matter the mode.
        /// </summary>
        public ProjectsMode Mode
        {
            get => _mode;
            private set
            {
                if (SetProperty(ref _mode, value))
                {
                    Title = value switch
                    {
                        ProjectsMode.Creating => "PROJECTS - NEW",
                        ProjectsMode.Editing => "PROJECTS - EDIT",
                        _ => "PROJECTS"
                    };
                }
            }
        }

        /// <summary>The active projects, ordered by name (per <see cref="IStintDataGateway.GetActiveProjectsAsync"/>).</summary>
        public IReadOnlyList<ProjectListRow> Projects { get => _projects; private set => SetProperty(ref _projects, value); }

        /// <summary>The currently highlighted row's index into <see cref="Projects"/>.</summary>
        public int SelectedIndex { get => _selectedIndex; private set => SetProperty(ref _selectedIndex, value); }

        /// <summary>The name being typed while <see cref="Mode"/> is Creating or Editing.</summary>
        public string NameInput { get => _nameInput; private set => SetProperty(ref _nameInput, value); }

        /// <summary>The error from the last failed refresh, if any.</summary>
        public string? LoadError { get => _loadError; private set => SetProperty(ref _loadError, value); }

        /// <summary>The row at <see cref="SelectedIndex"/>, or null if <see cref="Projects"/> is empty.</summary>
        public ProjectListRow? SelectedProject => SelectedIndex >= 0 && SelectedIndex < Projects.Count ? Projects[SelectedIndex] : null;

        /// <summary>Whether <see cref="NameInput"/> is currently being typed into.</summary>
        public bool IsEditingName => Mode is ProjectsMode.Creating or ProjectsMode.Editing;

        /// <summary>
        /// Whether <see cref="NameInput"/> (trimmed) collides with another active project's name -
        /// checked locally against the already-loaded <see cref="Projects"/> rather than a fresh
        /// gateway call per keystroke, since the whole active list is already in memory. The
        /// project currently being renamed never collides with its own (unchanged) name.
        /// </summary>
        public bool IsNameTaken => Projects.Any(p =>
            p.Id != _editingProjectId && string.Equals(p.Name, NameInput.Trim(), StringComparison.OrdinalIgnoreCase));

        /// <summary>The "TAKEN"/"OK" label shown next to <see cref="NameInput"/> while typing.</summary>
        public string NameStatusLabel => string.IsNullOrWhiteSpace(NameInput) ? string.Empty : (IsNameTaken ? "TAKEN" : "OK");

        /// <summary>Number of pages <see cref="CurrentPageProjects"/> paginates over.</summary>
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(Projects.Count / (double)PageSize));

        /// <summary>Which page <see cref="SelectedIndex"/> currently falls on.</summary>
        public int CurrentPageIndex => Projects.Count == 0 ? 0 : SelectedIndex / PageSize;

        /// <summary><see cref="SelectedIndex"/> relative to the start of <see cref="CurrentPageProjects"/>.</summary>
        public int SelectedIndexOnPage => SelectedIndex - CurrentPageIndex * PageSize;

        /// <summary>The slice of <see cref="Projects"/> for <see cref="CurrentPageIndex"/>.</summary>
        public IReadOnlyList<ProjectListRow> CurrentPageProjects => Projects
            .Skip(CurrentPageIndex * PageSize)
            .Take(PageSize)
            .ToList();

        /// <summary>Whether Ctrl+Z (handled by the view, not a KeyHint) would do anything right now.</summary>
        public bool CanUndoDelete => Mode == ProjectsMode.Idle && _undoDeleteStack.Count > 0;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void OnActivated()
        {
            // Fire-and-forget for the same reason as Home's OnActivated: a local SQLite read is
            // fast enough that the one-frame stale gap is harmless.
            _ = RefreshAsync();
        }

        /// <summary>
        /// Appends one character to <see cref="NameInput"/>, while <see cref="IsEditingName"/>.
        /// The render pipeline decides which physical key counts as printable text - this method
        /// takes the character itself, never a <see cref="ConsoleKey"/>/<see cref="ConsoleKeyInfo"/>.
        /// </summary>
        public void AppendNameCharacter(char character)
        {
            if (!IsEditingName || char.IsControl(character) || NameInput.Length >= MaxNameLength)
            {
                return;
            }

            NameInput += character;
        }

        /// <summary>
        /// Removes the last typed character of <see cref="NameInput"/>, while <see cref="IsEditingName"/>.
        /// If there's nothing left to remove, cancels back to <see cref="ProjectsMode.Idle"/> instead
        /// of leaving an empty name field sitting open.
        /// </summary>
        public void RemoveNameCharacter()
        {
            if (!IsEditingName)
            {
                return;
            }

            if (NameInput.Length > 0)
            {
                NameInput = NameInput[..^1];
            }
            else
            {
                Cancel();
            }
        }

        /// <summary>Pops the most recently soft-deleted project and reactivates it. See <see cref="CanUndoDelete"/>.</summary>
        public async Task UndoLastDeleteAsync()
        {
            if (_undoDeleteStack.Count == 0)
            {
                return;
            }

            var projectId = _undoDeleteStack.Pop();
            await _gateway.SetProjectActiveAsync(projectId, true);
            await RefreshAsync(projectId);
        }

        #endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionUp()
            => SelectedIndex = Math.Max(0, SelectedIndex - 1);

        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionDown()
            => SelectedIndex = Math.Min(Projects.Count - 1, SelectedIndex + 1);

        // Enter's behavior depends on Mode: commits the typed name while Creating/Editing. Idle
        // is handled by the separate Open command below instead - CanConfirm keeps this one
        // inert then, so the footer shows "[enter] open" rather than "[enter] confirm".
        [RelayCommand(CanExecute = nameof(CanConfirm))] private async Task ConfirmAsync()
        {
            switch (Mode)
            {
                case ProjectsMode.Creating:
                    var created = await _gateway.AddProjectAsync(new Project { Name = NameInput.Trim() });
                    Mode = ProjectsMode.Idle;
                    NameInput = string.Empty;
                    await RefreshAsync(created.Id);
                    break;

                case ProjectsMode.Editing:
                    if (_editingProjectId is int id)
                    {
                        await _gateway.UpdateProjectAsync(new Project { Id = id, Name = NameInput.Trim(), IsActive = true });

                        Mode = ProjectsMode.Idle;
                        _editingProjectId = null;
                        NameInput = string.Empty;
                        await RefreshAsync(id);
                    }
                    break;

                case ProjectsMode.Idle:
                    break;
            }
        }

        // Enter while Idle: drills into the selected project's task list. Bound to the same key
        // as Confirm above - CanOpen/CanConfirm are never true at the same time, so exactly one
        // of the two ever shows in the footer.
        [RelayCommand(CanExecute = nameof(CanOpen))] private void Open()
        {
            var selected = SelectedProject;
            if (selected is null)
            {
                return;
            }

            Navigation.NavigateTo<ProjectScreenViewModel, ProjectListRow>(selected);
        }

        // Esc while Creating/Editing: discards whatever was typed (and, while Creating, the
        // never-persisted project along with it - nothing is written to the gateway until
        // Confirm) and drops back to Idle, staying on this screen. Esc again from Idle is what
        // actually leaves - see GoBack - so backing all the way out to Home always takes two
        // presses once you're mid-edit, never one.
        [RelayCommand(CanExecute = nameof(CanCancel))] private void Cancel()
        {
            Mode = ProjectsMode.Idle;
            _editingProjectId = null;
            NameInput = string.Empty;
        }

        // Esc while Idle: leaves Projects and returns to Home. CanGoBack requires Idle so this
        // never fires ahead of Cancel above - both are bound to the same key, and ConsoleHost
        // dispatch takes the first hint (in KeyHints order) whose command can execute.
        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        [RelayCommand(CanExecute = nameof(CanBeginCreate))] private void BeginCreate()
        {
            Mode = ProjectsMode.Creating;
            _editingProjectId = null;
            NameInput = string.Empty;
        }

        [RelayCommand(CanExecute = nameof(CanBeginEdit))] private void BeginEdit()
        {
            var selected = SelectedProject;
            if (selected is null)
            {
                return;
            }

            Mode = ProjectsMode.Editing;
            _editingProjectId = selected.Id;
            NameInput = selected.Name;
        }

        // Soft delete only - there's no hard-delete in the gateway, and GetActiveProjectsAsync
        // already filters to IsActive, so deactivating is enough to drop it from this list. The
        // id goes on the undo stack so Ctrl+Z (see CanUndoDelete) can bring it right back.
        [RelayCommand(CanExecute = nameof(CanDelete))] private async Task DeleteAsync()
        {
            var selected = SelectedProject;
            if (selected is null)
            {
                return;
            }

            await _gateway.SetProjectActiveAsync(selected.Id, false);
            _undoDeleteStack.Push(selected.Id);
            await RefreshAsync();
        }

        [RelayCommand] private void Quit()
        {
            // TODO: replace with a proper shutdown hook (flush logs, dispose the DI container)
            // once the render pipeline/host loop exists - same TODO as Home's Quit.
            Environment.Exit(0);
        }

        #endregion

        #region CanExecute

        private bool CanMoveSelection() => Mode == ProjectsMode.Idle && Projects.Count > 0;

        private bool CanConfirm() => Mode switch
        {
            ProjectsMode.Creating or ProjectsMode.Editing => IsNameValid,
            _ => false
        };

        private bool CanCancel() => Mode != ProjectsMode.Idle;

        private bool CanGoBack() => Mode == ProjectsMode.Idle && Navigation.CanGoBack;

        private bool CanOpen() => Mode == ProjectsMode.Idle && Projects.Count > 0;

        private bool CanBeginCreate() => Mode == ProjectsMode.Idle;

        private bool CanBeginEdit() => Mode == ProjectsMode.Idle && Projects.Count > 0;

        private bool CanDelete() => Mode == ProjectsMode.Idle && Projects.Count > 0;

        private bool IsNameValid => !string.IsNullOrWhiteSpace(NameInput) && !IsNameTaken;

        #endregion

        #region Helpers

        private async Task RefreshAsync(int? selectProjectId = null)
        {
            try
            {
                var activeProjects = await _gateway.GetActiveProjectsAsync();
                var rows = new List<ProjectListRow>(activeProjects.Count);

                foreach (var project in activeProjects)
                {
                    var taskCount = await _gateway.GetTaskCountForProjectAsync(project.Id);
                    rows.Add(new ProjectListRow { Id = project.Id, Name = project.Name, TaskCount = taskCount });
                }

                Projects = rows;

                var targetIndex = selectProjectId is int id
                    ? rows.FindIndex(r => r.Id == id)
                    : SelectedIndex;

                SelectedIndex = Math.Clamp(targetIndex < 0 ? 0 : targetIndex, 0, Math.Max(0, rows.Count - 1));
                LoadError = null;
            }
            catch (Exception ex)
            {
                // No logging pipeline exists yet - keep the error on the VM itself rather than
                // losing it silently, same approach as Home's RefreshAsync.
                LoadError = ex.Message;
            }
        }

        #endregion
    }
}
