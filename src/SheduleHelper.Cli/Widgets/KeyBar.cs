using SheduleHelper.Cli.Infrastructure;

namespace SheduleHelper.Cli.Widgets
{
    /// <summary>
    /// The bottom key-binding bar every screen shares: a rule, then a row of "Key Label" pairs
    /// (e.g. "Esc Back"), the key itself in the accent colour.
    /// </summary>
    public static class KeyBar
    {
        #region Methods

        /// <summary>
        /// Draws the key bar on the frame's last row, with a rule immediately above it.
        /// </summary>
        /// <param name="frame">The frame to draw into.</param>
        /// <param name="bindings">The key/label pairs to display, left to right.</param>
        public static void Draw(Frame frame, params (string Key, string Label)[] bindings)
        {
            var y = frame.Height - 1;
            frame.Rule(y - 1);

            var x = 1;
            foreach (var (key, label) in bindings)
            {
                frame.Write(x, y, key, ColorToken.Accent);
                frame.Write(x + key.Length + 1, y, label);
                x += key.Length + 1 + label.Length + 3;
            }
        }

        #endregion
    }
}
