using SheduleHelper.Cli.Infrastructure;

namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// A single-line editable text buffer: cursor position, insert, backspace, delete, Home/End,
    /// left/right, and horizontal scroll when the value is wider than the field. The one genuinely
    /// tedious piece raw <see cref="System.Console"/> rendering needs - written once here, reused
    /// by every editor screen instead of re-solved per screen.
    /// </summary>
    /// <remarks>
    /// Deliberately does not interpret <see cref="ConsoleKey.Enter"/> or <see cref="ConsoleKey.Escape"/>
    /// itself - what those mean (commit the field, leave edit mode, cancel the whole form) is a
    /// per-screen decision, made by the form before it ever forwards a key here.
    /// </remarks>
    public sealed class TextField
    {
        #region Fields

        private readonly List<char> _chars;
        private int _cursor;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TextField"/> class with the given starting value.
        /// </summary>
        public TextField(string initialValue = "")
        {
            _chars = new List<char>(initialValue);
            _cursor = _chars.Count;
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
        /// consumed - <see cref="ConsoleKey.Enter"/>/<see cref="ConsoleKey.Escape"/> are never
        /// consumed (see remarks); everything else that isn't a printable character is ignored too.
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
                case ConsoleKey.Home:
                    _cursor = 0;
                    return true;
                case ConsoleKey.End:
                    _cursor = _chars.Count;
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
        /// drawn in the accent colour as a stand-in for a blinking terminal cursor.
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

        #endregion
    }
}
