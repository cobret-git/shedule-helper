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
        private const int MaxMessageLines = 3;

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
                // Outcomes/errors can run past one line - wrap, keeping the last line on
                // MessageRow so a one-line message sits exactly where it always did.
                var lines = Wrap(message, ScreenBuffer.Width, MaxMessageLines);
                for (var i = 0; i < lines.Count; i++)
                {
                    buffer.SetLine(MessageRow - (lines.Count - 1 - i), lines[i]);
                }
            }
        }

        #endregion

        #region Helpers

        // Greedy word wrap; whatever doesn't fit in maxLines is cut off (SetLine truncates a
        // single over-long word the same way).
        private static List<string> Wrap(string text, int width, int maxLines)
        {
            var lines = new List<string>();
            var line = string.Empty;

            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (candidate.Length > width && line.Length > 0)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }

            if (line.Length > 0)
            {
                lines.Add(line);
            }

            return lines.Take(maxLines).ToList();
        }

        private static string GetLabel(DatabaseAction action) => action switch
        {
            DatabaseAction.CreateBackup => "Create backup",
            DatabaseAction.RestoreFromBackup => "Restore from backup",
            _ => action.ToString()
        };

        #endregion
    }
}
