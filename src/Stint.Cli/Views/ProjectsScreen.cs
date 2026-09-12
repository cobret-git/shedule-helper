using Stint.Cli.Components;
using Stint.Cli.Models;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="ProjectsScreenViewModel"/>'s body content and intercepts the keys its
    /// free-form project name entry - and Ctrl+Z undo - need that no fixed <see cref="KeyHint"/>
    /// could represent.
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
            // Not a KeyHint: KeyHint dispatch matches on key.Key alone and ignores modifiers, so
            // a "Z" hint would also fire on a bare "z" - which is a perfectly ordinary character
            // to type while naming a project. Checking the Control modifier directly here, ahead
            // of the free-text interception below, is what keeps the two from colliding.
            if (key.Key == ConsoleKey.Z && key.Modifiers.HasFlag(ConsoleModifiers.Control) && viewModel.CanUndoDelete)
            {
                _ = viewModel.UndoLastDeleteAsync();
                return true;
            }

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
                RenderNameEntry(buffer, row, viewModel.NameInput, viewModel.NameStatusLabel);
            }
            else
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var isSelected = i == viewModel.SelectedIndexOnPage;

                    if (isSelected && viewModel.Mode != ProjectsMode.Idle)
                    {
                        // Creating and Editing both overlay the name-entry row in place of
                        // whatever sits at the current selection, rather than inserting/shifting
                        // the rest of the list - simplest single path for both, and matches the
                        // mockups once you read "creating" as "cursor happened to be on row 0".
                        RenderNameEntry(buffer, row, viewModel.NameInput, viewModel.NameStatusLabel);
                    }
                    else
                    {
                        RenderProjectRow(buffer, row, rows[i], isSelected);
                    }

                    row++;
                }
            }

            if (viewModel.TotalPages > 1)
            {
                buffer.SetLine(PagerRow, LineFormat.DotLeader($"  < page {viewModel.CurrentPageIndex + 1}/{viewModel.TotalPages} >", string.Empty));
            }
        }

        private static void RenderProjectRow(ScreenBuffer buffer, int row, ProjectListRow project, bool isSelected)
        {
            var marker = isSelected ? "> " : "  ";
            var line = marker + LineFormat.DotLeader(project.Name, LineFormat.FormatTaskCount(project.TaskCount), ScreenBuffer.Width - 2);

            if (isSelected)
            {
                buffer.SetLine(row, line, marker.Length, project.Name.Length);
            }
            else
            {
                buffer.SetLine(row, line);
            }
        }

        private static void RenderNameEntry(ScreenBuffer buffer, int row, string nameInput, string statusLabel)
        {
            const string marker = "> ";
            var typed = nameInput + "_";
            var line = marker + LineFormat.DotLeader(typed, statusLabel, ScreenBuffer.Width - 2);

            buffer.SetLine(row, line);
            buffer.AddColorSpan(row, marker.Length, typed.Length, ConsoleColor.Black, ConsoleColor.White);
        }

        #endregion
    }
}
