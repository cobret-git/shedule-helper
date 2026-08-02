using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Drilled into from <see cref="ProjectsScreen"/> - a single project's details and task list.
    /// </summary>
    public sealed class ProjectScreen : IScreen
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly int _projectId;
        private readonly ILogger _logger = Log.ForContext<ProjectScreen>();

        private Project? _project;
        private List<Row> _rows = new();
        private SelectList _list = new(1);
        private string? _message;
        private bool _confirmingDelete;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectScreen"/> class.
        /// </summary>
        public ProjectScreen(ILocalDbContextFactory dbContextFactory, ICurrentUserContext currentUserContext, int projectId)
        {
            _dbContextFactory = dbContextFactory;
            _currentUserContext = currentUserContext;
            _projectId = projectId;
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

            frame.Write(1, 3, project.Description ?? "(no description)", ColorToken.Dim);
            frame.WriteRight(frame.Width - 1, 3, project.IsActive ? "active" : "archived", project.IsActive ? ColorToken.Positive : ColorToken.Dim);

            frame.Write(1, 5, "Tasks", ColorToken.Accent);
            frame.Rule(6);

            if (_rows.Count == 0)
            {
                frame.Write(1, 8, "No tasks yet - press N to create one.", ColorToken.Dim);
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var selected = _list.SelectedIndex == i;
                var marker = selected ? "►" : " ";
                var color = selected ? ColorToken.Accent : ColorToken.Default;

                frame.Write(1, 7 + i, $"{marker} {row.Task.Title,-30}{StatusLabel(row.Task.Status),-14}{Formatting.Duration(row.LoggedTime),8}", color);
            }

            if (_confirmingDelete)
            {
                frame.Write(1, frame.Height - 4, "Delete this task? Y to confirm, any other key to cancel.", ColorToken.Negative);
            }
            else if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("N", "New task"), ("E", "Edit"), ("D", "Cycle status"), ("Del", "Delete"), ("P", "Edit project"), ("Esc", "Back"));
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
                case ConsoleKey.N:
                    await screens.Push(new TaskEditScreen(_dbContextFactory, _project.Id, null));
                    break;
                case ConsoleKey.E when _rows.Count > 0:
                    await screens.Push(new TaskEditScreen(_dbContextFactory, _project.Id, _rows[_list.SelectedIndex].Task));
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

        private static string StatusLabel(TaskItemStatus status) => status switch
        {
            TaskItemStatus.Todo => "todo",
            TaskItemStatus.InProgress => "in progress",
            TaskItemStatus.Done => "done",
            _ => status.ToString(),
        };

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

                _rows = tasks.Select(t => new Row(t, totals.GetValueOrDefault(t.Id, TimeSpan.Zero))).ToList();
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

        private readonly record struct Row(TaskItem Task, TimeSpan LoggedTime);

        #endregion
    }
}
