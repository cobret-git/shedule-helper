using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Represents recorded block of time spent on a project (and optionally a task)
    /// within a parent daily attendance session. Maps to the 'ProjectTimeLogs' table.
    /// </summary>
    [Table("ProjectTimeLogs")]
    public class ProjectTimeLog
    {
        #region Properties

        /// <summary>
        /// Unique identifier for the detail time log (Primary Key). Maps to 'Id'.
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
        /// Foreign Key to the Project being worked on. Maps to 'ProjectId'.
        /// </summary>
        [Column("ProjectId")]
        public int ProjectId { get; set; }

        /// <summary>
        /// Optional Foreign Key to a specific Task associated with the project. Maps to 'TaskId'.
        /// </summary>
        [Column("TaskId")]
        public int? TaskId { get; set; }

        /// <summary>
        /// Timestamp indicating when work on this block started. Maps to 'StartTime'.
        /// </summary>
        [Required]
        [Column("StartTime")]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Timestamp indicating when work on this block ended (nullable). Maps to 'EndTime'.
        /// </summary>
        [Column("EndTime")]
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Navigation property for the parent daily attendance log.
        /// </summary>
        [ForeignKey(nameof(AttendanceLogId))]
        public virtual AttendanceLog AttendanceLog { get; set; } = null!;

        /// <summary>
        /// Navigation property for the associated Project.
        /// </summary>
        [ForeignKey(nameof(ProjectId))]
        public virtual Project Project { get; set; } = null!;

        /// <summary>
        /// Navigation property for the associated Task (nullable).
        /// </summary>
        [ForeignKey(nameof(TaskId))]
        public virtual TaskItem? Task { get; set; }

        #endregion
    }
}
