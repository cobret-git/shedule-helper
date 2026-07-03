using System;
using System.IO;
using SheduleHelper.Core.Services;
using Windows.Storage;

namespace SheduleHelper.Modern.Services
{
    /// <summary>
    /// <see cref="IDatabasePathProvider"/> for the packaged (MSIX) WinUI application. In Debug,
    /// resolves to the build output directory. In Release, resolves into the package's isolated
    /// local storage via <see cref="ApplicationData"/>, which is the correct, sandboxed location
    /// for a packaged app's local data - never hand-rolled.
    /// </summary>
    public class DatabasePathProvider : IDatabasePathProvider
    {
        #region Fields

        private const string DatabaseFileName = "data.db";

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabasePathProvider"/> class and resolves the database file path.
        /// </summary>
        public DatabasePathProvider()
        {
            DatabaseFilePath = ResolveDatabaseFilePath();
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public string DatabaseFilePath { get; }

        #endregion

        #region Helpers

        private static string ResolveDatabaseFilePath()
        {
#if DEBUG
            // For Debug: use the build output directory. AppContext.BaseDirectory always resolves to the
            // executable's own output folder, unlike Environment.CurrentDirectory, which can be unreliable
            // depending on how the packaged app is launched/debugged.
            return Path.Combine(AppContext.BaseDirectory, DatabaseFileName);
#else
            // For Release: use the package's isolated local storage.
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, DatabaseFileName);
#endif
        }

        #endregion
    }
}
