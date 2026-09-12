using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stint.Core
{
    /// <summary>
    /// Design-time-only factory used by the <c>dotnet ef</c> tooling (e.g. <c>dotnet ef migrations add</c>)
    /// to construct a <see cref="LocalDbContext"/> without running the application's own DI startup.
    /// Not used at runtime - see <see cref="IDatabaseMigrator"/> for that.
    /// </summary>
    public class LocalDbContextFactory : IDesignTimeDbContextFactory<LocalDbContext>
    {
        #region Methods
        public LocalDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<LocalDbContext>()
                .UseSqlite("Data Source=stint.design.db")
                .Options;

            return new LocalDbContext(options);
        }
        #endregion
    }
}
