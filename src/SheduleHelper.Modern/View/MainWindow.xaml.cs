using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.Services;
using SheduleHelper.Modern.Services;
using System;
using System.Linq;
using Windows.UI;

namespace SheduleHelper.Modern.View
{
    /// <summary>
    /// The application's main window, hosting the breadcrumb bar and page navigation Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        #region Fields

        private readonly NavigationService _navigationService;
        private readonly DialogService _dialogService;
        private readonly DispatcherService _dispatcherService;
        private readonly ThemeService _themeService;

        private bool _isSyncingNavigationViewSelection;

        #endregion

        #region Constructors

        public MainWindow()
        {
            InitializeComponent();

            _dispatcherService = App.Current.Services.GetRequiredService<DispatcherService>();
            _dispatcherService.Initialize(DispatcherQueue);

            _navigationService = App.Current.Services.GetRequiredService<NavigationService>();
            _dialogService = App.Current.Services.GetRequiredService<DialogService>();

            _navigationService.Initialize(ContentFrame);
            _navigationService.RootNavigated += NavigationService_RootNavigated;

            // Hides the default system title bar.
            ExtendsContentIntoTitleBar = true;
            // Replace system title bar with the WinUI TitleBar control.
            SetTitleBar(titleBar);

            // Must run after ExtendsContentIntoTitleBar/SetTitleBar - setting
            // ExtendsContentIntoTitleBar resets AppWindow.TitleBar's button colors back to the
            // OS-theme defaults, which would otherwise wipe out the colors ThemeService applies.
            _themeService = App.Current.Services.GetRequiredService<ThemeService>();
            _themeService.Initialize(RootGrid, GetAppWindowForCurrentWindow());
        }

        #endregion

        #region Properties

        /// <summary>
        /// Drives page navigation and the breadcrumb trail bound by <see cref="AppBreadcrumbBar"/>.
        /// </summary>
        public INavigationService NavigationService => _navigationService;

        #endregion

        #region Handlers

        private void RootGrid_Loaded(object sender, RoutedEventArgs e)
        {
            _dialogService.Initialize(RootGrid.XamlRoot);

            // The TitleBar control re-syncs AppWindow.TitleBar's caption-button colors to the OS
            // theme once it finishes loading, overwriting what ThemeService applied in the
            // constructor - reapply here, after it (and the rest of the page) has loaded, so ours
            // wins.
            _themeService.ReapplyTitleBarColors();
        }

        private void AppBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (args.Item is BreadcrumbItem item)
            {
                _navigationService.NavigateToBreadcrumbItem(item);
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            // Setting nv.SelectedItem to sync with a programmatic navigation (see
            // NavigationService_RootNavigated) fires this same handler - skip re-navigating in
            // that case, the navigation already happened.
            if (_isSyncingNavigationViewSelection)
            {
                return;
            }

            if (args.IsSettingsSelected)
            {
                _navigationService.NavigateToSettings();
                return;
            }

            if (args.SelectedItemContainer is not NavigationViewItem { Tag: string tag })
            {
                return;
            }

            switch (tag)
            {
                case "home":
                    _navigationService.NavigateToHome();
                    break;
                case "projectsAndTasks":
                    _navigationService.NavigateToProjectsAndTasks();
                    break;
                case "historyAndReports":
                    _navigationService.NavigateToHistoryAndReports();
                    break;
                case "settings":
                    _navigationService.NavigateToSettings();
                    break;
            }
        }

        #endregion

        #region Handlers (NavigationService)

        /// <summary>
        /// Keeps <see cref="nv"/>'s selection in sync when navigation happens programmatically
        /// (e.g. the initial navigation on startup) rather than via the user selecting an item.
        /// </summary>
        private void NavigationService_RootNavigated(object? sender, string navigationViewItemTag)
        {
            var matchingItem = nv.MenuItems
                .Concat(nv.FooterMenuItems)
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => Equals(item.Tag, navigationViewItemTag));

            if (matchingItem is null || Equals(nv.SelectedItem, matchingItem))
            {
                return;
            }

            _isSyncingNavigationViewSelection = true;
            nv.SelectedItem = matchingItem;
            _isSyncingNavigationViewSelection = false;
        }

        #endregion

        #region Helpers
        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Microsoft.UI.WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(windowId);
        }
        #endregion
    }
}
