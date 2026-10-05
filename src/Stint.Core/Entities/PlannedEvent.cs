using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Stint.Core
{
    /// <summary>
    /// A full-day absence planned ahead of time (a week of vacation next month, a day off in lieu):
    /// one or more consecutive calendar days of the same <see cref="DayType"/>. Maps to the
    /// 'PlannedEvents' table.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="AttendanceLog"/> on purpose: that table allows one row per date and
    /// treats a row as "this day is recorded", whereas a plan spans a range and may change many times
    /// before the days arrive. Reports expand a plan into its days at query time, and the credited
    /// duration is decided when the day arrives, not when it is planned.
    /// </remarks>
    [Table("PlannedEvents")]
    public class PlannedEvent
    {
        #region Properties

        /// <summary>
        /// Unique identifier for the planned event (Primary Key). Maps to 'Id'.
        /// </summary>
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// What kind of absence this is. Never <see cref="Core.DayType.Worked"/>. Maps to 'DayType'.
        /// </summary>
        [Required]
        [Column("DayType")]
        public DayType DayType { get; set; }

        /// <summary>
        /// First calendar day of the event. Maps to 'StartDate'.
        /// </summary>
        [Required]
        [Column("StartDate")]
        public DateOnly StartDate { get; set; }

        /// <summary>
        /// Last calendar day of the event, inclusive - equal to <see cref="StartDate"/> for a
        /// single day. Maps to 'EndDate'.
        /// </summary>
        [Required]
        [Column("EndDate")]
        public DateOnly EndDate { get; set; }

        /// <summary>
        /// Optional free-text note ("Skiing"). Maps to 'Note'.
        /// </summary>
        [MaxLength(32)]
        [Column("Note")]
        public string? Note { get; set; }

        /// <summary>
        /// Timestamp indicating when the event was planned. Maps to 'CreatedAt'.
        /// </summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        #endregion
    }
}
