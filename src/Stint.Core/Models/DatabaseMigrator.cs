using Microsoft.EntityFrameworkCore;

namespace Stint.Core
{
    public class DatabaseMigrator : IDatabaseMigrator
    {
        #region Fields
        private readonly IDbContextFactory<LocalDbContext> _dbContextFactory;
        #endregion

        #region Constructors
        public DatabaseMigrator(IDbContextFactory<LocalDbContext> dbContextFactory)
        {
            _dbContextFactory = dbContextFactory;
        }
        #endregion

        #region Methods
        public async Task MigrateAsync(CancellationToken ct = default)
        {
            await using var context = await _dbContextFactory.CreateDbContextAsync(ct);

            // Creates the SQLite file (and full schema) if it doesn't exist yet, otherwise applies
            // any migrations that haven't been recorded in __EFMigrationsHistory. Never combine this
            // with Database.EnsureCreated() - the two bypass each other's bookkeeping.
            await context.Database.MigrateAsync(ct);
        }
        #endregion
    }
}
