using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Represents configuration settings for a specific user.
    /// Maps to the 'UserSettings' table.
    /// </summary>
    [Table("UserSettings")]
    public class UserSetting
    {
        #region Properties

        /// <summary>
        /// Foreign Key and Primary Key linking directly to the associated User. Maps to 'UserId'.
        /// </summary>
        [Key]
        [Column("UserId")]
        public int UserId { get; set; }

        /// <summary>
        /// Daily target shift hours (e.g., 8.00). Maps to 'TargetShiftHours'.
        /// </summary>
        [Column("TargetShiftHours")]
        public decimal TargetShiftHours { get; set; } = 8.00m;

        /// <summary>
        /// The default daily start time. Maps to 'DefaultClockInTime'.
        /// </summary>
        [Column("DefaultClockInTime")]
        public TimeOnly DefaultClockInTime { get; set; } = new(8, 30);

        /// <summary>
        /// The default daily end time. Maps to 'DefaultClockOutTime'.
        /// </summary>
        [Column("DefaultClockOutTime")]
        public TimeOnly DefaultClockOutTime { get; set; } = new(17, 0);

        /// <summary>
        /// Defines the user's lunch break strategy. Maps to 'LunchStrategy'.
        /// </summary>
        [Column("LunchStrategy")]
        public LunchStrategy LunchStrategy { get; set; } = LunchStrategy.FixedWindow;

        /// <summary>
        /// The daily start time of the lunch window. Maps to 'LunchStartTime'.
        /// </summary>
        [Column("LunchStartTime")]
        public TimeOnly LunchStartTime { get; set; } = new(11, 0);

        /// <summary>
        /// The daily end time of the lunch window. Maps to 'LunchEndTime'.
        /// </summary>
        [Column("LunchEndTime")]
        public TimeOnly LunchEndTime { get; set; } = new(11, 30);

        /// <summary>
        /// Total lunch duration in minutes. Maps to 'LunchDurationMinutes'.
        /// </summary>
        [Column("LunchDurationMinutes")]
        public int LunchDurationMinutes { get; set; } = 30;

        /// <summary>
        /// Navigation property to the User who owns these settings.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;

        #endregion
    }
}
