using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SheduleHelper.WpfApp.Components.Entities;

namespace SheduleHelper.WpfApp.Model
{
    /// <summary>
    /// Entity Framework Core database context for local SQLite database operations.
    /// Manages Projects, TaskItems, and TaskTypes with their relationships and constraints.
    /// </summary>
    public class LocalDbContext : DbContext
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDbContext"/> class.
        /// </summary>
        /// <param name="options">The options to be used by the DbContext.</param>
        public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options)
        {
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the DbSet for Project entities.
        /// Represents project containers that group related tasks.
        /// </summary>
        public DbSet<Project> Projects { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for TaskItem entities.
        /// Represents actual work items with support for hierarchical subtasks.
        /// </summary>
        public DbSet<TaskItem> TaskItems { get; set; }

        /// <summary>
        /// Gets or sets the DbSet for TaskType entities.
        /// Represents reusable task classifications/categories with hierarchical support.
        /// </summary>
        public DbSet<TaskType> TaskTypes { get; set; }

        #endregion

        #region Handlers

        /// <summary>
        /// Configures the database model and relationships using Fluent API.
        /// Sets up delete behaviors, query filters, and constraints that cannot be configured via attributes.
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // TaskType: Prevent deletion if has children
            modelBuilder.Entity<TaskType>()
                .HasOne(t => t.ParentType)
                .WithMany(t => t.ChildTypes)
                .OnDelete(DeleteBehavior.Restrict);

            // TaskType: Set NULL on tasks when deleted (hard delete)
            modelBuilder.Entity<TaskType>()
                .HasMany(t => t.Tasks)
                .WithOne(t => t.TaskType)
                .OnDelete(DeleteBehavior.SetNull);

            // Project: Set NULL on tasks when deleted
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Tasks)
                .WithOne(t => t.Project)
                .OnDelete(DeleteBehavior.SetNull);

            // TaskItem: Cascade delete subtasks when parent deleted
            modelBuilder.Entity<TaskItem>()
                .HasOne(t => t.ParentTask)
                .WithMany(t => t.SubTasks)
                .OnDelete(DeleteBehavior.Cascade);

            base.OnModelCreating(modelBuilder);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Saves all changes made in this context to the database asynchronously.
        /// Automatically updates the UpdatedAt timestamp for modified entities.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous save operation. The task result contains the number of state entries written to the database.</returns>
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Saves all changes made in this context to the database.
        /// Automatically updates the UpdatedAt timestamp for modified entities.
        /// </summary>
        /// <returns>The number of state entries written to the database.</returns>
        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Updates the UpdatedAt timestamp for all modified entities that have this property.
        /// </summary>
        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Entity is Project project)
                {
                    project.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is TaskItem taskItem)
                {
                    taskItem.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is TaskType taskType)
                {
                    taskType.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        #endregion
    }
}
