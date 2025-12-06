using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SheduleHelper.WpfApp.Components.Data;
using SheduleHelper.WpfApp.Components.Entities;

namespace SheduleHelper.WpfApp.Services
{
    /// <summary>
    /// Defines the contract for database operations related to tasks, projects, and task types.
    /// Manages all CRUD operations, database initialization, and maintenance.
    /// </summary>
    public interface IScheduleDataService : IDisposable
    {
        #region Initialization

        /// <summary>
        /// Initializes the database by creating the directory, applying migrations, and seeding initial data.
        /// Should be called once at application startup.
        /// </summary>
        /// <returns>A result indicating success or failure of initialization.</returns>
        Task<Result> InitializeDatabaseAsync();

        #endregion

        #region TaskItem Operations

        /// <summary>
        /// Gets all non-deleted tasks from the database.
        /// </summary>
        /// <returns>A result containing the collection of tasks or an error.</returns>
        Task<Result<IEnumerable<TaskItem>>> GetAllTasksAsync();

        /// <summary>
        /// Gets a task by its unique identifier.
        /// </summary>
        /// <param name="id">The task identifier.</param>
        /// <returns>A result containing the task or an error if not found.</returns>
        Task<Result<TaskItem>> GetTaskByIdAsync(int id);

        /// <summary>
        /// Gets all tasks belonging to a specific project.
        /// </summary>
        /// <param name="projectId">The project identifier.</param>
        /// <returns>A result containing the collection of tasks or an error.</returns>
        Task<Result<IEnumerable<TaskItem>>> GetTasksByProjectIdAsync(int projectId);

        /// <summary>
        /// Gets all subtasks for a given parent task.
        /// </summary>
        /// <param name="parentTaskId">The parent task identifier.</param>
        /// <returns>A result containing the collection of subtasks or an error.</returns>
        Task<Result<IEnumerable<TaskItem>>> GetSubTasksAsync(int parentTaskId);

        /// <summary>
        /// Creates a new task in the database.
        /// </summary>
        /// <param name="task">The task to create.</param>
        /// <returns>A result containing the created task ID or an error.</returns>
        Task<Result<int>> CreateTaskAsync(TaskItem task);

        /// <summary>
        /// Updates an existing task in the database.
        /// </summary>
        /// <param name="task">The task with updated values.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> UpdateTaskAsync(TaskItem task);

        /// <summary>
        /// Deletes a task from the database.
        /// If the task has subtasks, they will be cascade deleted.
        /// </summary>
        /// <param name="id">The task identifier.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> DeleteTaskAsync(int id);

        #endregion

        #region Project Operations

        /// <summary>
        /// Gets all projects from the database.
        /// </summary>
        /// <returns>A result containing the collection of projects or an error.</returns>
        Task<Result<IEnumerable<Project>>> GetAllProjectsAsync();

        /// <summary>
        /// Gets a project by its unique identifier.
        /// </summary>
        /// <param name="id">The project identifier.</param>
        /// <returns>A result containing the project or an error if not found.</returns>
        Task<Result<Project>> GetProjectByIdAsync(int id);

        /// <summary>
        /// Creates a new project in the database.
        /// </summary>
        /// <param name="project">The project to create.</param>
        /// <returns>A result containing the created project ID or an error.</returns>
        Task<Result<int>> CreateProjectAsync(Project project);

        /// <summary>
        /// Updates an existing project in the database.
        /// </summary>
        /// <param name="project">The project with updated values.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> UpdateProjectAsync(Project project);

        /// <summary>
        /// Deletes a project from the database.
        /// Sets all related task ProjectId references to NULL.
        /// </summary>
        /// <param name="id">The project identifier.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> DeleteProjectAsync(int id);

        #endregion

        #region TaskType Operations

        /// <summary>
        /// Gets all task types from the database.
        /// </summary>
        /// <returns>A result containing the collection of task types or an error.</returns>
        Task<Result<IEnumerable<TaskType>>> GetAllTaskTypesAsync();

        /// <summary>
        /// Gets a task type by its unique identifier.
        /// </summary>
        /// <param name="id">The task type identifier.</param>
        /// <returns>A result containing the task type or an error if not found.</returns>
        Task<Result<TaskType>> GetTaskTypeByIdAsync(int id);

        /// <summary>
        /// Creates a new task type in the database.
        /// </summary>
        /// <param name="taskType">The task type to create.</param>
        /// <returns>A result containing the created task type ID or an error.</returns>
        Task<Result<int>> CreateTaskTypeAsync(TaskType taskType);

        /// <summary>
        /// Updates an existing task type in the database.
        /// </summary>
        /// <param name="taskType">The task type with updated values.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> UpdateTaskTypeAsync(TaskType taskType);

        /// <summary>
        /// Deletes a task type from the database.
        /// Fails if the type has child types or is in use by tasks.
        /// Sets all related task TaskTypeId references to NULL.
        /// </summary>
        /// <param name="id">The task type identifier.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> DeleteTaskTypeAsync(int id);

        #endregion

        #region Maintenance

        /// <summary>
        /// Creates a backup copy of the database file.
        /// </summary>
        /// <param name="backupPath">The destination path for the backup file.</param>
        /// <returns>A result containing the backup file path or an error.</returns>
        Task<Result<string>> BackupDatabaseAsync(string backupPath);

        /// <summary>
        /// Restores the database from a backup file.
        /// WARNING: This will overwrite the current database.
        /// </summary>
        /// <param name="backupPath">The path to the backup file.</param>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> RestoreDatabaseAsync(string backupPath);

        /// <summary>
        /// Executes VACUUM command to reclaim unused space and optimize the database.
        /// </summary>
        /// <returns>A result indicating success or failure.</returns>
        Task<Result> VacuumDatabaseAsync();

        #endregion
    }
}
