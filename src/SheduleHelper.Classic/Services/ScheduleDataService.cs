using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using SheduleHelper.WpfApp.Components.Data;
using SheduleHelper.WpfApp.Components.Entities;
using SheduleHelper.WpfApp.Model;
using System.IO.Abstractions;

namespace SheduleHelper.WpfApp.Services
{
    /// <summary>
    /// Provides database operations for managing tasks, projects, and task types.
    /// Uses Entity Framework Core with SQLite for local data persistence.
    /// </summary>
    public class ScheduleDataService : IScheduleDataService
    {
        #region Fields

        private readonly IFileSystem _fileSystem;
        private readonly DatabaseConfiguration _configuration;
        private readonly IDbContextFactory<LocalDbContext> _contextFactory;
        private bool _disposed;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduleDataService"/> class.
        /// </summary>
        /// <param name="fileSystem">The file system abstraction for file operations.</param>
        /// <param name="configuration">The database configuration settings.</param>
        /// <param name="contextFactory">The factory for creating database contexts.</param>
        public ScheduleDataService(
            IFileSystem fileSystem,
            DatabaseConfiguration configuration,
            IDbContextFactory<LocalDbContext> contextFactory)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        #endregion

        #region Initialization

        /// <inheritdoc/>
        public async Task<Result> InitializeDatabaseAsync()
        {
            try
            {
                // Ensure database directory exists
                var dbDirectory = _configuration.DatabasePath;
                if (!_fileSystem.Directory.Exists(dbDirectory))
                {
                    _fileSystem.Directory.CreateDirectory(dbDirectory);
                    Log.Information("Created database directory: {Path}", dbDirectory);
                }

                // Apply migrations
                using var context = _contextFactory.CreateDbContext();
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
                var migrationsList = pendingMigrations.ToList();

                if (migrationsList.Any())
                {
                    Log.Information("Applying {Count} pending migrations", migrationsList.Count);
                    await context.Database.MigrateAsync();
                }

                Log.Information("Database initialized successfully at {Path}", _configuration.GetFullDatabasePath());
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize database");
                return Result.Failure($"Database initialization failed: {ex.Message}");
            }
        }

        #endregion

        #region TaskItem Operations

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<TaskItem>>> GetAllTasksAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var tasks = await context.TaskItems
                    .Include(t => t.TaskType)
                    .Include(t => t.Project)
                    .Include(t => t.ParentTask)
                    .ToListAsync();

                return Result<IEnumerable<TaskItem>>.Success(tasks);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve all tasks");
                return Result<IEnumerable<TaskItem>>.Failure($"Failed to retrieve tasks: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<TaskItem>> GetTaskByIdAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var task = await context.TaskItems
                    .Include(t => t.TaskType)
                    .Include(t => t.Project)
                    .Include(t => t.ParentTask)
                    .Include(t => t.SubTasks)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (task == null)
                {
                    return Result<TaskItem>.Failure($"Task with ID {id} not found");
                }

                return Result<TaskItem>.Success(task);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve task {TaskId}", id);
                return Result<TaskItem>.Failure($"Failed to retrieve task: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<TaskItem>>> GetTasksByProjectIdAsync(int projectId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var tasks = await context.TaskItems
                    .Include(t => t.TaskType)
                    .Include(t => t.ParentTask)
                    .Where(t => t.ProjectId == projectId)
                    .ToListAsync();

                return Result<IEnumerable<TaskItem>>.Success(tasks);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve tasks for project {ProjectId}", projectId);
                return Result<IEnumerable<TaskItem>>.Failure($"Failed to retrieve tasks: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<TaskItem>>> GetSubTasksAsync(int parentTaskId)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var subTasks = await context.TaskItems
                    .Include(t => t.TaskType)
                    .Where(t => t.ParentTaskId == parentTaskId)
                    .ToListAsync();

                return Result<IEnumerable<TaskItem>>.Success(subTasks);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve subtasks for task {ParentTaskId}", parentTaskId);
                return Result<IEnumerable<TaskItem>>.Failure($"Failed to retrieve subtasks: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<int>> CreateTaskAsync(TaskItem task)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                task.CreatedAt = DateTime.UtcNow;
                task.UpdatedAt = null;

                context.TaskItems.Add(task);
                await context.SaveChangesAsync();

                Log.Information("Task created: {TaskId} - {Title}", task.Id, task.Title);
                return Result<int>.Success(task.Id);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create task: {Title}", task.Title);
                return Result<int>.Failure($"Failed to create task: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result> UpdateTaskAsync(TaskItem task)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var existing = await context.TaskItems.FindAsync(task.Id);
                if (existing == null)
                {
                    return Result.Failure($"Task with ID {task.Id} not found");
                }

                context.Entry(existing).CurrentValues.SetValues(task);
                await context.SaveChangesAsync();

                Log.Information("Task updated: {TaskId}", task.Id);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update task {TaskId}", task.Id);
                return Result.Failure($"Failed to update task: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result> DeleteTaskAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var task = await context.TaskItems.FindAsync(id);
                if (task == null)
                {
                    return Result.Failure($"Task with ID {id} not found");
                }

                context.TaskItems.Remove(task);
                await context.SaveChangesAsync();

                Log.Information("Task deleted: {TaskId}", id);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete task {TaskId}", id);
                return Result.Failure($"Failed to delete task: {ex.Message}");
            }
        }

        #endregion

        #region Project Operations

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<Project>>> GetAllProjectsAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var projects = await context.Projects
                    .Include(p => p.DefaultTaskType)
                    .ToListAsync();

                return Result<IEnumerable<Project>>.Success(projects);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve all projects");
                return Result<IEnumerable<Project>>.Failure($"Failed to retrieve projects: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<Project>> GetProjectByIdAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var project = await context.Projects
                    .Include(p => p.DefaultTaskType)
                    .Include(p => p.Tasks)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (project == null)
                {
                    return Result<Project>.Failure($"Project with ID {id} not found");
                }

                return Result<Project>.Success(project);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve project {ProjectId}", id);
                return Result<Project>.Failure($"Failed to retrieve project: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<int>> CreateProjectAsync(Project project)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check for duplicate name
                var nameExists = await context.Projects
                    .AnyAsync(p => p.Name == project.Name);
                if (nameExists)
                {
                    Log.Warning("Project creation blocked: Name '{Name}' already exists", project.Name);
                    return Result<int>.Failure($"Project with name '{project.Name}' already exists");
                }

                project.CreatedAt = DateTime.UtcNow;
                project.UpdatedAt = null;

                context.Projects.Add(project);
                await context.SaveChangesAsync();

                Log.Information("Project created: {ProjectId} - {Name}", project.Id, project.Name);
                return Result<int>.Success(project.Id);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
            {
                Log.Warning("Project creation blocked: Unique constraint violation for '{Name}'", project.Name);
                return Result<int>.Failure($"Project with name '{project.Name}' already exists");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create project: {Name}", project.Name);
                return Result<int>.Failure($"Failed to create project: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result> UpdateProjectAsync(Project project)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var existing = await context.Projects.FindAsync(project.Id);
                if (existing == null)
                {
                    return Result.Failure($"Project with ID {project.Id} not found");
                }

                // Check for duplicate name (excluding current project)
                var nameExists = await context.Projects
                    .AnyAsync(p => p.Name == project.Name && p.Id != project.Id);
                if (nameExists)
                {
                    Log.Warning("Project update blocked: Name '{Name}' already exists", project.Name);
                    return Result.Failure($"Project with name '{project.Name}' already exists");
                }

                context.Entry(existing).CurrentValues.SetValues(project);
                await context.SaveChangesAsync();

                Log.Information("Project updated: {ProjectId}", project.Id);
                return Result.Success();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
            {
                Log.Warning("Project update blocked: Unique constraint violation for '{Name}'", project.Name);
                return Result.Failure($"Project with name '{project.Name}' already exists");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update project {ProjectId}", project.Id);
                return Result.Failure($"Failed to update project: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result> DeleteProjectAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var project = await context.Projects.FindAsync(id);
                if (project == null)
                {
                    return Result.Failure($"Project with ID {id} not found");
                }

                context.Projects.Remove(project);
                await context.SaveChangesAsync();

                Log.Information("Project deleted: {ProjectId}", id);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete project {ProjectId}", id);
                return Result.Failure($"Failed to delete project: {ex.Message}");
            }
        }

        #endregion

        #region TaskType Operations

        /// <inheritdoc/>
        public async Task<Result<IEnumerable<TaskType>>> GetAllTaskTypesAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var taskTypes = await context.TaskTypes
                    .Include(t => t.ParentType)
                    .Include(t => t.ChildTypes)
                    .ToListAsync();

                return Result<IEnumerable<TaskType>>.Success(taskTypes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve all task types");
                return Result<IEnumerable<TaskType>>.Failure($"Failed to retrieve task types: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<TaskType>> GetTaskTypeByIdAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                var taskType = await context.TaskTypes
                    .Include(t => t.ParentType)
                    .Include(t => t.ChildTypes)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (taskType == null)
                {
                    return Result<TaskType>.Failure($"Task type with ID {id} not found");
                }

                return Result<TaskType>.Success(taskType);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to retrieve task type {TaskTypeId}", id);
                return Result<TaskType>.Failure($"Failed to retrieve task type: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result<int>> CreateTaskTypeAsync(TaskType taskType)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Validate
                var validationResult = await ValidateTaskTypeAsync(taskType, context);
                if (!validationResult.IsSuccess)
                {
                    return Result<int>.Failure(validationResult.ErrorMessage!);
                }

                taskType.CreatedAt = DateTime.UtcNow;
                taskType.UpdatedAt = null;

                context.TaskTypes.Add(taskType);
                await context.SaveChangesAsync();

                Log.Information("Task type created: {TaskTypeId} - {Name}", taskType.Id, taskType.Name);
                return Result<int>.Success(taskType.Id);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
            {
                Log.Warning("Task type creation blocked: Name '{Name}' already exists", taskType.Name);
                return Result<int>.Failure($"Task type with name '{taskType.Name}' already exists");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create task type: {Name}", taskType.Name);
                return Result<int>.Failure($"Failed to create task type: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result> UpdateTaskTypeAsync(TaskType taskType)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var existing = await context.TaskTypes.FindAsync(taskType.Id);
                if (existing == null)
                {
                    return Result.Failure($"Task type with ID {taskType.Id} not found");
                }

                // Validate
                var validationResult = await ValidateTaskTypeAsync(taskType, context);
                if (!validationResult.IsSuccess)
                {
                    return validationResult;
                }

                context.Entry(existing).CurrentValues.SetValues(taskType);
                await context.SaveChangesAsync();

                Log.Information("Task type updated: {TaskTypeId}", taskType.Id);
                return Result.Success();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
            {
                Log.Warning("Task type update blocked: Name '{Name}' already exists", taskType.Name);
                return Result.Failure($"Task type with name '{taskType.Name}' already exists");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update task type {TaskTypeId}", taskType.Id);
                return Result.Failure($"Failed to update task type: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result> DeleteTaskTypeAsync(int id)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                var taskType = await context.TaskTypes.FindAsync(id);
                if (taskType == null)
                {
                    return Result.Failure($"Task type with ID {id} not found");
                }

                // Check if has child types
                var hasChildren = await context.TaskTypes.AnyAsync(t => t.ParentTypeId == id);
                if (hasChildren)
                {
                    Log.Warning("Task type deletion blocked: {TaskTypeId} has child types", id);
                    return Result.Failure("Cannot delete task type with child types");
                }

                context.TaskTypes.Remove(taskType);
                await context.SaveChangesAsync();

                Log.Information("Task type deleted: {TaskTypeId}", id);
                return Result.Success();
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqliteEx && sqliteEx.SqliteErrorCode == 19)
            {
                Log.Warning("Task type deletion blocked: {TaskTypeId} has child types", id);
                return Result.Failure("Cannot delete task type with child types");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to delete task type {TaskTypeId}", id);
                return Result.Failure($"Failed to delete task type: {ex.Message}");
            }
        }

        #endregion

        #region Maintenance

        /// <inheritdoc/>
        public async Task<Result<string>> BackupDatabaseAsync(string backupPath)
        {
            try
            {
                var sourcePath = _configuration.GetFullDatabasePath();

                if (!_fileSystem.File.Exists(sourcePath))
                {
                    return Result<string>.Failure("Database file does not exist");
                }

                // Ensure backup directory exists
                var backupDirectory = _fileSystem.Path.GetDirectoryName(backupPath);
                if (!string.IsNullOrEmpty(backupDirectory) && !_fileSystem.Directory.Exists(backupDirectory))
                {
                    _fileSystem.Directory.CreateDirectory(backupDirectory);
                }

                // Copy database file
                await Task.Run(() => _fileSystem.File.Copy(sourcePath, backupPath, overwrite: true));

                Log.Information("Database backup created: {BackupPath}", backupPath);
                return Result<string>.Success(backupPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create database backup");
                return Result<string>.Failure($"Failed to create backup: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result> RestoreDatabaseAsync(string backupPath)
        {
            try
            {
                if (!_fileSystem.File.Exists(backupPath))
                {
                    return Result.Failure("Backup file does not exist");
                }

                var targetPath = _configuration.GetFullDatabasePath();

                // Copy backup to database location
                await Task.Run(() => _fileSystem.File.Copy(backupPath, targetPath, overwrite: true));

                Log.Information("Database restored from backup: {BackupPath}", backupPath);
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to restore database from backup");
                return Result.Failure($"Failed to restore database: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<Result> VacuumDatabaseAsync()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();
                await context.Database.ExecuteSqlRawAsync("VACUUM;");

                Log.Information("Database vacuumed successfully");
                return Result.Success();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to vacuum database");
                return Result.Failure($"Failed to vacuum database: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Validates task type for creation or update.
        /// Checks for unique name and circular references.
        /// </summary>
        private async Task<Result> ValidateTaskTypeAsync(TaskType taskType, LocalDbContext context)
        {
            // Unique name check
            var nameExists = await context.TaskTypes
                .AnyAsync(t => t.Name == taskType.Name && t.Id != taskType.Id);
            if (nameExists)
            {
                Log.Warning("Task type validation failed: Name '{Name}' already exists", taskType.Name);
                return Result.Failure($"Task type with name '{taskType.Name}' already exists");
            }

            // Circular reference check
            if (taskType.ParentTypeId.HasValue)
            {
                if (await HasCircularReferenceAsync(taskType, context))
                {
                    Log.Warning("Task type validation failed: Circular reference detected for '{Name}'", taskType.Name);
                    return Result.Failure("Circular reference detected in task type hierarchy");
                }
            }

            return Result.Success();
        }

        /// <summary>
        /// Checks if a task type has a circular reference in its parent hierarchy.
        /// </summary>
        private async Task<bool> HasCircularReferenceAsync(TaskType taskType, LocalDbContext context)
        {
            var currentParentId = taskType.ParentTypeId;
            var visited = new HashSet<int> { taskType.Id };

            while (currentParentId.HasValue)
            {
                if (!visited.Add(currentParentId.Value))
                {
                    return true; // Circular reference found
                }

                var parent = await context.TaskTypes.FindAsync(currentParentId.Value);
                if (parent == null)
                {
                    break;
                }

                currentParentId = parent.ParentTypeId;
            }

            return false;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases resources used by the service.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        #endregion
    }
}
