using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Pushed while a description is being edited in an external GUI text editor - see
    /// <see cref="ExternalEditor"/>. Shows a waiting message and polls the launched process each
    /// tick purely to update that message; the user presses Enter to actually read the edit back
    /// and return, or Escape to discard it and return with the field untouched.
    /// </summary>
    /// <remarks>
    /// Enter isn't automatic on the process exiting, even though most editors this is aimed at
    /// (Notepad, a blocking <c>code --wait</c>) do exit when the user is done - <see cref="IScreen.OnTick"/>
    /// has no way to navigate the <see cref="ScreenStack"/> on its own, only <see cref="IScreen.HandleKey"/>
    /// does, so the tick can only update what's on screen, not leave it. That said, this isn't
    /// purely a workaround: plenty of GUI editors don't block at all - VS Code without
    /// <c>--wait</c>, or a file handed to an already-running instance - so trusting the process
    /// alone would strand the user here forever in those cases regardless.
    /// </remarks>
    public sealed class ExternalEditScreen : IScreen
    {
        #region Fields

        private readonly ExternalEditSession _session;
        private readonly string _ownerTitle;
        private readonly string _initialValue;
        private readonly Action<string> _onSave;

        private bool _processExited;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalEditScreen"/> class, already having
        /// launched the editor - see <see cref="ExternalEditor.Begin"/>.
        /// </summary>
        /// <param name="session">The in-progress edit, from <see cref="ExternalEditor.Begin"/>.</param>
        /// <param name="ownerTitle">The task/project title this description belongs to, shown in the header for context.</param>
        /// <param name="initialValue">The description's value before this edit, kept as-is if the user cancels.</param>
        /// <param name="onSave">
        /// Called with the edited text once the user accepts it (Enter). The caller only needs to
        /// update its own in-memory field - the owning edit screen persists it to the database when
        /// the whole form is saved, same as <see cref="DescriptionEditScreen"/>'s contract.
        /// </param>
        public ExternalEditScreen(ExternalEditSession session, string ownerTitle, string initialValue, Action<string> onSave)
        {
            _session = session;
            _ownerTitle = ownerTitle;
            _initialValue = initialValue;
            _onSave = onSave;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, $"DESCRIPTION > {_ownerTitle}", "Esc Discard");

            if (_processExited)
            {
                frame.Write(1, 3, "Editor closed.", ColorToken.Dim);
                frame.Write(1, 4, "Press Enter to bring the changes back here.", ColorToken.Dim);
            }
            else
            {
                frame.Write(1, 3, "Editing in your text editor - switch back here once you're done.", ColorToken.Dim);
                frame.Write(1, 4, "Press Enter as soon as you're done, even if the editor is still open.", ColorToken.Dim);
            }

            KeyBar.Draw(frame, ("Enter", "Done"), ("Esc", "Discard"));
        }

        /// <inheritdoc/>
        public Task OnTick()
        {
            // Only ever updates the message above - see the class remarks for why this can't also
            // pop the screen.
            if (_session.Process is not { HasExited: false })
            {
                _processExited = true;
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    _onSave(ExternalEditor.Accept(_session, _initialValue));
                    return screens.Pop();
                case ConsoleKey.Escape:
                    ExternalEditor.Discard(_session);
                    return screens.Pop();
                default:
                    return Task.CompletedTask;
            }
        }

        #endregion
    }
}
