using SheduleHelper.Core.Services;
using System.IO.Abstractions;

namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// <see cref="IPathProvider"/> for the console host. Defaults to
    /// <c>%LOCALAPPDATA%\SheduleHelper</c> - never next to the executable, which breaks the moment
    /// the app is installed under Program Files. Portable mode is opt-in: either a <c>portable.txt</c>
    /// marker file next to the executable, or the <c>SHEDULEHELPER_DATA</c> environment variable
    /// naming an explicit directory (checked first, so it always wins).
    /// </summary>
    public sealed class ConsolePathProvider : IPathProvider
    {
        #region Fields

        private const string DatabaseFileName = "data.db";
        private const string SettingsFileName = "settings.json";
        private const string PortableMarkerFileName = "portable.txt";
        private const string DataDirEnvironmentVariable = "SHEDULEHELPER_DATA";

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsolePathProvider"/> class, resolving and
        /// creating its storage directory.
        /// </summary>
        /// <param name="fileSystem">Checks for the portable marker and creates the storage directory.</param>
        public ConsolePathProvider(IFileSystem fileSystem)
        {
            var directory = ResolveStorageDirectory(fileSystem);
            fileSystem.Directory.CreateDirectory(directory);

            DatabaseFilePath = Path.Combine(directory, DatabaseFileName);
            SettingsFilePath = Path.Combine(directory, SettingsFileName);
            LogsDirectory = Path.Combine(directory, "logs");
        }

        #endregion

        #region Properties

        /// <inheritdoc/>
        public string DatabaseFilePath { get; }

        /// <inheritdoc/>
        public string SettingsFilePath { get; }

        /// <summary>
        /// The directory Serilog's file sink writes into. Not part of <see cref="IPathProvider"/> -
        /// only <see cref="Program"/> needs it, to configure logging before the rest of the app starts.
        /// </summary>
        public string LogsDirectory { get; }

        #endregion

        #region Helpers

        private static string ResolveStorageDirectory(IFileSystem fileSystem)
        {
            var explicitDataDir = Environment.GetEnvironmentVariable(DataDirEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(explicitDataDir))
            {
                return explicitDataDir;
            }

            var exeDirectory = AppContext.BaseDirectory;
            if (fileSystem.File.Exists(Path.Combine(exeDirectory, PortableMarkerFileName)))
            {
                return exeDirectory;
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "SheduleHelper");
        }

        #endregion
    }
}
