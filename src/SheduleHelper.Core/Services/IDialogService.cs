using SheduleHelper.Core.Components.Entities;
using SheduleHelper.Core.Models;
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

        /// <summary>
        /// Shows the Edit Task Item dialog.
        /// </summary>
        /// <param name="context">The task to edit (or <see langword="null"/> to create a new one), together with the project it belongs to.</param>
        /// <returns>The created/edited task, or <see langword="null"/> if the dialog was dismissed without confirming.</returns>
        Task<TaskItem?> ShowEditTaskItemDialogAsync(EditTaskItemDialogContext context);

        /// <summary>
        /// Shows a simple informational dialog with a single close button - e.g. to tell the user a
        /// setting (like the display language) needs an app restart to take effect.
        /// </summary>
        /// <param name="title">The dialog's title.</param>
        /// <param name="message">The dialog's body text.</param>
        Task ShowMessageDialogAsync(string title, string message);

        /// <summary>
        /// Closes the dialog currently being shown, if any.
        /// </summary>
        void CloseDialog();

        #endregion
    }
}
