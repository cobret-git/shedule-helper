using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SheduleHelper.WpfApp
{
    public partial class MainWindow : Window
    {
        #region Fields
        private const int WM_GETMINMAXINFO = 0x0024;
        private const int WM_NCHITTEST = 0x0084;
        private HwndSource _hwndSource;
        private bool isDarkTheme = false;
        #endregion

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += MainWindow_SourceInitialized;
            Loaded += MainWindow_Loaded;
        }

        #region Initialization
        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            _hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            _hwndSource.AddHook(WndProc);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            IntPtr newStyle = new IntPtr(WS_CAPTION | WS_SIZEBOX | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
            SetWindowLongPtr64(hwnd, GWL_STYLE, newStyle);

            // IMPORTANT: Set to 0 to ENABLE transitions (confusing API naming!)
            int value = 0;
            DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref value, sizeof(int));

            // Enable modern DWM window corners (Windows 11)
            int cornerPreference = (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

            // Enable DWM composition (this is crucial)
            DwmEnableComposition(1);
        }
        #endregion

        #region Event Handlers
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Don't drag if clicking on a button
            if (e.OriginalSource is DependencyObject source && FindParent<Button>(source) != null)
                return;

            // Double-click to maximize/restore
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                return;
            }

            // Drag window
            if (e.ChangedButton == MouseButton.Left)
            {
                try { DragMove(); }
                catch { }
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            // Use Win32 API for native animation
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            ShowWindow(hwnd, SW_MINIMIZE);
        }

        private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
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

        #region Window State Management
        private void ToggleMaximize()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            if (WindowState == WindowState.Normal)
            {
                // Maximize with native animation
                ShowWindow(hwnd, SW_MAXIMIZE);
                WindowState = WindowState.Maximized;
            }
            else
            {
                // Restore with native animation
                ShowWindow(hwnd, SW_RESTORE);
                WindowState = WindowState.Normal;
            }
        }
        #endregion

        #region Window Procedure
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_GETMINMAXINFO:
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = false;
                    break;
            }
            return IntPtr.Zero;
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                GetMonitorInfo(monitor, ref monitorInfo);

                Rect rcWorkArea = RectangleToRect(monitorInfo.rcWork);
                Rect rcMonitorArea = RectangleToRect(monitorInfo.rcMonitor);

                mmi.ptMaxPosition.x = (int)(rcWorkArea.Left - rcMonitorArea.Left);
                mmi.ptMaxPosition.y = (int)(rcWorkArea.Top - rcMonitorArea.Top);
                mmi.ptMaxSize.x = (int)(rcWorkArea.Width);
                mmi.ptMaxSize.y = (int)(rcWorkArea.Height);
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }
        #endregion

        #region Helper Methods
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t)
                    return t;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private static Rect RectangleToRect(RECT r) =>
            new Rect(r.left, r.top, r.right - r.left, r.bottom - r.top);
        #endregion

        #region Native Methods - User32.dll
        private const int SW_MINIMIZE = 6;
        private const int SW_MAXIMIZE = 3;
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        // P/Invoke declarations:
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
        const int GWL_STYLE = -16;
        // Style flags:
        const long WS_CAPTION = 0x00C00000L;
        const long WS_SIZEBOX = 0x00040000L;
        const long WS_MINIMIZEBOX = 0x00020000L;
        const long WS_MAXIMIZEBOX = 0x00010000L;
        const long WS_SYSMENU = 0x00080000L;
        #endregion

        #region Native Methods - DWM (Desktop Window Manager)
        private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd,
            int attr,
            ref int attrValue,
            int attrSize);


        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmEnableComposition(int uCompositionAction);

        private enum DWM_WINDOW_CORNER_PREFERENCE
        {
            DWMWCP_DEFAULT = 0,
            DWMWCP_DONOTROUND = 1,
            DWMWCP_ROUND = 2,
            DWMWCP_ROUNDSMALL = 3
        }
        #endregion

        #region Structures
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }
        #endregion

        
    }
}