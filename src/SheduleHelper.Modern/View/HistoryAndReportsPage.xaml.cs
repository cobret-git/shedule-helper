using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.ViewModels;

namespace SheduleHelper.Modern.View
{
    /// <summary>
    /// History &amp; Reports page (The Evaluator). Resolves its own <see cref="HistoryAndReportsViewModel"/>
    /// into its DataContext; <see cref="Services.NavigationService"/> reads it back to drive the lifecycle.
    /// </summary>
    public sealed partial class HistoryAndReportsPage : Page
    {
        public HistoryAndReportsPage()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<HistoryAndReportsViewModel>();
        }
    }
}
