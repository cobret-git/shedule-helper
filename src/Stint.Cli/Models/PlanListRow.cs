using Stint.Core;

namespace Stint.Cli.Models
{
    /// <summary>
    /// One planned event as the Plan list shows it: the stored event plus what the list derives
    /// from it - the working days it covers and what they add up to at the current daily target.
    /// </summary>
    /// <param name="Event">The stored event.</param>
    /// <param name="WorkingDays">How many working days <paramref name="Event"/> covers.</param>
    /// <param name="Duration"><paramref name="WorkingDays"/> times the current daily target.</param>
    /// <param name="IsUpcoming">True while the event hasn't ended yet (it may be under way); false once it's over.</param>
    public sealed record PlanListRow(PlannedEvent Event, int WorkingDays, TimeSpan Duration, bool IsUpcoming);
}
