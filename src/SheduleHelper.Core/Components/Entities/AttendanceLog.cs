using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Tracks daily overall attendance (Clock In/Out) for a user on a given date.
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
        /// Foreign Key to the User table. Maps to 'UserId'.
        /// </summary>
        [Column("UserId")]
        public int UserId { get; set; }

        /// <summary>
        /// The calendar work date. Unique per user in the database. Maps to 'WorkDate'.
        /// </summary>
        [Required]
        [Column("WorkDate")]
        public string WorkDate { get; set; } = null!;

        /// <summary>
        /// Overall clock in timestamp. Maps to 'ClockIn'.
        /// </summary>
        [Required]
        [Column("ClockIn")]
        public DateTime ClockIn { get; set; }

        /// <summary>
        /// Overall clock out timestamp (nullable). Maps to 'ClockOut'.
        /// </summary>
        [Column("ClockOut")]
        public DateTime? ClockOut { get; set; }

        /// <summary>
        /// Navigation property to the User associated with this log.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;

        /// <summary>
        /// Detailed project time log breakdowns recorded within this overall attendance session.
        /// </summary>
        public virtual ICollection<ProjectTimeLog> ProjectTimeLogs { get; set; } = new List<ProjectTimeLog>();

        #endregion
    }
}
