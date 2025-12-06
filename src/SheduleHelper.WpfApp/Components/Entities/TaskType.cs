using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SheduleHelper.WpfApp.Components.Entities
{
    /// <summary>
    /// Represents a reusable task classification/category.
    /// Supports two-level hierarchy (e.g., Development → Bug Fix).
    /// </summary>
    [Table("TaskTypes")]
    [Index(nameof(Name), IsUnique = true)]
    public class TaskType
    {
        #region Properties

        /// <summary>
        /// Gets or sets the unique identifier for the task type.
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the unique name of the task type.
        /// Examples: "Development", "Bug Fix", "Documentation"
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parent task type identifier for hierarchical organization.
        /// Null indicates a root-level task type.
        /// </summary>
        public int? ParentTypeId { get; set; }

        /// <summary>
        /// Gets or sets the hierarchy depth level (0-based).
        /// Level 0 = Root category, Level 1 = Subcategory.
        /// Maximum recommended depth is 2 levels.
        /// </summary>
        public int Level { get; set; }

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
        /// Gets or sets the UTC timestamp when this task type was created.
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the UTC timestamp when this task type was last modified.
        /// Null if never modified since creation.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Gets or sets the parent task type for hierarchical organization.
        /// </summary>
        [ForeignKey(nameof(ParentTypeId))]
        public TaskType? ParentType { get; set; }

        /// <summary>
        /// Gets or sets the collection of child task types.
        /// </summary>
        public ICollection<TaskType> ChildTypes { get; set; } = new List<TaskType>();

        /// <summary>
        /// Gets or sets the collection of tasks associated with this task type.
        /// </summary>
        public ICollection<TaskItem> Tasks { get; set; } = new List<TaskItem>();

        #endregion
    }
}
