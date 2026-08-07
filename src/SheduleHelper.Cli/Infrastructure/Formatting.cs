namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// Small display-formatting helpers shared across screens, kept in one place so "8h 05m" vs
    /// "+8h 05m" vs "8:05" formatting never drifts between screens.
    /// </summary>
    public static class Formatting
    {
        #region Methods

        /// <summary>
        /// Formats a duration as "<c>Xh YYm</c>", e.g. "8h 05m". The sign is dropped - callers that
        /// care about sign (e.g. a balance) prepend it themselves, see <see cref="Balance"/>.
        /// </summary>
        public static string Duration(TimeSpan duration)
        {
            var totalMinutes = (int)Math.Round(Math.Abs(duration.TotalMinutes));
            return $"{totalMinutes / 60}h {totalMinutes % 60:00}m";
        }

        /// <summary>
        /// Formats a balance as a signed duration, e.g. "+2h 15m" or "-0h 45m".
        /// </summary>
        public static string Balance(TimeSpan balance)
        {
            var sign = balance < TimeSpan.Zero ? "-" : "+";
            return $"{sign}{Duration(balance)}";
        }

        /// <summary>
        /// Formats a <see cref="TimeOnly"/> as "HH:mm", with the colon escaped so it is never
        /// swapped for the current culture's time separator.
        /// </summary>
        public static string Time(TimeOnly time) => time.ToString(@"HH\:mm");

        /// <summary>
        /// Formats a <see cref="DateTime"/>'s time-of-day as "HH:mm", with the colon escaped so it
        /// is never swapped for the current culture's time separator.
        /// </summary>
        public static string Time(DateTime time) => time.ToString(@"HH\:mm");

        /// <summary>
        /// Shortens <paramref name="text"/> to at most <paramref name="width"/> columns, marking the
        /// cut with an ellipsis. <see cref="Frame"/> clips silently, which is right for a value that
        /// merely runs to the edge but wrong for a sentence - a clipped sentence reads as a complete
        /// one that happens to end oddly, so the reader never learns anything was lost.
        /// </summary>
        public static string Truncate(string text, int width)
        {
            if (width <= 0)
            {
                return string.Empty;
            }

            return text.Length <= width ? text : $"{text[..(width - 1)].TrimEnd()}…";
        }

        /// <summary>
        /// Splits a description into at most <paramref name="maxLines"/> lines, each shortened to
        /// <paramref name="width"/> columns via <see cref="Truncate"/>, for previewing it inline
        /// without showing the whole thing. <c>Truncated</c> says whether there was more left over -
        /// either more line breaks than <paramref name="maxLines"/>, or (for the last shown line) the
        /// line itself ran past <paramref name="width"/> - so callers know whether to offer a way to
        /// view the rest rather than silently dropping it.
        /// </summary>
        public static (List<string> Lines, bool Truncated) PreviewLines(string text, int width, int maxLines)
        {
            var lines = text.Split('\n');
            var shown = lines.Take(maxLines).Select(line => Truncate(line, width)).ToList();
            var truncated = lines.Length > maxLines || lines.Take(maxLines).Any(line => line.Length > width);

            return (shown, truncated);
        }

        /// <summary>
        /// Summarizes a description as its first line plus a "(+N more)" count of any lines after
        /// it, for a one-row field preview (e.g. Task/Project edit screens' Description row) where
        /// there's no room for more than that. Returns <c>"(none)"</c> when there's no description
        /// at all, so the row never reads as blank/broken.
        /// </summary>
        public static string PreviewSummary(string? text, int width)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "(none)";
            }

            var lines = text.Split('\n');
            if (lines.Length == 1)
            {
                return Truncate(lines[0], width);
            }

            var suffix = $" (+{lines.Length - 1} more)";
            return Truncate(lines[0], Math.Max(1, width - suffix.Length)) + suffix;
        }

        /// <summary>
        /// Word-wraps <paramref name="text"/> to <paramref name="width"/> columns, for showing a
        /// description as a paragraph rather than a single truncated line. Explicit line breaks in
        /// <paramref name="text"/> are kept as hard breaks (including blank lines); a single word
        /// too long to fit on its own line is hard-broken at <paramref name="width"/> rather than
        /// overflowing it, since there is no space to break on.
        /// </summary>
        public static List<string> Wrap(string text, int width)
        {
            width = Math.Max(1, width);
            var result = new List<string>();

            foreach (var paragraph in text.Split('\n'))
            {
                if (paragraph.Length == 0)
                {
                    result.Add(string.Empty);
                    continue;
                }

                var current = "";
                foreach (var word in paragraph.Split(' '))
                {
                    var candidate = current.Length == 0 ? word : $"{current} {word}";
                    if (candidate.Length <= width)
                    {
                        current = candidate;
                        continue;
                    }

                    if (current.Length > 0)
                    {
                        result.Add(current);
                        current = "";
                    }

                    var remaining = word;
                    while (remaining.Length > width)
                    {
                        result.Add(remaining[..width]);
                        remaining = remaining[width..];
                    }

                    current = remaining;
                }

                result.Add(current);
            }

            return result;
        }

        /// <summary>
        /// Marks <paramref name="text"/> as cut off by ending it with an ellipsis within
        /// <paramref name="width"/> columns - even when <paramref name="text"/> already fits on its
        /// own, unlike <see cref="Truncate"/>. For the last line a preview can fit when there is more
        /// text after it that the preview has nowhere to put (e.g. more wrapped lines than the
        /// available rows) - a plain "…" rather than a "more" prompt, since the preview itself isn't
        /// interactive or scrollable; the affordance to actually read the rest is a separate action
        /// the caller offers alongside it.
        /// </summary>
        public static string MarkTruncated(string text, int width)
        {
            if (width <= 0)
            {
                return string.Empty;
            }

            return text.Length <= width - 1 ? $"{text}…" : $"{text[..(width - 1)].TrimEnd()}…";
        }

        #endregion
    }
}
