using SheduleHelper.Core.Components.Entities;
using System.Collections.ObjectModel;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Drives navigation between the app's pages and exposes the breadcrumb trail for the current
    /// location. Deliberately platform-agnostic - it exposes only the semantic destinations a
    /// caller can navigate to, none of the hosting UI framework's own navigation types (e.g. WinUI's
    /// <c>Frame</c>). Concrete implementations live in each host application.
    /// </summary>
    public interface INavigationService
    {
        #region Properties

        /// <summary>
        /// The current breadcrumb trail, from the root destination to the active page.
        /// </summary>
        ObservableCollection<BreadcrumbItem> Breadcrumbs { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Navigates to the Home page (The Daily Control Center), resetting the breadcrumb trail.
        /// </summary>
        void NavigateToHome();

        /// <summary>
        /// Navigates to the Projects &amp; Tasks page (The Organizer), resetting the breadcrumb trail.
        /// </summary>
        void NavigateToProjectsAndTasks();

        /// <summary>
        /// Navigates to the given project's page, appending it to the current breadcrumb trail.
        /// </summary>
        /// <param name="project">The project to display.</param>
        void NavigateToProject(Project project);

        /// <summary>
        /// Navigates to the History &amp; Reports page (The Evaluator), resetting the breadcrumb trail.
        /// </summary>
        void NavigateToHistoryAndReports();

        /// <summary>
        /// Navigates to the Settings page (The Rule Setter), resetting the breadcrumb trail.
        /// </summary>
        void NavigateToSettings();

        /// <summary>
        /// Re-activates a previously visited breadcrumb segment, discarding any segments after it.
        /// </summary>
        /// <param name="item">The breadcrumb segment to navigate back to. Must be an item currently in <see cref="Breadcrumbs"/>.</param>
        void NavigateToBreadcrumbItem(BreadcrumbItem item);

        #endregion
    }
}
