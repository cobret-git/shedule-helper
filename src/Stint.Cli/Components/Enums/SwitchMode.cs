namespace Stint.Cli.Components
{
    /// <summary>
    /// Which of Switch's two renders is current.
    /// </summary>
    public enum SwitchMode
    {
        /// <summary>Browsing the project/task tree - nothing marked yet.</summary>
        Idle,

        /// <summary>
        /// At least one pending mark exists (a switch target, pause, and/or one or more tasks
        /// marked done) - entered by marking anything, left by either confirming (commits every
        /// pending mark in one atomic batch, all time-log operations sharing one timestamp) or
        /// cancelling (discards every pending mark, no gateway call at all).
        /// </summary>
        Reviewing
    }
}
