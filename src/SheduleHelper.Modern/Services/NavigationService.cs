using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Services;
using SheduleHelper.Core.ViewModels;
using SheduleHelper.Modern.View;
using System;
using System.Collections.ObjectModel;

namespace SheduleHelper.Modern.Services
{
    /// <summary>
    /// Concrete WinUI implementation of <see cref="INavigationService"/>. In addition to the
    /// platform-agnostic navigation surface, it owns the lifecycle (activation, disposal) of the
    /// page ViewModels involved and requires one-time setup via <see cref="Initialize"/> with the
    /// hosting <see cref="Frame"/> - both of which are WinUI-specific concerns and therefore live
    /// only here, not on the interface. Pages are responsible for resolving their own ViewModel
    /// into their DataContext; this service reads it back after navigating to apply lifecycle hooks.
    /// </summary>
    public class NavigationService : INavigationService
    {
        #region Fields

        private Frame? _frame;
        private PageViewModelBase? _currentViewModel;

        #endregion

        #region Constructors

        public NavigationService()
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// The current breadcrumb trail, from the root destination to the active page.
        /// </summary>
        public ObservableCollection<BreadcrumbItem> Breadcrumbs { get; } = new();

        #endregion

        #region Methods

        /// <summary>
        /// Associates this service with the <see cref="Frame"/> that hosts page content. Must be
        /// called once, before any <c>NavigateToX</c> method.
        /// </summary>
        /// <param name="frame">The Frame that hosts the app's pages.</param>
        public void Initialize(Frame frame)
        {
            _frame = frame;
        }

        /// <summary>
        /// Navigates to the Home page (The Daily Control Center), resetting the breadcrumb trail.
        /// </summary>
        public void NavigateToHome()
        {
            NavigateToRoot<HomeViewModel, HomePage>("Home");
        }

        /// <summary>
        /// Navigates to the Projects &amp; Tasks page (The Organizer), resetting the breadcrumb trail.
        /// </summary>
        public void NavigateToProjectsAndTasks()
        {
            NavigateToRoot<ProjectsAndTasksViewModel, ProjectsAndTasksPage>("Projects & Tasks");
        }

        /// <summary>
        /// Navigates to the given project's page, appending it to the current breadcrumb trail.
        /// </summary>
        /// <param name="project">The project to display.</param>
        public void NavigateToProject(Project project)
        {
            NavigateToChild<ProjectViewModel, ProjectPage, Project>(project.Name, project);
        }

        /// <summary>
        /// Navigates to the History &amp; Reports page (The Evaluator), resetting the breadcrumb trail.
        /// </summary>
        public void NavigateToHistoryAndReports()
        {
            NavigateToRoot<HistoryAndReportsViewModel, HistoryAndReportsPage>("History & Reports");
        }

        /// <summary>
        /// Navigates to the Settings page (The Rule Setter), resetting the breadcrumb trail.
        /// </summary>
        public void NavigateToSettings()
        {
            NavigateToRoot<SettingsViewModel, SettingsPage>("Settings");
        }

        /// <summary>
        /// Re-activates a previously visited breadcrumb segment, discarding any segments after it.
        /// </summary>
        /// <param name="item">The breadcrumb segment to navigate back to. Must be an item currently in <see cref="Breadcrumbs"/>.</param>
        public void NavigateToBreadcrumbItem(BreadcrumbItem item)
        {
            var index = Breadcrumbs.IndexOf(item);
            if (index < 0)
            {
                return;
            }

            item.Reactivate();

            while (Breadcrumbs.Count > index + 1)
            {
                Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Navigates to a root-level destination (Home, Projects &amp; Tasks, History &amp; Reports, Settings),
        /// replacing the entire breadcrumb trail with a single segment for that destination.
        /// </summary>
        private void NavigateToRoot<TViewModel, TPage>(string label)
            where TViewModel : PageViewModelBase
            where TPage : Page
        {
            NavigateCore<TViewModel, TPage>();

            Breadcrumbs.Clear();
            Breadcrumbs.Add(new BreadcrumbItem(label, NavigateCore<TViewModel, TPage>));
        }

        /// <summary>
        /// Navigates to a destination drilled into from the current one, appending a segment to the breadcrumb trail.
        /// </summary>
        private void NavigateToChild<TViewModel, TPage, TContext>(string label, TContext context)
            where TViewModel : PageViewModel<TContext>
            where TPage : Page
        {
            NavigateCore<TViewModel, TPage, TContext>(context);

            Breadcrumbs.Add(new BreadcrumbItem(label, () => NavigateCore<TViewModel, TPage, TContext>(context)));
        }

        /// <summary>
        /// Swaps the Frame's content to <typeparamref name="TPage"/> and activates its ViewModel.
        /// Does not touch <see cref="Breadcrumbs"/> - callers are responsible for that.
        /// </summary>
        private void NavigateCore<TViewModel, TPage>()
            where TViewModel : PageViewModelBase
            where TPage : Page
        {
            EnsureInitialized();

            _currentViewModel?.Dispose();
            _frame!.Navigate(typeof(TPage));

            if (_frame.Content is TPage page && page.DataContext is TViewModel viewModel)
            {
                _currentViewModel = viewModel;
            }
        }

        /// <summary>
        /// Swaps the Frame's content to <typeparamref name="TPage"/> and activates its ViewModel with
        /// the given context. Does not touch <see cref="Breadcrumbs"/> - callers are responsible for that.
        /// </summary>
        private void NavigateCore<TViewModel, TPage, TContext>(TContext context)
            where TViewModel : PageViewModel<TContext>
            where TPage : Page
        {
            EnsureInitialized();

            _currentViewModel?.Dispose();
            _frame!.Navigate(typeof(TPage));

            if (_frame.Content is TPage page && page.DataContext is TViewModel viewModel)
            {
                viewModel.SetContext(context);
                _currentViewModel = viewModel;
            }
        }

        private void EnsureInitialized()
        {
            if (_frame is null)
            {
                throw new InvalidOperationException($"{nameof(NavigationService)} has not been initialized. Call {nameof(Initialize)} first.");
            }
        }

        #endregion
    }
}
