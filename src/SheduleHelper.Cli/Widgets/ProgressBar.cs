using SheduleHelper.Cli.Infrastructure;

namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// A horizontal filled/empty bar - "████░░░░ 5h 28m / 8h 00m" - sized to fit the available
    /// width, with a caption drawn immediately after it.
    /// </summary>
    public static class ProgressBar
    {
        #region Methods

        /// <summary>
        /// Draws the bar at (<paramref name="x"/>, <paramref name="y"/>).
        /// </summary>
        /// <param name="frame">The frame to draw into.</param>
        /// <param name="x">The column the bar starts at.</param>
        /// <param name="y">The row to draw on.</param>
        /// <param name="maxBarWidth">The bar's width when the frame is wide enough to afford it - shrinks to fit narrower terminals.</param>
        /// <param name="ratio">
        /// The filled proportion. Values above 1 (worked past the target) still fill the bar
        /// completely rather than throwing or overflowing - use <paramref name="fillColor"/> to
        /// signal overtime instead, since a full bar alone can't distinguish "just hit the target"
        /// from "hours past it".
        /// </param>
        /// <param name="caption">Text drawn immediately after the bar, e.g. "5h 28m / 8h 00m".</param>
        /// <param name="fillColor">
        /// Colour for the filled portion. Defaults to <see cref="ColorToken.Accent"/>; callers pass
        /// <see cref="ColorToken.Warning"/> once <paramref name="ratio"/> exceeds 1 to flag overtime.
        /// </param>
        public static void Draw(Frame frame, int x, int y, int maxBarWidth, double ratio, string caption, ColorToken fillColor = ColorToken.Accent)
        {
            var availableWidth = frame.Width - x - caption.Length - 3;
            var barWidth = Math.Max(0, Math.Min(maxBarWidth, availableWidth));
            var filled = (int)Math.Round(barWidth * Math.Clamp(ratio, 0, 1));

            frame.Fill(x, y, filled, '█', fillColor);
            frame.Fill(x + filled, y, barWidth - filled, '░', ColorToken.Dim);
            frame.Write(x + barWidth + 2, y, caption);
        }

        /// <summary>
        /// Draws the bar at (<paramref name="x"/>, <paramref name="y"/>), split into two filled
        /// segments - e.g. a blue "within shift" segment followed by a yellow "overtime" segment -
        /// so overtime reads as its own portion of the bar rather than the whole bar just changing
        /// colour once the target is passed.
        /// </summary>
        /// <param name="frame">The frame to draw into.</param>
        /// <param name="x">The column the bar starts at.</param>
        /// <param name="y">The row to draw on.</param>
        /// <param name="maxBarWidth">The bar's width when the frame is wide enough to afford it - shrinks to fit narrower terminals.</param>
        /// <param name="primaryRatio">Fraction of the bar's width filled with <paramref name="primaryColor"/>, drawn first.</param>
        /// <param name="secondaryRatio">Fraction of the bar's width filled with <paramref name="secondaryColor"/>, drawn immediately after the primary segment.</param>
        /// <param name="caption">Text drawn immediately after the bar, e.g. "9h 05m / 8h 00m (+1h 05m)".</param>
        /// <param name="primaryColor">Colour for the primary segment. Defaults to <see cref="ColorToken.Accent"/>.</param>
        /// <param name="secondaryColor">Colour for the secondary segment. Defaults to <see cref="ColorToken.Warning"/>.</param>
        public static void Draw(Frame frame, int x, int y, int maxBarWidth, double primaryRatio, double secondaryRatio, string caption, ColorToken primaryColor = ColorToken.Accent, ColorToken secondaryColor = ColorToken.Warning)
        {
            var availableWidth = frame.Width - x - caption.Length - 3;
            var barWidth = Math.Max(0, Math.Min(maxBarWidth, availableWidth));

            var primaryWidth = (int)Math.Round(barWidth * Math.Clamp(primaryRatio, 0, 1));
            var secondaryWidth = Math.Min((int)Math.Round(barWidth * Math.Clamp(secondaryRatio, 0, 1)), barWidth - primaryWidth);

            frame.Fill(x, y, primaryWidth, '█', primaryColor);
            frame.Fill(x + primaryWidth, y, secondaryWidth, '█', secondaryColor);
            frame.Fill(x + primaryWidth + secondaryWidth, y, barWidth - primaryWidth - secondaryWidth, '░', ColorToken.Dim);
            frame.Write(x + barWidth + 2, y, caption);
        }

        /// <summary>
        /// <see cref="Region"/>-scoped equivalent of the single-segment <see cref="Draw(Frame, int, int, int, double, string, ColorToken)"/> -
        /// sizes itself against the region's own width rather than the underlying frame's, so a bar
        /// drawn into a narrowed column (e.g. beside an inspector pane) can't overflow into whatever
        /// is next to it.
        /// </summary>
        public static void Draw(Region region, int x, int y, int maxBarWidth, double ratio, string caption, ColorToken fillColor = ColorToken.Accent)
        {
            var availableWidth = region.Width - x - caption.Length - 3;
            var barWidth = Math.Max(0, Math.Min(maxBarWidth, availableWidth));
            var filled = (int)Math.Round(barWidth * Math.Clamp(ratio, 0, 1));

            region.Fill(x, y, filled, '█', fillColor);
            region.Fill(x + filled, y, barWidth - filled, '░', ColorToken.Dim);
            region.Write(x + barWidth + 2, y, caption);
        }

        /// <summary>
        /// <see cref="Region"/>-scoped equivalent of the two-segment <see cref="Draw(Frame, int, int, int, double, double, string, ColorToken, ColorToken)"/> -
        /// see the single-segment <see cref="Region"/> overload above for why this matters.
        /// </summary>
        public static void Draw(Region region, int x, int y, int maxBarWidth, double primaryRatio, double secondaryRatio, string caption, ColorToken primaryColor = ColorToken.Accent, ColorToken secondaryColor = ColorToken.Warning)
        {
            var availableWidth = region.Width - x - caption.Length - 3;
            var barWidth = Math.Max(0, Math.Min(maxBarWidth, availableWidth));

            var primaryWidth = (int)Math.Round(barWidth * Math.Clamp(primaryRatio, 0, 1));
            var secondaryWidth = Math.Min((int)Math.Round(barWidth * Math.Clamp(secondaryRatio, 0, 1)), barWidth - primaryWidth);

            region.Fill(x, y, primaryWidth, '█', primaryColor);
            region.Fill(x + primaryWidth, y, secondaryWidth, '█', secondaryColor);
            region.Fill(x + primaryWidth + secondaryWidth, y, barWidth - primaryWidth - secondaryWidth, '░', ColorToken.Dim);
            region.Write(x + barWidth + 2, y, caption);
        }

        #endregion
    }
}
