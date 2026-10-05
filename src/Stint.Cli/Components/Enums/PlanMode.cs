namespace Stint.Cli.Components
{
    /// <summary>
    /// Which of Plan's two renders is current.
    /// </summary>
    public enum PlanMode
    {
        /// <summary>Browsing the list - selecting, opening an event, starting a new one or a delete.</summary>
        Idle,

        /// <summary>
        /// The selected event is marked for deletion - entered by pressing delete on it, left by
        /// either confirming (deletes it) or cancelling (no gateway call at all).
        /// </summary>
        ConfirmingDelete
    }
}
