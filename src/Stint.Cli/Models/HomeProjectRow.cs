namespace Stint.Cli.Models
{
    /// <summary>
    /// One task row nested under a <see cref="HomeProjectRow"/> on the Home screen.
    /// </summary>
    public sealed class HomeTaskRow
    {
        #region Properties

        /// <summary>
        /// The task's title.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Whether this task currently has an open (still-running) time log segment. When true,
        /// the renderer shows "ACTIVE" in place of <see cref="Duration"/>.
        /// </summary>
        public required bool IsActive { get; init; }

        /// <summary>
        /// Total time logged against this task today across all of its closed segments -
        /// excludes whatever segment is currently open (see <see cref="IsActive"/>).
        /// </summary>
        public required TimeSpan Duration { get; init; }

        #endregion
    }

    /// <summary>
    /// One project row on the Home screen, with its tasks nested beneath it.
    /// </summary>
    public sealed class HomeProjectRow
    {
        #region Properties

        /// <summary>
        /// The project's name.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Whether this project currently has an open (still-running) time log segment - on
        /// this project directly, or on one of its <see cref="Tasks"/>. When true, the
        /// renderer shows "ACTIVE" in place of <see cref="Duration"/>.
        /// </summary>
        public required bool IsActive { get; init; }

        /// <summary>
        /// Total time logged against this project today across all of its closed segments
        /// (task and no-task alike) - excludes whatever segment is currently open (see
        /// <see cref="IsActive"/>).
        /// </summary>
        public required TimeSpan Duration { get; init; }

        /// <summary>
        /// This project's tasks that have logged time today, most recently active first. A
        /// segment logged with no task attached contributes to this project's own
        /// <see cref="Duration"/> but produces no row here.
        /// </summary>
        public required IReadOnlyList<HomeTaskRow> Tasks { get; init; }

        #endregion
    }
}
