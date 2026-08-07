namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// A single character cell in a <see cref="Frame"/>.
    /// </summary>
    public readonly record struct Cell(char Char, ColorToken Fg)
    {
        public static readonly Cell Empty = new(' ', ColorToken.Default);
    }

    /// <summary>
    /// The char/colour buffer a screen draws into. Nothing touches the real console until
    /// <see cref="FrameRenderer.Flush"/> diffs a <see cref="Frame"/> against the previously rendered
    /// one - this is what lets independent widgets share one screen without fighting over the cursor.
    /// </summary>
    public sealed class Frame
    {
        #region Fields

        private readonly Cell[,] _cells;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new, blank <see cref="Frame"/> of the given size.
        /// </summary>
        public Frame(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new Cell[height, width];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    _cells[y, x] = Cell.Empty;
                }
            }
        }

        #endregion

        #region Properties

        public int Width { get; }
        public int Height { get; }

        /// <summary>
        /// Gets the cell at the given column/row. Out-of-bounds access is a bug in the caller, not
        /// something to silently clamp - unlike the <c>Write*</c> methods below, which clip.
        /// </summary>
        public Cell this[int x, int y] => _cells[y, x];

        #endregion

        #region Methods

        /// <summary>
        /// Writes <paramref name="text"/> starting at (<paramref name="x"/>, <paramref name="y"/>).
        /// Silently clips anything that falls outside the frame, so callers never need to
        /// pre-truncate for a narrow terminal.
        /// </summary>
        public void Write(int x, int y, string text, ColorToken fg = ColorToken.Default)
        {
            if (y < 0 || y >= Height)
            {
                return;
            }

            for (var i = 0; i < text.Length; i++)
            {
                var cx = x + i;
                if (cx < 0 || cx >= Width)
                {
                    continue;
                }

                _cells[y, cx] = new Cell(text[i], fg);
            }
        }

        /// <summary>
        /// Writes <paramref name="text"/> right-aligned so it ends at <paramref name="rightEdgeExclusive"/>.
        /// </summary>
        public void WriteRight(int rightEdgeExclusive, int y, string text, ColorToken fg = ColorToken.Default)
        {
            Write(rightEdgeExclusive - text.Length, y, text, fg);
        }

        /// <summary>
        /// Fills <paramref name="width"/> columns starting at (<paramref name="x"/>, <paramref name="y"/>) with <paramref name="c"/>.
        /// </summary>
        public void Fill(int x, int y, int width, char c, ColorToken fg = ColorToken.Default)
        {
            Write(x, y, new string(c, width), fg);
        }

        /// <summary>
        /// Draws a full-width horizontal rule at row <paramref name="y"/>.
        /// </summary>
        public void Rule(int y, ColorToken fg = ColorToken.Dim)
        {
            Fill(0, y, Width, '─', fg);
        }

        /// <summary>
        /// Draws a vertical rule at column <paramref name="x"/>, spanning <paramref name="height"/>
        /// rows starting at <paramref name="y"/> - the divider between two side-by-side
        /// <see cref="Region"/>s (e.g. a list and an inspector pane).
        /// </summary>
        public void VRule(int x, int y, int height, ColorToken fg = ColorToken.Dim)
        {
            for (var row = y; row < y + height; row++)
            {
                Write(x, row, "│", fg);
            }
        }

        #endregion
    }
}
