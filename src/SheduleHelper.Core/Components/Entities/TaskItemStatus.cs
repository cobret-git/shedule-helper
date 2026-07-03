namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Defines the lifecycle status of a <see cref="TaskItem"/>.
    /// </summary>
    public enum TaskItemStatus
    {
        /// <summary>
        /// The task has not been started yet.
        /// </summary>
        Todo = 0,

        /// <summary>
        /// The task is currently being worked on.
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// The task has been completed.
        /// </summary>
        Done = 2
    }
}
