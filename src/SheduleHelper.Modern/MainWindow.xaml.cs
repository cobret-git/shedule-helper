using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.Services;
using SheduleHelper.Modern.Services;

namespace SheduleHelper.Modern
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
            _navigationService.NavigateToHome();
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
        }

        private void AppBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            if (args.Item is BreadcrumbItem item)
            {
                _navigationService.NavigateToBreadcrumbItem(item);
            }
        }

        #endregion
    }
}
