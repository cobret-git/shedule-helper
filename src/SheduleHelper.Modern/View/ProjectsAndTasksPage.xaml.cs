using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.ViewModels;

namespace SheduleHelper.Modern.View
{
    /// <summary>
    /// Projects &amp; Tasks page (The Organizer). Resolves its own <see cref="ProjectsAndTasksViewModel"/>
    /// into its DataContext; <see cref="Services.NavigationService"/> reads it back to drive the lifecycle.
    /// </summary>
    public sealed partial class ProjectsAndTasksPage : Page
    {
        public ProjectsAndTasksPage()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<ProjectsAndTasksViewModel>();
        }
    }
}
