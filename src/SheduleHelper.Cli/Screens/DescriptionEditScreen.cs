using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// A full-screen multiline editor for a Task or Project description - pushed from
    /// <see cref="TaskEditScreen"/>/<see cref="ProjectEditScreen"/> instead of squeezing a paragraph
    /// into a two- or three-row "keyhole" box inline. Generic across callers the same way
    /// <see cref="TimeEntryScreen"/> is - it doesn't know or care whether the text belongs to a task
    /// or a project, only that there's a title to show and a value to hand back on save.
    /// </summary>
    public sealed class DescriptionEditScreen : IScreen
    {
        #region Fields

        private readonly string _ownerTitle;
        private readonly TextField _field;
        private readonly Action<string> _onSave;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DescriptionEditScreen"/> class.
        /// </summary>
        /// <param name="ownerTitle">The task title or project name this description belongs to, shown in the header for context.</param>
        /// <param name="initialValue">The description's current text.</param>
        /// <param name="onSave">
        /// Called with the edited text when the user saves (<c>F10</c>). The caller only needs to
        /// update its own in-memory field - the owning edit screen persists it to the database when
        /// the whole form is saved, same as the Title/Name field already does.
        /// </param>
        public DescriptionEditScreen(string ownerTitle, string initialValue, Action<string> onSave)
        {
            _ownerTitle = ownerTitle;
            _field = new TextField(initialValue, multiline: true);
            _onSave = onSave;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, $"DESCRIPTION > {_ownerTitle}", "Esc Cancel");

            var height = Math.Max(1, frame.Height - 5);
            _field.DrawMultiline(frame, 1, 2, Math.Max(1, frame.Width - 2), height, editing: true);

            KeyBar.Draw(frame, ("up/down", "Line"), ("left/right", "Move"), ("Enter", "New line"), ("F10", "Save"), ("Esc", "Cancel"));
        }

        /// <inheritdoc/>
        public Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    return screens.Pop();
                case ConsoleKey.F10:
                    _onSave(_field.Value);
                    return screens.Pop();
            }

            _field.HandleKey(key);
            return Task.CompletedTask;
        }

        #endregion
    }
}
