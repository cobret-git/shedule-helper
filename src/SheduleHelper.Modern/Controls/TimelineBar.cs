using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using SheduleHelper.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using Windows.UI;
using System.Runtime.InteropServices.WindowsRuntime;

namespace SheduleHelper.Modern.Controls
{
    [TemplatePart(Name = CanvasPartName, Type = typeof(Canvas))]
    public sealed class TimelineBar : Control
    {
        private const string CanvasPartName = "PART_Canvas";

        // Reserve of categorical colors for segments, drawn from the app's Material palette
        // (Assets/Palettes/*.xaml) so it always matches the active theme. Used only when the
        // caller hasn't supplied an explicit Palette. Ordered for maximum perceptual distance
        // first; libraries like D3/Chart.js do the same thing with a fixed-size categorical
        // array and cycle through it (see GetBrushForSegment) once callers exceed its length.
        private static readonly string[] DefaultPaletteBrushKeys =
        {
            "MdPrimaryBrush",
            "MdTertiaryContainerBrush",
            "MdSecondaryContainerBrush",
            "MdPrimaryContainerBrush",
            "MdTertiaryBrush",
            "MdSecondaryBrush",
            "MdPrimaryFixedDimBrush",
            "MdTertiaryFixedDimBrush",
            "MdSecondaryFixedDimBrush",
            "MdInversePrimaryBrush",
        };

        private const string HatchBackgroundColorKey = "MdSurfaceVariantColor";
        private const string HatchLineColorKey = "MdOutlineVariantColor";

        private Canvas? _canvas;
        private DispatcherTimer _liveTimer;
        private readonly Dictionary<string, Brush> _assignedBrushes = new();
        private int _paletteIndex = 0;

        public TimelineBar()
        {
            this.DefaultStyleKey = typeof(TimelineBar);

            // Timer to update the "Live" task width and the current time indicator every minute
            _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _liveTimer.Tick += (s, e) => DrawTimeline();

            // The app switches themes by swapping the merged palette dictionary at runtime
            // (see ThemeService.SwapPaletteDictionary) rather than via WinUI's built-in
            // light/dark resource switching, so brushes resolved here need an explicit redraw.
            this.ActualThemeChanged += (s, e) => DrawTimeline();
        }

        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_canvas != null)
            {
                _canvas.SizeChanged -= OnCanvasSizeChanged;
            }

            _canvas = GetTemplateChild(CanvasPartName) as Canvas;

            if (_canvas != null)
            {
                _canvas.SizeChanged += OnCanvasSizeChanged;
                DrawTimeline();
            }
        }

        private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Redraw everything when the window/control is resized
            DrawTimeline();
        }

        #region Dependency Properties

        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable<TimelineSegment>), typeof(TimelineBar), new PropertyMetadata(null, OnItemsSourceChanged));

        public IEnumerable<TimelineSegment> ItemsSource
        {
            get => (IEnumerable<TimelineSegment>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public static readonly DependencyProperty DayStartProperty =
            DependencyProperty.Register(nameof(DayStart), typeof(TimeSpan), typeof(TimelineBar), new PropertyMetadata(TimeSpan.FromHours(8), OnPropertyChanged));

        public TimeSpan DayStart
        {
            get => (TimeSpan)GetValue(DayStartProperty);
            set => SetValue(DayStartProperty, value);
        }

        public static readonly DependencyProperty DayEndProperty =
            DependencyProperty.Register(nameof(DayEnd), typeof(TimeSpan), typeof(TimelineBar), new PropertyMetadata(TimeSpan.FromHours(18), OnPropertyChanged));

        public TimeSpan DayEnd
        {
            get => (TimeSpan)GetValue(DayEndProperty);
            set => SetValue(DayEndProperty, value);
        }

        // You can bind a collection of brushes from your ResourceDictionary here
        public static readonly DependencyProperty PaletteProperty =
            DependencyProperty.Register(nameof(Palette), typeof(IList<Brush>), typeof(TimelineBar), new PropertyMetadata(new List<Brush>()));

        public IList<Brush> Palette
        {
            get => (IList<Brush>)GetValue(PaletteProperty);
            set => SetValue(PaletteProperty, value);
        }

        public static readonly DependencyProperty IndicatorBrushProperty =
            DependencyProperty.Register(nameof(IndicatorBrush), typeof(Brush), typeof(TimelineBar), new PropertyMetadata(null, OnPropertyChanged));

        public Brush IndicatorBrush
        {
            get => (Brush)GetValue(IndicatorBrushProperty);
            set => SetValue(IndicatorBrushProperty, value);
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (TimelineBar)d;

            if (e.OldValue is INotifyCollectionChanged oldCollection)
                oldCollection.CollectionChanged -= control.OnCollectionChanged;

            if (e.NewValue is INotifyCollectionChanged newCollection)
                newCollection.CollectionChanged += control.OnCollectionChanged;

            control.ResetPaletteAssignments();
            control.DrawTimeline();
        }

        private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((TimelineBar)d).DrawTimeline();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            DrawTimeline();
        }

        #endregion

        #region Rendering Logic

        private void ResetPaletteAssignments()
        {
            _assignedBrushes.Clear();
            _paletteIndex = 0;
        }

        private Brush GetBrushForSegment(TimelineSegment segment)
        {
            // Return previously assigned color for this specific task name
            if (_assignedBrushes.TryGetValue(segment.Name, out var brush))
            {
                return brush;
            }

            // Assign a new color from the palette (falling back to the default categorical
            // reserve when the caller hasn't supplied one), cycling once segments outnumber it.
            var palette = Palette != null && Palette.Count > 0 ? Palette : ResolveDefaultPalette();
            if (palette.Count > 0)
            {
                var newBrush = palette[_paletteIndex % palette.Count];
                _assignedBrushes[segment.Name] = newBrush;
                _paletteIndex++;
                return newBrush;
            }

            return null; // Fallback handled by DataTemplate or default styling
        }

        private static IList<Brush> ResolveDefaultPalette()
        {
            var resources = Application.Current.Resources;
            var brushes = new List<Brush>(DefaultPaletteBrushKeys.Length);

            foreach (var key in DefaultPaletteBrushKeys)
            {
                if (resources.TryGetValue(key, out var value) && value is Brush brush)
                {
                    brushes.Add(brush);
                }
            }

            return brushes;
        }

        private static Color ResolveColor(string key)
        {
            return (Color)Application.Current.Resources[key];
        }

        private void DrawTimeline()
        {
            if (_canvas == null || ItemsSource == null || ActualWidth == 0) return;

            _canvas.Children.Clear();
            bool hasLiveTask = false;

            var totalMinutes = (DayEnd - DayStart).TotalMinutes;
            if (totalMinutes <= 0) return;

            foreach (var segment in ItemsSource.OrderBy(s => s.StartTime))
            {
                var startMinutes = (segment.StartTime.TimeOfDay - DayStart).TotalMinutes;

                // Calculate end time, defaulting to DateTime.Now if it's the active live task
                var endTimeToUse = segment.EndTime ?? DateTime.Now;
                var endMinutes = (endTimeToUse.TimeOfDay - DayStart).TotalMinutes;

                // Clamp values to ensure we don't draw outside the bounds of the day
                startMinutes = Math.Max(0, Math.Min(startMinutes, totalMinutes));
                endMinutes = Math.Max(0, Math.Min(endMinutes, totalMinutes));

                var widthMinutes = endMinutes - startMinutes;
                if (widthMinutes <= 0) continue;

                var xPosition = (startMinutes / totalMinutes) * _canvas.ActualWidth;
                var width = (widthMinutes / totalMinutes) * _canvas.ActualWidth;

                Brush segmentBrush;

                // Check if this segment requires the hatched pattern
                if (segment.DisplayStyle == SegmentDisplayStyle.Hatched)
                {
                    var bgColor = ResolveColor(HatchBackgroundColorKey);
                    var lineColor = ResolveColor(HatchLineColorKey);

                    segmentBrush = CreateHatchedBrush(width, _canvas.ActualHeight, bgColor, lineColor);
                }
                else
                {
                    segmentBrush = GetBrushForSegment(segment);
                }

                var rect = new Rectangle
                {
                    Width = width,
                    Height = _canvas.ActualHeight,
                    Fill = segmentBrush,
                    StrokeThickness = 0
                };

                Canvas.SetLeft(rect, xPosition);
                Canvas.SetTop(rect, 0);
                _canvas.Children.Add(rect);

                if (segment.IsActive) hasLiveTask = true;
            }

            DrawCurrentTimeIndicator(totalMinutes);

            // Only run the timer if there is an active task or the current time indicator needs moving
            if (hasLiveTask || (DateTime.Now.TimeOfDay >= DayStart && DateTime.Now.TimeOfDay <= DayEnd))
            {
                _liveTimer.Start();
            }
            else
            {
                _liveTimer.Stop();
            }
        }

        private void DrawCurrentTimeIndicator(double totalDayMinutes)
        {
            var now = DateTime.Now.TimeOfDay;
            if (now < DayStart || now > DayEnd) return; // Don't draw if outside the tracked hours

            var nowMinutes = (now - DayStart).TotalMinutes;
            var xPosition = (nowMinutes / totalDayMinutes) * _canvas.ActualWidth;

            var indicatorLine = new Line
            {
                X1 = 0,
                X2 = 0,
                Y1 = 0,
                Y2 = _canvas.ActualHeight,
                Stroke = IndicatorBrush,
                StrokeThickness = 2
            };

            Canvas.SetLeft(indicatorLine, xPosition);
            _canvas.Children.Add(indicatorLine);
        }

        /// <summary>
        /// Generates an in-memory hatched pattern matched to the exact dimensions of the target rectangle.
        /// </summary>
        private ImageBrush CreateHatchedBrush(double width, double height, Color backgroundColor, Color hatchColor)
        {
            // Ensure minimum dimensions to avoid 0-byte buffer crashes
            int w = (int)Math.Max(1, width);
            int h = (int)Math.Max(1, height);

            var bitmap = new WriteableBitmap(w, h);

            // WinUI WriteableBitmap expects a flat 1D array of BGRA bytes (Blue, Green, Red, Alpha)
            byte[] pixels = new byte[w * h * 4];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int index = (y * w + x) * 4;

                    // Creates a diagonal stripe 3 pixels wide, repeating every 8 pixels
                    bool isStripe = (x + y) % 8 < 3;
                    Color color = isStripe ? hatchColor : backgroundColor;

                    // Assign BGRA values
                    pixels[index] = color.B;
                    pixels[index + 1] = color.G;
                    pixels[index + 2] = color.R;
                    pixels[index + 3] = color.A;
                }
            }

            // Direct byte-to-buffer copy (Requires using System.Runtime.InteropServices.WindowsRuntime;)
            pixels.CopyTo(bitmap.PixelBuffer);

            return new ImageBrush
            {
                ImageSource = bitmap,
                Stretch = Stretch.None // Prevent anti-aliasing blurring on the sharp lines
            };
        }
        #endregion
    }
}
