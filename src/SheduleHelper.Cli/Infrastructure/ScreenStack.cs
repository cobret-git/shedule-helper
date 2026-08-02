namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// A stack of active <see cref="IScreen"/>s - the TUI's navigation surface. A pushed screen is
    /// how the app shows what would be a modal dialog in WinUI; popping it returns to the screen
    /// underneath exactly where it left off. Clearing the stack (<see cref="Quit"/>) ends the app.
    /// </summary>
    public sealed class ScreenStack
    {
        #region Fields

        private readonly List<IScreen> _stack = new();

        #endregion

        #region Properties

        /// <summary>
        /// The currently active screen, or <see langword="null"/> once <see cref="Quit"/> has been called.
        /// </summary>
        public IScreen? Current => _stack.Count > 0 ? _stack[^1] : null;

        #endregion

        #region Methods

        /// <summary>
        /// Pushes a new screen on top of the stack, activating it.
        /// </summary>
        public async Task Push(IScreen screen)
        {
            if (Current is { } current)
            {
                await current.OnLeave();
            }

            _stack.Add(screen);
            await screen.OnEnter();
        }

        /// <summary>
        /// Pops the active screen, re-activating the one underneath (if any).
        /// </summary>
        public async Task Pop()
        {
            if (_stack.Count == 0)
            {
                return;
            }

            var leaving = _stack[^1];
            _stack.RemoveAt(_stack.Count - 1);
            await leaving.OnLeave();

            if (Current is { } current)
            {
                await current.OnEnter();
            }
        }

        /// <summary>
        /// Replaces the active screen with a new one, without growing the stack - used for
        /// root-level navigation (e.g. Home to Reports), as opposed to <see cref="Push"/>, which is
        /// for screens the user expects to back out of.
        /// </summary>
        public async Task Replace(IScreen screen)
        {
            if (_stack.Count > 0)
            {
                var leaving = _stack[^1];
                _stack[^1] = screen;
                await leaving.OnLeave();
            }
            else
            {
                _stack.Add(screen);
            }

            await screen.OnEnter();
        }

        /// <summary>
        /// Clears the stack, ending the app - <see cref="ConsoleApp"/>'s loop exits once
        /// <see cref="Current"/> becomes <see langword="null"/>.
        /// </summary>
        public async Task Quit()
        {
            while (_stack.Count > 0)
            {
                var leaving = _stack[^1];
                _stack.RemoveAt(_stack.Count - 1);
                await leaving.OnLeave();
            }
        }

        #endregion
    }
}
