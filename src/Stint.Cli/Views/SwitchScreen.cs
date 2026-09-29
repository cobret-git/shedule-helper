using Stint.Cli.Models;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Draws <see cref="SwitchScreenViewModel"/>'s body content. No free-form text entry on this
    /// screen, so <see cref="HandleKey"/> is left at its default no-op.
    /// </summary>
    /// <remarks>
    /// Row positions/how many project groups fit are approximate for now - tune once this is
    /// actually seen running against a real console, same caveat as <see cref="HomeScreen"/>/
    /// <see cref="ProjectsScreen"/>.
    /// </remarks>
    public sealed class SwitchScreen : ScreenView<SwitchScreenViewModel>
    {
        #region Fields

        private const int HeaderRow = 2;
        private const int RuleRow = 3;
        private const int ListStartRow = 4;
        private const int PagerRow = 18;
        private const int NoTargetsMessageRow = 12;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(SwitchScreenViewModel viewModel, ScreenBuffer buffer)
        {
            buffer.SetLine(HeaderRow, "Choose a project or task to switch to:");
            buffer.SetLine(RuleRow, new string('-', ScreenBuffer.Width));

            if (viewModel.Projects.Count == 0)
            {
                buffer.SetLine(NoTargetsMessageRow, "no projects or tasks yet");
                return;
            }

            RenderTree(viewModel, buffer);
        }

        #endregion

        #region Helpers

        private static void RenderTree(SwitchScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var row = ListStartRow;

            foreach (var project in viewModel.CurrentPageProjects)
            {
                RenderProjectRow(buffer, row, project, viewModel);
                row++;

                foreach (var task in project.Tasks)
                {
                    RenderTaskRow(buffer, row, project, task, viewModel);
                    row++;
                }

                row++;
            }

            if (viewModel.TotalPages > 1)
            {
                buffer.SetLine(PagerRow, $"  < page {viewModel.CurrentPageIndex + 1}/{viewModel.TotalPages} >");
            }
        }

        private static void RenderProjectRow(ScreenBuffer buffer, int row, SwitchProjectRow project, SwitchScreenViewModel viewModel)
        {
            var isSelected = viewModel.SelectedProjectId == project.Id && viewModel.SelectedTaskId is null;
            var marker = isSelected ? "> " : "  ";

            RenderRow(buffer, row, marker, project.Name, project.IsActive, isSelected);
        }

        private static void RenderTaskRow(ScreenBuffer buffer, int row, SwitchProjectRow project, SwitchTaskRow task, SwitchScreenViewModel viewModel)
        {
            var isSelected = viewModel.SelectedProjectId == project.Id && viewModel.SelectedTaskId == task.Id;
            var marker = isSelected ? "  > " : "    ";

            RenderRow(buffer, row, marker, task.Name, task.IsActive, isSelected);
        }

        // Shared by project and task rows: an active row gets a dot-leader over to "ACTIVE" (the
        // only value Switch ever shows - there's no duration here, unlike Home); every other row
        // is plain marker + name with no trailing dots at all, matching the mockup exactly.
        private static void RenderRow(ScreenBuffer buffer, int row, string marker, string name, bool isActive, bool isSelected)
        {
            if (isActive)
            {
                buffer.SetLine(row, marker + LineFormat.DotLeader(name, "ACTIVE", ScreenBuffer.Width - marker.Length));
            }
            else
            {
                buffer.SetLine(row, marker + name);
            }

            if (isSelected)
            {
                buffer.AddColorSpan(row, marker.Length, name.Length, ConsoleColor.Black, ConsoleColor.White);
            }
        }

        #endregion
    }
}
