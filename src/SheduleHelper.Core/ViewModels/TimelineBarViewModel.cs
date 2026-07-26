using System;
using System.Collections.Generic;
using System.Text;

namespace SheduleHelper.Core.ViewModels
{
    internal class TimelineBarViewModel
    {
    }

    /// <summary>
    /// Dictates how the timeline segment should be rendered, 
    /// keeping the UI control completely unaware of business logic (e.g., "Break" vs "Work").
    /// </summary>
    public enum SegmentDisplayStyle
    {
        Solid,      // Regular filled block (e.g., Project A)
        Hatched     // Striped/Patterned block (e.g., Lunch/Break)
    }

    /// <summary>
    /// An immutable representation of a single block of time on the graph.
    /// </summary>
    public readonly record struct TimelineSegment(
        string Name,
        DateTime StartTime,
        DateTime? EndTime,
        SegmentDisplayStyle DisplayStyle
    )
    {
        // Helper property for the UI control to easily check if it should draw a "Live" indicator
        public bool IsActive => !EndTime.HasValue;
    }
}
