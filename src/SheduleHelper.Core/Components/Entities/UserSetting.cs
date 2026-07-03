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
        /// The default daily start time. Format matches SQLite 'HH:mm:ss' text representation. Maps to 'DefaultClockInTime'.
        /// </summary>
        [Column("DefaultClockInTime")]
        public string DefaultClockInTime { get; set; } = "08:30:00";

        /// <summary>
        /// The default daily end time. Format matches SQLite 'HH:mm:ss' text representation. Maps to 'DefaultClockOutTime'.
        /// </summary>
        [Column("DefaultClockOutTime")]
        public string DefaultClockOutTime { get; set; } = "17:00:00";

        /// <summary>
        /// Defines the user's lunch break strategy (e.g., 'FIXED_WINDOW', 'AUTO_DEDUCT'). Maps to 'LunchStrategy'.
        /// </summary>
        [Column("LunchStrategy")]
        public string LunchStrategy { get; set; } = "FIXED_WINDOW";

        /// <summary>
        /// The daily start time of the lunch window. Maps to 'LunchStartTime'.
        /// </summary>
        [Column("LunchStartTime")]
        public string LunchStartTime { get; set; } = "11:00:00";

        /// <summary>
        /// The daily end time of the lunch window. Maps to 'LunchEndTime'.
        /// </summary>
        [Column("LunchEndTime")]
        public string LunchEndTime { get; set; } = "11:30:00";

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
