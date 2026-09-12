namespace Stint.Core
{
    /// <summary>
    /// Ensures the local SQLite database exists and is on the latest schema. Intended to run once,
    /// at application startup, before anything touches <see cref="IStintDataGateway"/>.
    /// </summary>
    public interface IDatabaseMigrator
    {
        /// <summary>
        /// Creates the database file if it doesn't exist yet, then applies any pending migrations.
        /// </summary>
        Task MigrateAsync(CancellationToken ct = default);
    }
}
