using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stint.Core
{
    /// <summary>
    /// Tracks the record for a single calendar date: either a worked day (Clock In/Out and
    /// <see cref="ProjectTimeLogs"/> apply) or a full-day absence (<see cref="DayType"/> other than
    /// <see cref="Core.DayType.Worked"/>, credited via <see cref="CreditedDuration"/> instead).
    /// Maps to the 'AttendanceLogs' table.
    /// </summary>
    [Table("AttendanceLogs")]
    public class AttendanceLog
    {
        #region Properties

        /// <summary>
        /// Unique identifier for the attendance session (Primary Key). Maps to 'Id'.
        /// </summary>
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// The calendar work date. Unique in the database - at most one attendance log per date.
        /// Maps to 'WorkDate'.
        /// </summary>
        [Required]
        [Column("WorkDate")]
        public string WorkDate { get; set; } = null!;

        /// <summary>
        /// What kind of day this is. Determines whether <see cref="ClockIn"/>/<see cref="ClockOut"/>
        /// and <see cref="ProjectTimeLogs"/> are meaningful (<see cref="Core.DayType.Worked"/>), or
        /// whether <see cref="CreditedDuration"/> is used instead. Maps to 'DayType'.
        /// </summary>
        [Required]
        [Column("DayType")]
        public DayType DayType { get; set; } = DayType.Worked;

        /// <summary>
        /// Overall clock in timestamp. Only set when <see cref="DayType"/> is
        /// <see cref="Core.DayType.Worked"/>. Maps to 'ClockIn'.
        /// </summary>
        [Column("ClockIn")]
        public DateTime? ClockIn { get; set; }

        /// <summary>
        /// Overall clock out timestamp (nullable). Only meaningful when <see cref="DayType"/> is
        /// <see cref="Core.DayType.Worked"/>. Maps to 'ClockOut'.
        /// </summary>
        [Column("ClockOut")]
        public DateTime? ClockOut { get; set; }

        /// <summary>
        /// Duration credited toward the daily balance for a non-worked day (vacation, sick, holiday,
        /// unpaid). Set to that day's target shift duration at the time it was recorded, so a later
        /// change to <c>TargetShiftDuration</c> doesn't retroactively change past balance. Null for
        /// <see cref="Core.DayType.Worked"/>, where the credited amount is derived from
        /// <see cref="ProjectTimeLogs"/>/clock times instead. Maps to 'CreditedDuration'.
        /// </summary>
        [Column("CreditedDuration")]
        public TimeSpan? CreditedDuration { get; set; }

        /// <summary>
        /// Detailed project time log breakdowns recorded within this overall attendance session.
        /// Empty for non-<see cref="Core.DayType.Worked"/> days.
        /// </summary>
        public virtual ICollection<ProjectTimeLog> ProjectTimeLogs { get; set; } = new List<ProjectTimeLog>();

        #endregion
    }
}
