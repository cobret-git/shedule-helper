using Microsoft.EntityFrameworkCore.Design;

namespace SheduleHelper.Core.Models
{
    /// <summary>
    /// Factory for creating <see cref="LocalDbContext"/> instances at design-time for EF Core
    /// tooling (e.g. <c>dotnet ef migrations add</c>, <c>Add-Migration</c>). Takes precedence over
    /// any other design-time discovery pattern, so <see cref="LocalDbContext"/> itself never needs
    /// a parameterless constructor - the real database file path, resolved by
    /// <see cref="Services.IDatabasePathProvider"/>, is only known at runtime and is irrelevant here.
    /// </summary>
    public class LocalDbContextFactory : IDesignTimeDbContextFactory<LocalDbContext>
    {
        #region Methods

        /// <summary>
        /// Creates a new instance of <see cref="LocalDbContext"/> for design-time operations.
        /// </summary>
        /// <param name="args">Arguments passed from EF Core tools.</param>
        /// <returns>A <see cref="LocalDbContext"/> instance pointed at a placeholder file path, only used for generating migration files.</returns>
        public LocalDbContext CreateDbContext(string[] args)
        {
            return new LocalDbContext("design-time.db");
        }

        #endregion
    }
}
