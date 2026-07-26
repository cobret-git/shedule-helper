using SheduleHelper.Core.Components.Settings;
using System;

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
        #region Events

        /// <summary>
        /// Raised whenever <see cref="Settings"/> changes - after <see cref="Load"/> reads (or
        /// falls back to defaults) and after <see cref="Save"/> persists. The event argument is the
        /// current <see cref="Settings"/> instance.
        /// </summary>
        event EventHandler<AppSettingsData> SettingsChanged;

        #endregion

        #region Properties

        /// <summary>
        /// The currently loaded settings. Populated by <see cref="Load"/>; mutate its properties
        /// directly, then call <see cref="Save"/> to persist and raise <see cref="SettingsChanged"/>.
        /// </summary>
        AppSettingsData Settings { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Reads the settings file from disk and assigns <see cref="Settings"/>, falling back to
        /// defaults if the file doesn't exist or can't be read.
        /// </summary>
        void Load();

        /// <summary>
        /// Persists the current <see cref="Settings"/> to disk.
        /// </summary>
        void Save();

        #endregion
    }
}
