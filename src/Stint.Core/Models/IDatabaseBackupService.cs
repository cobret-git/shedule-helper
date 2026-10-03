namespace Stint.Core
{
    /// <summary>
    /// Creates backups of the live database and restores one over it. Works on whichever file
    /// the injected <see cref="Microsoft.EntityFrameworkCore.IDbContextFactory{TContext}"/> points
    /// at, so callers never pass the live path around.
    /// </summary>
    public interface IDatabaseBackupService
    {
        #region Methods

        /// <summary>
        /// Writes a consistent snapshot of the live database to <paramref name="backupPath"/>,
        /// replacing whatever file is already there. The snapshot is built next to the target
        /// first and moved into place at the end, so a failed backup never destroys the previous one.
        /// </summary>
        /// <exception cref="DatabaseBackupException">The target is the live database itself.</exception>
        Task CreateBackupAsync(string backupPath, CancellationToken ct = default);

        /// <summary>
        /// Returns <see langword="true"/> if every table the model maps holds zero rows - the only
        /// state a restore may happen in without first clearing the local data.
        /// </summary>
        Task<bool> IsDatabaseEmptyAsync(CancellationToken ct = default);

        /// <summary>
        /// Replaces the live database with the backup at <paramref name="backupPath"/>. The backup
        /// is validated and brought up to the current schema in a staging file beside the live
        /// database first; the live file is only touched once that has fully succeeded.
        /// </summary>
        /// <param name="backupPath">The backup file to restore from. Never modified.</param>
        /// <param name="safetyCopyPath">
        /// Where to save a copy of the current database before it is replaced, or
        /// <see langword="null"/> to skip it (nothing worth keeping, e.g. an empty database).
        /// </param>
        /// <exception cref="DatabaseBackupException">
        /// The file is not a Stint database, is damaged, or was made by a newer version of the app.
        /// </exception>
        Task RestoreAsync(string backupPath, string? safetyCopyPath, CancellationToken ct = default);

        #endregion
    }
}
