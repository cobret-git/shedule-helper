using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SheduleHelper.Core.ViewModels
{
    /// <summary>
    /// Base class for ViewModels backing a navigable page. Lifecycle is driven exclusively by
    /// <see cref="Services.NavigationService"/>, which owns construction and disposal of page
    /// ViewModels - pages/ViewModels never need to know about navigation themselves.
    /// </summary>
    public abstract class PageViewModelBase : ObservableObject, IDisposable
    {
        #region Methods

        /// <summary>
        /// Releases resources held by this ViewModel. Called by the navigation service once this
        /// instance is no longer the active page.
        /// </summary>
        public abstract void Dispose();
        #endregion
    }

    /// <summary>
    /// Base class for ViewModels backing a page that requires a navigation context (e.g. the
    /// entity being viewed) in order to activate.
    /// </summary>
    /// <typeparam name="TContext">The type of context this page's ViewModel requires.</typeparam>
    public abstract class PageViewModel<TContext> : PageViewModelBase
    {
        #region Methods

        /// <summary>
        /// Called by the navigation service immediately after this ViewModel becomes the active
        /// page, supplying the <typeparamref name="TContext"/> it needs to display.
        /// </summary>
        /// <param name="context">The navigation context passed to the typed <c>NavigateToX</c> call.</param>
        public abstract void SetContext(TContext context);
        #endregion
    }
}
