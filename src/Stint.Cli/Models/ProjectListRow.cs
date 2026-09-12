namespace Stint.Cli.Models
{
    /// <summary>
    /// One row on the Projects screen's list: an active project's id/name plus how many tasks
    /// it has, per <see cref="Stint.Core.IStintDataGateway.GetTaskCountForProjectAsync"/>.
    /// </summary>
    /// <remarks>
    /// Display-only - it never round-trips back to the gateway. <see cref="Stint.Cli.ViewModels.ProjectsScreenViewModel"/>
    /// keeps the full <see cref="Stint.Core.Project"/> entities behind these rows around separately
    /// so a rename doesn't have to (and can't accidentally) wipe fields this row doesn't carry,
    /// like <see cref="Stint.Core.Project.Description"/>.
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
