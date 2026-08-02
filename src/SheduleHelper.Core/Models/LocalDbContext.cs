using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

            // UserSetting: store LunchStrategy enum as its string name
            modelBuilder.Entity<UserSetting>()
                .Property(s => s.LunchStrategy)
                .HasConversion<string>();

            // TaskItem: store TaskItemStatus enum as its string name
            modelBuilder.Entity<TaskItem>()
                .Property(t => t.Status)
                .HasConversion<string>();

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

        #region Create Methods

        /// <summary>
        /// Creates a new user and persists it to the database.
        /// </summary>
        /// <param name="userName">The username of the new user.</param>
        /// <param name="email">The unique email address of the new user.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created <see cref="User"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when <paramref name="email"/> violates the unique constraint on <see cref="User.Email"/>.</exception>
        public async Task<User> CreateUserAsync(string userName, string email, CancellationToken cancellationToken)
        {
            var user = new User
            {
                Username = userName,
                Email = email,
                CreatedAt = DateTime.UtcNow
            };

            await Users.AddAsync(user, cancellationToken);
            await SaveChangesAsync(cancellationToken);

            return user;
        }

        /// <summary>
        /// Creates a new project owned by the specified user and persists it to the database.
        /// </summary>
        /// <param name="name">The unique name of the new project.</param>
        /// <param name="userId">The identifier of the user who owns the project.</param>
        /// <param name="description">An optional description of the project.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created <see cref="Project"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when <paramref name="name"/> violates the unique constraint on <see cref="Project.Name"/>, or when <paramref name="userId"/> does not reference an existing <see cref="User"/> (foreign key violation).</exception>
        public async Task<Project> CreateProjectAsync(string name, int userId, string? description, CancellationToken cancellationToken)
        {
            var project = new Project
            {
                Name = name,
                UserId = userId,
                Description = description,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await Projects.AddAsync(project, cancellationToken);
            await SaveChangesAsync(cancellationToken);

            return project;
        }

        /// <summary>
        /// Creates a new task under the specified project and persists it to the database.
        /// </summary>
        /// <param name="title">The title of the new task.</param>
        /// <param name="description">An optional description of the task.</param>
        /// <param name="status">The initial status of the task.</param>
        /// <param name="projectId">The identifier of the project the task belongs to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created <see cref="TaskItem"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when <paramref name="projectId"/> does not reference an existing <see cref="Project"/> (foreign key violation).</exception>
        public async Task<TaskItem> CreateTaskAsync(string title, string? description, TaskItemStatus status, int projectId, CancellationToken cancellationToken)
        {
            var task = new TaskItem
            {
                Title = title,
                Description = description,
                Status = status,
                ProjectId = projectId,
                CreatedAt = DateTime.UtcNow
            };

            await Tasks.AddAsync(task, cancellationToken);
            await SaveChangesAsync(cancellationToken);

            return task;
        }

        /// <summary>
        /// Creates a default settings row for the specified user and persists it to the database.
        /// </summary>
        /// <param name="userId">The identifier of the user the settings belong to.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created <see cref="UserSetting"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when a settings row for the user already exists (primary key violation), or when <paramref name="userId"/> does not reference an existing <see cref="User"/> (foreign key violation).</exception>
        public async Task<UserSetting> CreateUserSettingAsync(int userId, CancellationToken cancellationToken)
        {
            var userSetting = new UserSetting
            {
                UserId = userId
            };

            await UserSettings.AddAsync(userSetting, cancellationToken);
            await SaveChangesAsync(cancellationToken);

            return userSetting;
        }

        /// <summary>
        /// Opens a new attendance session for the specified user by recording the clock-in time.
        /// </summary>
        /// <param name="userId">The identifier of the user clocking in.</param>
        /// <param name="clockIn">The clock-in timestamp. Its date component is used as the work date.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created <see cref="AttendanceLog"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when an attendance log for the same user and work date already exists (unique constraint violation), or when <paramref name="userId"/> does not reference an existing <see cref="User"/> (foreign key violation).</exception>
        public async Task<AttendanceLog> ClockInAsync(int userId, DateTime clockIn, CancellationToken cancellationToken)
        {
            var attendanceLog = new AttendanceLog
            {
                UserId = userId,
                WorkDate = clockIn.Date.ToString("yyyy-MM-dd"),
                ClockIn = clockIn
            };

            await AttendanceLogs.AddAsync(attendanceLog, cancellationToken);
            await SaveChangesAsync(cancellationToken);

            return attendanceLog;
        }

        /// <summary>
        /// Creates a completed attendance log for a past work date, setting both the clock-in and clock-out timestamps directly.
        /// Intended for backfilling attendance that was not recorded live (e.g., previous months).
        /// </summary>
        /// <param name="userId">The identifier of the user the attendance log belongs to.</param>
        /// <param name="workDate">The calendar work date being logged.</param>
        /// <param name="clockIn">The clock-in timestamp.</param>
        /// <param name="clockOut">The clock-out timestamp.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created <see cref="AttendanceLog"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when an attendance log for the same user and work date already exists (unique constraint violation), or when <paramref name="userId"/> does not reference an existing <see cref="User"/> (foreign key violation).</exception>
        public async Task<AttendanceLog> LogAttendanceAsync(int userId, DateTime workDate, DateTime clockIn, DateTime clockOut, CancellationToken cancellationToken)
        {
            var attendanceLog = new AttendanceLog
            {
                UserId = userId,
                WorkDate = workDate.Date.ToString("yyyy-MM-dd"),
                ClockIn = clockIn,
                ClockOut = clockOut
            };

            await AttendanceLogs.AddAsync(attendanceLog, cancellationToken);
            await SaveChangesAsync(cancellationToken);

            return attendanceLog;
        }

        /// <summary>
        /// Opens a new project time log for the specified attendance session, closing any currently open
        /// project time log for that session first. This encapsulates the continuous-timeline invariant:
        /// switching projects/tasks automatically stops the previously tracked segment.
        /// </summary>
        /// <param name="attendanceLogId">The identifier of the attendance session this segment belongs to.</param>
        /// <param name="projectId">The identifier of the project being tracked.</param>
        /// <param name="taskId">The optional identifier of the task being tracked within the project.</param>
        /// <param name="startTime">The timestamp this segment starts at, and the timestamp used to close the previously open segment, if any.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created <see cref="ProjectTimeLog"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when <paramref name="attendanceLogId"/>, <paramref name="projectId"/>, or <paramref name="taskId"/> does not reference an existing entity (foreign key violation).</exception>
        public async Task<ProjectTimeLog> StartProjectTimeLogAsync(int attendanceLogId, int projectId, int? taskId, DateTime startTime, CancellationToken cancellationToken)
        {
            var openProjectTimeLog = await GetOpenProjectTimeLogAsync(attendanceLogId, cancellationToken);
            if (openProjectTimeLog is not null)
            {
                openProjectTimeLog.EndTime = startTime;
            }

            var projectTimeLog = new ProjectTimeLog
            {
                AttendanceLogId = attendanceLogId,
                ProjectId = projectId,
                TaskId = taskId,
                StartTime = startTime
            };

            await ProjectTimeLogs.AddAsync(projectTimeLog, cancellationToken);
            await SaveChangesAsync(cancellationToken);

            return projectTimeLog;
        }

        /// <summary>
        /// Creates a completed project time log segment, setting both the start and end timestamps directly.
        /// Intended for backfilling project/task time that was not recorded live.
        /// </summary>
        /// <param name="attendanceLogId">The identifier of the attendance session this segment belongs to.</param>
        /// <param name="projectId">The identifier of the project being tracked.</param>
        /// <param name="taskId">The optional identifier of the task being tracked within the project.</param>
        /// <param name="startTime">The timestamp this segment starts at.</param>
        /// <param name="endTime">The timestamp this segment ends at.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The created <see cref="ProjectTimeLog"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when <paramref name="attendanceLogId"/>, <paramref name="projectId"/>, or <paramref name="taskId"/> does not reference an existing entity (foreign key violation).</exception>
        public async Task<ProjectTimeLog> LogProjectTimeAsync(int attendanceLogId, int projectId, int? taskId, DateTime startTime, DateTime endTime, CancellationToken cancellationToken)
        {
            var projectTimeLog = new ProjectTimeLog
            {
                AttendanceLogId = attendanceLogId,
                ProjectId = projectId,
                TaskId = taskId,
                StartTime = startTime,
                EndTime = endTime
            };

            await ProjectTimeLogs.AddAsync(projectTimeLog, cancellationToken);
            await SaveChangesAsync(cancellationToken);

            return projectTimeLog;
        }

        #endregion

        #region Read Methods

        /// <summary>
        /// Retrieves all users from the database.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list containing all <see cref="User"/> entities.</returns>
        public async Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken)
        {
            return await Users.ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Retrieves all projects owned by the specified user.
        /// </summary>
        /// <param name="userId">The identifier of the user whose projects should be retrieved.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list containing the <see cref="Project"/> entities owned by the user.</returns>
        public async Task<List<Project>> GetProjectsByUserIdAsync(int userId, CancellationToken cancellationToken)
        {
            return await Projects.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Retrieves all tasks belonging to the specified project.
        /// </summary>
        /// <param name="projectId">The identifier of the project whose tasks should be retrieved.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list containing the <see cref="TaskItem"/> entities belonging to the project.</returns>
        public async Task<List<TaskItem>> GetTasksByProjectIdAsync(int projectId, CancellationToken cancellationToken)
        {
            return await Tasks.Where(t => t.ProjectId == projectId).ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Retrieves the specified user's currently active (open) attendance session, if any.
        /// </summary>
        /// <param name="userId">The identifier of the user whose active attendance session should be retrieved.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The open <see cref="AttendanceLog"/> entity, or <see langword="null"/> if the user has no active session.</returns>
        public async Task<AttendanceLog?> GetActiveAttendanceLogAsync(int userId, CancellationToken cancellationToken)
        {
            return await GetOpenAttendanceLogAsync(userId, cancellationToken);
        }

        /// <summary>
        /// Retrieves the currently open project time log segment for the specified attendance
        /// session, with its <see cref="ProjectTimeLog.Project"/> and <see cref="ProjectTimeLog.Task"/>
        /// eagerly loaded - unlike the private open-segment lookup <see cref="StartProjectTimeLogAsync"/>
        /// and <see cref="EndProjectTimeLogAsync"/> use internally, which only need the segment's id.
        /// </summary>
        /// <param name="attendanceLogId">The identifier of the attendance session whose active tracking should be retrieved.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The open <see cref="ProjectTimeLog"/> entity, or <see langword="null"/> if nothing is currently being tracked.</returns>
        public async Task<ProjectTimeLog?> GetActiveProjectTimeLogAsync(int attendanceLogId, CancellationToken cancellationToken)
        {
            return await ProjectTimeLogs
                .Include(l => l.Project)
                .Include(l => l.Task)
                .Where(l => l.AttendanceLogId == attendanceLogId && l.EndTime == null)
                .OrderByDescending(l => l.StartTime)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Retrieves the settings belonging to the specified user.
        /// </summary>
        /// <param name="userId">The identifier of the user whose settings should be retrieved.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The <see cref="UserSetting"/> entity, or <see langword="null"/> if the user has no settings configured.</returns>
        public async Task<UserSetting?> GetUserSettingAsync(int userId, CancellationToken cancellationToken)
        {
            return await UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
        }

        /// <summary>
        /// Retrieves all of the specified user's attendance logs whose clock-in falls within the given period.
        /// Intended as raw input for <see cref="TimeBudgetCalculator"/> rolling budget calculations.
        /// </summary>
        /// <param name="userId">The identifier of the user whose attendance logs should be retrieved.</param>
        /// <param name="from">The inclusive start of the period.</param>
        /// <param name="to">The inclusive end of the period.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list containing the matching <see cref="AttendanceLog"/> entities.</returns>
        public async Task<List<AttendanceLog>> GetAttendanceLogsAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            return await AttendanceLogs
                .Where(a => a.UserId == userId && a.ClockIn >= from && a.ClockIn <= to)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Retrieves all of the specified user's project time log segments whose start time falls within the given period.
        /// Intended as raw input for <see cref="TimeBudgetCalculator"/> per-project/per-task time summaries.
        /// </summary>
        /// <param name="userId">The identifier of the user whose project time logs should be retrieved.</param>
        /// <param name="from">The inclusive start of the period.</param>
        /// <param name="to">The inclusive end of the period.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>A list containing the matching <see cref="ProjectTimeLog"/> entities.</returns>
        public async Task<List<ProjectTimeLog>> GetProjectTimeLogsAsync(int userId, DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            return await ProjectTimeLogs
                .Where(l => l.AttendanceLog.UserId == userId && l.StartTime >= from && l.StartTime <= to)
                .ToListAsync(cancellationToken);
        }

        #endregion

        #region Update Methods

        /// <summary>
        /// Updates an existing user in the database.
        /// </summary>
        /// <param name="user">The <see cref="User"/> entity with updated values.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated <see cref="User"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when <see cref="User.Email"/> violates the unique constraint, or when <paramref name="user"/> does not exist in the database.</exception>
        /// <exception cref="DbUpdateConcurrencyException">Thrown when the user was deleted or modified concurrently.</exception>
        public async Task<User> UpdateUserAsync(User user, CancellationToken cancellationToken)
        {
            Users.Update(user);
            await SaveChangesAsync(cancellationToken);

            return user;
        }

        /// <summary>
        /// Updates an existing project in the database.
        /// </summary>
        /// <param name="project">The <see cref="Project"/> entity with updated values.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated <see cref="Project"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when <see cref="Project.Name"/> violates the unique constraint, or when <see cref="Project.UserId"/> does not reference an existing <see cref="User"/> (foreign key violation).</exception>
        /// <exception cref="DbUpdateConcurrencyException">Thrown when the project was deleted or modified concurrently.</exception>
        public async Task<Project> UpdateProjectAsync(Project project, CancellationToken cancellationToken)
        {
            Projects.Update(project);
            await SaveChangesAsync(cancellationToken);

            return project;
        }

        /// <summary>
        /// Updates an existing task in the database.
        /// </summary>
        /// <param name="task">The <see cref="TaskItem"/> entity with updated values.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated <see cref="TaskItem"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when <see cref="TaskItem.ProjectId"/> does not reference an existing <see cref="Project"/> (foreign key violation).</exception>
        /// <exception cref="DbUpdateConcurrencyException">Thrown when the task was deleted or modified concurrently.</exception>
        public async Task<TaskItem> UpdateTaskAsync(TaskItem task, CancellationToken cancellationToken)
        {
            Tasks.Update(task);
            await SaveChangesAsync(cancellationToken);

            return task;
        }

        /// <summary>
        /// Updates an existing user's settings in the database.
        /// </summary>
        /// <param name="userSetting">The <see cref="UserSetting"/> entity with updated values.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated <see cref="UserSetting"/> entity.</returns>
        /// <exception cref="DbUpdateException">Thrown when <paramref name="userSetting"/> does not exist in the database.</exception>
        /// <exception cref="DbUpdateConcurrencyException">Thrown when the user setting was deleted or modified concurrently.</exception>
        public async Task<UserSetting> UpdateUserSettingAsync(UserSetting userSetting, CancellationToken cancellationToken)
        {
            UserSettings.Update(userSetting);
            await SaveChangesAsync(cancellationToken);

            return userSetting;
        }

        /// <summary>
        /// Closes the specified user's currently open attendance session by recording the clock-out time.
        /// The open session is resolved automatically as the most recent <see cref="AttendanceLog"/> for the user with a null <see cref="AttendanceLog.ClockOut"/>.
        /// </summary>
        /// <param name="userId">The identifier of the user clocking out.</param>
        /// <param name="clockOut">The clock-out timestamp.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated <see cref="AttendanceLog"/> entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the user has no open attendance session (no prior <see cref="ClockInAsync"/> without a matching clock-out).</exception>
        public async Task<AttendanceLog> ClockOutAsync(int userId, DateTime clockOut, CancellationToken cancellationToken)
        {
            var openAttendanceLog = await GetOpenAttendanceLogAsync(userId, cancellationToken);
            if (openAttendanceLog is null)
            {
                throw new InvalidOperationException($"No open attendance session found for user {userId}.");
            }

            openAttendanceLog.ClockOut = clockOut;
            await SaveChangesAsync(cancellationToken);

            return openAttendanceLog;
        }

        /// <summary>
        /// Closes the currently open project time log segment for the specified attendance session.
        /// </summary>
        /// <param name="attendanceLogId">The identifier of the attendance session whose open segment should be closed.</param>
        /// <param name="endTime">The timestamp this segment ends at.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The updated <see cref="ProjectTimeLog"/> entity.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the attendance session has no open project time log segment.</exception>
        public async Task<ProjectTimeLog> EndProjectTimeLogAsync(int attendanceLogId, DateTime endTime, CancellationToken cancellationToken)
        {
            var openProjectTimeLog = await GetOpenProjectTimeLogAsync(attendanceLogId, cancellationToken);
            if (openProjectTimeLog is null)
            {
                throw new InvalidOperationException($"No open project time log found for attendance log {attendanceLogId}.");
            }

            openProjectTimeLog.EndTime = endTime;
            await SaveChangesAsync(cancellationToken);

            return openProjectTimeLog;
        }

        #endregion

        #region Delete Methods

        /// <summary>
        /// Deletes the specified user and relies on the database's cascade configuration to remove
        /// their settings, projects, and attendance logs.
        /// </summary>
        /// <param name="userId">The identifier of the user to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns><see langword="true"/> if the user was found and deleted; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> DeleteUserAsync(int userId, CancellationToken cancellationToken)
        {
            var user = await Users.FindAsync(new object?[] { userId }, cancellationToken);
            if (user is null)
            {
                return false;
            }

            Users.Remove(user);
            await SaveChangesAsync(cancellationToken);

            return true;
        }

        /// <summary>
        /// Deletes the specified project and relies on the database's cascade configuration to remove
        /// its tasks and project time logs.
        /// </summary>
        /// <param name="projectId">The identifier of the project to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns><see langword="true"/> if the project was found and deleted; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> DeleteProjectAsync(int projectId, CancellationToken cancellationToken)
        {
            var project = await Projects.FindAsync(new object?[] { projectId }, cancellationToken);
            if (project is null)
            {
                return false;
            }

            Projects.Remove(project);
            await SaveChangesAsync(cancellationToken);

            return true;
        }

        /// <summary>
        /// Deletes the specified task. Any <see cref="ProjectTimeLog"/> referencing it has its
        /// <see cref="ProjectTimeLog.TaskId"/> set to <see langword="null"/> by the database's cascade configuration.
        /// </summary>
        /// <param name="taskId">The identifier of the task to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns><see langword="true"/> if the task was found and deleted; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> DeleteTaskAsync(int taskId, CancellationToken cancellationToken)
        {
            var task = await Tasks.FindAsync(new object?[] { taskId }, cancellationToken);
            if (task is null)
            {
                return false;
            }

            Tasks.Remove(task);
            await SaveChangesAsync(cancellationToken);

            return true;
        }

        /// <summary>
        /// Deletes the specified attendance log and relies on the database's cascade configuration to remove
        /// its project time logs.
        /// </summary>
        /// <param name="attendanceLogId">The identifier of the attendance log to delete.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns><see langword="true"/> if the attendance log was found and deleted; otherwise, <see langword="false"/>.</returns>
        public async Task<bool> DeleteAttendanceLogAsync(int attendanceLogId, CancellationToken cancellationToken)
        {
            var attendanceLog = await AttendanceLogs.FindAsync(new object?[] { attendanceLogId }, cancellationToken);
            if (attendanceLog is null)
            {
                return false;
            }

            AttendanceLogs.Remove(attendanceLog);
            await SaveChangesAsync(cancellationToken);

            return true;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Finds the specified user's currently open attendance session, i.e. the most recent
        /// <see cref="AttendanceLog"/> for the user with a null <see cref="AttendanceLog.ClockOut"/>.
        /// </summary>
        /// <param name="userId">The identifier of the user whose open attendance session should be found.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The open <see cref="AttendanceLog"/> entity, or <see langword="null"/> if none exists.</returns>
        private async Task<AttendanceLog?> GetOpenAttendanceLogAsync(int userId, CancellationToken cancellationToken)
        {
            return await AttendanceLogs
                .Where(a => a.UserId == userId && a.ClockOut == null)
                .OrderByDescending(a => a.ClockIn)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Finds the currently open project time log segment for the specified attendance session, i.e. the
        /// most recent <see cref="ProjectTimeLog"/> for that session with a null <see cref="ProjectTimeLog.EndTime"/>.
        /// </summary>
        /// <param name="attendanceLogId">The identifier of the attendance session whose open segment should be found.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to observe while waiting for the task to complete.</param>
        /// <returns>The open <see cref="ProjectTimeLog"/> entity, or <see langword="null"/> if none exists.</returns>
        private async Task<ProjectTimeLog?> GetOpenProjectTimeLogAsync(int attendanceLogId, CancellationToken cancellationToken)
        {
            return await ProjectTimeLogs
                .Where(l => l.AttendanceLogId == attendanceLogId && l.EndTime == null)
                .OrderByDescending(l => l.StartTime)
                .FirstOrDefaultAsync(cancellationToken);
        }

        #endregion
    }
}
