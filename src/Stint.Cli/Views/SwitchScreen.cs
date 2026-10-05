using Stint.Cli.Components;
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

        private const int PendingPauseRow = 1;
        private const int HeaderRow = 2;
        private const int RuleRow = 3;
        private const int ListStartRow = 4;
        private const int PagerRow = 18;
        private const int NoTargetsMessageRow = 12;
        private const int ErrorRow = ScreenBuffer.Height - 4;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Render(SwitchScreenViewModel viewModel, ScreenBuffer buffer)
        {
            if (viewModel.PendingPause)
            {
                // The one pending action that isn't tied to a specific row - called out on its
                // own line rather than as a per-row marker, same reason Home's own status lines
                // sit above the tree instead of inside it.
                buffer.SetLine(PendingPauseRow, "Pending: pause tracking");
            }

            RenderHeader(viewModel, buffer);
            buffer.SetLine(RuleRow, new string('-', ScreenBuffer.Width));

            if (viewModel.LoadError is { } error)
            {
                buffer.SetLine(ErrorRow, error);
                buffer.AddColorSpan(ErrorRow, 0, Math.Min(error.Length, ScreenBuffer.Width), ConsoleColor.Red);
            }

            if (viewModel.View == SwitchView.Away)
            {
                RenderAwayList(viewModel, buffer);
                return;
            }

            if (viewModel.Projects.Count == 0)
            {
                buffer.SetLine(NoTargetsMessageRow, "no projects or tasks yet");
                return;
            }

            RenderTree(viewModel, buffer);
        }

        #endregion

        #region Helpers

        // One line for both views: the prompt, then the two views side by side with the current one
        // inverted - the same contrast the selected row gets - so Tab's effect is visible at a glance.
        private static void RenderHeader(SwitchScreenViewModel viewModel, ScreenBuffer buffer)
        {
            const string prompt = "Choose what to switch to:";
            const string projectsTab = " PROJECTS ";
            const string awayTab = " AWAY ";

            var projectsStart = prompt.Length + 2;
            var awayStart = projectsStart + projectsTab.Length + 1;

            buffer.SetLine(HeaderRow, $"{prompt}  {projectsTab} {awayTab}");

            var (activeStart, activeLength) = viewModel.View == SwitchView.Projects
                ? (projectsStart, projectsTab.Length)
                : (awayStart, awayTab.Length);

            buffer.AddColorSpan(HeaderRow, activeStart, activeLength, ConsoleColor.Black, ConsoleColor.White);
        }

        private static void RenderAwayList(SwitchScreenViewModel viewModel, ScreenBuffer buffer)
        {
            var row = ListStartRow;

            for (var i = 0; i < viewModel.AwayKinds.Count; i++)
            {
                var kind = viewModel.AwayKinds[i];
                var isSelected = i == viewModel.SelectedAwayIndex;
                var isPendingTarget = viewModel.PendingAwayKind == kind;
                var marker = isSelected ? "> " : "  ";

                RenderRow(buffer, row++, marker, AwayKindNames.GetName(kind).ToUpperInvariant(), isActive: false, isPendingTarget, isPendingDone: false, isPendingPause: false, isSelected);
            }
        }

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
            var isPendingTarget = viewModel.PendingSwitchTargetProjectId == project.Id && viewModel.PendingSwitchTargetTaskId is null;
            var isPendingPause = viewModel.PendingPause && project.IsActive;
            var marker = isSelected ? "> " : "  ";

            RenderRow(buffer, row, marker, project.Name, project.IsActive, isPendingTarget, isPendingDone: false, isPendingPause, isSelected);
        }

        private static void RenderTaskRow(ScreenBuffer buffer, int row, SwitchProjectRow project, SwitchTaskRow task, SwitchScreenViewModel viewModel)
        {
            var isSelected = viewModel.SelectedProjectId == project.Id && viewModel.SelectedTaskId == task.Id;
            var isPendingTarget = viewModel.PendingSwitchTargetProjectId == project.Id && viewModel.PendingSwitchTargetTaskId == task.Id;
            var isPendingDone = viewModel.PendingDoneTaskIds.Contains(task.Id);
            var isPendingPause = viewModel.PendingPause && task.IsActive;
            var marker = isSelected ? "  > " : "    ";

            RenderRow(buffer, row, marker, task.Name, task.IsActive, isPendingTarget, isPendingDone, isPendingPause, isSelected);
        }

        // Shared by project and task rows. Right-side label priority when more than one applies
        // (the running row can be both currently active and pending-paused, or a task can be both
        // active and marked pending-done): pending-done wins, since that's the imminent state the
        // user is about to confirm, then pending switch-target, then pending-pause (only ever the
        // one row that's actually running), then the plain "ACTIVE" label Home also uses - every
        // other row is plain marker + name with no trailing dots at all, matching the mockup's
        // idle rows exactly.
        private static void RenderRow(ScreenBuffer buffer, int row, string marker, string name, bool isActive, bool isPendingTarget, bool isPendingDone, bool isPendingPause, bool isSelected)
        {
            var (label, labelBackground) = isPendingDone
                ? ("DONE", (ConsoleColor?)ConsoleColor.Magenta)
                : isPendingTarget
                    ? ("NEXT", (ConsoleColor?)ConsoleColor.Green)
                    : isPendingPause
                        ? ("PAUSE", (ConsoleColor?)ConsoleColor.Yellow)
                        : isActive
                            ? ("ACTIVE", (ConsoleColor?)null)
                            : (string.Empty, (ConsoleColor?)null);

            string line;
            if (label.Length > 0)
            {
                line = marker + LineFormat.DotLeader(name, label, ScreenBuffer.Width - marker.Length);
            }
            else
            {
                line = marker + name;
            }

            buffer.SetLine(row, line);

            if (isSelected)
            {
                buffer.AddColorSpan(row, marker.Length, name.Length, ConsoleColor.Black, ConsoleColor.White);
            }

            if (labelBackground is ConsoleColor background)
            {
                buffer.AddColorSpan(row, line.Length - label.Length, label.Length, ConsoleColor.Black, background);
            }
        }

        #endregion
    }
}
