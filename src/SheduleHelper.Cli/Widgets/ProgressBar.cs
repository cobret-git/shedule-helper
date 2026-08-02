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
        /// <param name="ratio">The filled proportion, clamped to [0, 1].</param>
        /// <param name="caption">Text drawn immediately after the bar, e.g. "5h 28m / 8h 00m".</param>
        public static void Draw(Frame frame, int x, int y, int maxBarWidth, double ratio, string caption)
        {
            var availableWidth = frame.Width - x - caption.Length - 3;
            var barWidth = Math.Max(0, Math.Min(maxBarWidth, availableWidth));
            var filled = (int)Math.Round(barWidth * Math.Clamp(ratio, 0, 1));

            frame.Fill(x, y, filled, '█', ColorToken.Accent);
            frame.Fill(x + filled, y, barWidth - filled, '░', ColorToken.Dim);
            frame.Write(x + barWidth + 2, y, caption);
        }

        #endregion
    }
}
