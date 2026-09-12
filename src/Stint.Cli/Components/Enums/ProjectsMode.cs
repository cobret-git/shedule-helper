namespace Stint.Cli.Components
{
    /// <summary>
    /// Which of Projects' three renders is current.
    /// </summary>
    public enum ProjectsMode
    {
        /// <summary>Browsing the list - selecting, opening, deleting, or starting a new project.</summary>
        Idle,

        /// <summary>Typing the name of a brand-new project.</summary>
        Creating,

        /// <summary>Typing a new name for the currently selected project.</summary>
        Editing
    }
}
