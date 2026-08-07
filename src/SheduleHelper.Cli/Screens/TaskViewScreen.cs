using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// A full-screen, read-only view of a single task - pushed from <see cref="ProjectScreen"/> via
    /// "Open". Shows the same facts and description as the <see cref="Inspector"/> pane beside the
    /// task list, just with the whole screen to itself: useful both as the pane's full-width
    /// fallback on a narrow terminal, and as somewhere to read a description too long for even the
    /// pane's row budget, via the scrolling the pane deliberately doesn't offer.
    /// </summary>
    public sealed class TaskViewScreen : IScreen
    {
        #region Fields

        private readonly InspectorContent _content;
        private int _scrollOffset;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TaskViewScreen"/> class.
        /// </summary>
        public TaskViewScreen(InspectorContent content)
        {
            _content = content;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, $"TASK > {_content.Title}", "Esc Back");

            var y = 2;
            foreach (var (label, value, color) in _content.Facts)
            {
                frame.Write(1, y, label.PadRight(10));
                frame.Write(11, y, value, color);
                y++;
            }

            y++; // blank separator row between the facts and the description

            var lines = string.IsNullOrWhiteSpace(_content.Description)
                ? Array.Empty<string>()
                : Formatting.Wrap(_content.Description, Math.Max(1, frame.Width - 2)).ToArray();

            // -3 for the blank row above the key bar's rule, the rule itself, and the key bar row -
            // the same reservation DescriptionViewScreen makes for the same reason.
            var height = Math.Max(1, frame.Height - y - 3);
            var maxScroll = Math.Max(0, lines.Length - height);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

            for (var row = 0; row < height; row++)
            {
                var lineIndex = _scrollOffset + row;
                if (lineIndex >= lines.Length)
                {
                    break;
                }

                frame.Write(1, y + row, lines[lineIndex], ColorToken.Dim);
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
