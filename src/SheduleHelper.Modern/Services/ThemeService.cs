using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SheduleHelper.Core.Components.Settings;
using SheduleHelper.Core.Services;
using System;
using System.Linq;
using Windows.UI;

namespace SheduleHelper.Modern.Services
{
    /// <summary>
    /// Forces the app's theme independently of the OS setting. Swaps the active palette dictionary
    /// (<c>LightPalette.xaml</c>/<c>DarkPalette.xaml</c>) in <see cref="Application.Resources"/> and
    /// sets <see cref="FrameworkElement.RequestedTheme"/> on the hosting root so built-in control
    /// theme dictionaries (e.g. <c>XamlControlsResources</c>) follow the same theme. Subscribes to
    /// <see cref="ISettingsService.SettingsChanged"/> and re-applies automatically - callers just
    /// mutate <see cref="ISettingsService.Settings"/>'s <see cref="AppSettingsData.Theme"/> and call
    /// <see cref="ISettingsService.Save"/>, they never need to talk to this class directly except
    /// for the one-time <see cref="Initialize"/> call. Requires that one-time setup with the
    /// window's root element, mirroring <see cref="DialogService.Initialize"/>.
    /// </summary>
    public class ThemeService : IDisposable
    {
        #region Fields

        private const string LightHighContrastPaletteSource = "Assets/Palettes/LightHighContrastPalette.xaml";
        private const string LightMediumContrastPaletteSource = "Assets/Palettes/LightMediumContrastPalette.xaml";
        private const string LightPaletteSource = "Assets/Palettes/LightPalette.xaml";
        private const string DarkPaletteSource = "Assets/Palettes/DarkPalette.xaml";
        private const string DarkHighContrastPaletteSource = "Assets/Palettes/DarkHighContrastPalette.xaml";
        private const string DarkMediumContrastPaletteSource = "Assets/Palettes/DarkMediumContrastPalette.xaml";

        private readonly ISettingsService _settingsService;

        private FrameworkElement? _root;
        private Microsoft.UI.Windowing.AppWindow? _appWindow;
        private ElementTheme _currentTheme = ElementTheme.Light;
        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeService"/> class and subscribes to
        /// <see cref="ISettingsService.SettingsChanged"/> so later theme changes are applied
        /// automatically. Call <see cref="Dispose"/> to unsubscribe.
        /// </summary>
        /// <param name="settingsService">Persists the chosen theme across app restarts and notifies of changes.</param>
        public ThemeService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _settingsService.SettingsChanged += SettingsService_SettingsChanged;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Associates this service with the window's root element and applies whichever theme
        /// <see cref="ISettingsService"/> has persisted (defaulting to Light the very first run).
        /// Must be called once, before any theme change can be applied.
        /// </summary>
        /// <param name="root">The root element whose <see cref="FrameworkElement.RequestedTheme"/> drives the window's theme.</param>
        public void Initialize(FrameworkElement root, AppWindow appWindow)
        {
            _root = root;
            _appWindow = appWindow;
            ApplyTheme(_settingsService.Settings.Theme, _settingsService.Settings.ThemeContrast);
        }

        /// <summary>
        /// Re-applies the caption-button colors for <see cref="_currentTheme"/>. The WinUI
        /// <c>TitleBar</c> control re-syncs <see cref="AppWindow.TitleBar"/> to the OS theme once it
        /// finishes loading, silently overwriting whatever was applied earlier - call this again
        /// after that control (and the rest of the page) has loaded, e.g. from the root element's
        /// <c>Loaded</c> event, so our colors are applied last and win.
        /// </summary>
        public void ReapplyTitleBarColors()
        {
            UpdateTitleBarColors();
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
            // Not initialized yet - Initialize() will apply the current value itself once it runs.
            if (_root is null)
            {
                return;
            }

            ApplyTheme(settings.Theme, settings.ThemeContrast);
        }

        #endregion

        #region Helpers

        private void ApplyTheme(AppTheme theme, ThemeContrast contrast)
        {
            if (_root is null)
            {
                throw new InvalidOperationException($"{nameof(ThemeService)} has not been initialized. Call {nameof(Initialize)} first.");
            }

            var elementTheme = ToElementTheme(theme);

            SwapPaletteDictionary(theme, contrast);
            _root.RequestedTheme = elementTheme;
            _currentTheme = elementTheme;
            UpdateTitleBarColors();
        }

        private static ElementTheme ToElementTheme(AppTheme theme) => theme == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;

        private static string PaletteSource(AppTheme theme, ThemeContrast contrast) => (theme, contrast) switch
        {
            (AppTheme.Dark, ThemeContrast.High) => DarkHighContrastPaletteSource,
            (AppTheme.Dark, ThemeContrast.Medium) => DarkMediumContrastPaletteSource,
            (AppTheme.Dark, _) => DarkPaletteSource,
            (AppTheme.Light, ThemeContrast.High) => LightHighContrastPaletteSource,
            (AppTheme.Light, ThemeContrast.Medium) => LightMediumContrastPaletteSource,
            _ => LightPaletteSource,
        };

        /// <summary>
        /// Replaces the currently merged palette dictionary with the one matching
        /// <paramref name="theme"/>/<paramref name="contrast"/>.
        /// </summary>
        private static void SwapPaletteDictionary(AppTheme theme, ThemeContrast contrast)
        {
            var targetSource = PaletteSource(theme, contrast);

            var allPaletteSources = new[]
            {
                LightPaletteSource, LightMediumContrastPaletteSource, LightHighContrastPaletteSource,
                DarkPaletteSource, DarkMediumContrastPaletteSource, DarkHighContrastPaletteSource,
            };

            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
            var existingPalette = mergedDictionaries.FirstOrDefault(dictionary =>
                dictionary.Source is { } source &&
                allPaletteSources.Any(paletteSource => source.OriginalString.EndsWith(paletteSource, StringComparison.OrdinalIgnoreCase)));

            var paletteDictionary = new ResourceDictionary { Source = new Uri($"ms-appx:///{targetSource}") };

            if (existingPalette is not null)
            {
                var index = mergedDictionaries.IndexOf(existingPalette);
                mergedDictionaries.RemoveAt(index);
                mergedDictionaries.Insert(index, paletteDictionary);
            }
            else
            {
                mergedDictionaries.Add(paletteDictionary);
            }
        }
        private void UpdateTitleBarColors()
        {
            if (_appWindow == null) return;

            var titleBar = _appWindow.TitleBar;

            // Determine if we're in dark mode
            bool isDarkMode = (_currentTheme == ElementTheme.Dark);

            if (isDarkMode)
            {
                // Dark theme colors
                titleBar.ButtonForegroundColor = Colors.White;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverForegroundColor = Colors.White;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF);
                titleBar.ButtonPressedForegroundColor = Colors.White;
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF);
            }
            else
            {
                // Light theme colors
                titleBar.ButtonForegroundColor = Colors.Black;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverForegroundColor = Colors.Black;
                titleBar.ButtonHoverBackgroundColor = Color.FromArgb(0x33, 0x00, 0x00, 0x00);
                titleBar.ButtonPressedForegroundColor = Colors.Black;
                titleBar.ButtonPressedBackgroundColor = Color.FromArgb(0x66, 0x00, 0x00, 0x00);
            }
        }
        #endregion
    }
}
