using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Services;
using MSG = SheduleHelper.Core.Resources.Strings.Messages;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Creates or edits a <see cref="TaskItem"/> under a given project. Pushed with
    /// <see langword="null"/> for a new task, or an existing one to edit.
    /// </summary>
    public sealed class TaskEditScreen : IScreen
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly int _projectId;
        private readonly TaskItem? _existingTask;
        private readonly ILogger _logger = Log.ForContext<TaskEditScreen>();

        private readonly TextField _title;
        private readonly SelectList _rows = new(3);
        private string _description;
        private TaskItemStatus _status;
        private string? _message;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskEditScreen"/> class.
        /// </summary>
        /// <param name="projectId">The project this task belongs to (or will belong to, for a new task).</param>
        /// <param name="existingTask">The task to edit, or <see langword="null"/> to create a new one.</param>
        public TaskEditScreen(ILocalDbContextFactory dbContextFactory, int projectId, TaskItem? existingTask)
        {
            _dbContextFactory = dbContextFactory;
            _projectId = projectId;
            _existingTask = existingTask;
            _title = new TextField(existingTask?.Title ?? string.Empty);
            _description = existingTask?.Description ?? string.Empty;
            _status = existingTask?.Status ?? TaskItemStatus.Todo;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, _existingTask is null ? "NEW TASK" : "EDIT TASK", "Esc Cancel");

            DrawLabel(frame, 3, 0, "Title");
            _title.Draw(frame, 16, 3, 50, _rows.SelectedIndex == 0);

            // Description is a preview only - editing it happens on its own full-screen editor (see
            // the Enter case below) rather than in a 2-3 row inline box too small for a paragraph.
            DrawLabel(frame, 4, 1, "Description");
            frame.Write(16, 4, Formatting.PreviewSummary(_description, Math.Max(1, frame.Width - 17)), ColorToken.Dim);

            DrawLabel(frame, 5, 2, "Status");
            frame.Write(16, 5, $"< {StatusLabel(_status)} >");

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            // Enter means something different depending on focus: it saves the form everywhere
            // except Description, where it opens the full-screen editor instead - F10 is the one key
            // that always saves, regardless of which field is focused.
            var enterHint = _rows.SelectedIndex == 1 ? ("Enter", "Edit description") : ("Enter", "Save");
            KeyBar.Draw(frame, ("up/down", "Field"), ("left/right", "Move/Change"), enterHint, ("F10", "Save"), ("Esc", "Cancel"));
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    await screens.Pop();
                    return;
                case ConsoleKey.F10:
                    await SaveAsync(screens);
                    return;
                case ConsoleKey.Enter when _rows.SelectedIndex == 1:
                    await screens.Push(new DescriptionEditScreen(_title.Value, _description, value => _description = value));
                    return;
                case ConsoleKey.Enter:
                    await SaveAsync(screens);
                    return;
            }

            if (_rows.HandleKey(key))
            {
                return;
            }

            if (_rows.SelectedIndex == 2)
            {
                if (key.Key is ConsoleKey.LeftArrow or ConsoleKey.RightArrow)
                {
                    _status = CycleStatus(_status, key.Key == ConsoleKey.RightArrow);
                }

                return;
            }

            if (_rows.SelectedIndex == 0)
            {
                _title.HandleKey(key);
            }
        }

        #endregion

        #region Helpers

        private void DrawLabel(Frame frame, int y, int index, string label)
        {
            var selected = _rows.SelectedIndex == index;
            frame.Write(1, y, selected ? $"► {label}" : $"  {label}", selected ? ColorToken.Accent : ColorToken.Default);
        }

        private static string StatusLabel(TaskItemStatus status) => status switch
        {
            TaskItemStatus.Todo => "Todo",
            TaskItemStatus.InProgress => "In progress",
            TaskItemStatus.Done => "Done",
            _ => status.ToString(),
        };

        private static TaskItemStatus CycleStatus(TaskItemStatus current, bool forward)
        {
            var values = Enum.GetValues<TaskItemStatus>();
            var index = Array.IndexOf(values, current);
            var nextIndex = forward ? (index + 1) % values.Length : (index - 1 + values.Length) % values.Length;
            return values[nextIndex];
        }

        private async Task SaveAsync(ScreenStack screens)
        {
            var title = _title.Value.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                _message = MSG.error_taskTitleEmpty;
                return;
            }

            try
            {
                await using var db = _dbContextFactory.CreateDbContext();

                if (_existingTask is null)
                {
                    await db.CreateTaskAsync(title, NullIfEmpty(_description), _status, _projectId, CancellationToken.None);
                }
                else
                {
                    _existingTask.Title = title;
                    _existingTask.Description = NullIfEmpty(_description);
                    _existingTask.Status = _status;
                    await db.UpdateTaskAsync(_existingTask, CancellationToken.None);
                }

                await screens.Pop();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save task.");
                _message = MSG.error_taskSaveUnexpected;
            }
        }

        private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        #endregion
    }
}
