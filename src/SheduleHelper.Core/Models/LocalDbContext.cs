using Microsoft.EntityFrameworkCore;
using SheduleHelper.Core.Components.Entities;

namespace SheduleHelper.Core.Models
{
    /// <summary>
    /// Entity Framework Core database context for the local SQLite time-tracking database.
    /// Manages Users, Projects, Tasks, AttendanceLogs, and ProjectTimeLogs with their relationships.
    /// </summary>
    public class LocalDbContext : DbContext
    {
        #region Fields

        private readonly string _filePath;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalDbContext"/> class.
        /// </summary>
        /// <param name="filePath">Path to the SQLite database file used as the connection data source.</param>
        public LocalDbContext(string filePath)
        {
            _filePath = filePath;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the DbSet for User entities.
        /// </summary>
        public DbSet<User> Users { get; set; } = null!;

        /// <summary>
        /// Gets or sets the DbSet for UserSetting entities.
        /// </summary>
        public DbSet<UserSetting> UserSettings { get; set; } = null!;

        /// <summary>
        /// Gets or sets the DbSet for Project entities.
        /// </summary>
        public DbSet<Project> Projects { get; set; } = null!;

        /// <summary>
        /// Gets or sets the DbSet for TaskItem entities.
        /// </summary>
        public DbSet<TaskItem> Tasks { get; set; } = null!;

        /// <summary>
        /// Gets or sets the DbSet for AttendanceLog entities.
        /// </summary>
        public DbSet<AttendanceLog> AttendanceLogs { get; set; } = null!;

        /// <summary>
        /// Gets or sets the DbSet for ProjectTimeLog entities.
        /// </summary>
        public DbSet<ProjectTimeLog> ProjectTimeLogs { get; set; } = null!;

        #endregion

        #region Handlers

        /// <summary>
        /// Configures the database connection to use SQLite with the file path provided at construction.
        /// </summary>
        /// <param name="optionsBuilder">The builder being used to configure this context.</param>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={_filePath}");
        }

        /// <summary>
        /// Configures the database model and relationships using Fluent API.
        /// </summary>
        /// <param name="modelBuilder">The builder being used to construct the model for this context.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User 1:1 UserSetting
            modelBuilder.Entity<User>()
                .HasOne(u => u.UserSetting)
                .WithOne(s => s.User)
                .HasForeignKey<UserSetting>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // User 1:N Projects
            modelBuilder.Entity<User>()
                .HasMany(u => u.Projects)
                .WithOne(p => p.Owner)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // User 1:N AttendanceLogs
            modelBuilder.Entity<User>()
                .HasMany(u => u.AttendanceLogs)
                .WithOne(a => a.User)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // User: unique email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // AttendanceLog: unique work date per user
            modelBuilder.Entity<AttendanceLog>()
                .HasIndex(a => new { a.UserId, a.WorkDate })
                .IsUnique();

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
