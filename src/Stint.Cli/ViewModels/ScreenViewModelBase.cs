using Stint.Cli.Services;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// Base class for a screen's ViewModel. Adds the navigation service to the plumbing of
    /// <see cref="ScreenViewModelCore"/> - so concrete screens (HomeViewModel, ProjectsViewModel,
    /// ...) only need to describe their own state and commands.
    /// </summary>
    public abstract class ScreenViewModelBase : ScreenViewModelCore
    {
        #region Constructors

        protected ScreenViewModelBase(INavigationService navigation)
        {
            ArgumentNullException.ThrowIfNull(navigation);

            Navigation = navigation;
        }
        #endregion

        #region Properties

        /// <summary>
        /// Service used to move between screens. Derived classes call into this from their
        /// commands (e.g. a "switch" key hint calling <c>Navigation.NavigateTo&lt;SwitchViewModel&gt;()</c>).
        /// </summary>
        protected INavigationService Navigation { get; }
        #endregion
    }
}
