using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SheduleHelper.WpfApp.Components.Entities
{
    /// <summary>
    /// Represents a project that groups related tasks together.
    /// Examples: "VisualStudio", "Office", "Windows"
    /// </summary>
    [Table("Projects")]
    [Index(nameof(Name), IsUnique = true, IsDescending = new[] { false })]
    public class Project
    {
        #region Properties

        /// <summary>
        /// Gets or sets the unique identifier for the project.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the unique name of the project.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the project description.
        /// </summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the default task type identifier for tasks in this project.
        /// Optional - tasks can override this with their own type.
        /// </summary>
        public int? DefaultTaskTypeId { get; set; }

        /// <summary>
        /// Gets or sets the color for UI display in hexadecimal format.
        /// Example: "#FF6A2C"
        /// </summary>
        [MaxLength(9)]
        public string? Color { get; set; }

        /// <summary>
        /// Gets or sets the sort order for manual ordering in UI.
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this project is archived.
        /// Archived projects are hidden from active views but retain their data.
        /// </summary>
        public bool IsArchived { get; set; }

        /// <summary>
        /// Gets or sets the UTC timestamp when this project was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp when this project was last modified.
        /// Null if never modified since creation.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the default task type for this project.
        /// </summary>
        [ForeignKey(nameof(DefaultTaskTypeId))]
        public TaskType? DefaultTaskType { get; set; }

        /// <summary>
        /// Gets or sets the collection of tasks associated with this project.
        /// </summary>
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

        #endregion
    }
}
