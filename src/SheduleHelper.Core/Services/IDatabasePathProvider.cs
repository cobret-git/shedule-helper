namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Resolves the file path of the local SQLite database, isolating callers from
    /// build-configuration and host-application storage differences.
    /// </summary>
    public interface IDatabasePathProvider
    {
        #region Properties

        /// <summary>
        /// Gets the full path to the SQLite database file to use for the current session.
        /// </summary>
        string DatabaseFilePath { get; }

        #endregion
    }
}
