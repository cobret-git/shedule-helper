using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Represents a user in the time-tracking system.
    /// Maps to the 'Users' table.
    /// </summary>
    [Table("Users")]
    public class User
    {
        #region Properties

        /// <summary>
        /// Unique identifier for the user (Primary Key). Maps to 'Id'.
        /// </summary>
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// The unique username of the user. Maps to 'Username'.
        /// </summary>
        [Required]
        [MaxLength(50)]
        [Column("Username")]
        public string Username { get; set; } = null!;

        /// <summary>
        /// The unique email address of the user. Maps to 'Email'.
        /// </summary>
        [Required]
        [MaxLength(254)]
        [Column("Email")]
        public string Email { get; set; } = null!;

        /// <summary>
        /// The timestamp when the user account was created. Maps to 'CreatedAt'.
        /// </summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The settings configuration for this user.
        /// </summary>
        public virtual UserSetting? UserSetting { get; set; }

        /// <summary>
        /// The projects owned/managed by this user.
        /// </summary>
        public virtual ICollection<Project> Projects { get; set; } = new List<Project>();

        /// <summary>
        /// The attendance logs recorded for this user.
        /// </summary>
        public virtual ICollection<AttendanceLog> AttendanceLogs { get; set; } = new List<AttendanceLog>();

        #endregion
    }
}
