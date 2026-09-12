using Microsoft.Extensions.DependencyInjection;
using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Services
{
    /// <summary>
    /// Default <see cref="INavigationService"/>: a back stack of screens, resolved on demand
    /// through the app's <see cref="IServiceProvider"/> so screens can take whatever
    /// dependencies they need without this class knowing about any of them.
    /// </summary>
    /// <remarks>
    /// Register this as a singleton - it *is* the back stack, so the app needs exactly one
    /// of it for its whole lifetime. Screens themselves should be registered transient: this
    /// class asks the container for a fresh instance on every push, and disposes it itself
    /// when it's later popped for good (see <see cref="GoBack"/>).
    /// </remarks>
    public sealed class NavigationService : INavigationService
    {
        #region Fields

        private readonly IServiceProvider _services;
        private readonly Stack<IScreenViewModel> _stack = new();
        #endregion

        #region Constructors

        public NavigationService(IServiceProvider services)
        {
            ArgumentNullException.ThrowIfNull(services);

            _services = services;
        }
        #endregion

        #region Events

        /// <inheritdoc />
        public event EventHandler<NavigatedEventArgs>? Navigated;
        #endregion

        #region Properties

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">
        /// No screen has been pushed yet - the app must call <see cref="NavigateTo{TScreen}"/>
        /// once at startup before anything tries to read <see cref="Current"/>.
        /// </exception>
        public IScreenViewModel Current => _stack.Peek();

        /// <inheritdoc />
        public bool CanGoBack => _stack.Count > 1;
        #endregion

        #region Methods

        /// <inheritdoc />
        public void NavigateTo<TScreen>() where TScreen : IScreenViewModel
        {
            var screen = _services.GetRequiredService<TScreen>();
            Push(screen);
        }

        /// <inheritdoc />
        public void NavigateTo<TScreen, TContext>(TContext context) where TScreen : IScreenViewModel<TContext>
        {
            var screen = _services.GetRequiredService<TScreen>();
            screen.Initialize(context);
            Push(screen);
        }

        /// <inheritdoc />
        public bool GoBack()
        {
            if (!CanGoBack)
            {
                return false;
            }

            var poppedScreen = _stack.Pop();
            poppedScreen.OnDeactivated();
            poppedScreen.Dispose();

            var revealedScreen = _stack.Peek();
            revealedScreen.OnActivated();

            Navigated?.Invoke(this, new NavigatedEventArgs(poppedScreen, revealedScreen, NavigationDirection.Back));

            return true;
        }
        #endregion

        #region Helpers

        private void Push(IScreenViewModel screen)
        {
            var previousScreen = _stack.Count > 0 ? _stack.Peek() : null;
            previousScreen?.OnDeactivated();

            _stack.Push(screen);
            screen.OnActivated();

            Navigated?.Invoke(this, new NavigatedEventArgs(previousScreen, screen, NavigationDirection.Forward));
        }
        #endregion
    }
}
