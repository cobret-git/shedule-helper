using CommunityToolkit.Mvvm.ComponentModel;
using Stint.Cli.Services;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// Base class for a screen's ViewModel. Wires up the plumbing every screen needs - change
    /// notification (via <see cref="ObservableObject"/>), a guarded <see cref="Dispose()"/>,
    /// and a reference to the navigation service - so concrete screens (HomeViewModel,
    /// ProjectsViewModel, ...) only need to describe their own state and commands.
    /// </summary>
    public abstract class ScreenViewModelBase : ObservableObject, IScreenViewModel
    {
        #region Fields

        private static readonly IReadOnlyList<KeyHint> NoKeyHints = Array.Empty<KeyHint>();

        private string _title = string.Empty;
        private IReadOnlyList<KeyHint> _keyHints = NoKeyHints;
        private bool _isDisposed;
        #endregion

        #region Constructors

        protected ScreenViewModelBase(INavigationService navigation)
        {
            ArgumentNullException.ThrowIfNull(navigation);

            Navigation = navigation;
        }
        #endregion

        #region Properties

        /// <inheritdoc />
        public string Title { get => _title; protected set => SetProperty(ref _title, value); }

        /// <inheritdoc />
        public IReadOnlyList<KeyHint> KeyHints { get => _keyHints; protected set => SetProperty(ref _keyHints, value); }

        /// <summary>
        /// Service used to move between screens. Derived classes call into this from their
        /// commands (e.g. a "switch" key hint calling <c>Navigation.NavigateTo&lt;SwitchViewModel&gt;()</c>).
        /// </summary>
        protected INavigationService Navigation { get; }
        #endregion

        #region Methods

        /// <inheritdoc />
        public virtual void OnActivated() { }

        /// <inheritdoc />
        public virtual void OnDeactivated() { }
        #endregion

        #region IDisposable

        /// <summary>
        /// Releases this screen's resources. Safe to call more than once (e.g. once from the
        /// navigation service on leave, once from app shutdown for a screen it also owns) -
        /// only the first call has any effect.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            OnDispose();
        }

        /// <summary>
        /// Override to release timers, subscriptions, or anything else this screen picked up
        /// while active. Called at most once, guaranteed by <see cref="Dispose()"/>.
        /// </summary>
        protected virtual void OnDispose()
        {
        }
        #endregion
    }
}
