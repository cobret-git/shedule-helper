using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Applies pending Entity Framework Core migrations to the local database at application
    /// startup, via <c>Database.MigrateAsync</c>, which also creates the database file if it does
    /// not exist yet. Must be awaited once at application startup, before any other component
    /// queries the database. Owns a <see cref="CancellationTokenSource"/> for that in-flight
    /// migration, canceled on <see cref="Dispose"/> - registered as a singleton, it is disposed by
    /// the DI container on application shutdown.
    /// </summary>
    public class DatabaseMigrationService : IDisposable
    {
        #region Fields

        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly ILogger _logger;
        private CancellationTokenSource? _cts;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseMigrationService"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Creates the <see cref="Models.LocalDbContext"/> used to apply migrations.</param>
        /// <param name="logger">Logs failures encountered while migrating the database.</param>
        public DatabaseMigrationService(ILocalDbContextFactory dbContextFactory, ILogger logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Creates the local database if it does not exist, or brings it up to date with the
        /// latest migration otherwise.
        /// </summary>
        public async Task MigrateAsync()
        {
            try
            {
                var ct = CreateCancellationToken();
                await using var db = _dbContextFactory.CreateDbContext();
                await db.Database.MigrateAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to migrate the local database.");
                throw;
            }
        }

        #endregion

        #region Helpers
        private CancellationToken CreateCancellationToken()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new();
            return _cts.Token;
        }
        #endregion

        #region IDisposable

        /// <inheritdoc/>
        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        #endregion
    }
}
