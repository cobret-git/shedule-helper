namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// A rectangular sub-area of a <see cref="Frame"/>, with its own local (0,0) origin. Lets a
    /// screen split its body into independent columns (e.g. a task list beside an inspector pane)
    /// without every draw call doing its own <c>frame.Width - paneWidth - ...</c> arithmetic - each
    /// side just writes at its own local coordinates and is clipped to its own bounds, never
    /// bleeding into the region next to it.
    /// </summary>
    public readonly record struct Region(Frame Frame, int X, int Y, int Width, int Height)
    {
        #region Methods

        /// <summary>
        /// Writes <paramref name="text"/> starting at (<paramref name="x"/>, <paramref name="y"/>),
        /// local to this region. Clips to the region's own bounds - not just the underlying frame's -
        /// so content from one region can never overwrite a neighbouring one.
        /// </summary>
        public void Write(int x, int y, string text, ColorToken fg = ColorToken.Default)
        {
            if (y < 0 || y >= Height || x >= Width || text.Length == 0)
            {
                return;
            }

            if (x < 0)
            {
                if (text.Length <= -x)
                {
                    return;
                }

                text = text[(-x)..];
                x = 0;
            }

            var available = Width - x;
            if (text.Length > available)
            {
                text = text[..available];
            }

            Frame.Write(X + x, Y + y, text, fg);
        }

        /// <summary>
        /// Writes <paramref name="text"/> right-aligned so it ends at <paramref name="rightEdgeExclusive"/>,
        /// local to this region.
        /// </summary>
        public void WriteRight(int rightEdgeExclusive, int y, string text, ColorToken fg = ColorToken.Default)
        {
            Write(rightEdgeExclusive - text.Length, y, text, fg);
        }

        /// <summary>
        /// Fills <paramref name="width"/> columns starting at (<paramref name="x"/>, <paramref name="y"/>)
        /// with <paramref name="c"/>, local to this region.
        /// </summary>
        public void Fill(int x, int y, int width, char c, ColorToken fg = ColorToken.Default)
        {
            Write(x, y, new string(c, width), fg);
        }

        /// <summary>
        /// Draws a horizontal rule spanning this region's full width at row <paramref name="y"/> -
        /// the region-scoped equivalent of <see cref="Frame.Rule"/>.
        /// </summary>
        public void Rule(int y, ColorToken fg = ColorToken.Dim)
        {
            Fill(0, y, Width, '─', fg);
        }

        #endregion
    }
}
