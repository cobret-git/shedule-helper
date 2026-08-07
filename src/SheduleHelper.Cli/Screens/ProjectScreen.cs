using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Drilled into from <see cref="ProjectsScreen"/> - a single project's task list. The project's
    /// own description isn't shown here - a project is a single, brief row in
    /// <see cref="ProjectsScreen"/>, not something this screen needs to keep re-explaining every
    /// time you're just here to work through its tasks. Each task's own details (status, logged
    /// time, description, ...) live in the inspector pane beside the list instead - see
    /// <see cref="Inspector"/>.
    /// </summary>
    public sealed class ProjectScreen : IScreen
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly int _projectId;
        private readonly IPathProvider _pathProvider;
        private readonly ILogger _logger = Log.ForContext<ProjectScreen>();

        private Project? _project;
        private List<Row> _rows = new();
        private SelectList _list = new(1);
        private string? _message;
        private bool _confirmingDelete;

        // Column widths for the task table. Task title is the only column that grows/shrinks with
        // the list region's width - Status and Duration are small, fixed-width values that always fit.
        private const int MinTitleColumnWidth = 10;
        private const int TitlePrefixWidth = 2;   // "{marker} "
        private const int TitleToStatusGap = 1;   // guarantees a visible gap even when the title is
                                                   // truncated - padding alone isn't reliable here
                                                   // since "…" can render wider than one column.
        private const int StatusColumnWidth = 14;
        private const int DurationColumnWidth = 8;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectScreen"/> class.
        /// </summary>
        public ProjectScreen(ILocalDbContextFactory dbContextFactory, ICurrentUserContext currentUserContext, int projectId, IPathProvider pathProvider)
        {
            _dbContextFactory = dbContextFactory;
            _currentUserContext = currentUserContext;
            _projectId = projectId;
            _pathProvider = pathProvider;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public Task OnEnter() => LoadAsync();

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            if (_project is null)
            {
                Header.Draw(frame, "PROJECT", "Esc Back");
                frame.Write(1, 3, _message ?? "Loading...", ColorToken.Dim);
                return;
            }

            var project = _project;
            Header.Draw(frame, $"PROJECTS > {project.Name}", "Esc Back");

            // The body splits into the task list and an inspector pane once there's room for both -
            // see Inspector.ShouldShowPane. Below that width the list simply takes the full body,
            // same as before the pane existed; "Open" (see the key bar below) is how a narrow
            // terminal still gets to see a task's details.
            // The body owns every row between the header's rule and the key bar's own rule, so the
            // divider below runs the full height of the split rather than stopping short of it.
            const int bodyTop = 2;
            var bodyHeight = Math.Max(1, frame.Height - bodyTop - 2);
            var showPane = Inspector.ShouldShowPane(frame.Width);
            var paneWidth = showPane ? Inspector.PaneWidth(frame.Width) : 0;
            var listWidth = showPane ? frame.Width - paneWidth - 1 : frame.Width;

            var list = new Region(frame, 0, bodyTop, listWidth, bodyHeight);

            list.Write(1, 1, "Tasks", ColorToken.Accent);
            var statusText = project.IsActive ? "active" : "archived";
            list.WriteRight(list.Width - 1, 1, statusText, project.IsActive ? ColorToken.Positive : ColorToken.Dim);
            list.Rule(2);

            if (_rows.Count == 0)
            {
                list.Write(1, 4, "No tasks yet - press N to create one.", ColorToken.Dim);
            }

            var titleWidth = TitleColumnWidth(list.Width);

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var selected = _list.SelectedIndex == i;
                var marker = selected ? "►" : " ";
                var color = selected ? ColorToken.Accent : ColorToken.Default;
                var title = Formatting.Truncate(row.Task.Title, titleWidth).PadRight(titleWidth);

                list.Write(1, 3 + i, $"{marker} {title} {StatusLabel(row.Task.Status),-14}{Formatting.Duration(row.LoggedTime),8}", color);
            }

            if (showPane)
            {
                frame.VRule(listWidth, bodyTop, bodyHeight);
                var pane = new Region(frame, listWidth + 1, bodyTop, paneWidth, bodyHeight);

                if (_rows.Count > 0)
                {
                    Inspector.Draw(pane, BuildInspectorContent(_rows[_list.SelectedIndex]));
                }
                else
                {
                    pane.Write(1, 1, "No task selected.", ColorToken.Dim);
                }
            }

            // Drawn into the list region rather than the frame, and after the rows above, so a long
            // message is clipped at the divider instead of running under the pane - and so it wins
            // the last row over any task row that reaches it.
            if (_confirmingDelete)
            {
                list.Write(1, list.Height - 1, "Delete this task? Y to confirm, any other key to cancel.", ColorToken.Negative);
            }
            else if (!string.IsNullOrWhiteSpace(_message))
            {
                list.Write(1, list.Height - 1, _message, ColorToken.Negative);
            }

            var keyBindings = new List<(string Key, string Label)>
            {
                ("N", "New task"), ("E", "Edit"), ("D", "Cycle status"), ("Del", "Delete"), ("P", "Edit project"),
            };

            if (_rows.Count > 0)
            {
                keyBindings.Add(("Enter", "Open"));
            }

            keyBindings.Add(("Esc", "Back"));
            KeyBar.Draw(frame, keyBindings.ToArray());
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            if (_project is null)
            {
                if (key.Key == ConsoleKey.Escape)
                {
                    await screens.Pop();
                }

                return;
            }

            if (_confirmingDelete)
            {
                _confirmingDelete = false;
                if (key.Key == ConsoleKey.Y)
                {
                    await DeleteSelectedAsync();
                }

                return;
            }

            if (_list.HandleKey(key))
            {
                return;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    await screens.Pop();
                    break;
                case ConsoleKey.Enter when _rows.Count > 0:
                    await screens.Push(new TaskViewScreen(BuildInspectorContent(_rows[_list.SelectedIndex])));
                    break;
                case ConsoleKey.N:
                    await screens.Push(new TaskEditScreen(_dbContextFactory, _project.Id, null, _pathProvider));
                    break;
                case ConsoleKey.E when _rows.Count > 0:
                    await screens.Push(new TaskEditScreen(_dbContextFactory, _project.Id, _rows[_list.SelectedIndex].Task, _pathProvider));
                    break;
                case ConsoleKey.D when _rows.Count > 0:
                    await CycleStatusAsync();
                    break;
                case ConsoleKey.Delete when _rows.Count > 0:
                    _confirmingDelete = true;
                    break;
                case ConsoleKey.P:
                    await screens.Push(new ProjectEditScreen(_dbContextFactory, _currentUserContext, _project));
                    break;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Builds what the inspector pane (and the full-screen "Open" view) shows for a task row -
        /// its title as the heading, status/logged time/last-tracked as facts, and its description
        /// underneath.
        /// </summary>
        private static InspectorContent BuildInspectorContent(Row row)
        {
            var facts = new List<(string Label, string Value, ColorToken Color)>
            {
                ("Status", StatusLabel(row.Task.Status), row.Task.Status == TaskItemStatus.Done ? ColorToken.Positive : ColorToken.Default),
                ("Logged", Formatting.Duration(row.LoggedTime), ColorToken.Default),
                ("Last", row.LastTracked is { } last ? $"{last:ddd d MMM} {Formatting.Time(last)}" : "never tracked", ColorToken.Dim),
            };

            return new InspectorContent(row.Task.Title, facts, row.Task.Description);
        }

        /// <summary>
        /// Computes how wide the Task title column can be for the given list region width, so it
        /// grows to use available space instead of always clipping at a fixed width.
        /// </summary>
        private static int TitleColumnWidth(int listWidth)
        {
            var fixedWidth = TitlePrefixWidth + TitleToStatusGap + StatusColumnWidth + DurationColumnWidth;
            return Math.Max(MinTitleColumnWidth, listWidth - fixedWidth - 1);
        }

        private static string StatusLabel(TaskItemStatus status) => status switch
        {
            TaskItemStatus.Todo => "todo",
            TaskItemStatus.InProgress => "in progress",
            TaskItemStatus.Done => "done",
            _ => status.ToString(),
        };

        /// <summary>
        /// Groups already-loaded time log segments by task, keeping only the most recent finished
        /// one per task - the inspector pane's "Last" fact. A task with no finished segment at all
        /// is simply absent from the result, so callers see that as "never tracked" rather than some
        /// sentinel date.
        /// </summary>
        private static Dictionary<int, DateTime> LastTrackedByTask(IEnumerable<ProjectTimeLog> logs)
        {
            return logs
                .Where(l => l.EndTime is not null && l.TaskId is not null)
                .GroupBy(l => l.TaskId!.Value)
                .ToDictionary(g => g.Key, g => g.Max(l => l.EndTime!.Value));
        }

        private async Task LoadAsync()
        {
            try
            {
                await using var db = _dbContextFactory.CreateDbContext();

                var project = await db.Projects.FindAsync(new object?[] { _projectId }, CancellationToken.None);
                if (project is null)
                {
                    _message = "This project no longer exists.";
                    return;
                }

                _project = project;

                var tasks = (await db.GetTasksByProjectIdAsync(_projectId, CancellationToken.None))
                    .OrderBy(t => t.Title)
                    .ToList();

                var logs = await db.GetProjectTimeLogsAsync(project.UserId, DateTime.MinValue, DateTime.Now, CancellationToken.None);
                var totals = TimeBudgetCalculator.SummarizeByTask(logs);
                var lastTracked = LastTrackedByTask(logs);

                _rows = tasks
                    .Select(t => new Row(t, totals.GetValueOrDefault(t.Id, TimeSpan.Zero), lastTracked.TryGetValue(t.Id, out var last) ? last : null))
                    .ToList();
                _list = new SelectList(Math.Max(_rows.Count, 1));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load project {ProjectId}.", _projectId);
                _message = "Failed to load this project.";
            }
        }

        private async Task CycleStatusAsync()
        {
            var task = _rows[_list.SelectedIndex].Task;
            var nextStatus = task.Status switch
            {
                TaskItemStatus.Todo => TaskItemStatus.InProgress,
                TaskItemStatus.InProgress => TaskItemStatus.Done,
                _ => TaskItemStatus.Todo,
            };

            try
            {
                await using var db = _dbContextFactory.CreateDbContext();
                task.Status = nextStatus;
                await db.UpdateTaskAsync(task, CancellationToken.None);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to update task {TaskId} status.", task.Id);
                _message = "Something went wrong updating the task.";
            }
        }

        private async Task DeleteSelectedAsync()
        {
            var task = _rows[_list.SelectedIndex].Task;
            try
            {
                await using var db = _dbContextFactory.CreateDbContext();
                await db.DeleteTaskAsync(task.Id, CancellationToken.None);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete task {TaskId}.", task.Id);
                _message = "Something went wrong deleting the task.";
            }
        }

        #endregion

        #region Structures

        private readonly record struct Row(TaskItem Task, TimeSpan LoggedTime, DateTime? LastTracked);

        #endregion
    }
}
