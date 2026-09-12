using System.Text.Json;

namespace Stint.Cli.Services
{
    /// <summary>
    /// Default <see cref="ISettingsService{TSettings}"/>: one JSON file per
    /// <typeparamref name="TSettings"/>, named after the type (e.g. "AppSettings.json") so more
    /// than one settings type can share <see cref="IAppPaths.SettingsDirectory"/> without
    /// colliding.
    /// </summary>
    public sealed class SettingsService<TSettings> : ISettingsService<TSettings> where TSettings : new()
    {
        #region Fields

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true
        };

        private readonly string _filePath;

        #endregion

        #region Constructors

        public SettingsService(IAppPaths appPaths)
        {
            ArgumentNullException.ThrowIfNull(appPaths);

            _filePath = Path.Combine(appPaths.SettingsDirectory, $"{typeof(TSettings).Name}.json");
            Settings = new TSettings();
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        public TSettings Settings { get; private set; }

        #endregion

        #region Methods

        /// <inheritdoc />
        public async Task LoadAsync(CancellationToken ct = default)
        {
            if (!File.Exists(_filePath))
            {
                Settings = new TSettings();
                return;
            }

            await using var stream = File.OpenRead(_filePath);
            Settings = await JsonSerializer.DeserializeAsync<TSettings>(stream, SerializerOptions, ct)
                ?? new TSettings();
        }

        /// <inheritdoc />
        public async Task SaveAsync(CancellationToken ct = default)
        {
            await using var stream = File.Create(_filePath);
            await JsonSerializer.SerializeAsync(stream, Settings, SerializerOptions, ct);
        }

        #endregion
    }
}
