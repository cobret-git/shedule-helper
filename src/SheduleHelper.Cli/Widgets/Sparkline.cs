using SheduleHelper.Cli.Infrastructure;

namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// A single row of block characters (▁▂▃▄▅▆▇█), one per value, height scaled to the largest
    /// value in the series. Used for the Week/Quarter/Year zoom levels of the Reports screen -
    /// Month uses <see cref="CalendarGrid"/> instead. A simplification of a true multi-row bar
    /// chart: one glyph of resolution per column rather than a full grid, in exchange for needing
    /// only a single row.
    /// </summary>
    public static class Sparkline
    {
        #region Fields

        private static readonly char[] Levels = { '▁', '▂', '▃', '▄', '▅', '▆', '▇', '█' };

        #endregion

        #region Methods

        /// <summary>
        /// Draws one glyph per value, left to right starting at (<paramref name="x"/>, <paramref name="y"/>).
        /// </summary>
        public static void Draw(Frame frame, int x, int y, IReadOnlyList<double> values, ColorToken color)
        {
            if (values.Count == 0)
            {
                return;
            }

            var max = values.Max();
            if (max <= 0)
            {
                max = 1;
            }

            for (var i = 0; i < values.Count; i++)
            {
                var level = (int)Math.Round(values[i] / max * (Levels.Length - 1));
                level = Math.Clamp(level, 0, Levels.Length - 1);
                frame.Write(x + i, y, Levels[level].ToString(), color);
            }
        }

        #endregion
    }
}
