namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Why a <see cref="ProjectTimeLog"/> segment stopped. Recorded because it is the difference
    /// between "I was working on this and the day ended" and "I deliberately stopped working on
    /// this" - which is exactly what
    /// <see cref="Services.ITrackingService.ResumeLastAsync"/> needs in order to know whether
    /// tomorrow's clock-in should pick the same project back up.
    /// </summary>
    public enum TimeLogCloseReason
    {
        /// <summary>
        /// Closed because tracking moved to a different project/task. The new segment is the
        /// continuation intent, so this one is no longer a resume candidate in its own right.
        /// </summary>
        Switched = 0,

        /// <summary>
        /// Closed because the user clocked out with this segment still running. The work isn't
        /// finished, it just ran out of day - this is the reason that makes a segment resumable.
        /// </summary>
        ClockedOut = 1,

        /// <summary>
        /// Closed because the user explicitly stopped tracking without starting anything else.
        /// Deliberate, so it suppresses resumption.
        /// </summary>
        Stopped = 2
    }
}
