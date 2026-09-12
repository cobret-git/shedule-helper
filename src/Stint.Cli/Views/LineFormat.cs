namespace Stint.Cli.Views
{
    /// <summary>
    /// Small text-formatting helpers shared across screen views (dot-leader rows, durations).
    /// </summary>
    public static class LineFormat
    {
        #region Methods

        /// <summary>
        /// A left label and a right-aligned value joined by a run of dots, e.g.
        /// <c>FIX ARG PARSER ...................... ACTIVE</c>.
        /// </summary>
        public static string DotLeader(string left, string right, int width = ScreenBuffer.Width)
        {
            var dotsCount = Math.Max(1, width - left.Length - right.Length - 2);
            return $"{left} {new string('.', dotsCount)} {right}";
        }

        /// <summary>Formats a duration as <c>Xh MMm</c>.</summary>
        public static string FormatDuration(TimeSpan value)
        {
            var magnitude = value.Duration();
            return $"{(value < TimeSpan.Zero ? "-" : string.Empty)}{(int)magnitude.TotalHours}h {magnitude.Minutes:00}m";
        }

        /// <summary>Formats a balance as <c>+Xh MMm</c>/<c>-Xh MMm</c> - always signed.</summary>
        public static string FormatBalance(TimeSpan value)
        {
            var magnitude = value.Duration();
            return $"{(value < TimeSpan.Zero ? "-" : "+")}{(int)magnitude.TotalHours}h {magnitude.Minutes:00}m";
        }

        #endregion
    }
}
