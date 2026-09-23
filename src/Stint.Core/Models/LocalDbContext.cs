using Microsoft.EntityFrameworkCore;

namespace Stint.Core
{
    public class LocalDbContext : DbContext
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDbContext"/> class.
        /// </summary>
        /// <param name="options">Options configured by the DI container (provider, connection string, etc.), typically via <c>AddDbContextFactory</c>.</param>
        public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options)
        {
        }

        #endregion

        #region Properties
        public DbSet<Project> Projects { get; set; } = null!;
        public DbSet<TaskItem> Tasks { get; set; } = null!;
        public DbSet<AttendanceLog> AttendanceLogs { get; set; } = null!;
        public DbSet<ProjectTimeLog> ProjectTimeLogs { get; set; } = null!;
        #endregion

        #region Handlers
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Enums are stored as their underlying int (EF Core default) everywhere in this model -
            // cheaper than strings. Values are pinned by their explicit numbers in each enum
            // declaration, so don't reorder or remove them; only append.

            // AttendanceLog: at most one attendance log per calendar date
            modelBuilder.Entity<AttendanceLog>()
                .HasIndex(a => a.WorkDate)
                .IsUnique();

            // Project: unique name among active projects only - a soft-deleted project's name
            // (IsActive = 0) stays in the table forever, and must free up for reuse rather than
            // permanently squatting on it.
            modelBuilder.Entity<Project>()
                .HasIndex(p => p.Name)
                .IsUnique()
                .HasFilter("\"IsActive\" = 1");

            // Project 1:N Tasks
            modelBuilder.Entity<Project>()
                .HasMany(p => p.Tasks)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Project 1:N ProjectTimeLogs
            modelBuilder.Entity<Project>()
                .HasMany(p => p.ProjectTimeLogs)
                .WithOne(l => l.Project)
                .HasForeignKey(l => l.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // AttendanceLog 1:N ProjectTimeLogs
            modelBuilder.Entity<AttendanceLog>()
                .HasMany(a => a.ProjectTimeLogs)
                .WithOne(l => l.AttendanceLog)
                .HasForeignKey(l => l.AttendanceLogId)
                .OnDelete(DeleteBehavior.Cascade);

            // TaskItem 0..1:N ProjectTimeLogs
            modelBuilder.Entity<TaskItem>()
                .HasMany(t => t.ProjectTimeLogs)
                .WithOne(l => l.Task)
                .HasForeignKey(l => l.TaskId)
                .OnDelete(DeleteBehavior.SetNull);

            base.OnModelCreating(modelBuilder);
        }
        #endregion
    }
}