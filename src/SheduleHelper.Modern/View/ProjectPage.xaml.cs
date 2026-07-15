using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.ViewModels;

namespace SheduleHelper.Modern.View
{
    /// <summary>
    /// Single Project page, drilled into from Projects &amp; Tasks. Resolves its own
    /// <see cref="ProjectViewModel"/> into its DataContext; <see cref="Services.NavigationService"/>
    /// reads it back to supply the navigated-to <c>Project</c> and drive the lifecycle.
    /// </summary>
    public sealed partial class ProjectPage : Page
    {
        public ProjectPage()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<ProjectViewModel>();
        }
    }
}
