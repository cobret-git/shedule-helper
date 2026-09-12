namespace Stint.Cli.Views
{
    /// <summary>
    /// The fixed-size character grid every screen renders into.
    /// </summary>
    /// <remarks>
    /// A frame is built up in memory via <see cref="SetLine"/> calls, then <see cref="Flush"/>
    /// writes it to the console line by line, each line padded/truncated to exactly
    /// <see cref="Width"/> characters. Overwriting a full-width line every time - rather than
    /// clearing the console and rewriting it - is what keeps a shorter new frame from leaving
    /// a longer previous frame's leftover characters on screen, without any visible flicker.
    /// </remarks>
    public sealed class ScreenBuffer
    {
        #region Fields

        public const int Width = 48;
        public const int Height = 23;

        private readonly string[] _lines = new string[Height];

        // Per-line runs of inverted/colored text (e.g. the selected picker option, or a
        // progress bar's filled/empty segments). Empty means "draw this line in the console's
        // own current colors" - most lines never need this at all.
        private readonly List<ColorSpan>[] _colorSpans = Enumerable.Range(0, Height).Select(_ => new List<ColorSpan>()).ToArray();

        #endregion

        #region Constructors

        public ScreenBuffer()
        {
            Clear();
        }

        #endregion

        #region Methods

        /// <summary>Resets every line to blank (and clears any color spans). Call once per frame before drawing into it.</summary>
        public void Clear()
        {
            Array.Fill(_lines, new string(' ', Width));

            foreach (var spans in _colorSpans)
            {
                spans.Clear();
            }
        }

        /// <summary>
        /// Sets one line of the frame, padding with spaces or truncating so it's always
        /// exactly <see cref="Width"/> characters. Clears any color spans previously set for this row.
        /// </summary>
        public void SetLine(int row, string text)
        {
            if (row < 0 || row >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            _lines[row] = text.Length >= Width ? text[..Width] : text.PadRight(Width);
            _colorSpans[row].Clear();
        }

        /// <summary>
        /// Sets one line of the frame like <see cref="SetLine(int, string)"/>, and marks
        /// <paramref name="highlightLength"/> characters starting at <paramref name="highlightStart"/>
        /// to be drawn inverted (black on white) - how the picker shows which option is selected,
        /// without relying on a background/foreground brush concept the plain-text buffer doesn't have.
        /// </summary>
        public void SetLine(int row, string text, int highlightStart, int highlightLength)
        {
            SetLine(row, text);
            AddColorSpan(row, highlightStart, highlightLength, ConsoleColor.Black, ConsoleColor.White);
        }

        /// <summary>
        /// Marks <paramref name="length"/> characters starting at <paramref name="start"/> of a
        /// row already set via <see cref="SetLine(int, string)"/> to be drawn in
        /// <paramref name="foreground"/> (and, optionally, a different <paramref name="background"/>
        /// than the console's own current one) - e.g. one segment of a multi-color progress bar.
        /// Multiple non-overlapping spans may be added to the same row.
        /// </summary>
        public void AddColorSpan(int row, int start, int length, ConsoleColor foreground, ConsoleColor? background = null)
        {
            if (row < 0 || row >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            if (length <= 0)
            {
                return;
            }

            var clampedStart = Math.Clamp(start, 0, Width);
            var clampedLength = Math.Clamp(length, 0, Width - clampedStart);

            if (clampedLength > 0)
            {
                _colorSpans[row].Add(new ColorSpan(clampedStart, clampedLength, foreground, background));
            }
        }

        /// <summary>
        /// Writes the current frame to the console, one line at a time, anchored at
        /// <paramref name="left"/>/<paramref name="top"/> instead of the console's origin - how the
        /// pipeline centers the fixed-size grid inside a larger (or repositioned) console window.
        /// </summary>
        public void Flush(int left, int top)
        {
            var originalForeground = Console.ForegroundColor;
            var originalBackground = Console.BackgroundColor;

            for (var row = 0; row < Height; row++)
            {
                Console.SetCursorPosition(left, top + row);
                WriteLine(row, originalForeground, originalBackground);
            }

            // Leave the console exactly as we found it - nothing else in the pipeline expects
            // colors to still be changed after a frame is drawn.
            Console.ForegroundColor = originalForeground;
            Console.BackgroundColor = originalBackground;
        }

        #endregion

        #region Helpers

        /// <summary>Writes one line, switching colors for each of its spans (if any), in order.</summary>
        private void WriteLine(int row, ConsoleColor originalForeground, ConsoleColor originalBackground)
        {
            var text = _lines[row];
            var spans = _colorSpans[row];

            if (spans.Count == 0)
            {
                Console.Write(text);
                return;
            }

            var cursor = 0;
            foreach (var span in spans.OrderBy(s => s.Start))
            {
                if (span.Start > cursor)
                {
                    Console.Write(text[cursor..span.Start]);
                }

                Console.ForegroundColor = span.Foreground;
                Console.BackgroundColor = span.Background ?? originalBackground;
                Console.Write(text.Substring(span.Start, span.Length));
                Console.ForegroundColor = originalForeground;
                Console.BackgroundColor = originalBackground;

                cursor = span.Start + span.Length;
            }

            if (cursor < text.Length)
            {
                Console.Write(text[cursor..]);
            }
        }

        #endregion

        /// <summary>One colored run within a line - see <see cref="AddColorSpan"/>.</summary>
        private readonly record struct ColorSpan(int Start, int Length, ConsoleColor Foreground, ConsoleColor? Background);
    }
}
