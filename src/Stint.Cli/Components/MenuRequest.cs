namespace Stint.Cli.Components
{
    /// <summary>
    /// What the navigation menu is opened with: the section the user is on right now.
    /// </summary>
    /// <param name="Current">
    /// The menu entry for the screen the menu was opened from - listed but not selectable - or null
    /// when that screen is a sub-screen that has no entry of its own (Project, Plan event, ...), in
    /// which case every available entry can be picked.
    /// </param>
    public sealed record MenuRequest(MenuDestination? Current);
}
