using SheduleHelper.Core.Components.Entities;
using System.Threading.Tasks;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Shows modal dialogs and exposes a way to close whichever one is currently open. Deliberately
    /// platform-agnostic - it exposes only the semantic dialogs a caller can show, none of the
    /// hosting UI framework's own dialog types (e.g. WinUI's <c>ContentDialog</c>). Concrete
    /// implementations live in each host application.
    /// </summary>
    public interface IDialogService
    {
        #region Methods

        /// <summary>
        /// Shows the Edit Project dialog.
        /// </summary>
        /// <param name="existingProject">The project to edit, or <see langword="null"/> to create a new one.</param>
        /// <returns>The created/edited project, or <see langword="null"/> if the dialog was dismissed without confirming.</returns>
        Task<Project?> ShowEditProjectDialogAsync(Project? existingProject);

        Task<TaskItem?> ShowEditTaskItemDialogAsync(TaskItem? existingTaskItem);

        /// <summary>
        /// Closes the dialog currently being shown, if any.
        /// </summary>
        void CloseDialog();

        #endregion
    }
}
