using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stint.Core
{
    /// <summary>
    /// A part-day absence within a worked day (a doctor's appointment, an errand): the user was
    /// clocked in but not working on any project from <see cref="StartTime"/> until
    /// <see cref="EndTime"/>. Maps to the 'AwayLogs' table.
    /// </summary>
    /// <remarks>
    /// Not project time - the project segment running when the user went away is closed at
    /// <see cref="StartTime"/> (and remembered in <see cref="PausedTimeLogId"/>), and a new one for the
    /// same project/task is opened at <see cref="EndTime"/>. The time away still sits between clock-in
    /// and clock-out, so it counts toward the day's balance like any other clocked time.
    /// </remarks>
    [Table("AwayLogs")]
    public class AwayLog
    {
        #region Properties

        /// <summary>
        /// Unique identifier for the away log (Primary Key). Maps to 'Id'.
        /// </summary>
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign Key to the parent AttendanceLog. Maps to 'AttendanceLogId'.
        /// </summary>
        [Column("AttendanceLogId")]
        public int AttendanceLogId { get; set; }

        /// <summary>
        /// What the absence is for. Maps to 'Kind'.
        /// </summary>
        [Required]
        [Column("Kind")]
        public AwayKind Kind { get; set; }

        /// <summary>
        /// When the user went away. Maps to 'StartTime'.
        /// </summary>
        [Required]
        [Column("StartTime")]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the user came back - <see langword="null"/> while the absence is still open. Maps to 'EndTime'.
        /// </summary>
        [Column("EndTime")]
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// The project segment that was running when the user went away, closed at
        /// <see cref="StartTime"/> - what coming back resumes. Null when nothing was being tracked.
        /// Maps to 'PausedTimeLogId'.
        /// </summary>
        [Column("PausedTimeLogId")]
        public int? PausedTimeLogId { get; set; }

        /// <summary>
        /// Navigation property for the parent daily attendance log.
        /// </summary>
        [ForeignKey(nameof(AttendanceLogId))]
        public virtual AttendanceLog AttendanceLog { get; set; } = null!;

        /// <summary>
        /// Navigation property for the segment that was paused by going away (nullable).
        /// </summary>
        [ForeignKey(nameof(PausedTimeLogId))]
        public virtual ProjectTimeLog? PausedTimeLog { get; set; }

        #endregion
    }
}
