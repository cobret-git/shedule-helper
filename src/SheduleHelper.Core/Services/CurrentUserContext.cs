using Serilog;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Default <see cref="ICurrentUserContext"/> implementation. Fetches the single local user if
    /// one already exists, or provisions a default one on first run - no login UI is involved.
    /// Registered as a singleton, it is disposed by the DI container on application shutdown,
    /// canceling any in-flight <see cref="EnsureInitializedAsync"/> call.
    /// </summary>
    public class CurrentUserContext : ICurrentUserContext
    {
        #region Fields

        private const string DefaultUsername = "Local User";
        private const string DefaultEmail = "local-user@sheduletracker.local";

        private readonly ILocalDbContextFactory _dbContextFactory;
        private readonly ILogger _logger;
        private CancellationTokenSource? _cts;
        private int? _userId;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CurrentUserContext"/> class.
        /// </summary>
        /// <param name="dbContextFactory">Creates the <see cref="Models.LocalDbContext"/> used to fetch/provision the current user.</param>
        /// <param name="logger">Logs failures encountered while resolving the current user.</param>
        public CurrentUserContext(ILocalDbContextFactory dbContextFactory, ILogger logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public int UserId => _userId ?? throw new InvalidOperationException($"{nameof(CurrentUserContext)} has not been initialized. Call {nameof(EnsureInitializedAsync)} first.");

        #endregion

        #region Methods

        /// <inheritdoc/>
        public async Task EnsureInitializedAsync()
        {
            try
            {
                var ct = CreateCancellationToken();
                using var db = _dbContextFactory.CreateDbContext();

                var user = (await db.GetAllUsersAsync(ct)).FirstOrDefault();
                if (user is null)
                {
                    user = await db.CreateUserAsync(DefaultUsername, DefaultEmail, ct);
                    await db.CreateUserSettingAsync(user.Id, ct);
                }

                _userId = user.Id;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize the current user context.");
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
