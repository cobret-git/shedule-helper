namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// A single full-screen destination in the app - the TUI equivalent of a WinUI page. Owned and
    /// activated by <see cref="ScreenStack"/>, which takes the place of <c>INavigationService</c>;
    /// a pushed screen takes the place of a dialog.
    /// </summary>
    public interface IScreen
    {
        #region Methods

        /// <summary>
        /// Draws the screen's current state into <paramref name="frame"/>. Called once per loop
        /// iteration, after any key has been handled - screens should treat this as a pure
        /// projection of their state, not a place to trigger side effects.
        /// </summary>
        void Render(Frame frame);

        /// <summary>
        /// Handles a single key press. Implementations that navigate call back into
        /// <paramref name="screens"/> (e.g. <c>Push</c>, <c>Pop</c>) rather than owning navigation
        /// themselves. Returns a <see cref="Task"/> - and <see cref="ConsoleApp"/> awaits it fully
        /// before rendering the next frame - so a screen's own async work (a DB call) never runs
        /// concurrently with <see cref="Render"/> reading its state. A console app has no
        /// dispatcher to marshal continuations back to a UI thread the way WPF/WinUI do, so
        /// "always await before redrawing" is what keeps this safe instead.
        /// </summary>
        Task HandleKey(ConsoleKeyInfo key, ScreenStack screens);

        /// <summary>
        /// Called once this screen becomes the active one - either just pushed, or re-exposed after
        /// the screen above it was popped. Default no-op; override to (re)load state.
        /// </summary>
        Task OnEnter() => Task.CompletedTask;

        /// <summary>
        /// Called once this screen stops being the active one - either popped/replaced, or another
        /// screen was just pushed above it. Default no-op; override to release resources.
        /// </summary>
        Task OnLeave() => Task.CompletedTask;

        #endregion
    }
}
