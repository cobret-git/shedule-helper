using System.Text;

namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// Draws a <see cref="Frame"/> to the real console, writing only the cells that changed since
    /// the previously rendered frame. Redrawing every cell every tick (e.g. for the live clock)
    /// makes the whole screen shimmer; diffing means an idle screen rewrites a handful of characters.
    /// </summary>
    public static class FrameRenderer
    {
        #region Methods

        /// <summary>
        /// Flushes <paramref name="current"/> to the console, using <paramref name="previous"/> (if
        /// any, and the same size) to skip unchanged cells.
        /// </summary>
        public static void Flush(Frame current, Frame? previous)
        {
            if (previous is not null && (previous.Width != current.Width || previous.Height != current.Height))
            {
                previous = null;
            }

            if (Terminal.VirtualTerminalEnabled)
            {
                FlushVirtualTerminal(current, previous);
            }
            else
            {
                FlushFallback(current, previous);
            }
        }

        #endregion

        #region Helpers

        private static bool Unchanged(Frame current, Frame? previous, int x, int y)
        {
            if (previous is null)
            {
                return false;
            }

            var prevCell = previous[x, y];
            var cell = current[x, y];
            return prevCell.Char == cell.Char && prevCell.Fg == cell.Fg;
        }

        /// <summary>
        /// Renders via ANSI escape sequences - one cursor move per changed run of cells, batched
        /// into a single write to avoid a visible left-to-right "typing" effect.
        /// </summary>
        private static void FlushVirtualTerminal(Frame current, Frame? previous)
        {
            var sb = new StringBuilder();
            ColorToken? activeColor = null;

            for (var y = 0; y < current.Height; y++)
            {
                var x = 0;
                while (x < current.Width)
                {
                    if (Unchanged(current, previous, x, y))
                    {
                        x++;
                        continue;
                    }

                    // Start of a changed run: position the cursor once, then keep writing while
                    // consecutive cells on this row keep differing from the previous frame.
                    sb.Append($"\x1b[{y + 1};{x + 1}H");
                    activeColor = null;

                    while (x < current.Width && !Unchanged(current, previous, x, y))
                    {
                        var cell = current[x, y];
                        if (activeColor != cell.Fg)
                        {
                            sb.Append($"\x1b[{Theme.AnsiForeground(cell.Fg)}m");
                            activeColor = cell.Fg;
                        }

                        sb.Append(cell.Char);
                        x++;
                    }
                }
            }

            if (sb.Length == 0)
            {
                return;
            }

            sb.Append("\x1b[0m");
            Console.Out.Write(sb.ToString());
            Console.Out.Flush();
        }

        /// <summary>
        /// Renders via <see cref="Console.SetCursorPosition"/> and <see cref="Console.ForegroundColor"/>,
        /// for terminals where virtual-terminal processing could not be enabled.
        /// </summary>
        private static void FlushFallback(Frame current, Frame? previous)
        {
            ConsoleColor? activeColor = null;

            for (var y = 0; y < current.Height; y++)
            {
                for (var x = 0; x < current.Width; x++)
                {
                    if (Unchanged(current, previous, x, y))
                    {
                        continue;
                    }

                    var cell = current[x, y];
                    Console.SetCursorPosition(x, y);

                    var color = Theme.ColorEnabled ? Theme.ConsoleForeground(cell.Fg) : ConsoleColor.Gray;
                    if (activeColor != color)
                    {
                        Console.ForegroundColor = color;
                        activeColor = color;
                    }

                    Console.Write(cell.Char);
                }
            }
        }

        #endregion
    }
}
