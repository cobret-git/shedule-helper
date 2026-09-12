using Stint.Cli.Services;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Components
{
    /// <summary>
    /// Raised by <see cref="INavigationService.Navigated"/> whenever <see cref="INavigationService.Current"/> changes.
    /// </summary>
    public sealed class NavigatedEventArgs : EventArgs
    {
        #region Constructors

        public NavigatedEventArgs(IScreenViewModel? from, IScreenViewModel to, NavigationDirection direction)
        {
            ArgumentNullException.ThrowIfNull(to);

            From = from;
            To = to;
            Direction = direction;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The screen that was current before this navigation, or <c>null</c> for the very
        /// first navigation of the app - there's nothing to come "from" yet.
        /// </summary>
        public IScreenViewModel? From { get; }

        /// <summary>
        /// The screen that is now current.
        /// </summary>
        public IScreenViewModel To { get; }

        /// <summary>
        /// Whether this navigation pushed <see cref="To"/> on top of the stack or popped back
        /// to reveal it.
        /// </summary>
        public NavigationDirection Direction { get; }

        #endregion
    }
}
