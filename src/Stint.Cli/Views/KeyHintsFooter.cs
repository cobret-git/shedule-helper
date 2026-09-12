using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Turns a screen's <see cref="KeyHint"/> list into its footer line (e.g.
    /// <c>[i] in  [o] out  [s] switch  [q] quit</c>).
    /// </summary>
    /// <remarks>
    /// Only hints whose <see cref="KeyHint.Command"/> can currently execute are shown - a
    /// screen never has to add/remove <see cref="IScreenViewModel.KeyHints"/> entries itself
    /// just because state changed, see <see cref="IScreenViewModel.KeyHints"/>. Hints sharing
    /// the same <see cref="KeyHint.Label"/> (e.g. an UpArrow and a DownArrow hint both labelled
    /// "select") are collapsed into a single bracket.
    /// </remarks>
    public static class KeyHintsFooter
    {
        #region Methods

        public static string Render(IReadOnlyList<KeyHint> hints)
        {
            var groups = new List<(string Label, List<ConsoleKey> Keys)>();

            foreach (var hint in hints.Where(h => h.Command.CanExecute(null)))
            {
                var index = groups.FindIndex(g => g.Label == hint.Label);
                if (index < 0)
                {
                    groups.Add((hint.Label, [..hint.Keys]));
                }
                else
                {
                    groups[index].Keys.AddRange(hint.Keys);
                }
            }

            return string.Join("  ", groups.Select(g => $"[{string.Concat(g.Keys.Select(Glyph))}] {g.Label}"));
        }

        #endregion

        #region Helpers

        private static string Glyph(ConsoleKey key) => key switch
        {
            // Plain ASCII, same as Left/Right below - the Unicode arrow glyphs (U+2191/U+2193)
            // render as blank cells in a classic raster-font console (no TrueType/Unicode glyph
            // support), showing up as "[] select" instead of "[↑↓] select".
            ConsoleKey.UpArrow => "^",
            ConsoleKey.DownArrow => "v",
            ConsoleKey.LeftArrow => "<",
            ConsoleKey.RightArrow => ">",
            ConsoleKey.Enter => "enter",
            ConsoleKey.Escape => "esc",
            ConsoleKey.Spacebar => "space",
            _ => key.ToString().ToLowerInvariant()
        };

        #endregion
    }
}
