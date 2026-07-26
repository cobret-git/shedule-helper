using System;
using System.IO;
using SheduleHelper.Core.Services;
using Windows.Storage;

namespace SheduleHelper.Modern.Services
{
    /// <summary>
    /// <see cref="IPathProvider"/> for the packaged (MSIX) WinUI application. In Debug,
    /// resolves to the build output directory. In Release, resolves into the package's isolated
    /// local storage via <see cref="ApplicationData"/>, which is the correct, sandboxed location
    /// for a packaged app's local data - never hand-rolled.
    /// </summary>
    public class PathProvider : IPathProvider
    {
        #region Fields

        private const string DatabaseFileName = "data.db";
        private const string SettingsFileName = "settings.json";

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PathProvider"/> class and resolves its file paths.
        /// </summary>
        public PathProvider()
        {
            var directory = ResolveStorageDirectory();
            DatabaseFilePath = Path.Combine(directory, DatabaseFileName);
            SettingsFilePath = Path.Combine(directory, SettingsFileName);
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public string DatabaseFilePath { get; }

        /// <inheritdoc/>
        public string SettingsFilePath { get; }

        #endregion

        #region Helpers

        private static string ResolveStorageDirectory()
        {
#if DEBUG
            // For Debug: use the build output directory. AppContext.BaseDirectory always resolves to the
            // executable's own output folder, unlike Environment.CurrentDirectory, which can be unreliable
            // depending on how the packaged app is launched/debugged.
            return AppContext.BaseDirectory;
#else
            // For Release: use the package's isolated local storage.
            return ApplicationData.Current.LocalFolder.Path;
#endif
        }

        #endregion
    }
}
