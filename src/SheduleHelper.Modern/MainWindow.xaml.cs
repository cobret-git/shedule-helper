using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Modern.Services;

namespace SheduleHelper.Modern
{
    /// <summary>
    /// The application's main window, hosting the breadcrumb bar and page navigation Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        #region Fields
        #endregion

        #region Constructors

        public MainWindow()
        {
            InitializeComponent();

            //NavigationService.Initialize(ContentFrame);
            //NavigationService.NavigateToHome();
        }

        #endregion

        #region Handlers

        private void AppBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        {
            //if (args.Item is BreadcrumbItem item)
            //{
            //    NavigationService.NavigateToBreadcrumbItem(item);
            //}
        }

        #endregion
    }
}
