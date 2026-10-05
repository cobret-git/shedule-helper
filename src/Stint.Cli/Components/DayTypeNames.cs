using Stint.Core;

namespace Stint.Cli.Components
{
    /// <summary>
    /// How each <see cref="DayType"/> is worded on screen.
    /// </summary>
    public static class DayTypeNames
    {
        #region Methods

        /// <summary>The user-facing name of <paramref name="dayType"/>, e.g. "Sick leave".</summary>
        public static string GetName(DayType dayType) => dayType switch
        {
            DayType.Worked => "Worked day",
            DayType.Vacation => "Vacation",
            DayType.Sick => "Sick leave",
            DayType.Holiday => "Public holiday",
            DayType.Unpaid => "Unpaid leave",
            DayType.DayOffInLieu => "Day off in lieu",
            _ => dayType.ToString()
        };

        #endregion
    }
}
