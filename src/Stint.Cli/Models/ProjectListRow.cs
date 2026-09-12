namespace Stint.Cli.Models
{
    /// <summary>
    /// One row on the Projects screen's list: an active project's id/name plus how many tasks
    /// it has, per <see cref="Stint.Core.IStintDataGateway.GetTaskCountForProjectAsync"/>.
    /// </summary>
    /// <remarks>
    /// Display-only - it never round-trips back to the gateway.
    /// </remarks>
    public sealed class ProjectListRow
    {
        #region Properties

        /// <summary>The project's id.</summary>
        public required int Id { get; init; }

        /// <summary>The project's name.</summary>
        public required string Name { get; init; }

        /// <summary>How many tasks belong to this project.</summary>
        public required int TaskCount { get; init; }

        #endregion
    }
}
