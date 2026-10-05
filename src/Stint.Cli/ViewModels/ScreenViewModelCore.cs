using CommunityToolkit.Mvvm.ComponentModel;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The plumbing every screen needs, with no idea how screens are navigated between: change
    /// notification (via <see cref="ObservableObject"/>), the title, the key hints, activation
    /// hooks and a guarded <see cref="Dispose()"/>. <see cref="ScreenViewModelBase"/> adds the
    /// navigation service for ordinary screens; <see cref="DialogScreenViewModelBase{TContext, TResult}"/>
    /// deliberately doesn't, so a dialog has no way to navigate.
    /// </summary>
    public abstract class ScreenViewModelCore : ObservableObject, IScreenViewModel
    {
        #region Fields

        private static readonly IReadOnlyList<KeyHint> NoKeyHints = Array.Empty<KeyHint>();
        private string _title = string.Empty;
        private IReadOnlyList<KeyHint> _keyHints = NoKeyHints;
        private bool _isDisposed;
        #endregion

        #region Properties

        /// <inheritdoc />
        public string Title { get => _title; protected set => SetProperty(ref _title, value); }

        /// <inheritdoc />
        public IReadOnlyList<KeyHint> KeyHints { get => _keyHints; protected set => SetProperty(ref _keyHints, value); }
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
