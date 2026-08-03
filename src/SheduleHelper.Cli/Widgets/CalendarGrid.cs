using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Core.Models;

namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// A Monday-first month calendar - one row per week, one cell per day - used for the Month
    /// zoom level of the Reports screen. Each cell packs the day number and a plus/minus trend
    /// glyph into a single row rather than stacking day-number and balance on separate lines, to
    /// keep the whole grid to at most seven rows (a header plus six week rows).
    /// </summary>
    public static class CalendarGrid
    {
        #region Fields

        private static readonly string[] Headers = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
        private const int ColumnWidth = 8;

        #endregion

        #region Methods

        /// <summary>
        /// Draws the grid at (<paramref name="x"/>, <paramref name="y"/>): a header row, then one
        /// row per week.
        /// </summary>
        /// <param name="dayBuckets">Every day in the month, in chronological order.</param>
        public static void Draw(Frame frame, int x, int y, IReadOnlyList<ReportBucket> dayBuckets)
        {
            for (var i = 0; i < Headers.Length; i++)
            {
                frame.Write(x + i * ColumnWidth, y, Headers[i], ColorToken.Dim);
            }

            if (dayBuckets.Count == 0)
            {
                return;
            }

            var firstDayColumn = ((int)dayBuckets[0].Date.DayOfWeek + 6) % 7; // Monday=0 .. Sunday=6
            var row = 0;
            var col = firstDayColumn;

            foreach (var bucket in dayBuckets)
            {
                var cellX = x + col * ColumnWidth;
                var cellY = y + 1 + row;

                string text;
                ColorToken color;
                if (!bucket.HasData)
                {
                    text = $"{bucket.Date.Day,2} {(bucket.IsWeekend ? "·" : "-")}";
                    color = ColorToken.Dim;
                }
                else
                {
                    var balance = bucket.Worked - bucket.Target;
                    var symbol = balance >= TimeSpan.Zero ? "^" : "v";
                    text = $"{bucket.Date.Day,2} {symbol}";
                    color = balance >= TimeSpan.Zero ? ColorToken.Positive : ColorToken.Negative;
                }

                frame.Write(cellX, cellY, text, color);

                col++;
                if (col > 6)
                {
                    col = 0;
                    row++;
                }
            }
        }

        #endregion
    }
}
