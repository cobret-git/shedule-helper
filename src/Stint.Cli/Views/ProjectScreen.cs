using Stint.Cli.Components;
using Stint.Cli.ViewModels;
using Stint.Core;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="ProjectScreenViewModel"/>'s body content and intercepts the keys its
    /// free-form task title entry needs that no fixed <see cref="KeyHint"/> could represent.
    /// </summary>
    /// <remarks>
    /// Row positions/how many rows fit are approximate for now, same caveat as
    /// <see cref="ProjectsScreen"/> - tune once this is actually seen running against a real
    /// console.
    /// </remarks>
    public sealed class ProjectScreen : ScreenView<ProjectScreenViewModel>
    {
        #region Fields

        private const int HeaderRow = 3;
        private const int ListStartRow = 4;
        private const int PagerRow = 18;
        private const int NoTasksMessageRow = 12;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(ProjectScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(2, new string('-', ScreenBuffer.Width));

            // The header keeps the "> " marker for every mode except Creating/Editing, where it
            // hands the marker off to whichever task row is being typed into instead - the two
            // never appear together, mirroring how the mockups show exactly one "> " on screen
            // at a time.
            var headerMarker = viewModel.Mode is ProjectMode.Creating or ProjectMode.Editing ? "  " : "> ";
            buffer.SetLine(HeaderRow, headerMarker + viewModel.ProjectName);

            if (viewModel.Tasks.Count == 0 && viewModel.Mode == ProjectMode.Idle)
            {
                buffer.SetLine(NoTasksMessageRow, "no tasks yet");
                return;
            }

            RenderTaskList(viewModel, buffer);
        }

        /// <inheritdoc />
        public override bool HandleKey(ProjectScreenViewModel viewModel, ConsoleKeyInfo key)
        {
            if (!viewModel.IsEditingTitle)
            {
                return false;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                viewModel.RemoveTitleCharacter();
                return true;
            }

            if (!char.IsControl(key.KeyChar))
            {
                viewModel.AppendTitleCharacter(key.KeyChar);
                return true;
            }

            return false;
        }

        #endregion

        #region Helpers

        private static void RenderTaskList(ProjectScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var rows = viewModel.CurrentPageTasks;
            var row = ListStartRow;

            if (rows.Count == 0)
            {
                // Only reachable while Creating with no other active tasks yet - Idle with an
                // empty list is short-circuited by Render() before this is ever called.
                RenderTitleEntry(buffer, row, viewModel.TitleInput);
            }
            else
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var isSelected = i == viewModel.SelectedIndexOnPage;

                    if (isSelected && viewModel.Mode is ProjectMode.Creating or ProjectMode.Editing)
                    {
                        // Creating and Editing both overlay the title-entry row in place of
                        // whatever sits at the current selection, rather than inserting/shifting
                        // the rest of the list - same single path Projects uses for both.
                        RenderTitleEntry(buffer, row, viewModel.TitleInput);
                    }
                    else
                    {
                        RenderTaskRow(buffer, row, rows[i], isSelected, viewModel.PendingDeleteIds.Contains(rows[i].Id));
                    }

                    row++;
                }
            }

            if (viewModel.TotalPages > 1)
            {
                buffer.SetLine(PagerRow, LineFormat.DotLeader($"  < page {viewModel.CurrentPageIndex + 1}/{viewModel.TotalPages} >", string.Empty));
            }
        }

        private static void RenderTaskRow(ScreenBuffer buffer, int row, TaskItem task, bool isSelected, bool isMarkedForDelete)
        {
            const string indent = "    ";
            buffer.SetLine(row, indent + task.Title);

            // A pending delete always wins the highlight over plain selection - "this is about
            // to go away" is the more important thing to notice, and the two states are never
            // meaningfully ambiguous since the cursor is wherever it was last moved to anyway.
            if (isMarkedForDelete)
            {
                buffer.AddColorSpan(row, indent.Length, task.Title.Length, ConsoleColor.White, ConsoleColor.Red);
            }
            else if (isSelected)
            {
                buffer.AddColorSpan(row, indent.Length, task.Title.Length, ConsoleColor.Black, ConsoleColor.White);
            }
        }

        private static void RenderTitleEntry(ScreenBuffer buffer, int row, string titleInput)
        {
            const string marker = "  > ";
            var typed = titleInput + "_";
            var line = marker + typed;

            buffer.SetLine(row, line);
            buffer.AddColorSpan(row, marker.Length, typed.Length, ConsoleColor.Black, ConsoleColor.White);
        }

        #endregion
    }
}
