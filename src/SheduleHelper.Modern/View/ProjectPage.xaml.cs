using Microsoft.UI.Xaml.Controls;

namespace SheduleHelper.Modern.View
{
    /// <summary>
    /// Single Project page, drilled into from Projects &amp; Tasks. DataContext is assigned by
    /// <see cref="Services.NavigationService"/>.
    /// </summary>
    public sealed partial class ProjectPage : Page
    {
        public ProjectPage()
        {
            InitializeComponent();
        }
    }
}
