using SheduleHelper.Core.Components.Settings;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Applies a theme choice to the running app - the live, UI-framework-specific half of a theme
    /// change (persisting the choice is <see cref="ISettingsService"/>'s job; this is "and now
    /// actually make the window look different"). Concrete implementations live in each host
    /// application, mirroring <see cref="IDialogService"/>/<see cref="INavigationService"/>'s split
    /// between a platform-agnostic interface here and a platform-specific implementation in the
    /// host project (e.g. WinUI's <c>ThemeService</c>).
    /// </summary>
    public interface IThemeApplier
    {
        #region Methods

        /// <summary>
        /// Applies <paramref name="theme"/> to the running app and persists the choice via
        /// <see cref="ISettingsService"/>.
        /// </summary>
        void Apply(AppTheme theme);

        #endregion
    }
}
