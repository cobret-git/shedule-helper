using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="DatePickerScreenViewModel"/>'s three selector rows. See
    /// <c>docs/update-v1.2.0/date-picker-render-48__idle.txt</c>.
    /// </summary>
    public sealed class DatePickerScreen : ScreenView<DatePickerScreenViewModel>
    {
        #region Fields

        private const int FirstRow = 2;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(DatePickerScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var row = FirstRow;

            foreach (var pickerRow in viewModel.Rows)
            {
                var isSelected = pickerRow == viewModel.SelectedRow;
                var label = GetLabel(pickerRow);
                var marker = isSelected ? "> " : "  ";
                var line = LineFormat.Spaced(marker + label, viewModel.GetValueText(pickerRow));

                if (isSelected)
                {
                    // Same treatment as Home's clock picker: invert the row's own word so the
                    // selection has real contrast, not just a leading glyph.
                    buffer.SetLine(row, line, marker.Length, label.Length);
                }
                else
                {
                    buffer.SetLine(row, line);
                }

                row++;
            }
        }

        #endregion

        #region Helpers

        private static string GetLabel(DatePickerRow row) => row switch
        {
            DatePickerRow.Year => "Year",
            DatePickerRow.Month => "Month",
            DatePickerRow.Day => "Day",
            _ => row.ToString()
        };

        #endregion
    }
}
