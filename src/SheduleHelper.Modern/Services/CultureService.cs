using SheduleHelper.Core.Components.Settings;
using SheduleHelper.Core.Models;
using SheduleHelper.Core.Services;
using System;
using System.Globalization;
using Windows.Globalization;

namespace SheduleHelper.Modern.Services
{
    /// <summary>
    /// Applies the app's active UI culture independently of the OS setting. Subscribes to
    /// <see cref="ISettingsService.SettingsChanged"/> and re-applies automatically - callers just
    /// mutate <see cref="ISettingsService.Settings"/>'s <see cref="AppSettingsData.Culture"/> and
    /// call <see cref="ISettingsService.Save"/>, they never need to talk to this class directly
    /// except for the one-time <see cref="Initialize"/> call. Mirrors <see cref="ThemeService"/>,
    /// except <see cref="Initialize"/> must run before <c>InitializeComponent()</c> in <c>App</c>'s
    /// constructor rather than against a window root - every <c>.resx</c> lookup from the first
    /// XAML parse onward resolves via <see cref="CultureInfo.CurrentUICulture"/>, so applying it any
    /// later would flash the wrong language for a frame.
    /// </summary>
    public class CultureService : IDisposable
    {
        #region Fields

        private readonly ISettingsService _settingsService;

        private string? _currentCulture;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CultureService"/> class and subscribes to
        /// <see cref="ISettingsService.SettingsChanged"/> so later culture changes are applied
        /// automatically. Call <see cref="Dispose"/> to unsubscribe.
        /// </summary>
        /// <param name="settingsService">Persists the chosen culture across app restarts and notifies of changes.</param>
        public CultureService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _settingsService.SettingsChanged += SettingsService_SettingsChanged;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Applies whichever culture <see cref="ISettingsService"/> has persisted (already
        /// validated as one of <see cref="SupportedCultures.All"/> by <see cref="SettingsService"/>).
        /// Must be called once, before <c>InitializeComponent()</c>.
        /// </summary>
        public void Initialize()
        {
            ApplyCulture(_settingsService.Settings.Culture);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _settingsService.SettingsChanged -= SettingsService_SettingsChanged;
        }

        #endregion

        #region Handlers

        private void SettingsService_SettingsChanged(object? sender, AppSettingsData settings)
        {
            ApplyCulture(settings.Culture);
        }

        #endregion

        #region Helpers

        private void ApplyCulture(string culture)
        {
            if (string.Equals(_currentCulture, culture, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _currentCulture = culture;

            var cultureInfo = new CultureInfo(culture);

            // Every thread spun up afterward (e.g. ThreadPool continuations after an await) should
            // see the same culture as the UI thread, not just whichever LocalizationManager sets below.
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            try
            {
                // Best-effort: lets WinRT-native controls (e.g. TimePicker/DatePicker) format with
                // the same culture. Requires package identity, so this throws when running
                // unpackaged (e.g. local Debug builds) - harmless, CultureInfo already covers every
                // .resx lookup, which is what actually matters.
                ApplicationLanguages.PrimaryLanguageOverride = culture;
            }
            catch (Exception)
            {
            }

            // Sets CurrentCulture/CurrentUICulture on this thread and refreshes every
            // {local:LocalizedString} binding in the live UI tree.
            LocalizationManager.Instance.SetCulture(cultureInfo);
        }

        #endregion
    }
}
