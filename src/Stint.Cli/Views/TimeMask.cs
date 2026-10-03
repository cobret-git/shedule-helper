namespace Stint.Cli.Views
{
    /// <summary>
    /// How a time being typed into an HH:mm mask (see <c>TimeDigitsInput</c>) is drawn: the
    /// whole mask inverted, except the one column the next digit goes into, which is left
    /// un-inverted so it reads as a cursor sitting inside the highlighted field.
    /// </summary>
    public static class TimeMask
    {
        #region Methods

        /// <summary>
        /// Adds the inverted spans for <paramref name="mask"/> - which must already be the tail
        /// of <paramref name="line"/> on <paramref name="row"/> - to <paramref name="buffer"/>.
        /// </summary>
        public static void Render(ScreenBuffer buffer, int row, string line, string mask, int? cursorIndex)
        {
            buffer.SetLine(row, line);

            var maskStart = line.Length - mask.Length;

            if (cursorIndex is not int cursor)
            {
                buffer.AddColorSpan(row, maskStart, mask.Length, ConsoleColor.Black, ConsoleColor.White);
                return;
            }

            if (cursor > 0)
            {
                buffer.AddColorSpan(row, maskStart, cursor, ConsoleColor.Black, ConsoleColor.White);
            }

            var afterCursor = cursor + 1;
            if (afterCursor < mask.Length)
            {
                buffer.AddColorSpan(row, maskStart + afterCursor, mask.Length - afterCursor, ConsoleColor.Black, ConsoleColor.White);
            }
        }

        #endregion
    }
}
