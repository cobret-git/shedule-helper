namespace Stint.Cli.Components
{
    /// <summary>
    /// Which of Project's four renders is current.
    /// </summary>
    public enum ProjectMode
    {
        /// <summary>Browsing the task list - selecting, starting an edit/delete, or a new task.</summary>
        Idle,

        /// <summary>Typing the title of a brand-new task.</summary>
        Creating,

        /// <summary>Typing a new title for the currently selected task.</summary>
        Editing,

        /// <summary>
        /// Marking one or more tasks for deletion before committing the batch - entered by
        /// pressing delete on a task, left by either confirming (deletes every marked task) or
        /// cancelling (marks nothing, no gateway call at all).
        /// </summary>
        ConfirmingDelete
    }
}
