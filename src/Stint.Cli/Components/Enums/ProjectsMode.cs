namespace Stint.Cli.Components
{
    /// <summary>
    /// Which of Projects' four renders is current.
    /// </summary>
    public enum ProjectsMode
    {
        /// <summary>Browsing the list - selecting, opening, starting an edit/delete, or a new project.</summary>
        Idle,

        /// <summary>Typing the name of a brand-new project.</summary>
        Creating,

        /// <summary>Typing a new name for the currently selected project.</summary>
        Editing,

        /// <summary>
        /// Marking one or more projects for deletion before committing the batch - entered by
        /// pressing delete on a project, left by either confirming (deletes every marked project)
        /// or cancelling (marks nothing, no gateway call at all).
        /// </summary>
        ConfirmingDelete
    }
}
