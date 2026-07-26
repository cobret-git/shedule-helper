using Serilog;
using SheduleHelper.Core.Components.Settings;
using System;
using System.IO.Abstractions;
using System.Text.Json;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// <see cref="ISettingsService"/> backed by a small JSON file at
    /// <see cref="IPathProvider.SettingsFilePath"/>. Fully portable - unlike
    /// <see cref="IPathProvider"/>'s own implementations, which differ per host app because
    /// packaged-vs-unpackaged path resolution differs, this only consumes the already-resolved
    /// path, so one implementation can be shared by every host app. Mirrors <c>PathProvider</c>'s
    /// style: eager synchronous load, synchronous writes - this is a few bytes of local I/O, not
    /// worth async ceremony, and callers (e.g. <c>App</c>'s constructor, before any XAML loads)
    /// need the values synchronously anyway.
    /// </summary>
    public class SettingsService : ISettingsService
    {
        #region Fields

        private readonly ILogger _logger = Log.ForContext<SettingsService>();
        private readonly IFileSystem _fileSystem;
        private readonly string _filePath;
        private AppSettingsData _settings = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class and loads any
        /// existing settings file, falling back to defaults if none exists yet or it's unreadable.
        /// </summary>
        /// <param name="pathProvider">Resolves the settings file's path.</param>
        /// <param name="fileSystem">Reads/writes the settings file.</param>
        public SettingsService(IPathProvider pathProvider, IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            _filePath = pathProvider.SettingsFilePath;
            Load();
        }

        #endregion

        #region Events

        /// <inheritdoc/>
        public event EventHandler<AppSettingsData>? SettingsChanged;

        #endregion

        #region Properties

        /// <inheritdoc/>
        public AppSettingsData Settings
        {
            get => _settings;
            private set
            {
                _settings = value;
                SettingsChanged?.Invoke(this, _settings);
            }
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Load()
        {
            try
            {
                if (!_fileSystem.File.Exists(_filePath))
                {
                    Settings = new AppSettingsData();
                    return;
                }

                var json = _fileSystem.File.ReadAllText(_filePath);
                Settings = JsonSerializer.Deserialize<AppSettingsData>(json) ?? new AppSettingsData();
            }
            catch (Exception ex)
            {
                // A missing/corrupt settings file shouldn't prevent the app from starting - fall
                // back to defaults; the next Save() overwrites it with something valid.
                _logger.Warning(ex, "Failed to load settings file at {FilePath}; falling back to defaults.", _filePath);
                Settings = new AppSettingsData();
            }
        }

        /// <inheritdoc/>
        public void Save()
        {
            try
            {
                _fileSystem.File.WriteAllText(_filePath, JsonSerializer.Serialize(_settings));
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to save settings file at {FilePath}.", _filePath);
            }

            // Re-assign through the private setter so SettingsChanged fires for saves too, not
            // just loads - the setter doesn't compare old/new, so this works even though the
            // reference is unchanged.
            Settings = _settings;
        }

        #endregion
    }
}
