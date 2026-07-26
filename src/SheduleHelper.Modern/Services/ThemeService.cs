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
    /// theme dictionaries (e.g. <c>XamlControlsResources</c>) follow the same theme. Requires
    /// one-time setup via <see cref="Initialize"/> with the window's root element, mirroring
    /// <see cref="DialogService.Initialize"/>.
    /// </summary>
    // TODO#1: ElementTheme only covers Light/Dark - it doesn't cover the Medium/High contrast
    // palettes already added (LightHighContrastPalette.xaml, DarkHighContrastPalette.xaml, etc.).
    // We'll need our own extended theme enum (or a theme+contrast pair) once those are wired in.
    public class ThemeService : IThemeApplier
    {
        #region Fields

        private const string LightPaletteSource = "Assets/Palettes/LightPalette.xaml";
        private const string DarkPaletteSource = "Assets/Palettes/DarkPalette.xaml";

        private readonly ISettingsService _settingsService;

        private FrameworkElement? _root;
        private Microsoft.UI.Windowing.AppWindow? _appWindow;
        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeService"/> class.
        /// </summary>
        /// <param name="settingsService">Persists the chosen theme across app restarts.</param>
        public ThemeService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the currently applied theme.
        /// </summary>
        public ElementTheme CurrentTheme { get; private set; } = ElementTheme.Light;

        #endregion

        #region Methods

        /// <summary>
        /// Associates this service with the window's root element and applies whichever theme
        /// <see cref="ISettingsService"/> has persisted (defaulting to Light the very first run).
        /// Must be called once, before any <see cref="SetTheme"/> call.
        /// </summary>
        /// <param name="root">The root element whose <see cref="FrameworkElement.RequestedTheme"/> drives the window's theme.</param>
        public void Initialize(FrameworkElement root, AppWindow appWindow)
        {
            _root = root;
            _appWindow = appWindow;
            SetTheme(ToElementTheme(_settingsService.Theme));
        }

        /// <summary>
        /// Applies <paramref name="theme"/> by swapping the active palette dictionary and updating
        /// the root element's <see cref="FrameworkElement.RequestedTheme"/>, and persists the choice
        /// via <see cref="ISettingsService"/> so it's restored on the next launch.
        /// </summary>
        public void SetTheme(ElementTheme theme)
        {
            if (_root is null)
            {
                throw new InvalidOperationException($"{nameof(ThemeService)} has not been initialized. Call {nameof(Initialize)} first.");
            }

            if (theme == ElementTheme.Default)
            {
                throw new ArgumentException($"{nameof(ElementTheme.Default)} is not supported - the app must not follow the OS theme.", nameof(theme));
            }

            SwapPaletteDictionary(theme);
            _root.RequestedTheme = theme;
            CurrentTheme = theme;
            UpdateTitleBarColors();

            _settingsService.Theme = ToAppTheme(theme);
        }

        /// <inheritdoc/>
        public void Apply(AppTheme theme) => SetTheme(ToElementTheme(theme));

        /// <summary>
        /// Re-applies the caption-button colors for <see cref="CurrentTheme"/>. The WinUI
        /// <c>TitleBar</c> control re-syncs <see cref="AppWindow.TitleBar"/> to the OS theme once it
        /// finishes loading, silently overwriting whatever <see cref="SetTheme"/> applied earlier -
        /// call this again after that control (and the rest of the page) has loaded, e.g. from the
        /// root element's <c>Loaded</c> event, so our colors are applied last and win.
        /// </summary>
        public void ReapplyTitleBarColors()
        {
            UpdateTitleBarColors();
        }

        #endregion

        #region Helpers

        private static ElementTheme ToElementTheme(AppTheme theme) => theme == AppTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;

        private static AppTheme ToAppTheme(ElementTheme theme) => theme == ElementTheme.Dark ? AppTheme.Dark : AppTheme.Light;

        /// <summary>
        /// Replaces the currently merged palette dictionary (light or dark) with the one matching
        /// <paramref name="theme"/>.
        /// </summary>
        private static void SwapPaletteDictionary(ElementTheme theme)
        {
            var targetSource = theme == ElementTheme.Dark ? DarkPaletteSource : LightPaletteSource;

            var mergedDictionaries = Application.Current.Resources.MergedDictionaries;
            var existingPalette = mergedDictionaries.FirstOrDefault(dictionary =>
                dictionary.Source is { } source &&
                (source.OriginalString.EndsWith(LightPaletteSource, StringComparison.OrdinalIgnoreCase) ||
                 source.OriginalString.EndsWith(DarkPaletteSource, StringComparison.OrdinalIgnoreCase)));

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
            bool isDarkMode = (CurrentTheme == ElementTheme.Dark);

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
