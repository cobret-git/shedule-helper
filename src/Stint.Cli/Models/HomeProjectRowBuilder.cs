using Stint.Core;

namespace Stint.Cli.Models
{
    /// <summary>
    /// Groups a flat list of today's <see cref="ProjectTimeLog"/> segments - most recently
    /// started first, with <see cref="ProjectTimeLog.Project"/>/<see cref="ProjectTimeLog.Task"/>
    /// eager-loaded, per <see cref="IStintDataGateway.GetTimeLogsForAttendanceAsync"/> - into the
    /// project/task tree Home displays.
    /// </summary>
    public static class HomeProjectRowBuilder
    {
        #region Methods

        /// <summary>
        /// Builds one row per project, in the order its most recent segment appears in
        /// <paramref name="logsByRecency"/>, each with its tasks nested in that same order.
        /// </summary>
        public static IReadOnlyList<HomeProjectRow> Build(IReadOnlyList<ProjectTimeLog> logsByRecency)
        {
            var projects = new List<ProjectBuilder>();
            var projectsById = new Dictionary<int, ProjectBuilder>();

            foreach (var log in logsByRecency)
            {
                if (!projectsById.TryGetValue(log.ProjectId, out var project))
                {
                    project = new ProjectBuilder(log.Project.Name);
                    projectsById.Add(log.ProjectId, project);
                    projects.Add(project);
                }

                project.Add(log);
            }

            return projects.Select(p => p.Build()).ToList();
        }

        #endregion

        #region Helpers

        private sealed class ProjectBuilder(string name)
        {
            private readonly List<TaskBuilder> _tasks = [];
            private readonly Dictionary<int, TaskBuilder> _tasksById = [];

            private TimeSpan _closedDuration;
            private bool _isActive;

            public void Add(ProjectTimeLog log)
            {
                Accumulate(log, ref _isActive, ref _closedDuration);

                if (log.TaskId is null)
                {
                    return;
                }

                if (!_tasksById.TryGetValue(log.TaskId.Value, out var task))
                {
                    task = new TaskBuilder(log.Task!.Title);
                    _tasksById.Add(log.TaskId.Value, task);
                    _tasks.Add(task);
                }

                task.Add(log);
            }

            public HomeProjectRow Build() => new()
            {
                Name = name,
                IsActive = _isActive,
                Duration = _closedDuration,
                Tasks = _tasks.Select(t => t.Build()).ToList()
            };
        }

        private sealed class TaskBuilder(string name)
        {
            private TimeSpan _closedDuration;
            private bool _isActive;

            public void Add(ProjectTimeLog log) => Accumulate(log, ref _isActive, ref _closedDuration);

            public HomeTaskRow Build() => new()
            {
                Name = name,
                IsActive = _isActive,
                Duration = _closedDuration
            };
        }

        // Shared by both builders: a still-open segment (EndTime null) marks its row active and
        // contributes nothing to the closed-duration total; a closed one adds its span and never
        // clears an already-set active flag (a project/task can have one open segment and several
        // earlier closed ones on the same day).
        private static void Accumulate(ProjectTimeLog log, ref bool isActive, ref TimeSpan closedDuration)
        {
            if (log.EndTime is null)
            {
                isActive = true;
                return;
            }

            closedDuration += log.EndTime.Value - log.StartTime;
        }

        #endregion
    }
}
