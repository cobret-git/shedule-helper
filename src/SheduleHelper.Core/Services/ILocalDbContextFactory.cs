using SheduleHelper.Core.Models;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Creates short-lived <see cref="LocalDbContext"/> instances for individual units of work,
    /// so callers never need to know the database file path or manage a shared context's lifetime.
    /// </summary>
    public interface ILocalDbContextFactory
    {
        #region Methods

        /// <summary>
        /// Creates a new <see cref="LocalDbContext"/> instance. The caller owns the returned
        /// instance and is responsible for disposing it.
        /// </summary>
        /// <returns>A new <see cref="LocalDbContext"/> configured with the current database file path.</returns>
        LocalDbContext CreateDbContext();

        #endregion
    }
}
