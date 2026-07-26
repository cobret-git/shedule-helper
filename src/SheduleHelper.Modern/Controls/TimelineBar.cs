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
using Windows.Foundation;
using Windows.UI;
using System.Runtime.InteropServices.WindowsRuntime;

namespace SheduleHelper.Modern.Controls
{
    [TemplatePart(Name = CanvasPartName, Type = typeof(Canvas))]
    [TemplatePart(Name = TicksCanvasPartName, Type = typeof(Canvas))]
    public sealed class TimelineBar : Control
    {
        private const string CanvasPartName = "PART_Canvas";
        private const string TicksCanvasPartName = "PART_TicksCanvas";

        // Candidate hour-scale intervals (minutes), ascending. DrawTicks picks the smallest one
        // whose labels all fit the current width, so density adapts automatically on resize.
        private static readonly int[] TickIntervalCandidatesMinutes = { 15, 30, 60, 120, 180, 240, 360, 480, 720 };
        private const double TickMinGap = 8;

        // Reserve of categorical (background, foreground) color pairs for segments, drawn from
        // the app's Material palette (Assets/Palettes/*.xaml) so it always matches the active
        // theme. Used only when the caller hasn't supplied an explicit Palette. Each pair reuses
        // Material's own contrast-checked "on-color" companion, so labels drawn on top (see
        // AddSegmentLabelIfFits) are always readable without inventing new brushes. Ordered for
        // maximum perceptual distance first; libraries like D3/Chart.js do the same thing with a
        // fixed-size categorical array and cycle through it (see GetSegmentColors) once callers
        // exceed its length.
        private static readonly (string Background, string Foreground)[] DefaultPaletteKeys =
        {
            ("MdPrimaryBrush", "MdOnPrimaryBrush"),
            ("MdTertiaryContainerBrush", "MdOnTertiaryContainerBrush"),
            ("MdSecondaryContainerBrush", "MdOnSecondaryContainerBrush"),
            ("MdPrimaryContainerBrush", "MdOnPrimaryContainerBrush"),
            ("MdTertiaryBrush", "MdOnTertiaryBrush"),
            ("MdSecondaryBrush", "MdOnSecondaryBrush"),
            ("MdPrimaryFixedDimBrush", "MdOnPrimaryFixedVariantBrush"),
            ("MdTertiaryFixedDimBrush", "MdOnTertiaryFixedVariantBrush"),
            ("MdSecondaryFixedDimBrush", "MdOnSecondaryFixedVariantBrush"),
            ("MdPrimaryFixedBrush", "MdOnPrimaryFixedBrush"),
            ("MdTertiaryFixedBrush", "MdOnTertiaryFixedBrush"),
            ("MdSecondaryFixedBrush", "MdOnSecondaryFixedBrush"),
        };

        private const string HatchBackgroundColorKey = "MdSurfaceVariantColor";
        private const string HatchLineColorKey = "MdOutlineVariantColor";
        private const string DefaultForegroundBrushKey = "MdOnSurfaceVariantBrush";
        private const double LabelHorizontalPadding = 8;

        private Canvas? _canvas;
        private Canvas? _ticksCanvas;
        private DispatcherTimer _liveTimer;
        private readonly Dictionary<string, (Brush Background, Brush Foreground)> _assignedColors = new();
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
            if (_ticksCanvas != null)
            {
                _ticksCanvas.SizeChanged -= OnCanvasSizeChanged;
            }

            _canvas = GetTemplateChild(CanvasPartName) as Canvas;
            _ticksCanvas = GetTemplateChild(TicksCanvasPartName) as Canvas;

            if (_canvas != null)
            {
                _canvas.SizeChanged += OnCanvasSizeChanged;
            }
            if (_ticksCanvas != null)
            {
                _ticksCanvas.SizeChanged += OnCanvasSizeChanged;
            }

            if (_canvas != null)
            {
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

        // Uniform label color used only when Palette is overridden with plain backgrounds; falls
        // back to DefaultForegroundBrushKey. Callers overriding Palette are responsible for
        // picking backgrounds that this single foreground reads well against.
        public static readonly DependencyProperty SegmentForegroundProperty =
            DependencyProperty.Register(nameof(SegmentForeground), typeof(Brush), typeof(TimelineBar), new PropertyMetadata(null, OnPropertyChanged));

        public Brush SegmentForeground
        {
            get => (Brush)GetValue(SegmentForegroundProperty);
            set => SetValue(SegmentForegroundProperty, value);
        }

        public static readonly DependencyProperty IndicatorBrushProperty =
            DependencyProperty.Register(nameof(IndicatorBrush), typeof(Brush), typeof(TimelineBar), new PropertyMetadata(null, OnPropertyChanged));

        public Brush IndicatorBrush
        {
            get => (Brush)GetValue(IndicatorBrushProperty);
            set => SetValue(IndicatorBrushProperty, value);
        }

        // Off by default until the planned hover-driven "inspect the graph" interaction
        // replaces this always-on version with something driven by pointer position.
        public static readonly DependencyProperty ShowCurrentTimeIndicatorProperty =
            DependencyProperty.Register(nameof(ShowCurrentTimeIndicator), typeof(bool), typeof(TimelineBar), new PropertyMetadata(false, OnPropertyChanged));

        public bool ShowCurrentTimeIndicator
        {
            get => (bool)GetValue(ShowCurrentTimeIndicatorProperty);
            set => SetValue(ShowCurrentTimeIndicatorProperty, value);
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
            _assignedColors.Clear();
            _paletteIndex = 0;
        }

        private (Brush Background, Brush Foreground) GetSegmentColors(TimelineSegment segment)
        {
            // Return the previously assigned colors for this specific task name
            if (_assignedColors.TryGetValue(segment.Name, out var colors))
            {
                return colors;
            }

            // Assign the next colors from the palette (falling back to the default categorical
            // reserve when the caller hasn't supplied one), cycling once segments outnumber it.
            var palette = ResolveActivePalette();
            if (palette.Count > 0)
            {
                var newColors = palette[_paletteIndex % palette.Count];
                _assignedColors[segment.Name] = newColors;
                _paletteIndex++;
                return newColors;
            }

            return (null, null); // Fallback handled by DataTemplate or default styling
        }

        private IList<(Brush Background, Brush Foreground)> ResolveActivePalette()
        {
            if (Palette != null && Palette.Count > 0)
            {
                var foreground = SegmentForeground ?? ResolveBrush(DefaultForegroundBrushKey);
                return Palette.Select(background => (Background: background, Foreground: foreground)).ToList();
            }

            return DefaultPaletteKeys
                .Select(keys => (Background: ResolveBrush(keys.Background), Foreground: ResolveBrush(keys.Foreground)))
                .ToList();
        }

        private static Brush ResolveBrush(string key)
        {
            return (Brush)Application.Current.Resources[key];
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

            DrawTicks(totalMinutes);

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

                (Brush Background, Brush Foreground) colors;

                // Check if this segment requires the hatched pattern
                if (segment.DisplayStyle == SegmentDisplayStyle.Hatched)
                {
                    var bgColor = ResolveColor(HatchBackgroundColorKey);
                    var lineColor = ResolveColor(HatchLineColorKey);

                    colors = (CreateHatchedBrush(width, _canvas.ActualHeight, bgColor, lineColor), ResolveBrush(DefaultForegroundBrushKey));
                }
                else
                {
                    colors = GetSegmentColors(segment);
                }

                var rect = new Rectangle
                {
                    Width = width,
                    Height = _canvas.ActualHeight,
                    Fill = colors.Background,
                    StrokeThickness = 0
                };

                Canvas.SetLeft(rect, xPosition);
                Canvas.SetTop(rect, 0);
                _canvas.Children.Add(rect);

                AddSegmentLabelIfFits(segment.Name, colors.Foreground, xPosition, width, _canvas.ActualHeight);

                if (segment.IsActive) hasLiveTask = true;
            }

            if (ShowCurrentTimeIndicator)
            {
                DrawCurrentTimeIndicator(totalMinutes);
            }

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

        /// <summary>
        /// Draws <paramref name="text"/> centered over a segment, but only when it fits without
        /// wrapping or clipping - otherwise the segment is left as a plain colored block.
        /// </summary>
        private void AddSegmentLabelIfFits(string text, Brush foreground, double segmentX, double segmentWidth, double canvasHeight)
        {
            if (string.IsNullOrEmpty(text) || foreground == null) return;

            var label = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                TextWrapping = TextWrapping.NoWrap,
            };

            label.Measure(new Size(double.PositiveInfinity, canvasHeight));

            if (label.DesiredSize.Width + (LabelHorizontalPadding * 2) > segmentWidth) return;

            Canvas.SetLeft(label, segmentX + ((segmentWidth - label.DesiredSize.Width) / 2));
            Canvas.SetTop(label, (canvasHeight - label.DesiredSize.Height) / 2);
            _canvas.Children.Add(label);
        }

        /// <summary>
        /// Draws the hour scale above the bar, picking the coarsest interval from
        /// <see cref="TickIntervalCandidatesMinutes"/> whose *actual, clamped* label positions
        /// don't collide - so density adapts on resize instead of ever overlapping. Boundary
        /// labels (day start/end) get clamped fully inside the canvas rather than centered on
        /// their timestamp, which shifts them further toward the middle than an unclamped tick -
        /// simulating real positions (rather than a spacing-formula estimate) is what catches that.
        /// </summary>
        private void DrawTicks(double totalMinutes)
        {
            if (_ticksCanvas == null) return;

            _ticksCanvas.Children.Clear();

            var availableWidth = _ticksCanvas.ActualWidth;
            if (availableWidth <= 0) return;

            var labelWidth = MeasureTickLabelWidth();

            var positions = TickIntervalCandidatesMinutes
                .Select(interval => BuildTickPositions(interval, totalMinutes, availableWidth, labelWidth))
                .FirstOrDefault(candidate => !HasAdjacentOverlap(candidate, labelWidth));

            if (positions == null)
            {
                // Even the coarsest candidate collided - fall back to just the day boundaries
                // (and if the canvas is so narrow those two collide too, show nothing rather than
                // an unreadable jumble).
                var boundaries = new List<(TimeSpan TimeOfDay, double Left)>
                {
                    (DayStart, ClampLabelLeft(0, availableWidth, labelWidth)),
                    (DayEnd, ClampLabelLeft(availableWidth, availableWidth, labelWidth)),
                };
                positions = HasAdjacentOverlap(boundaries, labelWidth) ? new List<(TimeSpan TimeOfDay, double Left)>() : boundaries;
            }

            foreach (var (timeOfDay, left) in positions)
            {
                AddTickLabel(timeOfDay, left);
            }
        }

        private List<(TimeSpan TimeOfDay, double Left)> BuildTickPositions(int intervalMinutes, double totalMinutes, double availableWidth, double labelWidth)
        {
            var positions = new List<(TimeSpan TimeOfDay, double Left)>();

            for (var minutes = 0.0; minutes <= totalMinutes; minutes += intervalMinutes)
            {
                var x = (minutes / totalMinutes) * availableWidth;
                positions.Add((DayStart + TimeSpan.FromMinutes(minutes), ClampLabelLeft(x, availableWidth, labelWidth)));
            }

            // A candidate interval that doesn't evenly divide the day range otherwise never lands
            // on DayEnd, silently dropping the last label. Append it explicitly - if it then sits
            // too close to the previous tick, HasAdjacentOverlap rejects this candidate exactly
            // like any other collision, falling through to a coarser interval instead.
            if (positions[^1].TimeOfDay != DayEnd)
            {
                positions.Add((DayEnd, ClampLabelLeft(availableWidth, availableWidth, labelWidth)));
            }

            return positions;
        }

        // Positions are generated in ascending time (=> ascending x) order and clamping is
        // monotonic, so checking each pair of neighbors catches every possible collision.
        private bool HasAdjacentOverlap(List<(TimeSpan TimeOfDay, double Left)> positions, double labelWidth)
        {
            for (var i = 1; i < positions.Count; i++)
            {
                if (positions[i].Left < positions[i - 1].Left + labelWidth + TickMinGap)
                {
                    return true;
                }
            }

            return false;
        }

        private static double ClampLabelLeft(double center, double availableWidth, double labelWidth)
        {
            return Math.Clamp(center - (labelWidth / 2), 0, Math.Max(0, availableWidth - labelWidth));
        }

        private void AddTickLabel(TimeSpan timeOfDay, double left)
        {
            var label = new TextBlock
            {
                Text = FormatTick(timeOfDay),
                Foreground = ResolveBrush(DefaultForegroundBrushKey),
                TextWrapping = TextWrapping.NoWrap,
                FontSize = 11,
            };

            Canvas.SetLeft(label, left);
            Canvas.SetTop(label, 0);
            _ticksCanvas.Children.Add(label);
        }

        private static double MeasureTickLabelWidth()
        {
            var probe = new TextBlock { Text = FormatTick(TimeSpan.FromHours(23) + TimeSpan.FromMinutes(59)), FontSize = 11, TextWrapping = TextWrapping.NoWrap };
            probe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return probe.DesiredSize.Width;
        }

        private static string FormatTick(TimeSpan timeOfDay) => DateTime.Today.Add(timeOfDay).ToString("HH:mm");

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
