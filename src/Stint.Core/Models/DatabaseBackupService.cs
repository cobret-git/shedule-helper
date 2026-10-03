using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Stint.Core
{
    public class DatabaseBackupService : IDatabaseBackupService
    {
        #region Fields

        // Staged beside the live file (same volume) so the final swap is a rename, not a copy.
        private const string RestoreFileName = "restore-data.db";

        private static readonly string[] SidecarSuffixes = ["-wal", "-shm", "-journal"];

        private readonly IDbContextFactory<LocalDbContext> _dbContextFactory;
        #endregion

        #region Constructors
        public DatabaseBackupService(IDbContextFactory<LocalDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }
        #endregion

        #region Methods

        /// <inheritdoc />
        public async Task CreateBackupAsync(string backupPath, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

            var livePath = GetLiveDatabasePath();
            var targetPath = Path.GetFullPath(backupPath);

            if (IsSamePath(targetPath, livePath))
            {
                throw new DatabaseBackupException("A backup can't overwrite the live database.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            var tempPath = targetPath + ".tmp";
            DeleteDatabaseFiles(tempPath);

            try
            {
                await Task.Run(() => CopyDatabase(livePath, SqliteOpenMode.ReadWrite, tempPath), ct);
                File.Move(tempPath, targetPath, overwrite: true);
            }
            finally
            {
                DeleteDatabaseFiles(tempPath);
            }
        }

        /// <inheritdoc />
        public async Task<bool> IsDatabaseEmptyAsync(CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);

            // Straight from the model, so a table added later is covered without touching this.
            // Bookkeeping tables (__EFMigrationsHistory, ...) aren't mapped entities, so they never count.
            var tableNames = context.Model.GetEntityTypes()
                .Select(e => e.GetTableName())
                .OfType<string>()
                .Distinct()
                .ToList();

            await using var connection = new SqliteConnection(BuildConnectionString(GetLiveDatabasePath(), SqliteOpenMode.ReadWrite));
            await connection.OpenAsync(ct);

            foreach (var tableName in tableNames)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT EXISTS (SELECT 1 FROM \"{tableName}\")";

                if (Convert.ToInt64(await command.ExecuteScalarAsync(ct)) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public async Task RestoreAsync(string backupPath, string? safetyCopyPath, CancellationToken ct = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

            var livePath = GetLiveDatabasePath();
            var sourcePath = Path.GetFullPath(backupPath);

            if (IsSamePath(sourcePath, livePath))
            {
                throw new DatabaseBackupException("That is the live database, not a backup.");
            }

            if (!File.Exists(sourcePath))
            {
                throw new DatabaseBackupException("The backup file doesn't exist.");
            }

            await ValidateBackupAsync(sourcePath, ct);

            // Before anything is staged or deleted, so a failed safety copy aborts the restore
            // with the live database still exactly as it was.
            if (safetyCopyPath is not null)
            {
                await CreateBackupAsync(safetyCopyPath, ct);
            }

            var restorePath = Path.Combine(Path.GetDirectoryName(livePath)!, RestoreFileName);
            DeleteDatabaseFiles(restorePath);

            try
            {
                // The staging file is a full copy of the backup (not the backup itself), so
                // migrating it never touches what the user picked.
                await Task.Run(() => CopyDatabase(sourcePath, SqliteOpenMode.ReadOnly, restorePath), ct);
                await MigrateAsync(restorePath, ct);

                // Every connection the app opened to the live file is pooled and may still hold
                // it open - release them all, or deleting/replacing the file below fails with a
                // sharing violation. Our own connections above are non-pooled, already closed.
                SqliteConnection.ClearAllPools();

                DeleteSidecarFiles(livePath);
                File.Move(restorePath, livePath, overwrite: true);
            }
            finally
            {
                // Nothing left behind if anything above failed; already gone after a clean swap.
                DeleteDatabaseFiles(restorePath);
            }
        }

        #endregion

        #region Helpers

        // The factory is the single source of truth for where the live file is, so read it back
        // from there instead of threading a second copy of the path through the app.
        private string GetLiveDatabasePath()
        {
            using var context = _dbContextFactory.CreateDbContext();
            return Path.GetFullPath(context.Database.GetDbConnection().DataSource);
        }

        // Pooling is off for every connection this class opens itself: a pooled connection stays
        // open after Dispose, keeping the file locked, which is exactly what stops a rename/delete.
        private static string BuildConnectionString(string path, SqliteOpenMode mode) => new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false
        }.ToString();

        // SQLite's online backup API: a transactionally consistent copy of every page, schema and
        // migration history included, correct even while another connection is using the source.
        private static void CopyDatabase(string sourcePath, SqliteOpenMode sourceMode, string destinationPath)
        {
            using var source = new SqliteConnection(BuildConnectionString(sourcePath, sourceMode));
            using var destination = new SqliteConnection(BuildConnectionString(destinationPath, SqliteOpenMode.ReadWriteCreate));

            source.Open();
            destination.Open();
            source.BackupDatabase(destination);
        }

        private async Task ValidateBackupAsync(string path, CancellationToken ct)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);
            var knownMigrations = context.Database.GetMigrations().ToHashSet();

            var appliedMigrations = new List<string>();

            try
            {
                await using var connection = new SqliteConnection(BuildConnectionString(path, SqliteOpenMode.ReadOnly));
                await connection.OpenAsync(ct);

                await using (var check = connection.CreateCommand())
                {
                    check.CommandText = "PRAGMA quick_check";
                    if (!string.Equals(await check.ExecuteScalarAsync(ct) as string, "ok", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new DatabaseBackupException("The backup file is damaged.");
                    }
                }

                await using var history = connection.CreateCommand();
                history.CommandText = "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\"";

                await using var reader = await history.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    appliedMigrations.Add(reader.GetString(0));
                }
            }
            catch (SqliteException ex)
            {
                // Not a SQLite file at all, or one without the migration history table.
                throw new DatabaseBackupException("That file isn't a Stint database.", ex);
            }

            if (appliedMigrations.Count == 0)
            {
                throw new DatabaseBackupException("That file isn't a Stint database.");
            }

            if (appliedMigrations.Any(m => !knownMigrations.Contains(m)))
            {
                throw new DatabaseBackupException("The backup was made by a newer version of Stint.");
            }
        }

        private static async Task MigrateAsync(string path, CancellationToken ct)
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>()
                .UseSqlite(BuildConnectionString(path, SqliteOpenMode.ReadWriteCreate))
                .Options;

            await using var context = new LocalDbContext(options);
            await context.Database.MigrateAsync(ct);
        }

        private static bool IsSamePath(string first, string second)
            => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

        private static void DeleteDatabaseFiles(string path)
        {
            File.Delete(path);
            DeleteSidecarFiles(path);
        }

        private static void DeleteSidecarFiles(string path)
        {
            foreach (var suffix in SidecarSuffixes)
            {
                File.Delete(path + suffix);
            }
        }

        #endregion
    }
}
