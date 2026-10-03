using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="SettingsScreenViewModel"/>: the list of settings sub-pages under a rule,
    /// the selected one inverted. See <c>docs/update-v1.1.0/settings-render-48__idle.txt</c>.
    /// </summary>
    public sealed class SettingsScreen : ScreenView<SettingsScreenViewModel>
    {
        #region Fields

        private const int RuleRow = 2;
        private const int ListStartRow = 3;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(SettingsScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(RuleRow, new string('-', ScreenBuffer.Width));

            for (var i = 0; i < viewModel.Sections.Count; i++)
            {
                var name = viewModel.Sections[i].ToString().ToUpperInvariant();
                var isSelected = i == viewModel.SelectedIndex;
                var marker = isSelected ? "> " : "  ";

                buffer.SetLine(ListStartRow + i, marker + name);
                if (isSelected)
                {
                    buffer.AddColorSpan(ListStartRow + i, marker.Length, name.Length, ConsoleColor.Black, ConsoleColor.White);
                }
            }
        }

        #endregion
    }
}
