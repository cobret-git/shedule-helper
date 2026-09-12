using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="HomeScreenViewModel"/>'s body content and intercepts the keys its
    /// custom clock-in time mask needs that no fixed <see cref="KeyHint"/> could represent.
    /// </summary>
    /// <remarks>
    /// Row positions/how many project rows fit are approximate for now - tune once this is
    /// actually seen running against a real console. The overtime bar segment isn't drawn in a
    /// distinct color yet either - <see cref="ScreenBuffer"/> only carries plain text today.
    /// </remarks>
    public sealed class HomeScreen : ScreenView<HomeScreenViewModel>
    {
        #region Fields

        // TODO: tune both of these once this is actually seen rendered - they're guesses at
        // how much vertical space the 23-line grid leaves for the project/task tree.
        private const int MaxVisibleProjects = 3;
        private const int NoProjectsMessageRow = 14;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            if (viewModel.State == HomeState.NotClockedIn)
            {
                RenderClockInPicker(viewModel, buffer);
            }
            else
            {
                RenderShift(viewModel, buffer);
            }
        }

        /// <inheritdoc />
        public override bool HandleKey(HomeScreenViewModel viewModel, ConsoleKeyInfo key)
        {
            if (!viewModel.IsEditingCustomTime)
            {
                return false;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                viewModel.RemoveCustomTimeDigit();
                return true;
            }

            if (char.IsAsciiDigit(key.KeyChar))
            {
                viewModel.AppendCustomTimeDigit(key.KeyChar);
                return true;
            }

            return false;
        }

        #endregion

        #region Helpers

        private static void RenderClockInPicker(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(2, $"Date: {DateTime.Now:dd-MMM-yyyy  HH:mm}");
            buffer.SetLine(3, "Status: not clocked in");
            buffer.SetLine(5, "Clock in at:");

            var row = 6;
            foreach (var option in viewModel.ClockInOptions)
            {
                var isSelected = option == viewModel.SelectedClockInOption;
                var marker = isSelected ? "> " : "  ";
                var isTyping = isSelected && option == ClockInOption.Custom && viewModel.IsEditingCustomTime;
                var label = option.ToString() + (isTyping ? "_" : string.Empty);

                buffer.SetLine(row, marker + LineFormat.DotLeader(label, viewModel.GetClockInPreview(option), ScreenBuffer.Width - 2));
                row++;
            }
        }

        private static void RenderShift(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(2, $"Date: {DateTime.Now:dd-MMM-yyyy  HH:mm}");
            buffer.SetLine(3, viewModel.State == HomeState.ClockedIn
                ? $"Clock-in: {viewModel.ClockInTime:HH:mm}"
                : $"Shift: {viewModel.ClockInTime:HH:mm} - {viewModel.ClockOutTime:HH:mm}");

            RenderProgress(viewModel, buffer);
            RenderProjectTree(viewModel, buffer);

            buffer.SetLine(ScreenBuffer.Height - 3, new string('-', ScreenBuffer.Width));
            buffer.SetLine(ScreenBuffer.Height - 2, LineFormat.DotLeader("Balance:", LineFormat.FormatBalance(viewModel.Balance)));
        }

        private static void RenderProgress(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var elapsedOverTarget = $"{LineFormat.FormatDuration(viewModel.Elapsed)} / {LineFormat.FormatDuration(viewModel.Target)}";
            buffer.SetLine(5, LineFormat.DotLeader(elapsedOverTarget, $"{viewModel.PercentComplete}%"));
            buffer.SetLine(6, new string('*', ScreenBuffer.Width));

            var barWidth = ScreenBuffer.Width - 2;
            var normalCells = (int)Math.Round(viewModel.NormalFillFraction * barWidth);
            var overtimeCells = (int)Math.Round(viewModel.OvertimeFillFraction * barWidth);
            var emptyCells = Math.Max(0, barWidth - normalCells - overtimeCells);

            // TODO: render the overtime segment in a distinct color once ScreenBuffer supports
            // per-segment color - both segments use the same fill character for now.
            buffer.SetLine(7, $"[{new string('#', normalCells)}{new string('#', overtimeCells)}{new string('.', emptyCells)}]");
        }

        private static void RenderProjectTree(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var isClockedIn = viewModel.State == HomeState.ClockedIn;
            var rows = isClockedIn ? viewModel.ProjectRows : viewModel.CurrentPageProjectRows;

            if (rows.Count == 0)
            {
                buffer.SetLine(NoProjectsMessageRow, isClockedIn ? "no projects yet" : "no projects tracked today");
                return;
            }

            var row = 9;
            var visibleCount = Math.Min(rows.Count, MaxVisibleProjects);

            for (var i = 0; i < visibleCount; i++)
            {
                var project = rows[i];
                var marker = project.IsActive ? "+" : "-";
                var value = project.IsActive ? "ACTIVE" : LineFormat.FormatDuration(project.Duration);
                buffer.SetLine(row++, LineFormat.DotLeader($"{marker} {project.Name}", value));

                foreach (var task in project.Tasks)
                {
                    var taskValue = task.IsActive ? "ACTIVE" : LineFormat.FormatDuration(task.Duration);
                    buffer.SetLine(row++, LineFormat.DotLeader($"    {task.Name}", taskValue));
                }

                row++;
            }

            if (isClockedIn && rows.Count > visibleCount)
            {
                var remaining = rows.Skip(visibleCount).ToList();
                var remainingDuration = remaining.Aggregate(TimeSpan.Zero, static (sum, p) => sum + p.Duration);
                buffer.SetLine(row, LineFormat.DotLeader($"... {remaining.Count} more", LineFormat.FormatDuration(remainingDuration)));
            }
            else if (!isClockedIn && viewModel.TotalPages > 1)
            {
                buffer.SetLine(row, $"  < page {viewModel.CurrentPageIndex + 1}/{viewModel.TotalPages} >");
            }
        }

        #endregion
    }
}
