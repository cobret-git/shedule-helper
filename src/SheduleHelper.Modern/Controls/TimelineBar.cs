using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

namespace SheduleHelper.Modern.Controls
{
    [TemplatePart(Name = CanvasPartName, Type = typeof(Canvas))]
    [TemplatePart(Name = TicksCanvasPartName, Type = typeof(Canvas))]
    [TemplatePart(Name = HoverCanvasPartName, Type = typeof(Canvas))]
    public sealed class TimelineBar : Control
    {
        private const string CanvasPartName = "PART_Canvas";
        private const string TicksCanvasPartName = "PART_TicksCanvas";
        private const string HoverCanvasPartName = "PART_HoverCanvas";
        private const string TooltipBackgroundBrushKey = "MdInverseSurfaceBrush";
        private const string TooltipForegroundBrushKey = "MdInverseOnSurfaceBrush";

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
        private Canvas? _hoverCanvas;
        private Line? _hoverLine;
        private Border? _hoverTooltip;
        private TextBlock? _hoverTooltipText;
        private bool _cursorHidden;
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

            // Safety net: if this control is torn down while the pointer is still "inside" (e.g.
            // navigating away from the page mid-hover), make sure the system cursor comes back.
            this.Unloaded += (s, e) => SetCursorHidden(false);
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
            if (_hoverCanvas != null)
            {
                _hoverCanvas.PointerEntered -= OnHoverPointerEntered;
                _hoverCanvas.PointerMoved -= OnHoverPointerMoved;
                _hoverCanvas.PointerExited -= OnHoverPointerExited;
                _hoverCanvas.PointerCaptureLost -= OnHoverPointerCaptureLost;
            }

            // Leaving hover-hidden state behind on a template swap would be a stuck-cursor bug.
            SetCursorHidden(false);

            _canvas = GetTemplateChild(CanvasPartName) as Canvas;
            _ticksCanvas = GetTemplateChild(TicksCanvasPartName) as Canvas;
            _hoverCanvas = GetTemplateChild(HoverCanvasPartName) as Canvas;

            if (_canvas != null)
            {
                _canvas.SizeChanged += OnCanvasSizeChanged;
            }
            if (_ticksCanvas != null)
            {
                _ticksCanvas.SizeChanged += OnCanvasSizeChanged;
            }
            if (_hoverCanvas != null)
            {
                _hoverCanvas.PointerEntered += OnHoverPointerEntered;
                _hoverCanvas.PointerMoved += OnHoverPointerMoved;
                _hoverCanvas.PointerExited += OnHoverPointerExited;
                _hoverCanvas.PointerCaptureLost += OnHoverPointerCaptureLost;
                BuildHoverElements();
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

        /// <summary>
        /// Computes where <paramref name="segment"/> falls within <c>[DayStart, DayEnd]</c>, in
        /// minutes from <see cref="DayStart"/>, clamped to that range - or <c>null</c> if it's
        /// entirely outside it (so it shouldn't be drawn/hit-tested at all). An open-ended
        /// (<c>EndTime == null</c>) segment is treated as running until <see cref="DateTime.Now"/>.
        /// Shared by <see cref="DrawTimeline"/> and the hover hit-test (<see cref="FindSegmentAt"/>)
        /// so the two can never disagree about what's actually at a given x position.
        /// </summary>
        private (double StartMinutes, double EndMinutes)? GetVisibleRange(TimelineSegment segment, double totalMinutes)
        {
            var startMinutes = (segment.StartTime.TimeOfDay - DayStart).TotalMinutes;

            var endTimeToUse = segment.EndTime ?? DateTime.Now;
            var endMinutes = (endTimeToUse.TimeOfDay - DayStart).TotalMinutes;

            startMinutes = Math.Max(0, Math.Min(startMinutes, totalMinutes));
            endMinutes = Math.Max(0, Math.Min(endMinutes, totalMinutes));

            return endMinutes - startMinutes <= 0 ? null : (startMinutes, endMinutes);
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
                var range = GetVisibleRange(segment, totalMinutes);
                if (range == null) continue;

                var xPosition = (range.Value.StartMinutes / totalMinutes) * _canvas.ActualWidth;
                var width = ((range.Value.EndMinutes - range.Value.StartMinutes) / totalMinutes) * _canvas.ActualWidth;

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

        #endregion

        #region Hover Inspector

        /// <summary>
        /// Creates the hover crosshair line and tooltip pill once per template application and
        /// adds them to <see cref="_hoverCanvas"/>, initially hidden. Unlike segments/ticks, these
        /// are never rebuilt by <see cref="DrawTimeline"/> - only repositioned/re-texted on pointer
        /// events - so hovering never triggers a full redraw (see the plan's rationale).
        /// </summary>
        private void BuildHoverElements()
        {
            if (_hoverCanvas == null) return;

            _hoverCanvas.Children.Clear();

            _hoverLine = new Line
            {
                StrokeThickness = 2,
                Visibility = Visibility.Collapsed,
            };

            _hoverTooltipText = new TextBlock
            {
                FontSize = 12,
                TextWrapping = TextWrapping.NoWrap,
            };

            _hoverTooltip = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 4, 8, 4),
                Child = _hoverTooltipText,
                Visibility = Visibility.Collapsed,
            };

            _hoverCanvas.Children.Add(_hoverLine);
            _hoverCanvas.Children.Add(_hoverTooltip);
        }

        // Re-resolved at the start of each hover session (not every PointerMoved) rather than
        // cached once, so a theme swap that happens between hovers is picked up correctly - the
        // hover overlay isn't touched by DrawTimeline's own theme-change redraw.
        private void UpdateHoverBrushes()
        {
            if (_hoverLine != null) _hoverLine.Stroke = IndicatorBrush;
            if (_hoverTooltip != null) _hoverTooltip.Background = ResolveBrush(TooltipBackgroundBrushKey);
            if (_hoverTooltipText != null) _hoverTooltipText.Foreground = ResolveBrush(TooltipForegroundBrushKey);
        }

        /// <summary>
        /// Hides or restores the OS mouse cursor via the Win32 <c>ShowCursor</c> counter (see the
        /// Native Methods region). Guarded by <see cref="_cursorHidden"/> so hide/show calls are
        /// always paired 1:1 - <c>ShowCursor</c>'s counter would otherwise drift if, say, two
        /// PointerEntered events fired without a PointerExited in between.
        /// </summary>
        private void SetCursorHidden(bool hidden)
        {
            if (hidden == _cursorHidden) return;

            ShowCursor(!hidden);
            _cursorHidden = hidden;
        }

        private void OnHoverPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            SetCursorHidden(true);
            UpdateHoverBrushes();
            UpdateHoverIndicator(e);
        }

        private void OnHoverPointerMoved(object sender, PointerRoutedEventArgs e)
        {
            UpdateHoverIndicator(e);
        }

        private void OnHoverPointerExited(object sender, PointerRoutedEventArgs e)
        {
            SetCursorHidden(false);
            HideHoverIndicator();
        }

        private void OnHoverPointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            SetCursorHidden(false);
            HideHoverIndicator();
        }

        private void HideHoverIndicator()
        {
            if (_hoverLine != null) _hoverLine.Visibility = Visibility.Collapsed;
            if (_hoverTooltip != null) _hoverTooltip.Visibility = Visibility.Collapsed;
        }

        private void UpdateHoverIndicator(PointerRoutedEventArgs e)
        {
            if (_hoverCanvas == null || _hoverLine == null || _hoverTooltip == null || ItemsSource == null) return;

            var totalMinutes = (DayEnd - DayStart).TotalMinutes;
            if (totalMinutes <= 0) return;

            var canvasWidth = _hoverCanvas.ActualWidth;
            var canvasHeight = _hoverCanvas.ActualHeight;
            if (canvasWidth <= 0) return;

            var x = Math.Clamp(e.GetCurrentPoint(_hoverCanvas).Position.X, 0, canvasWidth);
            var timeOfDay = DayStart + TimeSpan.FromMinutes((x / canvasWidth) * totalMinutes);

            var segment = FindSegmentAt(timeOfDay, totalMinutes);

            _hoverLine.X1 = x;
            _hoverLine.X2 = x;
            _hoverLine.Y1 = 0;
            _hoverLine.Y2 = canvasHeight;
            _hoverLine.Visibility = Visibility.Visible;

            _hoverTooltipText.Text = segment == null
                ? FormatTick(timeOfDay)
                : $"{FormatTick(timeOfDay)} • {segment.Value.Name}";

            _hoverTooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var tooltipWidth = _hoverTooltip.DesiredSize.Width;
            var left = Math.Clamp(x - (tooltipWidth / 2), 0, Math.Max(0, canvasWidth - tooltipWidth));

            Canvas.SetLeft(_hoverTooltip, left);
            Canvas.SetTop(_hoverTooltip, 4);
            _hoverTooltip.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Finds which segment (if any) covers <paramref name="timeOfDay"/>, using the same
        /// clamped bounds <see cref="DrawTimeline"/> draws with (via <see cref="GetVisibleRange"/>)
        /// so the tooltip can never disagree with what's visually under the cursor. Segments are
        /// scanned in the same ascending-start-time order they're drawn in, keeping the last match
        /// so ties resolve to whichever segment paints on top.
        /// </summary>
        private TimelineSegment? FindSegmentAt(TimeSpan timeOfDay, double totalMinutes)
        {
            if (ItemsSource == null) return null;

            var minutes = (timeOfDay - DayStart).TotalMinutes;
            TimelineSegment? match = null;

            foreach (var segment in ItemsSource.OrderBy(s => s.StartTime))
            {
                var range = GetVisibleRange(segment, totalMinutes);
                if (range == null) continue;

                if (minutes >= range.Value.StartMinutes && minutes <= range.Value.EndMinutes)
                {
                    match = segment;
                }
            }

            return match;
        }

        #endregion

        #region Rendering Logic

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

        #region Native Methods

        // Only the plain Win32 show/hide is reliable for this on WinUI 3 desktop apps - see the
        // "Hiding the system cursor while hovering" section of the plan for why the higher-level
        // ProtectedCursor/InputSystemCursor API doesn't have a "hidden" option.
        [DllImport("user32.dll")]
        private static extern int ShowCursor(bool show);

        #endregion
    }
}
