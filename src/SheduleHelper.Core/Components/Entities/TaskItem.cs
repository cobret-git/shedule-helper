using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SheduleHelper.Core.Components.Entities
{
    /// <summary>
    /// Represents a specific task within a project.
    /// Maps to the 'Tasks' table.
    /// </summary>
    [Table("Tasks")]
    public class TaskItem
    {
        #region Properties

        /// <summary>
        /// Unique identifier for the task (Primary Key). Maps to 'Id'.
        /// </summary>
        [Key]
        [Column("Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Foreign Key to the parent Project. Maps to 'ProjectId'.
        /// </summary>
        [Column("ProjectId")]
        public int ProjectId { get; set; }

        /// <summary>
        /// Title of the task. Maps to 'Title'.
        /// </summary>
        [Required]
        [MaxLength(150)]
        [Column("Title")]
        public string Title { get; set; } = null!;

        /// <summary>
        /// Detailed description of the task. Maps to 'Description'.
        /// </summary>
        [MaxLength(1000)]
        [Column("Description")]
        public string? Description { get; set; }

        /// <summary>
        /// Current status of the task. Maps to 'Status'.
        /// </summary>
        [Column("Status")]
        public TaskItemStatus Status { get; set; } = TaskItemStatus.Todo;

        /// <summary>
        /// Timestamp indicating when the task was created. Maps to 'CreatedAt'.
        /// </summary>
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Navigation property for the parent Project.
        /// </summary>
        [ForeignKey(nameof(ProjectId))]
        public virtual Project Project { get; set; } = null!;

        /// <summary>
        /// Time logs specifically dedicated to this task.
        /// </summary>
        public virtual ICollection<ProjectTimeLog> ProjectTimeLogs { get; set; } = new List<ProjectTimeLog>();

        #endregion
    }
}
