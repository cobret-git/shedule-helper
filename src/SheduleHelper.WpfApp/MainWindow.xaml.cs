using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using SheduleHelper.WpfApp.Controls;

namespace SheduleHelper.WpfApp
{
    public partial class MainWindow : CustomWindow
    {
        #region Fields

        private bool isDarkTheme = false;

        #endregion

        #region Constructors

        public MainWindow()
        {
            InitializeComponent();
        }

        #endregion

        #region Handlers
        private void SwitchThemeButton_Click(object sender, RoutedEventArgs e)
        {
                // 1. Capture current visual as bitmap
                var renderBitmap = new RenderTargetBitmap(
                    (int)ActualWidth,
                    (int)ActualHeight,
                    96, 96,
                    PixelFormats.Pbgra32);

                renderBitmap.Render(this);

                // 2. Create overlay with the bitmap
                var overlay = new Image
                {
                    Source = renderBitmap,
                    Stretch = Stretch.None,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                };

                // Add to an AdornerLayer or separate Grid
                var grid = (Grid)Content;
                var overlayContainer = new Grid
                {
                    Background = Brushes.Transparent
                };
                overlayContainer.Children.Add(overlay);

                // Must be added AFTER the main content
                var parent = (Grid)this.Content;
                var mainContent = parent;

                // Create new root with overlay
                var root = new Grid();
                this.Content = null;
                root.Children.Add(mainContent);
                root.Children.Add(overlayContainer);
                this.Content = root;

                // 2. Switch theme instantly (no animation)
                isDarkTheme = !isDarkTheme;

            var themeResources = App.Current.Resources.MergedDictionaries.Where(x => x.Source.ToString().Contains("Theme")).ToArray();
            foreach (var item in themeResources)
                App.Current.Resources.MergedDictionaries.Remove(item);
            var themeUri = isDarkTheme
                ? new Uri("pack://application:,,,/SheduleHelper.WpfApp;component/Assets/Resources/DarkTheme.xaml", UriKind.Absolute)
                : new Uri("pack://application:,,,/SheduleHelper.WpfApp;component/Assets/Resources/LightTheme.xaml", UriKind.Absolute);
            App.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });

            // Force layout update
            root.UpdateLayout();

            // 4. Fade out overlay
            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            fadeOut.Completed += (s, args) =>
            {
                // Restore original structure
                root.Children.Remove(overlayContainer);
                root.Children.Remove(mainContent);
                this.Content = mainContent;
            };

            overlayContainer.BeginAnimation(OpacityProperty, fadeOut);
        }

        #endregion
    }
}