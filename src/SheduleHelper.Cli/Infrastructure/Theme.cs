namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// Semantic colours a screen can ask for - never a raw <see cref="ConsoleColor"/> or ANSI code,
    /// so the VT/fallback/NO_COLOR decision in <see cref="Theme"/> stays in exactly one place.
    /// </summary>
    public enum ColorToken
    {
        Default,
        Accent,
        Positive,
        Negative,
        Warning,
        Dim,
    }

    /// <summary>
    /// Resolves <see cref="ColorToken"/>s to the escape sequence or <see cref="ConsoleColor"/> to
    /// actually draw with, depending on what the terminal supports. Initialized once by
    /// <see cref="Terminal.Initialize"/>.
    /// </summary>
    public static class Theme
    {
        #region Properties

        /// <summary>
        /// Whether colour should be used at all - false when the terminal has no virtual-terminal
        /// support, or when the user has set <c>NO_COLOR</c>.
        /// </summary>
        public static bool ColorEnabled { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Decides <see cref="ColorEnabled"/> from the terminal's capability and the environment.
        /// </summary>
        /// <param name="virtualTerminalSupported">Whether <see cref="Terminal"/> managed to enable VT processing.</param>
        public static void Initialize(bool virtualTerminalSupported)
        {
            var noColor = Environment.GetEnvironmentVariable("NO_COLOR") is { Length: > 0 };
            ColorEnabled = virtualTerminalSupported && !noColor;
        }

        /// <summary>
        /// The ANSI SGR parameter body (without <c>ESC[</c>/<c>m</c>) for a 24-bit foreground colour, used on the VT rendering path.
        /// </summary>
        public static string AnsiForeground(ColorToken token) => token switch
        {
            ColorToken.Accent => "38;2;97;175;239",
            ColorToken.Positive => "38;2;98;209;150",
            ColorToken.Negative => "38;2;224;108;90",
            ColorToken.Warning => "38;2;224;175;90",
            ColorToken.Dim => "38;2;120;120;130",
            _ => "39",
        };

        /// <summary>
        /// The nearest 16-colour equivalent, used on the fallback rendering path (no VT support).
        /// </summary>
        public static ConsoleColor ConsoleForeground(ColorToken token) => token switch
        {
            ColorToken.Accent => ConsoleColor.Cyan,
            ColorToken.Positive => ConsoleColor.Green,
            ColorToken.Negative => ConsoleColor.Red,
            ColorToken.Warning => ConsoleColor.Yellow,
            ColorToken.Dim => ConsoleColor.DarkGray,
            _ => ConsoleColor.Gray,
        };

        #endregion
    }
}
