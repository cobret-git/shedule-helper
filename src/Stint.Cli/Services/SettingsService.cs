using System.Text.Json;
using System.Text.Json.Serialization;

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

        // Enums are written as their names rather than numbers, so reordering/inserting enum
        // members later can't silently change what an existing settings file means. Numbers are
        // still accepted on read, so a file written before this was added keeps loading.
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
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
                // First run for this settings type - write the defaults out now rather than only
                // ever holding them in memory, so there's an actual file to inspect/edit and this
                // same "nothing on disk yet" branch doesn't run again on every future launch.
                Settings = new TSettings();
                await SaveAsync(ct);
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
