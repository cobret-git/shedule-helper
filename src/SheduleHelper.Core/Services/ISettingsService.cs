using SheduleHelper.Core.Components.Settings;

namespace SheduleHelper.Core.Services
{
    /// <summary>
    /// Persists small, local app-instance preferences - as opposed to <see cref="Components.Entities.UserSetting"/>,
    /// which holds per-user work-schedule domain data synced/stored in the relational database.
    /// Theme and UI language are about this particular install (you might reasonably want different
    /// values on different machines under the same user), so they're kept out of that entity and
    /// out of EF entirely - a tiny local file is all this needs.
    /// </summary>
    public interface ISettingsService
    {
        #region Properties

        AppSettingsData Settings { get; }

        #endregion
    }
}
