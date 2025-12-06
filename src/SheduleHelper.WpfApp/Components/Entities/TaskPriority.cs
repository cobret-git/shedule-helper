namespace SheduleHelper.WpfApp.Components.Entities
{
    /// <summary>
    /// Defines the priority levels for tasks.
    /// </summary>
    public enum TaskPriority
    {
        /// <summary>
        /// Low priority task - can be delayed without significant impact.
        /// </summary>
        Low = 0,

        /// <summary>
        /// Normal priority task - standard work items.
        /// </summary>
        Normal = 1,

        /// <summary>
        /// High priority task - should be addressed soon.
        /// </summary>
        High = 2,

        /// <summary>
        /// Critical priority task - requires immediate attention.
        /// </summary>
        Critical = 3
    }
}
