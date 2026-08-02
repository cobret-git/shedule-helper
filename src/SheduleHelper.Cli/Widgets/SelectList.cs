namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// Owns the selection index for a vertical list of options moved with up/down, wrapping at
    /// each end. Rendering is left to the screen - each screen's rows differ too much (columns,
    /// colours, extra detail per row) to generalize yet; this only owns the index and its key handling.
    /// </summary>
    public sealed class SelectList
    {
        #region Fields

        private readonly int _count;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectList"/> class with the given number of rows.
        /// </summary>
        public SelectList(int count)
        {
            _count = count;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The currently selected row index.
        /// </summary>
        public int SelectedIndex { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Moves the selection on up/down arrow keys. Returns whether the key was handled, so a
        /// screen can fall through to its own bindings (Enter, Escape, ...) otherwise.
        /// </summary>
        public bool HandleKey(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    SelectedIndex = SelectedIndex == 0 ? _count - 1 : SelectedIndex - 1;
                    return true;
                case ConsoleKey.DownArrow:
                    SelectedIndex = SelectedIndex == _count - 1 ? 0 : SelectedIndex + 1;
                    return true;
                default:
                    return false;
            }
        }

        #endregion
    }
}
