using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace SheduleHelper.WpfApp.Controls
{
    public class CustomWindow : Window
    {
        #region Fields

        private const int WM_GETMINMAXINFO = 0x0024;
        private HwndSource? _hwndSource;
        private Border? _titleBar;
        private Button? _minimizeButton;
        private Button? _maximizeRestoreButton;
        private Button? _closeButton;

        #endregion

        #region Constructors

        static CustomWindow()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CustomWindow),
                new FrameworkPropertyMetadata(typeof(CustomWindow)));
        }

        public CustomWindow()
        {
            SourceInitialized += CustomWindow_SourceInitialized;
            Loaded += CustomWindow_Loaded;
        }

        #endregion

        #region Template Parts

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            // Unhook old events if reapplying template
            if (_titleBar != null)
                _titleBar.MouseLeftButtonDown -= TitleBar_MouseLeftButtonDown;
            if (_minimizeButton != null)
                _minimizeButton.Click -= Minimize_Click;
            if (_maximizeRestoreButton != null)
                _maximizeRestoreButton.Click -= MaximizeRestore_Click;
            if (_closeButton != null)
                _closeButton.Click -= Close_Click;

            // Get template parts
            _titleBar = GetTemplateChild("PART_TitleBar") as Border;
            _minimizeButton = GetTemplateChild("PART_MinimizeButton") as Button;
            _maximizeRestoreButton = GetTemplateChild("PART_MaximizeRestoreButton") as Button;
            _closeButton = GetTemplateChild("PART_CloseButton") as Button;

            // Hook up events
            if (_titleBar != null)
                _titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
            if (_minimizeButton != null)
                _minimizeButton.Click += Minimize_Click;
            if (_maximizeRestoreButton != null)
                _maximizeRestoreButton.Click += MaximizeRestore_Click;
            if (_closeButton != null)
                _closeButton.Click += Close_Click;
        }

        #endregion

        #region Initialization

        private void CustomWindow_SourceInitialized(object? sender, EventArgs e)
        {
            _hwndSource = (HwndSource)PresentationSource.FromVisual(this)!;
            _hwndSource.AddHook(WndProc);
        }

        private void CustomWindow_Loaded(object sender, RoutedEventArgs e)
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

        #region Handlers

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

        #endregion

        #region Helpers

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

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
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

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        // System metrics constants for calculating border thickness
        private const int SM_CXFRAME = 32;
        private const int SM_CYFRAME = 33;
        private const int SM_CXPADDEDBORDER = 92;

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
