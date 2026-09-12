namespace Stint.Cli.Components
{
    /// <summary>
    /// Whether a navigation pushed a new screen onto the back stack or popped back to a
    /// previous one.
    /// </summary>
    /// <remarks>
    /// Carried on <see cref="NavigatedEventArgs"/> purely so a future transition/animation
    /// mechanism has something to key off (e.g. slide-in vs. slide-back) - the navigation
    /// service itself has no opinion on what either direction should look like.
    /// </remarks>
    public enum NavigationDirection
    {
        Forward,
        Back
    }
}
