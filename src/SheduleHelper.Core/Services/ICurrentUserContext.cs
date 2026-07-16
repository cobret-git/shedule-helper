using System.Threading.Tasks;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Resolves the identity of the local user this application instance belongs to. For this
    /// development phase there is no login flow - a single local <c>User</c> row is fetched or
    /// auto-provisioned once at startup, and its id is cached for the rest of the session.
    /// </summary>
    public interface ICurrentUserContext : IDisposable
    {
        #region Properties

        /// <summary>
        /// Gets the identifier of the current user.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when accessed before <see cref="EnsureInitializedAsync"/> has completed.</exception>
        int UserId { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Resolves the current user, creating a default local user if none exists yet. Must be
        /// awaited once at application startup, before <see cref="UserId"/> is read.
        /// </summary>
        Task EnsureInitializedAsync();

        #endregion
    }
}
