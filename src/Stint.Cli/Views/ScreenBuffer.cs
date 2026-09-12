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

        #endregion

        #region Constructors

        public ScreenBuffer()
        {
            Clear();
        }

        #endregion

        #region Methods

        /// <summary>Resets every line to blank. Call once per frame before drawing into it.</summary>
        public void Clear() => Array.Fill(_lines, new string(' ', Width));

        /// <summary>
        /// Sets one line of the frame, padding with spaces or truncating so it's always
        /// exactly <see cref="Width"/> characters.
        /// </summary>
        public void SetLine(int row, string text)
        {
            if (row < 0 || row >= Height)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }

            _lines[row] = text.Length >= Width ? text[..Width] : text.PadRight(Width);
        }

        /// <summary>Writes the current frame to the console, one line at a time.</summary>
        public void Flush()
        {
            for (var row = 0; row < Height; row++)
            {
                Console.SetCursorPosition(0, row);
                Console.Write(_lines[row]);
            }
        }

        #endregion
    }
}
