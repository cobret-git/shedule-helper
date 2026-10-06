using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="ClockTimePickerScreenViewModel"/>'s Now/Default/Custom rows and intercepts the
    /// keys its custom time mask needs that no fixed <see cref="KeyHint"/> could represent.
    /// </summary>
    public sealed class ClockTimePickerScreen : ScreenView<ClockTimePickerScreenViewModel>
    {
        #region Methods

        /// <inheritdoc />
        public override void Render(ClockTimePickerScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(2, $"Date: {DateTime.Now:dd-MMM-yyyy  HH:mm}");
            var isClockOut = viewModel.Action == ClockTimePickerAction.ClockOut;
            buffer.SetLine(3, isClockOut ? $"Clock-in: {viewModel.ClockInTime:HH:mm}" : "Status: not clocked in");
            buffer.SetLine(5, isClockOut ? "Clock out at:" : "Clock in at:");

            var row = 6;
            foreach (var option in viewModel.Options)
            {
                var isSelected = option == viewModel.SelectedOption;
                var marker = isSelected ? "> " : "  ";
                var isTyping = isSelected && option == ClockTimeOption.Custom && viewModel.IsEditingCustomTime;
                var preview = viewModel.GetPreview(option);
                var line = marker + LineFormat.DotLeader(option.ToString(), preview, ScreenBuffer.Width - 2);

                if (isTyping)
                {
                    // While typing, the contrast moves from the option's word onto the "--:--"
                    // mask itself - it's the value being edited now, not the selection.
                    TimeMask.Render(buffer, row, line, preview, viewModel.CustomTimeCursorIndex);
                }
                else if (isSelected)
                {
                    buffer.SetLine(row, line, marker.Length, option.ToString().Length);
                }
                else
                {
                    buffer.SetLine(row, line);
                }

                row++;
            }

            if ((viewModel.LoadError ?? viewModel.ClockError) is { } error)
            {
                var errorRow = row + 1;
                buffer.SetLine(errorRow, error);
                buffer.AddColorSpan(errorRow, 0, error.Length, ConsoleColor.Red);
            }
        }

        /// <inheritdoc />
        public override bool HandleKey(ClockTimePickerScreenViewModel viewModel, ConsoleKeyInfo key)
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
    }
}
