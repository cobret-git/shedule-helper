namespace Stint.Cli.Components
{
    /// <summary>
    /// What the date picker screen hands back when it closes: whether a date was actually picked,
    /// and which one.
    /// </summary>
    /// <param name="IsPicked">True when the user confirmed a date, false when they cancelled.</param>
    /// <param name="Date">The confirmed date - meaningless (default) when <paramref name="IsPicked"/> is false.</param>
    public readonly record struct DatePickerResult(bool IsPicked, DateOnly Date)
    {
        #region Properties

        /// <summary>The result of backing out of the picker without choosing anything.</summary>
        public static DatePickerResult Cancelled => default;

        #endregion

        #region Methods

        /// <summary>The result of confirming <paramref name="date"/>.</summary>
        public static DatePickerResult Pick(DateOnly date) => new(true, date);

        #endregion
    }
}
