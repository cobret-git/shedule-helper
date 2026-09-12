namespace Stint.Cli.Services
{
    /// <summary>
    /// Default <see cref="IAppPaths"/>: same <c>data/settings/logs</c> shape in every
    /// configuration, only <see cref="RootDirectory"/> itself differs between them.
    /// </summary>
    public sealed class AppPaths : IAppPaths
    {
        #region Constructors

        public AppPaths()
        {
#if DEBUG
            // Keeps debug runs entirely inside the build output, well away from anything real -
            // wipe bin/ and this data goes with it.
            RootDirectory = Path.Combine(AppContext.BaseDirectory, "AppData");
#else
            // %LOCALAPPDATA%\Stint. Works unmodified whether this build is running loose or
            // packaged as MSIX - Windows transparently redirects LocalApplicationData to the
            // package's own virtualized folder once packaged, so no OS/package branching is
            // needed here; revisit only if a non-Windows target is ever added.
            RootDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Stint");
#endif

            DataDirectory = Path.Combine(RootDirectory, "data");
            SettingsDirectory = Path.Combine(RootDirectory, "settings");
            LogsDirectory = Path.Combine(RootDirectory, "logs");

            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(SettingsDirectory);
            Directory.CreateDirectory(LogsDirectory);
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        public string RootDirectory { get; }

        /// <inheritdoc />
        public string DataDirectory { get; }

        /// <inheritdoc />
        public string SettingsDirectory { get; }

        /// <inheritdoc />
        public string LogsDirectory { get; }

        #endregion
    }
}
