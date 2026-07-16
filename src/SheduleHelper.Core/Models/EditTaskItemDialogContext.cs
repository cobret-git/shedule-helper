using SheduleHelper.Core.Components.Entities;

namespace SheduleHelper.Core.Models
{
    /// <summary>
    /// Context passed to the Edit Task Item dialog: the task being edited (or <see langword="null"/>
    /// to create a new one) together with the project it belongs to, so the dialog knows which
    /// project to assign the task to without allowing that project to be edited.
    /// </summary>
    public class EditTaskItemDialogContext
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EditTaskItemDialogContext"/> class.
        /// </summary>
        /// <param name="project">The project the task belongs to (or will be created under).</param>
        /// <param name="taskItem">The task to edit, or <see langword="null"/> to create a new one.</param>
        public EditTaskItemDialogContext(Project project, TaskItem? taskItem = null)
        {
            Project = project;
            TaskItem = taskItem;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The project the task belongs to (or will be created under).
        /// </summary>
        public Project Project { get; }

        /// <summary>
        /// The task to edit, or <see langword="null"/> to create a new one.
        /// </summary>
        public TaskItem? TaskItem { get; }

        #endregion
    }
}
