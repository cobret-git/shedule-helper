using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// The <c>F1</c> help overlay, pushed on top of whatever screen was active. Purely static for
    /// now - grows a binding per screen as each one is built.
    /// </summary>
    public sealed class HelpScreen : IScreen
    {
        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, "HELP", "Esc Close");

            frame.Write(1, 3, "Global", ColorToken.Accent);
            frame.Write(3, 4, "F1    this help");
            frame.Write(3, 5, "F10   settings");
            frame.Write(3, 6, "Esc   back / cancel");
            frame.Write(3, 7, "Q     quit (from Home)");

            frame.Write(1, 9, "Lists", ColorToken.Accent);
            frame.Write(3, 10, "up/down move");
            frame.Write(3, 11, "Enter   open");
            frame.Write(3, 12, "1..9    jump to numbered row");

            frame.Write(1, 14, "Home", ColorToken.Accent);
            frame.Write(3, 15, "I  clock in           O  clock out");
            frame.Write(3, 16, "S  switch project     R  resolve unfinished day");
            frame.Write(3, 17, "P  projects browser");

            frame.Write(1, 19, "Editors", ColorToken.Accent);
            frame.Write(3, 20, "Enter  edit the selected field    left/right  toggle / change");
            frame.Write(3, 21, "Ctrl+S save                       Esc         cancel");

            KeyBar.Draw(frame, ("Esc", "Close"));
        }

        /// <inheritdoc/>
        public Task HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            return key.Key == ConsoleKey.Escape ? screens.Pop() : Task.CompletedTask;
        }

        #endregion
    }
}
