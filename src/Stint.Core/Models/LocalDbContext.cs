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
            // TaskItem: store TaskItemStatus enum as its string name
            modelBuilder.Entity<TaskItem>()
                .Property(t => t.Status)
                .HasConversion<string>();

            // ProjectTimeLog: store TimeLogCloseReason enum as its string name, nullable while open
            modelBuilder.Entity<ProjectTimeLog>()
                .Property(l => l.ClosedReason)
                .HasConversion<string>();

            // Project: unique name
            modelBuilder.Entity<Project>()
                .HasIndex(p => p.Name)
                .IsUnique();

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