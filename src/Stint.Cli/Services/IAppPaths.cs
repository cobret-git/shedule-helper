namespace Stint.Cli.Services
{
    /// <summary>
    /// Where this app stores its own data: a root directory - its location depends on build
    /// configuration and OS, see <see cref="AppPaths"/> - with fixed subdirectories beneath it
    /// for the database, settings, and logs. All directories are guaranteed to exist already.
    /// </summary>
    public interface IAppPaths
    {
        #region Properties

        /// <summary>
        /// The app's root data directory (e.g. <c>%LOCALAPPDATA%\Stint</c> in Release).
        /// </summary>
        string RootDirectory { get; }

        /// <summary>
        /// <c>RootDirectory/data</c> - holds the SQLite database file.
        /// </summary>
        string DataDirectory { get; }

        /// <summary>
        /// <c>RootDirectory/settings</c> - holds <c>settings.json</c>.
        /// </summary>
        string SettingsDirectory { get; }

        /// <summary>
        /// <c>RootDirectory/logs</c> - holds the day's log file.
        /// </summary>
        string LogsDirectory { get; }

        #endregion
    }
}
