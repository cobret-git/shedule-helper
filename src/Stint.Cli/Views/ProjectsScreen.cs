using Stint.Cli.Components;
using Stint.Cli.Models;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="ProjectsScreenViewModel"/>'s body content and intercepts the keys its
    /// free-form project name entry needs that no fixed <see cref="KeyHint"/> could represent.
    /// </summary>
    /// <remarks>
    /// Row positions/how many rows fit are approximate for now - tune once this is actually seen
    /// running against a real console, same caveat as <see cref="HomeScreen"/>.
    /// </remarks>
    public sealed class ProjectsScreen : ScreenView<ProjectsScreenViewModel>
    {
        #region Fields

        private const int ListStartRow = 3;
        private const int PagerRow = 18;
        private const int NoProjectsMessageRow = 10;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(ProjectsScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(2, new string('-', ScreenBuffer.Width));

            if (viewModel.Projects.Count == 0 && viewModel.Mode == ProjectsMode.Idle)
            {
                buffer.SetLine(NoProjectsMessageRow, "no projects yet");
                return;
            }

            RenderList(viewModel, buffer);
        }

        /// <inheritdoc />
        public override bool HandleKey(ProjectsScreenViewModel viewModel, ConsoleKeyInfo key)
        {
            if (!viewModel.IsEditingName)
            {
                return false;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                viewModel.RemoveNameCharacter();
                return true;
            }

            if (!char.IsControl(key.KeyChar))
            {
                viewModel.AppendNameCharacter(key.KeyChar);
                return true;
            }

            return false;
        }

        #endregion

        #region Helpers

        private static void RenderList(ProjectsScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var rows = viewModel.CurrentPageProjects;
            var row = ListStartRow;

            if (rows.Count == 0)
            {
                // Only reachable while Creating with no other active projects yet - Idle with an
                // empty list is short-circuited by Render() before this is ever called, and
                // Editing always has at least the row being renamed.
                RenderNameEntry(buffer, row, viewModel.NameInput, viewModel.NameStatusLabel, viewModel.IsNameTaken);
            }
            else if (viewModel.Mode == ProjectsMode.Creating)
            {
                // Creating appends the name-entry row after the existing list instead of
                // overlaying it on whatever the cursor happened to be sitting on - wherever
                // that was, it stays put and visible while the new name is typed.
                foreach (var project in rows)
                {
                    RenderProjectRow(buffer, row, project, isSelected: false, isMarkedForDelete: false);
                    row++;
                }

                RenderNameEntry(buffer, row, viewModel.NameInput, viewModel.NameStatusLabel, viewModel.IsNameTaken);
            }
            else
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var isSelected = i == viewModel.SelectedIndexOnPage;

                    if (isSelected && viewModel.Mode == ProjectsMode.Editing)
                    {
                        // Editing overlays the name-entry row in place of the row being renamed -
                        // unlike Creating, there's an existing row this one genuinely replaces.
                        RenderNameEntry(buffer, row, viewModel.NameInput, viewModel.NameStatusLabel, viewModel.IsNameTaken);
                    }
                    else
                    {
                        RenderProjectRow(buffer, row, rows[i], isSelected, viewModel.PendingDeleteIds.Contains(rows[i].Id));
                    }

                    row++;
                }
            }

            if (viewModel.TotalPages > 1)
            {
                buffer.SetLine(PagerRow, LineFormat.DotLeader($"  < page {viewModel.CurrentPageIndex + 1}/{viewModel.TotalPages} >", string.Empty));
            }
        }

        private static void RenderProjectRow(ScreenBuffer buffer, int row, ProjectListRow project, bool isSelected, bool isMarkedForDelete)
        {
            var marker = isSelected ? "> " : "  ";
            var line = marker + LineFormat.DotLeader(project.Name, LineFormat.FormatTaskCount(project.TaskCount), ScreenBuffer.Width - 2);

            buffer.SetLine(row, line);

            // A pending delete always wins the highlight over plain selection - "this is about
            // to go away" is the more important thing to notice, and the two states are never
            // meaningfully ambiguous since the cursor is wherever it was last moved to anyway.
            if (isMarkedForDelete)
            {
                buffer.AddColorSpan(row, marker.Length, project.Name.Length, ConsoleColor.White, ConsoleColor.Red);
            }
            else if (isSelected)
            {
                buffer.AddColorSpan(row, marker.Length, project.Name.Length, ConsoleColor.Black, ConsoleColor.White);
            }
        }

        private static void RenderNameEntry(ScreenBuffer buffer, int row, string nameInput, string statusLabel, bool isNameTaken)
        {
            const string marker = "> ";
            var typed = nameInput + "_";
            var line = marker + LineFormat.DotLeader(typed, statusLabel, ScreenBuffer.Width - 2);

            buffer.SetLine(row, line);
            buffer.AddColorSpan(row, marker.Length, typed.Length, ConsoleColor.Black, ConsoleColor.White);

            if (statusLabel.Length == 0)
            {
                return;
            }

            // TAKEN/OK get their own red/green background instead of the console's own colors -
            // much easier to spot at a glance than plain text while you're mid-keystroke.
            var (foreground, background) = isNameTaken
                ? (ConsoleColor.White, ConsoleColor.Red)
                : (ConsoleColor.Black, ConsoleColor.Green);

            buffer.AddColorSpan(row, line.Length - statusLabel.Length, statusLabel.Length, foreground, background);
        }

        #endregion
    }
}
