using SheduleHelper.Cli.Infrastructure;

namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// An editable text buffer: cursor position, insert, backspace, delete, Home/End, left/right,
    /// and horizontal scroll when the value is wider than the field. The one genuinely tedious
    /// piece raw <see cref="System.Console"/> rendering needs - written once here, reused by every
    /// editor screen instead of re-solved per screen.
    /// </summary>
    /// <remarks>
    /// Deliberately does not interpret <see cref="ConsoleKey.Escape"/> itself - leaving edit mode or
    /// cancelling the whole form is a per-screen decision, made by the form before it ever forwards
    /// a key here. <see cref="ConsoleKey.Enter"/> is the one exception: in single-line mode (the
    /// default) it is likewise left for the screen to interpret (commit the field, save the form),
    /// but in multiline mode - see the <paramref name="multiline"/> constructor parameter - it is
    /// consumed here to insert a line break, since a multiline field with no way to start a new line
    /// would defeat the point of being multiline.
    /// </remarks>
    public sealed class TextField
    {
        #region Fields

        private readonly List<char> _chars;
        private readonly bool _multiline;
        private int _cursor;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TextField"/> class with the given starting value.
        /// </summary>
        /// <param name="initialValue">The value the field starts pre-filled with.</param>
        /// <param name="multiline">
        /// When <see langword="true"/>, <see cref="ConsoleKey.Enter"/> inserts a line break instead
        /// of being left unconsumed, and <see cref="ConsoleKey.UpArrow"/>/<see cref="ConsoleKey.DownArrow"/>
        /// move the cursor between lines rather than being ignored. Use <see cref="DrawMultiline"/>
        /// to render a field constructed this way - <see cref="Draw"/> only shows a single line.
        /// </param>
        public TextField(string initialValue = "", bool multiline = false)
        {
            _chars = new List<char>(initialValue);
            _cursor = _chars.Count;
            _multiline = multiline;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The field's current text.
        /// </summary>
        public string Value => new string(_chars.ToArray());

        #endregion

        #region Methods

        /// <summary>
        /// Handles a single key press while this field is being edited. Returns whether the key was
        /// consumed - see the remarks on <see cref="ConsoleKey.Enter"/> and
        /// <see cref="ConsoleKey.Escape"/> above; everything else that isn't a printable character
        /// is ignored too.
        /// </summary>
        public bool HandleKey(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.LeftArrow:
                    if (_cursor > 0)
                    {
                        _cursor--;
                    }
                    return true;
                case ConsoleKey.RightArrow:
                    if (_cursor < _chars.Count)
                    {
                        _cursor++;
                    }
                    return true;
                case ConsoleKey.UpArrow when _multiline:
                    MoveVertical(-1);
                    return true;
                case ConsoleKey.DownArrow when _multiline:
                    MoveVertical(1);
                    return true;
                case ConsoleKey.Home:
                    _cursor = _multiline ? LineStart(_cursor) : 0;
                    return true;
                case ConsoleKey.End:
                    _cursor = _multiline ? LineEnd(_cursor) : _chars.Count;
                    return true;
                case ConsoleKey.Backspace:
                    if (_cursor > 0)
                    {
                        _chars.RemoveAt(_cursor - 1);
                        _cursor--;
                    }
                    return true;
                case ConsoleKey.Delete:
                    if (_cursor < _chars.Count)
                    {
                        _chars.RemoveAt(_cursor);
                    }
                    return true;
                case ConsoleKey.Enter:
                    if (!_multiline)
                    {
                        return false;
                    }

                    _chars.Insert(_cursor, '\n');
                    _cursor++;
                    return true;
                case ConsoleKey.Escape:
                    return false;
                default:
                    if (!char.IsControl(key.KeyChar))
                    {
                        _chars.Insert(_cursor, key.KeyChar);
                        _cursor++;
                        return true;
                    }
                    return false;
            }
        }

        /// <summary>
        /// Draws the field's value in a fixed-width box, scrolling horizontally so the cursor
        /// always stays visible. When <paramref name="editing"/>, the character at the cursor is
        /// drawn in the accent colour as a stand-in for a blinking terminal cursor. Only shows a
        /// single line - a multiline field drawn with this would just show whichever line the
        /// cursor is on with its newlines rendered as control characters, so use
        /// <see cref="DrawMultiline"/> for those instead.
        /// </summary>
        public void Draw(Frame frame, int x, int y, int width, bool editing)
        {
            var text = Value;
            var scrollOffset = _cursor >= width ? _cursor - width + 1 : 0;

            var visible = text.Length > scrollOffset
                ? text.Substring(scrollOffset, Math.Min(width, text.Length - scrollOffset))
                : string.Empty;

            frame.Write(x, y, visible.PadRight(width));

            if (!editing)
            {
                return;
            }

            var cursorColumn = _cursor - scrollOffset;
            var cursorChar = cursorColumn < visible.Length ? visible[cursorColumn] : ' ';
            frame.Write(x + cursorColumn, y, cursorChar.ToString(), ColorToken.Accent);
        }

        /// <summary>
        /// Draws the field's value across up to <paramref name="height"/> rows, one per line break,
        /// vertically scrolling so the cursor's line always stays visible and horizontally scrolling
        /// that one line the same way <see cref="Draw"/> does. Other, off-cursor lines are shown
        /// from their own start rather than scrolled - fine for a description's short, wrapped-by-hand
        /// lines; a line long enough to need mid-line scrolling while not focused is not a case worth
        /// solving here.
        /// </summary>
        public void DrawMultiline(Frame frame, int x, int y, int width, int height, bool editing)
        {
            var lines = Value.Split('\n');
            var (cursorLine, cursorColumn) = LineAndColumn(_cursor);

            var maxScroll = Math.Max(0, lines.Length - height);
            var scrollY = Math.Clamp(cursorLine - height + 1, 0, maxScroll);

            for (var row = 0; row < height; row++)
            {
                var lineIndex = scrollY + row;
                var line = lineIndex < lines.Length ? lines[lineIndex] : string.Empty;
                var onCursorLine = lineIndex == cursorLine;

                var scrollX = onCursorLine && cursorColumn >= width ? cursorColumn - width + 1 : 0;
                var visible = line.Length > scrollX
                    ? line.Substring(scrollX, Math.Min(width, line.Length - scrollX))
                    : string.Empty;

                frame.Write(x, y + row, visible.PadRight(width));

                if (editing && onCursorLine)
                {
                    var cursorX = cursorColumn - scrollX;
                    var cursorChar = cursorX < visible.Length ? visible[cursorX] : ' ';
                    frame.Write(x + cursorX, y + row, cursorChar.ToString(), ColorToken.Accent);
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The index of the character right after the previous line break, or 0 if <paramref name="index"/>
        /// falls on the first line.
        /// </summary>
        private int LineStart(int index)
        {
            var i = index;
            while (i > 0 && _chars[i - 1] != '\n')
            {
                i--;
            }

            return i;
        }

        /// <summary>
        /// The index of the next line break at or after <paramref name="index"/>, or the buffer's
        /// length if <paramref name="index"/> falls on the last line.
        /// </summary>
        private int LineEnd(int index)
        {
            var i = index;
            while (i < _chars.Count && _chars[i] != '\n')
            {
                i++;
            }

            return i;
        }

        /// <summary>
        /// Which line <paramref name="index"/> falls on (0-based, counting line breaks before it)
        /// and its column within that line.
        /// </summary>
        private (int Line, int Column) LineAndColumn(int index)
        {
            var line = 0;
            var lineStartIndex = 0;

            for (var i = 0; i < index; i++)
            {
                if (_chars[i] == '\n')
                {
                    line++;
                    lineStartIndex = i + 1;
                }
            }

            return (line, index - lineStartIndex);
        }

        /// <summary>
        /// Moves the cursor to the line above (<paramref name="direction"/> &lt; 0) or below it,
        /// keeping the same column where the target line is long enough, clamped to its end
        /// otherwise. A no-op at the first/last line.
        /// </summary>
        private void MoveVertical(int direction)
        {
            var lineStart = LineStart(_cursor);
            var column = _cursor - lineStart;

            int targetLineStart;
            if (direction < 0)
            {
                if (lineStart == 0)
                {
                    return;
                }

                targetLineStart = LineStart(lineStart - 1);
            }
            else
            {
                var lineEnd = LineEnd(_cursor);
                if (lineEnd == _chars.Count)
                {
                    return;
                }

                targetLineStart = lineEnd + 1;
            }

            var targetLineEnd = LineEnd(targetLineStart);
            _cursor = Math.Min(targetLineStart + column, targetLineEnd);
        }

        #endregion
    }
}
