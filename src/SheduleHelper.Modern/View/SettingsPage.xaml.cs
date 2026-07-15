using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.ViewModels;

namespace SheduleHelper.Modern.View
{
    /// <summary>
    /// Settings page (The Rule Setter). Resolves its own <see cref="SettingsViewModel"/> into its
    /// DataContext; <see cref="Services.NavigationService"/> reads it back to drive the lifecycle.
    /// </summary>
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<SettingsViewModel>();
        }
    }
}
