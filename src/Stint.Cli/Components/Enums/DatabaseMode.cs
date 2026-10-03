namespace Stint.Cli.Components
{
    /// <summary>
    /// Which of the Settings &gt; Database page's two renders is current.
    /// </summary>
    public enum DatabaseMode
    {
        /// <summary>Browsing the action rows.</summary>
        Idle,

        /// <summary>
        /// Restore was chosen but the local database still holds data - waiting for the user to
        /// agree to clear it (confirm) or back out (cancel). Nothing is picked or touched yet.
        /// </summary>
        ConfirmingRestore
    }
}
