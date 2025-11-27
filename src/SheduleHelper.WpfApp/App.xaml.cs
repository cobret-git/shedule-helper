using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Media;

namespace SheduleHelper.WpfApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public App()
        {
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
        }
    }

}
