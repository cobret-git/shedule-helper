using SheduleHelper.Cli.Infrastructure;

namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// The top title bar every screen shares: a left-aligned title, a right-aligned status/back
    /// hint, and the rule underneath separating it from the content area.
    /// </summary>
    public static class Header
    {
        #region Methods

        /// <summary>
        /// Draws the header at rows 0-1.
        /// </summary>
        /// <param name="frame">The frame to draw into.</param>
        /// <param name="title">The screen's title, left-aligned.</param>
        /// <param name="rightText">Status text (e.g. the clock) or a back hint, right-aligned.</param>
        public static void Draw(Frame frame, string title, string rightText)
        {
            frame.Write(1, 0, title);
            frame.WriteRight(frame.Width - 1, 0, rightText, ColorToken.Dim);
            frame.Rule(1);
        }

        #endregion
    }
}
