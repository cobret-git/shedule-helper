using SheduleHelper.Cli.Infrastructure;

namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// One block in a <see cref="TimelineStrip"/> - a span of time that was actively tracked. A
    /// <see langword="null"/> <see cref="End"/> means the block is still open (extends to "now").
    /// </summary>
    public readonly record struct TimelineBlock(DateTime Start, DateTime? End);

    /// <summary>
    /// A single row of block characters spanning a time range, filled wherever a
    /// <see cref="TimelineBlock"/> covers it and empty elsewhere - the CLI equivalent of the WinUI
    /// app's <c>TimelineBar</c> control. Deliberately one style for "tracked" and one for "gap" -
    /// colour-coding individual projects needs a larger palette than <see cref="ColorToken"/>
    /// currently has, so that's deferred until the Reports screen needs it too.
    /// </summary>
    public static class TimelineStrip
    {
        #region Methods

        /// <summary>
        /// Draws the strip at (<paramref name="x"/>, <paramref name="y"/>), spanning <paramref name="width"/> columns.
        /// </summary>
        /// <param name="frame">The frame to draw into.</param>
        /// <param name="x">The column the strip starts at.</param>
        /// <param name="y">The row to draw on.</param>
        /// <param name="width">How many columns wide the strip is.</param>
        /// <param name="rangeStart">The instant the first column represents.</param>
        /// <param name="rangeEnd">The instant the last column represents. Must be after <paramref name="rangeStart"/>.</param>
        /// <param name="blocks">The tracked spans to fill in.</param>
        public static void Draw(Frame frame, int x, int y, int width, DateTime rangeStart, DateTime rangeEnd, IReadOnlyList<TimelineBlock> blocks)
        {
            var totalSeconds = (rangeEnd - rangeStart).TotalSeconds;
            if (totalSeconds <= 0 || width <= 0)
            {
                return;
            }

            for (var column = 0; column < width; column++)
            {
                var instant = rangeStart.AddSeconds(totalSeconds * column / width);
                var tracked = IsTracked(blocks, instant);
                frame.Write(x + column, y, tracked ? "█" : "░", tracked ? ColorToken.Accent : ColorToken.Dim);
            }
        }

        #endregion

        #region Helpers

        private static bool IsTracked(IReadOnlyList<TimelineBlock> blocks, DateTime instant)
        {
            foreach (var block in blocks)
            {
                if (block.Start <= instant && instant < (block.End ?? DateTime.Now))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
