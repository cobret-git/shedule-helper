using Stint.Core;

namespace Stint.Cli.Components
{
    /// <summary>
    /// What the Plan event form is opened with: the kind of event, and the stored event being
    /// edited - or null for a brand-new one.
    /// </summary>
    /// <param name="DayType">The kind of absence. Fixed once an event exists - to change it, delete and re-plan.</param>
    /// <param name="Existing">The event being edited, or null when planning a new one.</param>
    public sealed record PlanEventContext(DayType DayType, PlannedEvent? Existing);
}
