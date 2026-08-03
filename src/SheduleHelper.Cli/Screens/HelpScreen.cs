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

            // Section name in a left gutter with its bindings alongside, per the plan's §5.4 sketch -
            // a heading-per-block layout runs past row 21 and gets clipped by the key bar at 80x24.
            frame.Write(1, 3, "Global", ColorToken.Accent);
            frame.Write(14, 3, "F1   this help            F10  settings");
            frame.Write(14, 4, "Esc  back / cancel        Q    quit (from Home)");

            frame.Write(1, 6, "Lists", ColorToken.Accent);
            frame.Write(14, 6, "up/down  move             Enter  open");
            frame.Write(14, 7, "1..9  jump to numbered row");

            frame.Write(1, 9, "Home", ColorToken.Accent);
            frame.Write(14, 9, "I  clock in    O  clock out    S  switch project");
            frame.Write(14, 10, "P  projects    R  reports, or resolve an unfinished day");
            frame.Write(14, 11, "Clock in and out both offer a custom time too.", ColorToken.Dim);

            frame.Write(1, 13, "Editors", ColorToken.Accent);
            frame.Write(14, 13, "up/down  move fields - the selected one is always editable");
            frame.Write(14, 14, "left/right  cursor / toggle    Enter or F10  save    Esc  cancel");

            frame.Write(1, 16, "Automation", ColorToken.Accent);
            frame.Write(14, 16, "F10 chooses whether launching closes an unfinished day,");
            frame.Write(14, 17, "clocks today in, and continues the project you last tracked.");
            frame.Write(14, 18, "Whatever it did shows on Home: Esc dismisses, O and S correct it.", ColorToken.Dim);

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
