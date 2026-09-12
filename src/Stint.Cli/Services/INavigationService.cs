using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Services
{
    /// <summary>
    /// Moves between screens (Home, Projects, Project, Settings, Switch, ...) using a back
    /// stack: <see cref="NavigateTo{TScreen}"/> pushes a new screen on top - the one you were
    /// on is deactivated but kept, in case you come back to it - and <see cref="GoBack"/> pops
    /// the top screen off (disposing it for good) and reveals the one beneath it again.
    /// </summary>
    public interface INavigationService
    {
        #region Properties

        /// <summary>
        /// The screen currently on top of the stack - the one that should be rendered.
        /// </summary>
        IScreenViewModel Current { get; }

        /// <summary>
        /// Whether <see cref="GoBack"/> would do anything right now. A screen uses this to
        /// decide whether to show a back/cancel key hint at all - the root screen never can.
        /// </summary>
        bool CanGoBack { get; }

        #endregion

        #region Events

        /// <summary>
        /// Raised whenever <see cref="Current"/> changes, after the change has already
        /// happened. The render pipeline subscribes to this to know when to redraw - and,
        /// later, to play a transition.
        /// </summary>
        event EventHandler<NavigatedEventArgs>? Navigated;

        #endregion

        #region Methods

        /// <summary>
        /// Pushes a fresh <typeparamref name="TScreen"/> on top of the stack and makes it
        /// <see cref="Current"/>.
        /// </summary>
        void NavigateTo<TScreen>() where TScreen : IScreenViewModel;

        /// <summary>
        /// Pushes a fresh <typeparamref name="TScreen"/> on top of the stack, hands it
        /// <paramref name="context"/> via <see cref="IScreenViewModel{TContext}.Initialize"/>,
        /// and makes it <see cref="Current"/>.
        /// </summary>
        void NavigateTo<TScreen, TContext>(TContext context) where TScreen : IScreenViewModel<TContext>;

        /// <summary>
        /// Pops the current screen off the stack and disposes it, revealing the screen
        /// beneath it. Does nothing and returns <c>false</c> if already at the root.
        /// </summary>
        bool GoBack();

        #endregion
    }
}
