using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// The Reports screen (The Evaluator) - worked/target/balance and a project or task breakdown
    /// for the week/month/quarter/year around a reference date. Month renders as a calendar grid;
    /// the other three zoom levels render as a sparkline, since the bucket count (day/week/month)
    /// changes with zoom so the chart never outgrows the terminal width regardless of period length.
    /// Day-level retro-editing and CSV export are not part of this screen yet.
    /// </summary>
    public sealed class ReportsScreen : IScreen
    {
        #region Fields

        private static readonly ReportZoom[] ZoomOrder = { ReportZoom.Week, ReportZoom.Month, ReportZoom.Quarter, ReportZoom.Year };

        private readonly IReportingService _reportingService;
        private readonly ICurrentUserContext _currentUserContext;
        private readonly ILogger _logger = Log.ForContext<ReportsScreen>();

        private ReportZoom _zoom = ReportZoom.Month;
        private DateTime _referenceDate = DateTime.Today;
        private PeriodReport? _report;
        private bool _showByTask;
        private string? _message;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ReportsScreen"/> class.
        /// </summary>
        public ReportsScreen(IReportingService reportingService, ICurrentUserContext currentUserContext)
        {
            _reportingService = reportingService;
            _currentUserContext = currentUserContext;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public Task OnEnter() => LoadAsync();

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, "REPORTS", "Esc Back");

            frame.Write(1, 3, ZoomSelectorText(_zoom));

            if (_report is null)
            {
                frame.Write(1, 6, _message ?? "Loading...", ColorToken.Dim);
                return;
            }

            var report = _report;
            frame.WriteRight(frame.Width - 1, 3, report.PeriodLabel, ColorToken.Dim);
            frame.Rule(4);

            var balance = report.TotalWorked - report.TotalTarget;
            frame.Write(1, 6, $"Worked {Formatting.Duration(report.TotalWorked)}");
            frame.Write(20, 6, $"Target {Formatting.Duration(report.TotalTarget)}");
            frame.Write(39, 6, $"Balance {Formatting.Balance(balance)}", balance >= TimeSpan.Zero ? ColorToken.Positive : ColorToken.Negative);

            frame.Write(1, 7, SummaryLine(report), ColorToken.Dim);

            if (_zoom == ReportZoom.Month)
            {
                CalendarGrid.Draw(frame, 1, 9, report.Buckets);
            }
            else
            {
                var values = report.Buckets.Select(b => b.Worked.TotalMinutes).ToList();
                Sparkline.Draw(frame, 1, 9, values, ColorToken.Accent);

                var x = 1;
                foreach (var bucket in report.Buckets)
                {
                    frame.Write(x, 10, bucket.Label, ColorToken.Dim);
                    x += bucket.Label.Length + 1;
                }
            }

            var breakdownHeaderRow = _zoom == ReportZoom.Month ? 17 : 12;
            DrawBreakdown(frame, breakdownHeaderRow, report);

            if (!string.IsNullOrWhiteSpace(_message))
            {
                frame.Write(1, frame.Height - 4, _message, ColorToken.Negative);
            }

            KeyBar.Draw(frame, ("left/right", "Period"), ("up/down", "Zoom"), ("T", "By task/project"), ("Esc", "Back"));
        }

        /// <inheritdoc/>
        public async Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    await screens.Pop();
                    return;
                case ConsoleKey.LeftArrow:
                    ShiftPeriod(forward: false);
                    await LoadAsync();
                    return;
                case ConsoleKey.RightArrow:
                    ShiftPeriod(forward: true);
                    await LoadAsync();
                    return;
                case ConsoleKey.UpArrow:
                    CycleZoom(forward: false);
                    await LoadAsync();
                    return;
                case ConsoleKey.DownArrow:
                    CycleZoom(forward: true);
                    await LoadAsync();
                    return;
                case ConsoleKey.T:
                    _showByTask = !_showByTask;
                    return;
            }
        }

        #endregion

        #region Helpers

        private static string ZoomSelectorText(ReportZoom zoom)
        {
            string Part(ReportZoom z, string label) => z == zoom ? $"[ {label} ]" : label;
            return $"‹ {Part(ReportZoom.Week, "Week")} · {Part(ReportZoom.Month, "Month")} · {Part(ReportZoom.Quarter, "Quarter")} · {Part(ReportZoom.Year, "Year")} ›";
        }

        private static string SummaryLine(PeriodReport report)
        {
            var unit = report.BucketUnitLabel;
            var summary = $"{report.LoggedBucketCount} of {report.Buckets.Count} {unit}s logged";

            if (report.LoggedBucketCount == 0)
            {
                return summary;
            }

            var average = report.TotalWorked / report.LoggedBucketCount;
            var best = report.Buckets.Where(b => b.HasData).OrderByDescending(b => b.Worked).First();
            return $"{summary}    avg {Formatting.Duration(average)}/{unit}    best {best.Label} {Formatting.Duration(best.Worked)}";
        }

        private void DrawBreakdown(Frame frame, int headerRow, PeriodReport report)
        {
            var breakdown = _showByTask ? report.TaskBreakdown : report.ProjectBreakdown;
            frame.Write(1, headerRow, _showByTask ? "By task" : "By project", ColorToken.Accent);

            if (breakdown.Count == 0)
            {
                frame.Write(1, headerRow + 1, "No tracked time in this period.", ColorToken.Dim);
                return;
            }

            var total = breakdown.Aggregate(TimeSpan.Zero, (sum, entry) => sum + entry.Time);

            for (var i = 0; i < breakdown.Count; i++)
            {
                var entry = breakdown[i];
                var ratio = total > TimeSpan.Zero ? entry.Time.TotalSeconds / total.TotalSeconds : 0;
                var percentage = (int)Math.Round(ratio * 100);

                frame.Write(1, headerRow + 1 + i, Truncate(entry.Name, 20).PadRight(20));
                ProgressBar.Draw(frame, 22, headerRow + 1 + i, 30, ratio, $"{Formatting.Duration(entry.Time)}  {percentage}%");
            }
        }

        private void CycleZoom(bool forward)
        {
            var index = Array.IndexOf(ZoomOrder, _zoom);
            var nextIndex = forward ? (index + 1) % ZoomOrder.Length : (index - 1 + ZoomOrder.Length) % ZoomOrder.Length;
            _zoom = ZoomOrder[nextIndex];
        }

        private void ShiftPeriod(bool forward)
        {
            var delta = forward ? 1 : -1;
            _referenceDate = _zoom switch
            {
                ReportZoom.Week => _referenceDate.AddDays(7 * delta),
                ReportZoom.Month => _referenceDate.AddMonths(delta),
                ReportZoom.Quarter => _referenceDate.AddMonths(3 * delta),
                ReportZoom.Year => _referenceDate.AddYears(delta),
                _ => _referenceDate,
            };
        }

        private static string Truncate(string value, int maxLength)
        {
            return value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
        }

        private async Task LoadAsync()
        {
            try
            {
                _report = await _reportingService.GetReportAsync(_currentUserContext.UserId, _zoom, _referenceDate, CancellationToken.None);
                _message = null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load report for user {UserId}.", _currentUserContext.UserId);
                _message = "Failed to load report.";
            }
        }

        #endregion
    }
}
