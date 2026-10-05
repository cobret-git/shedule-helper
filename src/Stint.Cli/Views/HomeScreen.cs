using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="HomeScreenViewModel"/>'s body content and intercepts the keys its
    /// custom clock-in/clock-out time mask needs that no fixed <see cref="KeyHint"/> could represent.
    /// </summary>
    /// <remarks>
    /// Row positions/how many project rows fit are approximate for now - tune once this is
    /// actually seen running against a real console.
    /// </remarks>
    public sealed class HomeScreen : ScreenView<HomeScreenViewModel>
    {
        #region Fields

        // TODO: tune both of these once this is actually seen rendered - they're guesses at
        // how much vertical space the 23-line grid leaves for the project/task tree.
        private const int MaxVisibleProjects = 3;
        private const int NoProjectsMessageRow = 14;

        // The away block takes the first rows of the tree's space (two lines and a blank one), so
        // while away the tree starts three rows lower and shows one project fewer.
        private const int AwayRow = 9;
        private const int DefaultTreeRow = 9;
        private const int TreeRowWhileAway = 12;
        private const int MaxVisibleProjectsWhileAway = 2;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            if (viewModel.IsPickerOpen)
            {
                RenderClockPicker(viewModel, buffer);
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

        // One picker for both directions: clock-in while not clocked in, clock-out (over the live
        // shift's own "Clock-in:" line) while clocking out.
        private static void RenderClockPicker(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var isClockingOut = viewModel.State == HomeState.ClockingOut;
            var isReturning = viewModel.State == HomeState.Returning;

            buffer.SetLine(2, $"Date: {DateTime.Now:dd-MMM-yyyy  HH:mm}");
            buffer.SetLine(3, isClockingOut || isReturning ? $"Clock-in: {viewModel.ClockInTime:HH:mm}" : "Status: not clocked in");
            buffer.SetLine(5, isReturning
                ? $"Back from {viewModel.AwayName.ToUpperInvariant()} at:"
                : isClockingOut ? "Clock out at:" : "Clock in at:");

            var row = 6;
            foreach (var option in viewModel.ClockOptions)
            {
                var isSelected = option == viewModel.SelectedClockOption;
                var marker = isSelected ? "> " : "  ";
                var isTyping = isSelected && option == ClockTimeOption.Custom && viewModel.IsEditingCustomTime;
                var preview = viewModel.GetClockPreview(option);
                var line = marker + LineFormat.DotLeader(option.ToString(), preview, ScreenBuffer.Width - 2);

                if (isTyping)
                {
                    // While typing, the contrast moves from the option's word onto the "--:--"
                    // mask itself - it's the value being edited now, not the selection. The next
                    // digit's own column is left un-inverted so it reads as a cursor sitting
                    // inside the otherwise-highlighted mask.
                    TimeMask.Render(buffer, row, line, preview, viewModel.CustomTimeCursorIndex);
                }
                else if (isSelected)
                {
                    // The "> " marker alone is easy to miss - also invert the option's own word so
                    // the selected row has real contrast, not just a leading glyph.
                    buffer.SetLine(row, line, marker.Length, option.ToString().Length);
                }
                else
                {
                    buffer.SetLine(row, line);
                }

                row++;
            }

            if (viewModel.ClockError is { } error)
            {
                // A refused clock-out time (see HomeScreenViewModel.ClockError) - one line under
                // the options, in red, until the next key changes anything.
                var errorRow = row + 1;
                buffer.SetLine(errorRow, error);
                buffer.AddColorSpan(errorRow, 0, error.Length, ConsoleColor.Red);
            }

            if (isReturning)
            {
                // What the answer is measured against, and what happens once it's given.
                var infoRow = row + 3;
                buffer.SetLine(infoRow, $"Event started {viewModel.AwayStart:HH:mm}. Can't be in the future.");

                if (viewModel.PausedLabel is { } paused)
                {
                    buffer.SetLine(infoRow + 1, $"{paused} resumes then.");
                }
            }
        }

        private static void RenderShift(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(2, $"Date: {DateTime.Now:dd-MMM-yyyy  HH:mm}");
            buffer.SetLine(3, viewModel.State == HomeState.ClockedIn
                ? $"Clock-in: {viewModel.ClockInTime:HH:mm}"
                : $"Shift: {viewModel.ClockInTime:HH:mm} - {viewModel.ClockOutTime:HH:mm}");

            RenderProgress(viewModel, buffer);
            RenderAway(viewModel, buffer);
            RenderProjectTree(viewModel, buffer);

            // No separator of Home's own here - the pipeline already draws one rule above the
            // footer (Height-2) for every screen, so a second dashed line right before Balance
            // would just be a redundant, Home-specific extra. Height-3 is deliberately left
            // untouched (Clear() already blanked it) so exactly one blank row separates Balance
            // from that rule, per the mockup.
            buffer.SetLine(ScreenBuffer.Height - 4, LineFormat.DotLeader("Balance:", LineFormat.FormatBalance(viewModel.Balance)));
        }

        private static void RenderProgress(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var elapsedOverTarget = $"{LineFormat.FormatDuration(viewModel.Elapsed)} / {LineFormat.FormatDuration(viewModel.Target)}";
            buffer.SetLine(5, LineFormat.DotLeader(elapsedOverTarget, $"{viewModel.PercentComplete}%"));
            buffer.SetLine(6, new string('*', ScreenBuffer.Width));

            const int barRow = 7;
            var barWidth = ScreenBuffer.Width;
            var normalCells = (int)Math.Round(viewModel.NormalFillFraction * barWidth);
            var overtimeCells = (int)Math.Round(viewModel.OvertimeFillFraction * barWidth);
            var emptyCells = Math.Max(0, barWidth - normalCells - overtimeCells);

            // Solid block for filled cells, light shade for empty ones - same glyphs/segment
            // split as the previous CLI's ProgressBar widget - with the overtime portion picked
            // out in its own color instead of blending into the rest of the fill.
            buffer.SetLine(barRow, $"{new string('█', normalCells)}{new string('█', overtimeCells)}{new string('░', emptyCells)}");
            buffer.AddColorSpan(barRow, 0, normalCells, ConsoleColor.Cyan);
            buffer.AddColorSpan(barRow, normalCells, overtimeCells, ConsoleColor.Yellow);
            buffer.AddColorSpan(barRow, normalCells + overtimeCells, emptyCells, ConsoleColor.DarkGray);
        }

        // While away: what the user is away for and since when, and the project/task that resumes on
        // return, in a block of its own above the tree (which makes room, see RenderProjectTree).
        private static void RenderAway(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            if (!viewModel.IsAway)
            {
                return;
            }

            var label = $"AWAY: {viewModel.AwayName.ToUpperInvariant()}";
            var value = $"since {viewModel.AwayStart:HH:mm}";
            buffer.SetLine(AwayRow, LineFormat.DotLeader(label, value));
            buffer.AddColorSpan(AwayRow, 0, label.Length, ConsoleColor.Black, ConsoleColor.Yellow);

            if (viewModel.PausedLabel is { } paused)
            {
                buffer.SetLine(AwayRow + 1, $"  Paused: {paused}");
            }
        }

        private static void RenderProjectTree(HomeScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var isClockedIn = viewModel.State == HomeState.ClockedIn;
            var rows = isClockedIn ? viewModel.ProjectRows : viewModel.CurrentPageProjectRows;
            var startRow = viewModel.IsAway ? TreeRowWhileAway : DefaultTreeRow;
            var maxVisibleProjects = viewModel.IsAway ? MaxVisibleProjectsWhileAway : MaxVisibleProjects;

            if (rows.Count == 0)
            {
                buffer.SetLine(NoProjectsMessageRow, isClockedIn ? "no projects yet" : "no projects tracked today");
                return;
            }

            var row = startRow;
            var visibleCount = Math.Min(rows.Count, maxVisibleProjects);

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
