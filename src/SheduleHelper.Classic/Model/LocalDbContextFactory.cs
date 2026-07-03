using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SheduleHelper.WpfApp.Model
{
    /// <summary>
    /// Factory for creating LocalDbContext instances at design-time for EF Core migrations.
    /// </summary>
    public class LocalDbContextFactory : IDesignTimeDbContextFactory<LocalDbContext>
    {
        /// <summary>
        /// Creates a new instance of LocalDbContext for design-time operations.
        /// </summary>
        /// <param name="args">Arguments passed from EF Core tools.</param>
        /// <returns>A configured LocalDbContext instance.</returns>
        public LocalDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<LocalDbContext>();

            // Use a temporary connection string for migrations
            // This won't be used at runtime - only for generating migration files
            optionsBuilder.UseSqlite("Data Source=temp.db");

            return new LocalDbContext(optionsBuilder.Options);
        }
    }
}
