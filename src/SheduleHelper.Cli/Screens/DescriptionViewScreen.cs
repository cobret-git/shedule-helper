using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// A full-screen, read-only view of a Task or Project description - pushed wherever a preview
    /// (see <see cref="Formatting.PreviewLines"/>) had to cut a description short, so there's
    /// somewhere to actually read the rest of it instead of it being permanently clipped.
    /// </summary>
    public sealed class DescriptionViewScreen : IScreen
    {
        #region Fields

        private readonly string _ownerTitle;
        private readonly string[] _lines;
        private int _scrollOffset;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DescriptionViewScreen"/> class.
        /// </summary>
        /// <param name="ownerTitle">The task title or project name this description belongs to, shown in the header for context.</param>
        /// <param name="text">The description's full text.</param>
        public DescriptionViewScreen(string ownerTitle, string text)
        {
            _ownerTitle = ownerTitle;
            _lines = text.Split('\n');
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, $"DESCRIPTION > {_ownerTitle}", "Esc Back");

            var height = Math.Max(1, frame.Height - 5);
            var maxScroll = Math.Max(0, _lines.Length - height);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

            for (var row = 0; row < height; row++)
            {
                var lineIndex = _scrollOffset + row;
                if (lineIndex >= _lines.Length)
                {
                    break;
                }

                frame.Write(1, 2 + row, Formatting.Truncate(_lines[lineIndex], Math.Max(1, frame.Width - 2)), ColorToken.Dim);
            }

            KeyBar.Draw(frame, ("up/down", "Scroll"), ("Esc", "Back"));
        }

        /// <inheritdoc/>
        public Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            switch (key.Key)
            {
                case ConsoleKey.UpArrow:
                    if (_scrollOffset > 0)
                    {
                        _scrollOffset--;
                    }
                    return Task.CompletedTask;
                case ConsoleKey.DownArrow:
                    _scrollOffset++; // clamped against the true max on the next Render
                    return Task.CompletedTask;
                case ConsoleKey.Escape:
                case ConsoleKey.Enter:
                    return screens.Pop();
                default:
                    return Task.CompletedTask;
            }
        }

        #endregion
    }
}
