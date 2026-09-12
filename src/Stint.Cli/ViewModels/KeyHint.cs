using CommunityToolkit.Mvvm.Input;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// One key binding a screen currently accepts: the physical key(s) that trigger it, the
    /// label shown for it in the footer (e.g. "in" for "[i] in"), and the command the render
    /// pipeline invokes when one of <see cref="Keys"/> is pressed and the command can execute.
    /// </summary>
    /// <remarks>
    /// Two hints may share the same <see cref="Label"/> while binding different keys to
    /// different commands (e.g. an UpArrow hint moving selection up and a DownArrow hint
    /// moving it down, both labelled "select") - the footer renderer is responsible for
    /// noticing that and collapsing them into a single "[↑↓] select" entry. Turning a
    /// <see cref="System.ConsoleKey"/> into the glyph shown on screen (↑, esc, enter, ...) is
    /// also the renderer's job, not this type's.
    /// </remarks>
    public sealed class KeyHint
    {
        #region Constructors

        public KeyHint(string label, IRelayCommand command, params ConsoleKey[] keys)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentNullException.ThrowIfNull(command);
            if (keys is null || keys.Length == 0)
            {
                throw new ArgumentException("A key hint must bind at least one key.", nameof(keys));
            }

            Label = label;
            Command = command;
            Keys = keys;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Text shown next to the key(s) in the footer (e.g. "in", "select", "quit").
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// The key(s) that trigger <see cref="Command"/>. Usually one, but a hint may bind
        /// several equivalent keys to the same command (e.g. Enter and NumPadEnter).
        /// </summary>
        public IReadOnlyList<ConsoleKey> Keys { get; }

        /// <summary>
        /// The command invoked when one of <see cref="Keys"/> is pressed, provided
        /// <see cref="IRelayCommand.CanExecute"/> allows it.
        /// </summary>
        public IRelayCommand Command { get; }

        #endregion
    }
}
