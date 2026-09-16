using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Turns a screen's <see cref="KeyHint"/> list into its footer lines (e.g.
    /// <c>[i] in  [o] out  [s] switch  [q] quit</c>), wrapped to fit <paramref name="width"/>.
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

        /// <summary>
        /// Packs each hint's rendered "[key] label" chunk onto as many lines as it takes to fit
        /// within <paramref name="width"/> - greedily filling a line with as many chunks as it
        /// holds, then repeating on the next line for whatever didn't fit, and so on. This is
        /// what keeps a screen with a wide hint set (or a future translation whose labels run
        /// longer) from silently truncating instead of just wrapping.
        /// </summary>
        public static IReadOnlyList<string> Render(IReadOnlyList<KeyHint> hints, int width)
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

            var chunks = groups.Select(g => $"[{string.Concat(g.Keys.Select(Glyph))}] {g.Label}").ToList();

            return Wrap(chunks, width);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Recursively packs <paramref name="chunks"/> two-space-separated onto lines no wider
        /// than <paramref name="width"/>: fills the first line with as many chunks as fit, then
        /// wraps whatever's left onto however many further lines it takes.
        /// </summary>
        private static IReadOnlyList<string> Wrap(IReadOnlyList<string> chunks, int width)
        {
            if (chunks.Count == 0)
            {
                return [];
            }

            // The first chunk always starts the line, even if it alone exceeds width - there's
            // nothing shorter to fall back to, and SetLine truncating it is preferable to an
            // infinite loop that never consumes it.
            var line = chunks[0];
            var consumed = 1;

            for (; consumed < chunks.Count; consumed++)
            {
                var candidate = line + "  " + chunks[consumed];
                if (candidate.Length > width)
                {
                    break;
                }

                line = candidate;
            }

            var rest = Wrap(chunks.Skip(consumed).ToList(), width);
            return [line, ..rest];
        }

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
