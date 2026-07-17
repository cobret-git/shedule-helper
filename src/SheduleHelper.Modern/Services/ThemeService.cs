using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;

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
    // TODO#2: Persist the selected theme/contrast option in user settings and have Initialize load
    // and apply the saved option instead of always defaulting to ElementTheme.Light.
    public class ThemeService
    {
        #region Fields

        private const string LightPaletteSource = "Assets/Palettes/LightPalette.xaml";
        private const string DarkPaletteSource = "Assets/Palettes/DarkPalette.xaml";

        private FrameworkElement? _root;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ThemeService"/> class.
        /// </summary>
        public ThemeService()
        {
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
        /// Associates this service with the window's root element and applies <paramref name="theme"/>.
        /// Must be called once, before any <see cref="SetTheme"/> call.
        /// </summary>
        /// <param name="root">The root element whose <see cref="FrameworkElement.RequestedTheme"/> drives the window's theme.</param>
        /// <param name="theme">The theme to apply on startup. Defaults to <see cref="ElementTheme.Light"/> rather than following the OS.</param>
        public void Initialize(FrameworkElement root, ElementTheme theme = ElementTheme.Light)
        {
            _root = root;
            SetTheme(theme);
        }

        /// <summary>
        /// Applies <paramref name="theme"/> by swapping the active palette dictionary and updating
        /// the root element's <see cref="FrameworkElement.RequestedTheme"/>.
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
        }

        #endregion

        #region Helpers

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

        #endregion
    }
}
