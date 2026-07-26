using SheduleHelper.Core.Models;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Default <see cref="ILocalDbContextFactory"/> implementation, creating each
    /// <see cref="LocalDbContext"/> against the path resolved by an <see cref="IPathProvider"/>.
    /// </summary>
    public class LocalDbContextFactory : ILocalDbContextFactory
    {
        #region Fields

        private readonly IPathProvider _pathProvider;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDbContextFactory"/> class.
        /// </summary>
        /// <param name="pathProvider">Provides the database file path to construct contexts against.</param>
        public LocalDbContextFactory(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public LocalDbContext CreateDbContext()
        {
            return new LocalDbContext(_pathProvider.DatabaseFilePath);
        }

        #endregion
    }
}
