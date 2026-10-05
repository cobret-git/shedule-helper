using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="MenuScreenViewModel"/>: the app's sections under a rule, laid out like the
    /// Settings root screen - the selected one inverted, the ones that can't be picked dimmed.
    /// </summary>
    public sealed class MenuScreen : ScreenView<MenuScreenViewModel>
    {
        #region Fields

        private const int RuleRow = 2;
        private const int ListStartRow = 3;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(MenuScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(RuleRow, new string('-', ScreenBuffer.Width));

            for (var i = 0; i < viewModel.Destinations.Count; i++)
            {
                var destination = viewModel.Destinations[i];
                var name = destination.ToString().ToUpperInvariant();
                var isSelected = i == viewModel.SelectedIndex;
                var marker = isSelected ? "> " : "  ";

                buffer.SetLine(ListStartRow + i, marker + name);

                if (!viewModel.IsEnabled(destination))
                {
                    buffer.AddColorSpan(ListStartRow + i, marker.Length, name.Length, ConsoleColor.DarkGray);
                }
                else if (isSelected)
                {
                    buffer.AddColorSpan(ListStartRow + i, marker.Length, name.Length, ConsoleColor.Black, ConsoleColor.White);
                }
            }
        }

        #endregion
    }
}
