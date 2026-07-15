using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Represents a project created by a user.
    /// Maps to the 'Projects' table.
    /// </summary>
    [Table("Projects")]
    public class Project
    {
        #region Properties

        /// <summary>
        /// Unique identifier for the project (Primary Key). Maps to 'Id'.
        /// </summary>
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign Key to the User table, identifying the project owner. Maps to 'UserId'.
        /// </summary>
        [Column("UserId")]
        public int UserId { get; set; }

        /// <summary>
        /// Name of the project. Maps to 'Name'.
        /// </summary>
        [Required]
        [MaxLength(100)]
        [Column("Name")]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Detailed description of the project. Maps to 'Description'.
        /// </summary>
        [MaxLength(500)]
        [Column("Description")]
        public string? Description { get; set; }

        /// <summary>
        /// Flag indicating whether the project is active (1) or inactive (0). Maps to 'IsActive'.
        /// </summary>
        [Column("IsActive")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Timestamp indicating when the project was created. Maps to 'CreatedAt'.
        /// </summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property to the User who owns this project.
        /// </summary>
        [ForeignKey(nameof(UserId))]
        public virtual User Owner { get; set; } = null!;

        /// <summary>
        /// Tasks grouped under this project.
        /// </summary>
        public virtual ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

        /// <summary>
        /// Logs associated with time spent on this project.
        /// </summary>
        public virtual ICollection<ProjectTimeLog> ProjectTimeLogs { get; set; } = new List<ProjectTimeLog>();

        #endregion
    }
}
