using System.Text.Json.Serialization;

namespace SheduleHelper.Core.Components.Settings
{
    /// <summary>
    /// The app's visual theme, as persisted by <see cref="Services.ISettingsService"/>. Deliberately
    /// Light/Dark only, with no "follow the OS" option - mirrors the same restriction already
    /// noted on the WinUI side in <c>ThemeService</c>'s <c>TODO#1</c>.
    /// </summary>
    public enum AppTheme
    {
        Light,
        Dark,
    }

    /// <summary>
    /// The contrast level to pair with <see cref="AppTheme"/> - matches the
    /// Light/Dark/LightHighContrast/DarkHighContrast/etc. palette dictionaries already present
    /// under <c>Assets/Palettes</c>, so the app can eventually offer them (see <c>ThemeService</c>'s
    /// <c>TODO#1</c>). <see cref="Default"/> is the regular, non-high-contrast palette.
    /// </summary>
    public enum ThemeContrast
    {
        Default,
        Medium,
        High,
    }

    /// <summary>
    /// The data persisted by <see cref="Services.ISettingsService"/> - small, local app-instance
    /// preferences (as opposed to <see cref="Entities.UserSetting"/>'s per-user work-schedule
    /// domain data). Serialized as-is to a JSON file; the <see cref="JsonPropertyName"/> attributes
    /// keep the on-disk keys stable even if the C# property names ever change.
    /// </summary>
    public class AppSettingsData
    {
        [JsonPropertyName("theme")]
        public AppTheme Theme { get; set; } = AppTheme.Light;

        [JsonPropertyName("themeContrast")]
        public ThemeContrast ThemeContrast { get; set; } = ThemeContrast.Default;

        [JsonPropertyName("culture")]
        public string Culture { get; set; } = SupportedCultures.Default;
    }
}
