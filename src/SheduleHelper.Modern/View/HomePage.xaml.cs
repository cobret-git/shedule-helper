using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.ViewModels;

namespace SheduleHelper.Modern.View
{
    /// <summary>
    /// Home page (The Daily Control Center). Resolves its own <see cref="HomeViewModel"/> into its
    /// DataContext; <see cref="Services.NavigationService"/> reads it back to drive the lifecycle.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<HomeViewModel>();
        }
    }
}
