using SheduleHelper.Core.Models;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Default <see cref="ILocalDbContextFactory"/> implementation, creating each
    /// <see cref="LocalDbContext"/> against the path resolved by an <see cref="IDatabasePathProvider"/>.
    /// </summary>
    public class LocalDbContextFactory : ILocalDbContextFactory
    {
        #region Fields

        private readonly IDatabasePathProvider _databasePathProvider;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDbContextFactory"/> class.
        /// </summary>
        /// <param name="databasePathProvider">Provides the database file path to construct contexts against.</param>
        public LocalDbContextFactory(IDatabasePathProvider databasePathProvider)
        {
            _databasePathProvider = databasePathProvider;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public LocalDbContext CreateDbContext()
        {
            return new LocalDbContext(_databasePathProvider.DatabaseFilePath);
        }

        #endregion
    }
}
