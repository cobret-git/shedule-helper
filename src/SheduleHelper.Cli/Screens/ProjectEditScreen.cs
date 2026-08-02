using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Services;
using MSG = SheduleHelper.Core.Resources.Strings.Messages;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Creates or edits a <see cref="Project"/>. Pushed with <see langword="null"/> for a new
    /// project, or an existing one to edit - the same screen, since the only difference is which
    /// <c>LocalDbContext</c> call <see cref="SaveAsync"/> makes.
    /// </summary>
    public sealed class ProjectEditScreen : IScreen
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly Project? _existingProject;
        private readonly ILogger _logger = Log.ForContext<ProjectEditScreen>();

        private readonly TextField _name;
        private readonly TextField _description;
        private readonly SelectList _rows = new(3);
        private bool _active;
        private bool _editing;
        private string? _message;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ProjectEditScreen"/> class.
        /// </summary>
        /// <param name="existingProject">The project to edit, or <see langword="null"/> to create a new one.</param>
        public ProjectEditScreen(ILocalDbContextFactory dbContextFactory, ICurrentUserContext currentUserContext, Project? existingProject)
        {
            _dbContextFactory = dbContextFactory;
            _currentUserContext = currentUserContext;
            _existingProject = existingProject;
            _name = new TextField(existingProject?.Name ?? string.Empty);
            _description = new TextField(existingProject?.Description ?? string.Empty);
            _active = existingProject?.IsActive ?? true;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, _existingProject is null ? "NEW PROJECT" : "EDIT PROJECT", "Esc Cancel");

            DrawLabel(frame, 3, 0, "Name");
            _name.Draw(frame, 16, 3, 50, _editing && _rows.SelectedIndex == 0);

            DrawLabel(frame, 4, 1, "Description");
            _description.Draw(frame, 16, 4, 50, _editing && _rows.SelectedIndex == 1);

            DrawLabel(frame, 5, 2, "Active");
            frame.Write(16, 5, _active ? "[ YES ]" : "[ NO  ]", _active ? ColorToken.Positive : ColorToken.Dim);

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("up/down", "Field"), ("Enter", "Edit"), ("left/right", "Toggle"), ("Ctrl+S", "Save"), ("Esc", "Cancel"));
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            if (_editing)
            {
                var field = _rows.SelectedIndex == 0 ? _name : _description;
                if (key.Key is ConsoleKey.Enter or ConsoleKey.Escape)
                {
                    _editing = false;
                    return;
                }

                field.HandleKey(key);
                return;
            }

            if (_rows.HandleKey(key))
            {
                return;
            }

            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    await screens.Pop();
                    break;
                case ConsoleKey.Enter when _rows.SelectedIndex is 0 or 1:
                    _editing = true;
                    break;
                case ConsoleKey.LeftArrow or ConsoleKey.RightArrow when _rows.SelectedIndex == 2:
                    _active = !_active;
                    break;
                case ConsoleKey.S when key.Modifiers.HasFlag(ConsoleModifiers.Control):
                    await SaveAsync(screens);
                    break;
            }
        }

        #endregion

        #region Helpers

        private void DrawLabel(Frame frame, int y, int index, string label)
        {
            var selected = _rows.SelectedIndex == index;
            frame.Write(1, y, selected ? $"► {label}" : $"  {label}", selected ? ColorToken.Accent : ColorToken.Default);
        }

        private async Task SaveAsync(ScreenStack screens)
        {
            var name = _name.Value.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                _message = MSG.error_projectNameEmpty;
                return;
            }

            try
            {
                await using var db = _dbContextFactory.CreateDbContext();

                if (_existingProject is null)
                {
                    await db.CreateProjectAsync(name, _currentUserContext.UserId, NullIfEmpty(_description.Value), CancellationToken.None);
                }
                else
                {
                    _existingProject.Name = name;
                    _existingProject.Description = NullIfEmpty(_description.Value);
                    _existingProject.IsActive = _active;
                    await db.UpdateProjectAsync(_existingProject, CancellationToken.None);
                }

                await screens.Pop();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save project.");
                _message = MSG.error_projectSaveUnexpected;
            }
        }

        private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        #endregion
    }
}
