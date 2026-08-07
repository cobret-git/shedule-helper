using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// The Projects browser (The Organizer) - lists every project owned by the current user, with
    /// task count and total logged time. Per-period (week/month/quarter) columns land with
    /// <c>ReportingService</c> later; for now this shows an all-time total only.
    /// </summary>
    public sealed class ProjectsScreen : IScreen
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly IPathProvider _pathProvider;
        private readonly ILogger _logger = Log.ForContext<ProjectsScreen>();

        private List<Row> _rows = new();
        private SelectList _list = new(1);
        private string? _message;
        private bool _confirmingDelete;

        // Column widths for the project table. Project name is the only column that grows/shrinks
        // with the console - the rest are small, fixed-width values that always fit.
        private const int MinNameColumnWidth = 10;
        private const int NamePrefixWidth = 4;   // "{marker}{index,2} "
        private const int NameToTasksGap = 1;    // guarantees a visible gap even when the name is
                                                  // truncated - padding alone isn't reliable here
                                                  // since "…" can render wider than one column.
        private const int TasksColumnWidth = 5;
        private const int TasksToTotalGap = 3;
        private const int TotalColumnWidth = 8;
        private const int TotalToStatusGap = 3;
        private const int StatusColumnWidth = 8; // "archived"

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectsScreen"/> class.
        /// </summary>
        public ProjectsScreen(ILocalDbContextFactory dbContextFactory, ICurrentUserContext currentUserContext, IPathProvider pathProvider)
        {
            _dbContextFactory = dbContextFactory;
            _currentUserContext = currentUserContext;
            _pathProvider = pathProvider;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public Task OnEnter() => LoadAsync();

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, "PROJECTS", "Esc Back");

            var nameWidth = NameColumnWidth(frame.Width);
            var statusX = 1 + NamePrefixWidth + nameWidth + NameToTasksGap + TasksColumnWidth + TasksToTotalGap + TotalColumnWidth + TotalToStatusGap;

            frame.Write(1, 3, $"#   {"Project".PadRight(nameWidth)} {"Tasks",5}   {"Total",8}", ColorToken.Dim);
            frame.Write(statusX, 3, "Status", ColorToken.Dim);
            frame.Rule(4);

            if (_rows.Count == 0)
            {
                frame.Write(1, 6, "No projects yet - press N to create one.", ColorToken.Dim);
            }

            for (var i = 0; i < _rows.Count; i++)
            {
                var row = _rows[i];
                var selected = _list.SelectedIndex == i;
                var marker = selected ? "►" : " ";
                var color = selected ? ColorToken.Accent : ColorToken.Default;
                var name = Formatting.Truncate(row.Project.Name, nameWidth).PadRight(nameWidth);

                frame.Write(1, 5 + i, $"{marker}{i + 1,2} {name} {row.TaskCount,5}   {Formatting.Duration(row.TotalTime),8}", color);
                frame.Write(statusX, 5 + i, row.Project.IsActive ? "active" : "archived", row.Project.IsActive ? ColorToken.Positive : ColorToken.Dim);
            }

            if (_confirmingDelete)
            {
                frame.Write(1, frame.Height - 4, "Delete this project and its tasks? Y to confirm, any other key to cancel.", ColorToken.Negative);
            }
            else if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("up/down", "Move"), ("Enter", "Open"), ("N", "New"), ("E", "Edit"), ("A", "Archive/Activate"), ("Del", "Delete"), ("Esc", "Back"));
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
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
                    await screens.Push(new ProjectScreen(_dbContextFactory, _currentUserContext, _rows[_list.SelectedIndex].Project.Id, _pathProvider));
                    break;
                case ConsoleKey.N:
                    await screens.Push(new ProjectEditScreen(_dbContextFactory, _currentUserContext, null));
                    break;
                case ConsoleKey.E when _rows.Count > 0:
                    await screens.Push(new ProjectEditScreen(_dbContextFactory, _currentUserContext, _rows[_list.SelectedIndex].Project));
                    break;
                case ConsoleKey.A when _rows.Count > 0:
                    await ToggleActiveAsync();
                    break;
                case ConsoleKey.Delete when _rows.Count > 0:
                    _confirmingDelete = true;
                    break;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Computes how wide the Project name column can be for the given console width, so it
        /// grows to use available space instead of always clipping at a fixed 28 columns.
        /// </summary>
        private static int NameColumnWidth(int frameWidth)
        {
            var fixedWidth = NamePrefixWidth + NameToTasksGap + TasksColumnWidth + TasksToTotalGap + TotalColumnWidth + TotalToStatusGap + StatusColumnWidth;
            return Math.Max(MinNameColumnWidth, frameWidth - fixedWidth - 1);
        }

        private async Task LoadAsync()
        {
            try
            {
                await using var db = _dbContextFactory.CreateDbContext();
                var userId = _currentUserContext.UserId;

                var projects = (await db.GetProjectsByUserIdAsync(userId, CancellationToken.None))
                    .OrderBy(p => p.Name)
                    .ToList();

                var logs = await db.GetProjectTimeLogsAsync(userId, DateTime.MinValue, DateTime.Now, CancellationToken.None);
                var totals = TimeBudgetCalculator.SummarizeByProject(logs);

                var rows = new List<Row>();
                foreach (var project in projects)
                {
                    var taskCount = (await db.GetTasksByProjectIdAsync(project.Id, CancellationToken.None)).Count;
                    rows.Add(new Row(project, taskCount, totals.GetValueOrDefault(project.Id, TimeSpan.Zero)));
                }

                _rows = rows;
                _list = new SelectList(Math.Max(rows.Count, 1));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load projects.");
                _message = "Failed to load projects.";
            }
        }

        private async Task ToggleActiveAsync()
        {
            var project = _rows[_list.SelectedIndex].Project;
            try
            {
                await using var db = _dbContextFactory.CreateDbContext();
                project.IsActive = !project.IsActive;
                await db.UpdateProjectAsync(project, CancellationToken.None);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to toggle project {ProjectId} active state.", project.Id);
                _message = "Something went wrong updating the project.";
            }
        }

        private async Task DeleteSelectedAsync()
        {
            var project = _rows[_list.SelectedIndex].Project;
            try
            {
                await using var db = _dbContextFactory.CreateDbContext();
                await db.DeleteProjectAsync(project.Id, CancellationToken.None);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to delete project {ProjectId}.", project.Id);
                _message = "Something went wrong deleting the project.";
            }
        }

        #endregion

        #region Structures

        private readonly record struct Row(Project Project, int TaskCount, TimeSpan TotalTime);

        #endregion
    }
}
