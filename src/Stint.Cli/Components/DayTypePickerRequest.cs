using Stint.Core;

namespace Stint.Cli.Components
{
    /// <summary>
    /// What the day-type picker dialog is opened with: its title (e.g. "Plan &gt; new") and the
    /// kinds on offer, in display order.
    /// </summary>
    /// <param name="Title">What is being chosen for - shown in the title bar.</param>
    /// <param name="Options">The kinds to choose from; never empty.</param>
    public sealed record DayTypePickerRequest(string Title, IReadOnlyList<DayType> Options);
}
