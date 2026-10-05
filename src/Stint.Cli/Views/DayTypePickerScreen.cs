using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="DayTypePickerScreenViewModel"/>'s list of kinds. See
    /// <c>docs/update-v1.2.0/plan-new-render-48__idle.txt</c>.
    /// </summary>
    public sealed class DayTypePickerScreen : ScreenView<DayTypePickerScreenViewModel>
    {
        #region Fields

        private const int RuleRow = 2;
        private const int FirstRow = 3;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(DayTypePickerScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(RuleRow, new string('-', ScreenBuffer.Width));

            for (var i = 0; i < viewModel.Options.Count; i++)
            {
                var isSelected = i == viewModel.SelectedIndex;
                var name = DayTypeNames.GetName(viewModel.Options[i]);
                var marker = isSelected ? "> " : "  ";

                // Same treatment as Home's clock picker: invert the row's own word so the
                // selection has real contrast, not just a leading glyph.
                if (isSelected)
                {
                    buffer.SetLine(FirstRow + i, marker + name, marker.Length, name.Length);
                }
                else
                {
                    buffer.SetLine(FirstRow + i, marker + name);
                }
            }
        }

        #endregion
    }
}
