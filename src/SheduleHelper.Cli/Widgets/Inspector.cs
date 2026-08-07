using SheduleHelper.Cli.Infrastructure;

namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// What the inspector pane shows for whatever row is currently selected - a heading, a handful
    /// of short facts that don't fit in a list row, and the free-form description they all share.
    /// A screen builds one of these per selection change; the pane itself never reaches back into
    /// the screen's data to get it.
    /// </summary>
    /// <param name="Title">The selected row's name/title, drawn as the pane's own heading.</param>
    /// <param name="Facts">Label/value/colour rows drawn under the heading, in order.</param>
    /// <param name="Description">The full description text, or <see langword="null"/>/empty for none.</param>
    /// <param name="WrapTitle">
    /// When <see langword="true"/>, the heading wraps across as many rows as it needs instead of
    /// truncating to one - for a pane that's the one place showing a name in full (e.g. Home's
    /// currently-tracked task), as opposed to a list-row pane where the row itself already shows
    /// the name and the pane just needs to identify which one, so truncating it there costs nothing.
    /// </param>
    public sealed record InspectorContent(string Title, IReadOnlyList<(string Label, string Value, ColorToken Color)> Facts, string? Description, bool WrapTitle = false);

    /// <summary>
    /// The side pane that previews whatever row is selected in a list beside it - facts too long or
    /// too numerous for a table row, plus the description as wrapped paragraph text instead of a
    /// single truncated line. Read-only and non-scrolling by design: it is a preview, not a second
    /// place to read the whole thing - a screen offers a full-screen view alongside it for that.
    /// Collapses below <see cref="MinFrameWidthForPane"/> columns, where there isn't room for a
    /// second column without squeezing the list itself unreadable.
    /// </summary>
    public static class Inspector
    {
        #region Fields

        /// <summary>
        /// The frame width below which the pane has nowhere reasonable to go - see
        /// <see cref="ShouldShowPane"/>.
        /// </summary>
        public const int MinFrameWidthForPane = 100;

        // Facts are drawn as "{label,-8}{value}" - wide enough for every label this app currently
        // uses ("Status", "Logged", "Last") plus a visible gap before the value.
        private const int LabelColumnWidth = 8;

        #endregion

        #region Methods

        /// <summary>
        /// Whether a frame of the given width has room for the pane at all.
        /// </summary>
        public static bool ShouldShowPane(int frameWidth) => frameWidth >= MinFrameWidthForPane;

        /// <summary>
        /// The pane's width for a frame of the given width - roughly a third of it, clamped so it's
        /// never so narrow that wrapped prose turns to mush, nor so wide that it starves the list
        /// beside it.
        /// </summary>
        public static int PaneWidth(int frameWidth) => Math.Clamp((int)Math.Round(frameWidth * 0.34), 30, 44);

        /// <summary>
        /// Draws <paramref name="content"/> into <paramref name="region"/>: heading, a rule, the
        /// facts, then the wrapped description filling whatever rows remain. Leaves row 0 of the
        /// region blank so the heading lands one row down - lining it up with a neighbouring list's
        /// own "Section title" row, which by this app's convention also sits one row below the top
        /// of its region.
        /// </summary>
        public static void Draw(Region region, InspectorContent content)
        {
            var titleWidth = Math.Max(1, region.Width - 2);
            var titleLines = content.WrapTitle ? Formatting.Wrap(content.Title, titleWidth) : new List<string> { Formatting.Truncate(content.Title, titleWidth) };

            var y = 1;
            foreach (var line in titleLines)
            {
                region.Write(1, y, line, ColorToken.Accent);
                y++;
            }

            region.Rule(y);
            y++;

            foreach (var (label, value, color) in content.Facts)
            {
                region.Write(1, y, label.PadRight(LabelColumnWidth));
                region.Write(1 + LabelColumnWidth, y, Formatting.Truncate(value, Math.Max(1, region.Width - 2 - LabelColumnWidth)), color);
                y++;
            }

            if (string.IsNullOrWhiteSpace(content.Description))
            {
                return;
            }

            y++; // blank separator row between the facts and the description

            var textWidth = Math.Max(1, region.Width - 2);
            var lines = Formatting.Wrap(content.Description, textWidth);
            var maxLines = Math.Max(0, region.Height - y);

            for (var i = 0; i < lines.Count && i < maxLines; i++)
            {
                var isLastVisible = i == maxLines - 1 && lines.Count > maxLines;
                var line = isLastVisible ? Formatting.MarkTruncated(lines[i], textWidth) : lines[i];
                region.Write(1, y + i, line, ColorToken.Dim);
            }
        }

        #endregion
    }
}
