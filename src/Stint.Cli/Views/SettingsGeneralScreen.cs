using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="SettingsGeneralScreenViewModel"/>'s grouped rows and intercepts the digit
    /// and backspace keys its time mask needs. See
    /// <c>docs/update-v1.1.0/settings-render-48__general-*.txt</c>.
    /// </summary>
    public sealed class SettingsGeneralScreen : ScreenView<SettingsGeneralScreenViewModel>
    {
        #region Fields

        private const int RuleRow = 2;
        private const int FirstSectionRow = 3;
        private const int MessageRow = ScreenBuffer.Height - 4;

        private static readonly (string Heading, GeneralSettingsRow[] Rows)[] Sections =
        [
            ("CLOCK", new[] { GeneralSettingsRow.DefaultClockIn, GeneralSettingsRow.DefaultClockOut, GeneralSettingsRow.RoundNowTimes }),
            ("DAY", new[] { GeneralSettingsRow.DailyTarget })
        ];

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(SettingsGeneralScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(RuleRow, new string('-', ScreenBuffer.Width));

            var row = FirstSectionRow;
            foreach (var (heading, rows) in Sections)
            {
                buffer.SetLine(row++, $"  {heading}");

                foreach (var settingsRow in rows)
                {
                    RenderRow(buffer, row++, settingsRow, viewModel);
                }

                row++;
            }

            if (viewModel.Message is { } message)
            {
                buffer.SetLine(MessageRow, message);
                buffer.AddColorSpan(MessageRow, 0, message.Length, viewModel.IsError ? ConsoleColor.Red : ConsoleColor.Green);
            }
        }

        /// <inheritdoc />
        public override bool HandleKey(SettingsGeneralScreenViewModel viewModel, ConsoleKeyInfo key)
        {
            if (!viewModel.IsEditing || !SettingsGeneralScreenViewModel.IsTimeRow(viewModel.SelectedRow))
            {
                return false;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                viewModel.RemoveTimeDigit();
                return true;
            }

            if (char.IsAsciiDigit(key.KeyChar))
            {
                viewModel.AppendTimeDigit(key.KeyChar);
                return true;
            }

            return false;
        }

        #endregion

        #region Helpers

        private static void RenderRow(ScreenBuffer buffer, int row, GeneralSettingsRow settingsRow, SettingsGeneralScreenViewModel viewModel)
        {
            var isSelected = settingsRow == viewModel.SelectedRow;
            var label = GetLabel(settingsRow);
            var value = viewModel.GetValueText(settingsRow);
            var marker = isSelected ? "  > " : "    ";
            var line = LineFormat.Spaced(marker + label, value);

            if (isSelected && viewModel.IsEditing)
            {
                // While editing, the contrast moves from the label onto the value being edited -
                // a time's mask gets Home's cursor treatment, a list value is inverted whole.
                if (SettingsGeneralScreenViewModel.IsTimeRow(settingsRow))
                {
                    TimeMask.Render(buffer, row, line, value, viewModel.TimeCursorIndex);
                }
                else
                {
                    buffer.SetLine(row, line, line.Length - value.Length, value.Length);
                }
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

        private static string GetLabel(GeneralSettingsRow row) => row switch
        {
            GeneralSettingsRow.DefaultClockIn => "Default clock-in",
            GeneralSettingsRow.DefaultClockOut => "Default clock-out",
            GeneralSettingsRow.RoundNowTimes => "Round 'Now' times",
            GeneralSettingsRow.DailyTarget => "Daily target",
            _ => row.ToString()
        };

        #endregion
    }
}
