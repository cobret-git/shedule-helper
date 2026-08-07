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
    /// <remarks>
    /// One vertical ring of three zones - Title, Status, Description - moved between with up/down,
    /// each with a rule under it so the zones read as distinct fields rather than one undifferentiated
    /// block. Description is read-only here: editing a paragraph one keystroke at a time in a
    /// terminal cell grid, wrapped or not, is a worse experience than any editor already installed
    /// on the machine, so <c>E</c> hands it to one instead - see <see cref="ExternalEditor"/>.
    /// </remarks>
    public sealed class TaskEditScreen : IScreen
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly int _projectId;
        private readonly TaskItem? _existingTask;
        private readonly IPathProvider _pathProvider;
        private readonly ILogger _logger = Log.ForContext<TaskEditScreen>();

        private readonly TextField _title;
        private string _description;
        private TaskItemStatus _status;
        private Zone _focus;
        private int _descriptionScroll;
        private string? _message;

        // Rows the Description zone's wrapped preview starts at - Title/rule/Status/rule/label above it.
        private const int DescriptionTop = 9;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskEditScreen"/> class.
        /// </summary>
        /// <param name="projectId">The project this task belongs to (or will belong to, for a new task).</param>
        /// <param name="existingTask">The task to edit, or <see langword="null"/> to create a new one.</param>
        /// <param name="pathProvider">Resolves where <c>E</c>'s external-editor scratch file lives.</param>
        public TaskEditScreen(ILocalDbContextFactory dbContextFactory, int projectId, TaskItem? existingTask, IPathProvider pathProvider)
        {
            _dbContextFactory = dbContextFactory;
            _projectId = projectId;
            _existingTask = existingTask;
            _pathProvider = pathProvider;
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

            DrawLabel(frame, 3, Zone.Title, "Title");
            _title.Draw(frame, 16, 3, Math.Max(1, frame.Width - 17), _focus == Zone.Title);
            frame.Rule(4);

            DrawLabel(frame, 5, Zone.Status, "Status");
            frame.Write(16, 5, $"< {StatusLabel(_status)} >");
            frame.Rule(6);

            DrawLabel(frame, 7, Zone.Description, "Description");
            frame.WriteRight(frame.Width - 1, 7, "E to edit", ColorToken.Dim);

            RenderDescription(frame);

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            var keyBindings = new List<(string Key, string Label)> { ("up/down", "Field"), ("left/right", "Move/Change") };

            if (_focus == Zone.Description)
            {
                keyBindings.Add(("E", "Edit description"));
            }

            keyBindings.Add(("F10/Enter", "Save"));
            keyBindings.Add(("Esc", "Cancel"));
            KeyBar.Draw(frame, keyBindings.ToArray());
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
                case ConsoleKey.Enter:
                    await SaveAsync(screens);
                    return;
            }

            switch (_focus)
            {
                case Zone.Title:
                    HandleTitleZoneKey(key);
                    break;
                case Zone.Status:
                    HandleStatusZoneKey(key);
                    break;
                case Zone.Description:
                    await HandleDescriptionZoneKey(key, screens);
                    break;
            }
        }

        #endregion

        #region Handlers

        private void HandleTitleZoneKey(ConsoleKeyInfo key)
        {
            if (key.Key == ConsoleKey.DownArrow)
            {
                _focus = Zone.Status;
                return;
            }

            _title.HandleKey(key);
        }

        private void HandleStatusZoneKey(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    _focus = Zone.Title;
                    break;
                case ConsoleKey.DownArrow:
                    _focus = Zone.Description;
                    break;
                case ConsoleKey.LeftArrow:
                    _status = CycleStatus(_status, forward: false);
                    break;
                case ConsoleKey.RightArrow:
                    _status = CycleStatus(_status, forward: true);
                    break;
            }
        }

        /// <summary>
        /// Up/down scroll the description's wrapped preview while there's more of it to see;
        /// reaching the top with nothing left to scroll moves focus back to Status instead - the
        /// same "arrows fall through to the next zone at the edge" rule Title/Status follow, just
        /// gated on scroll position here rather than being unconditional.
        /// </summary>
        private async Task HandleDescriptionZoneKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (_descriptionScroll > 0)
                    {
                        _descriptionScroll--;
                    }
                    else
                    {
                        _focus = Zone.Status;
                    }
                    break;
                case ConsoleKey.DownArrow:
                    _descriptionScroll++; // clamped against the true max in RenderDescription
                    break;
                case ConsoleKey.E:
                    await OpenExternalEditorAsync(screens);
                    break;
            }
        }

        #endregion

        #region Helpers

        private void RenderDescription(Frame frame)
        {
            var lines = string.IsNullOrWhiteSpace(_description)
                ? new List<string> { "(none)" }
                : Formatting.Wrap(_description, Math.Max(1, frame.Width - 2));

            // -3 for the blank row above the key bar's rule, the rule itself, and the key bar row.
            var height = Math.Max(1, frame.Height - DescriptionTop - 3);
            var maxScroll = Math.Max(0, lines.Count - height);
            _descriptionScroll = Math.Clamp(_descriptionScroll, 0, maxScroll);

            for (var row = 0; row < height; row++)
            {
                var lineIndex = _descriptionScroll + row;
                if (lineIndex >= lines.Count)
                {
                    break;
                }

                frame.Write(1, DescriptionTop + row, lines[lineIndex], ColorToken.Dim);
            }
        }

        private async Task OpenExternalEditorAsync(ScreenStack screens)
        {
            var session = ExternalEditor.Begin(_title.Value, _description, _pathProvider);

            if (session.Process is null)
            {
                ExternalEditor.Discard(session);
                _message = "Couldn't launch a text editor - set $EDITOR, or edit here instead.";
                await screens.Push(new DescriptionEditScreen(_title.Value, _description, value => _description = value));
                return;
            }

            await screens.Push(new ExternalEditScreen(session, _title.Value, _description, value => _description = value));
        }

        private void DrawLabel(Frame frame, int y, Zone zone, string label)
        {
            var selected = _focus == zone;
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

        #region Structures

        private enum Zone
        {
            Title,
            Status,
            Description,
        }

        #endregion
    }
}
