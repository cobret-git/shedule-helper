namespace Stint.Cli.Services
{
    /// <summary>
    /// Loads and saves one settings object of type <typeparamref name="TSettings"/> as JSON
    /// under <see cref="IAppPaths.SettingsDirectory"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="Settings"/> starts out as <c>new TSettings()</c> - its own defaults - the
    /// moment this service is constructed; nothing is read from disk until <see cref="LoadAsync"/>
    /// is called explicitly. That mirrors <c>IDatabaseMigrator.MigrateAsync</c>'s shape: an
    /// explicit async step run once at startup, rather than I/O hidden inside a constructor.
    /// </remarks>
    /// <typeparam name="TSettings">
    /// The settings type. Must be default-constructible so a missing settings file can fall
    /// back to its own defaults.
    /// </typeparam>
    public interface ISettingsService<TSettings> where TSettings : new()
    {
        #region Properties

        /// <summary>
        /// The current in-memory settings - whatever was last loaded or saved. Never null.
        /// </summary>
        TSettings Settings { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Reads the settings file from disk and replaces <see cref="Settings"/> with its
        /// contents. If no file exists yet, <see cref="Settings"/> is reset to
        /// <c>new TSettings()</c> instead - nothing is written to disk by this call.
        /// </summary>
        Task LoadAsync(CancellationToken ct = default);

        /// <summary>
        /// Writes the current <see cref="Settings"/> to disk, overwriting whatever was there.
        /// </summary>
        Task SaveAsync(CancellationToken ct = default);

        #endregion
    }
}
