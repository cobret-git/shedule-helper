using Stint.Cli.Components;
using Stint.Cli.Models;
using Stint.Cli.ViewModels;
using Stint.Core;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="PlanScreenViewModel"/>'s two sections of planned events (upcoming, then
    /// earlier this month). See <c>docs/update-v1.2.0/plan-render-48__*.txt</c>.
    /// </summary>
    /// <remarks>
    /// The list scrolls instead of paging: the section headings and the blank separator are laid
    /// out as display lines together with the events, and the window of lines shown follows the
    /// highlighted event.
    /// </remarks>
    public sealed class PlanScreen : ScreenView<PlanScreenViewModel>
    {
        #region Fields

        private const int RuleRow = 2;
        private const int ListStartRow = 3;
        private const int ListEndRow = ScreenBuffer.Height - 5;
        private const int EmptyMessageRow = 10;
        private const int ErrorRow = ScreenBuffer.Height - 4;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(PlanScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(RuleRow, new string('-', ScreenBuffer.Width));

            if (viewModel.Rows.Count == 0)
            {
                buffer.SetLine(EmptyMessageRow, "nothing planned yet");
            }
            else
            {
                RenderList(viewModel, buffer);
            }

            if (viewModel.LoadError is { } error)
            {
                buffer.SetLine(ErrorRow, error);
                buffer.AddColorSpan(ErrorRow, 0, Math.Min(error.Length, ScreenBuffer.Width), ConsoleColor.Red);
            }
        }

        #endregion

        #region Helpers

        private static void RenderList(PlanScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var lines = BuildLines(viewModel.Rows);
            var capacity = ListEndRow - ListStartRow + 1;

            // Scroll just far enough to keep the highlighted event on screen.
            var selectedLine = lines.FindIndex(l => l.RowIndex == viewModel.SelectedIndex);
            var start = Math.Clamp(selectedLine - capacity + 1, 0, Math.Max(0, lines.Count - capacity));

            var today = DateOnly.FromDateTime(DateTime.Now);
            var row = ListStartRow;

            foreach (var line in lines.Skip(start).Take(capacity))
            {
                if (line.RowIndex < 0)
                {
                    buffer.SetLine(row, line.Heading);
                }
                else
                {
                    var isSelected = line.RowIndex == viewModel.SelectedIndex;
                    var isMarkedForDelete = isSelected && viewModel.Mode == PlanMode.ConfirmingDelete;
                    RenderEventRow(buffer, row, viewModel.Rows[line.RowIndex], today, isSelected, isMarkedForDelete);
                }

                row++;
            }
        }

        // Headings and the blank row between sections are display lines of their own (RowIndex -1);
        // an event line carries its index into the view model's Rows.
        private static List<(string Heading, int RowIndex)> BuildLines(IReadOnlyList<PlanListRow> rows)
        {
            var lines = new List<(string Heading, int RowIndex)>();

            for (var i = 0; i < rows.Count; i++)
            {
                if (i == 0 || rows[i].IsUpcoming != rows[i - 1].IsUpcoming)
                {
                    if (i > 0)
                    {
                        lines.Add((string.Empty, -1));
                    }

                    lines.Add((rows[i].IsUpcoming ? "  UPCOMING" : "  EARLIER THIS MONTH", -1));
                }

                lines.Add((string.Empty, i));
            }

            return lines;
        }

        private static void RenderEventRow(ScreenBuffer buffer, int row, PlanListRow planRow, DateOnly today, bool isSelected, bool isMarkedForDelete)
        {
            var marker = isSelected ? "> " : "  ";
            var right = $"{planRow.WorkingDays}d / {LineFormat.FormatDuration(planRow.Duration)}";
            var left = $"{FormatRange(planRow.Event, today)}  {DayTypeNames.GetName(planRow.Event.DayType)}";

            // DotLeader needs the left text plus one dot and two spaces to fit beside the value -
            // trim a long kind name rather than overflow the row.
            var maxLeft = ScreenBuffer.Width - marker.Length - right.Length - 3;
            if (left.Length > maxLeft)
            {
                left = left[..maxLeft];
            }

            buffer.SetLine(row, marker + LineFormat.DotLeader(left, right, ScreenBuffer.Width - marker.Length));

            // A pending delete wins the highlight over plain selection - "this is about to go away"
            // is the more important thing to notice.
            if (isMarkedForDelete)
            {
                buffer.AddColorSpan(row, marker.Length, left.Length, ConsoleColor.White, ConsoleColor.Red);
            }
            else if (isSelected)
            {
                buffer.AddColorSpan(row, marker.Length, left.Length, ConsoleColor.Black, ConsoleColor.White);
            }
        }

        // 09-Nov..13-Nov, or just 17-Nov for a single day; a two-digit year is added to both ends
        // once either end isn't in the current year, so a range across New Year stays unambiguous.
        private static string FormatRange(PlannedEvent plannedEvent, DateOnly today)
        {
            var format = plannedEvent.StartDate.Year == today.Year && plannedEvent.EndDate.Year == today.Year ? "dd-MMM" : "dd-MMM-yy";

            return plannedEvent.StartDate == plannedEvent.EndDate
                ? plannedEvent.StartDate.ToString(format)
                : $"{plannedEvent.StartDate.ToString(format)}..{plannedEvent.EndDate.ToString(format)}";
        }

        #endregion
    }
}
