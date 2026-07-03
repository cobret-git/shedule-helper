namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Defines how a user's daily lunch break is deducted from their attendance time.
    /// </summary>
    public enum LunchStrategy
    {
        /// <summary>
        /// No automatic lunch deduction is applied.
        /// </summary>
        None = 0,

        /// <summary>
        /// Deduct a fixed window (<see cref="UserSetting.LunchStartTime"/> to <see cref="UserSetting.LunchEndTime"/>)
        /// when the attendance session spans it.
        /// </summary>
        FixedWindow = 1,

        /// <summary>
        /// Deduct a fixed duration (<see cref="UserSetting.LunchDurationMinutes"/>) once attendance exceeds a threshold.
        /// </summary>
        DurationBased = 2
    }
}
