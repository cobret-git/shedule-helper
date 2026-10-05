namespace Stint.Cli.Components
{
    /// <summary>
    /// What the navigation menu hands back: whether a section was actually picked, and which.
    /// </summary>
    /// <param name="IsPicked">True when the user chose a section, false when they closed the menu.</param>
    /// <param name="Destination">The chosen section - meaningless (default) when <paramref name="IsPicked"/> is false.</param>
    public readonly record struct MenuResult(bool IsPicked, MenuDestination Destination)
    {
        #region Properties

        /// <summary>The result of closing the menu without choosing anything.</summary>
        public static MenuResult Cancelled => default;

        #endregion

        #region Methods

        /// <summary>The result of choosing <paramref name="destination"/>.</summary>
        public static MenuResult Go(MenuDestination destination) => new(true, destination);

        #endregion
    }
}
