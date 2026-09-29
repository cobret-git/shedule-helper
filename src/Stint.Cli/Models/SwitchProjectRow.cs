namespace Stint.Cli.Models
{
    /// <summary>
    /// One task row nested under a <see cref="SwitchProjectRow"/> on the Switch screen.
    /// </summary>
    public sealed class SwitchTaskRow
    {
        #region Properties

        /// <summary>
        /// The task's id.
        /// </summary>
        public required int Id { get; init; }

        /// <summary>
        /// The task's title.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Whether this exact task is the currently open time log segment's task.
        /// </summary>
        public required bool IsActive { get; init; }

        #endregion
    }

    /// <summary>
    /// One project row on the Switch screen, with its tasks nested beneath it. Unlike
    /// <see cref="HomeProjectRow"/> (which only lists projects/tasks with logged time today),
    /// this covers every active project/task - anything switchable, whether or not it's been
    /// worked on yet today.
    /// </summary>
    public sealed class SwitchProjectRow
    {
        #region Properties

        /// <summary>
        /// The project's id.
        /// </summary>
        public required int Id { get; init; }

        /// <summary>
        /// The project's name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Whether this project itself (no task) is the currently open time log segment's target.
        /// </summary>
        public required bool IsActive { get; init; }

        /// <summary>
        /// This project's active tasks, in creation order.
        /// </summary>
        public required IReadOnlyList<SwitchTaskRow> Tasks { get; init; }

        #endregion
    }
}
