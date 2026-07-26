namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Resolves local file paths (the SQLite database, the app-settings JSON file), isolating
    /// callers from build-configuration and host-application storage differences.
    /// </summary>
    public interface IPathProvider
    {
        #region Properties

        /// <summary>
        /// Gets the full path to the SQLite database file to use for the current session.
        /// </summary>
        string DatabaseFilePath { get; }

        /// <summary>
        /// Gets the full path to the app-settings JSON file (see <see cref="ISettingsService"/>).
        /// </summary>
        string SettingsFilePath { get; }

        #endregion
    }
}
