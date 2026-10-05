using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="PlanEventScreenViewModel"/>'s From/To/Note rows and the working-day total,
    /// and intercepts the character and backspace keys the note's free-form entry needs. See
    /// <c>docs/update-v1.2.0/plan-event-render-48__*.txt</c>.
    /// </summary>
    public sealed class PlanEventScreen : ScreenView<PlanEventScreenViewModel>
    {
        #region Fields

        private const int RuleRow = 2;
        private const int FirstRow = 3;
        private const int SummaryRow = 7;
        private const int MessageRow = ScreenBuffer.Height - 4;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(PlanEventScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(RuleRow, new string('-', ScreenBuffer.Width));

            var row = FirstRow;
            foreach (var eventRow in viewModel.Rows)
            {
                RenderRow(buffer, row++, eventRow, viewModel);
            }

            RenderSummary(buffer, viewModel);

            if (viewModel.Message is { } message)
            {
                buffer.SetLine(MessageRow, message);
                buffer.AddColorSpan(MessageRow, 0, Math.Min(message.Length, ScreenBuffer.Width), ConsoleColor.Red);
            }
        }

        /// <inheritdoc />
        public override bool HandleKey(PlanEventScreenViewModel viewModel, ConsoleKeyInfo key)
        {
            if (!viewModel.IsEditingNote)
            {
                return false;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                viewModel.RemoveNoteCharacter();
                return true;
            }

            if (!char.IsControl(key.KeyChar))
            {
                viewModel.AppendNoteCharacter(key.KeyChar);
                return true;
            }

            return false;
        }

        #endregion

        #region Helpers

        private static void RenderRow(ScreenBuffer buffer, int row, PlanEventRow eventRow, PlanEventScreenViewModel viewModel)
        {
            var isSelected = eventRow == viewModel.SelectedRow;
            var label = GetLabel(eventRow);
            var value = GetValueText(eventRow, viewModel);
            var marker = isSelected ? "> " : "  ";
            var line = LineFormat.Spaced(marker + label, value);

            if (isSelected && viewModel.IsEditingNote)
            {
                // While typing, the contrast moves from the label onto the value being edited.
                buffer.SetLine(row, line, line.Length - value.Length, value.Length);
            }
            else if (isSelected)
            {
                buffer.SetLine(row, line, marker.Length, label.Length);
            }
            else
            {
                buffer.SetLine(row, line);
            }
        }

        private static void RenderSummary(ScreenBuffer buffer, PlanEventScreenViewModel viewModel)
        {
            if (viewModel.WorkingDays == 0)
            {
                const string none = "  No working days in this range";
                buffer.SetLine(SummaryRow, none);
                buffer.AddColorSpan(SummaryRow, 0, none.Length, ConsoleColor.Red);
                return;
            }

            var days = viewModel.WorkingDays == 1 ? "1 working day" : $"{viewModel.WorkingDays} working days";
            buffer.SetLine(SummaryRow, $"  {days}  -  {LineFormat.FormatDuration(viewModel.Duration)}");
            buffer.SetLine(SummaryRow + 1, "  Sat and Sun are skipped automatically.");
        }

        private static string GetLabel(PlanEventRow row) => row switch
        {
            PlanEventRow.From => "From",
            PlanEventRow.To => "To",
            PlanEventRow.Note => "Note",
            _ => row.ToString()
        };

        private static string GetValueText(PlanEventRow row, PlanEventScreenViewModel viewModel) => row switch
        {
            PlanEventRow.From => FormatDate(viewModel.From),
            PlanEventRow.To => FormatDate(viewModel.To),
            _ => viewModel.IsEditingNote ? viewModel.Note + "_" : (viewModel.Note.Length == 0 ? "(optional)" : viewModel.Note)
        };

        // Mon 09-Nov, with the year added once it isn't the current one.
        private static string FormatDate(DateOnly date)
            => date.ToString(date.Year == DateTime.Now.Year ? "ddd dd-MMM" : "ddd dd-MMM-yyyy");

        #endregion
    }
}
