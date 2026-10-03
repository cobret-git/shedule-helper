namespace Stint.Cli.Services
{
    /// <summary>
    /// The platform's own save/open file dialogs, filtered to database files (*.db). Both calls
    /// block until the dialog is closed. Neither sets a starting folder - Windows reopens the one
    /// last used for this app.
    /// </summary>
    public interface IFileDialogService
    {
        #region Methods

        /// <summary>
        /// Shows a "save as" dialog (asking before replacing an existing file) and returns the
        /// chosen path, or <see langword="null"/> if the user cancelled.
        /// </summary>
        /// <param name="title">The dialog's title bar text.</param>
        /// <param name="suggestedFileName">The file name pre-filled in the dialog.</param>
        string? PickFileToSave(string title, string suggestedFileName);

        /// <summary>
        /// Shows an "open" dialog for an existing file and returns the chosen path, or
        /// <see langword="null"/> if the user cancelled.
        /// </summary>
        /// <param name="title">The dialog's title bar text.</param>
        string? PickFileToOpen(string title);

        #endregion
    }
}
