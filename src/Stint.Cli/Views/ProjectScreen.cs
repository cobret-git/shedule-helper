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

            // The header always shows "+ " - it used to hand the marker off (blank itself)
            // while Creating/Editing, back when it shared "> " with the task row being typed
            // into. Now that the header uses its own "+ " instead of "> ", there's no longer a
            // clash to avoid, so it stays put in every mode.
            buffer.SetLine(HeaderRow, "+ " + viewModel.ProjectName);

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
            else if (viewModel.Mode == ProjectMode.Creating)
            {
                // Creating appends the title-entry row after the existing list instead of
                // overlaying it on whatever the cursor happened to be sitting on - wherever that
                // was, it stays put and visible while the new title is typed.
                foreach (var task in rows)
                {
                    RenderTaskRow(buffer, row, task, isSelected: false, isMarkedForDelete: false);
                    row++;
                }

                RenderTitleEntry(buffer, row, viewModel.TitleInput);
            }
            else
            {
                for (var i = 0; i < rows.Count; i++)
                {
                    var isSelected = i == viewModel.SelectedIndexOnPage;

                    if (isSelected && viewModel.Mode == ProjectMode.Editing)
                    {
                        // Editing overlays the title-entry row in place of the row being renamed -
                        // unlike Creating, there's an existing row this one genuinely replaces.
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
            // Same indent scheme as RenderTitleEntry's own marker below - "  > " for the selected
            // row, "    " otherwise - so the selection highlight always has the ">" to back it up,
            // rather than relying on background color alone.
            var marker = isSelected ? "  > " : "    ";
            buffer.SetLine(row, marker + task.Title);

            // A pending delete always wins the highlight over plain selection - "this is about
            // to go away" is the more important thing to notice, and the two states are never
            // meaningfully ambiguous since the cursor is wherever it was last moved to anyway.
            if (isMarkedForDelete)
            {
                buffer.AddColorSpan(row, marker.Length, task.Title.Length, ConsoleColor.White, ConsoleColor.Red);
            }
            else if (isSelected)
            {
                buffer.AddColorSpan(row, marker.Length, task.Title.Length, ConsoleColor.Black, ConsoleColor.White);
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
