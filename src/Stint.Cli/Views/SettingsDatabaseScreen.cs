using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="SettingsDatabaseScreenViewModel"/>: the file size and the two action rows.
    /// </summary>
    public sealed class SettingsDatabaseScreen : ScreenView<SettingsDatabaseScreenViewModel>
    {
        #region Fields

        private const int RuleRow = 2;
        private const int FileHeadingRow = 3;
        private const int SizeRow = 4;
        private const int ActionsHeadingRow = 6;
        private const int ActionsStartRow = 7;
        private const int MessageRow = ScreenBuffer.Height - 4;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(SettingsDatabaseScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(RuleRow, new string('-', ScreenBuffer.Width));

            buffer.SetLine(FileHeadingRow, "  FILE");
            buffer.SetLine(SizeRow, LineFormat.Spaced("    Size", viewModel.FileSizeText));

            buffer.SetLine(ActionsHeadingRow, "  ACTIONS");
            for (var i = 0; i < viewModel.Actions.Count; i++)
            {
                var label = GetLabel(viewModel.Actions[i]);
                var isSelected = i == viewModel.SelectedIndex;
                var marker = isSelected ? "  > " : "    ";

                buffer.SetLine(ActionsStartRow + i, marker + label);
                if (isSelected)
                {
                    buffer.AddColorSpan(ActionsStartRow + i, marker.Length, label.Length, ConsoleColor.Black, ConsoleColor.White);
                }
            }

            if (viewModel.Message is { } message)
            {
                buffer.SetLine(MessageRow, message);
            }
        }

        #endregion

        #region Helpers

        private static string GetLabel(DatabaseAction action) => action switch
        {
            DatabaseAction.CreateBackup => "Create backup",
            DatabaseAction.RestoreFromBackup => "Restore from backup",
            _ => action.ToString()
        };

        #endregion
    }
}
