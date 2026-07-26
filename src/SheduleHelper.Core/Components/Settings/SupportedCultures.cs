using System;
using System.Collections.Generic;
using System.Linq;

namespace SheduleHelper.Core.Components.Settings
{
    /// <summary>
    /// The BCP-47 culture tags this build ships translations for, as persisted by
    /// <see cref="Services.ISettingsService"/>. Alpha scope: English and Ukrainian only - kept as a
    /// single source of truth so the Settings page's language picker and
    /// <see cref="Services.SettingsService"/>'s validation (falling back to <see cref="Default"/> when
    /// the settings file names something else, e.g. hand-edited or left over from a build that
    /// supported more languages) never drift apart.
    /// </summary>
    public static class SupportedCultures
    {
        public const string Default = "en";

        public static IReadOnlyList<string> All { get; } = new[] { "en", "uk" };

        public static bool IsSupported(string? culture) =>
            culture is not null && All.Contains(culture, StringComparer.OrdinalIgnoreCase);
    }
}
