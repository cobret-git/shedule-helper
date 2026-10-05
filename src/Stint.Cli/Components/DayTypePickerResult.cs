using Stint.Core;

namespace Stint.Cli.Components
{
    /// <summary>
    /// What the day-type picker dialog hands back: whether a kind was actually picked, and which.
    /// </summary>
    /// <param name="IsPicked">True when the user confirmed a kind, false when they cancelled.</param>
    /// <param name="DayType">The confirmed kind - meaningless (default) when <paramref name="IsPicked"/> is false.</param>
    public readonly record struct DayTypePickerResult(bool IsPicked, DayType DayType)
    {
        #region Properties

        /// <summary>The result of backing out of the picker without choosing anything.</summary>
        public static DayTypePickerResult Cancelled => default;

        #endregion

        #region Methods

        /// <summary>The result of confirming <paramref name="dayType"/>.</summary>
        public static DayTypePickerResult Pick(DayType dayType) => new(true, dayType);

        #endregion
    }
}
