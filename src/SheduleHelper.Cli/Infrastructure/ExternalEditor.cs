using System.Diagnostics;
using System.Text;
using SheduleHelper.Cli.Screens;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// A single external-editor round trip in progress: the temp file <see cref="ExternalEditor.Begin"/>
    /// wrote to, and the process it launched on that file (or <see langword="null"/> if launching
    /// it failed outright).
    /// </summary>
    public sealed class ExternalEditSession
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ExternalEditSession"/> class.
        /// </summary>
        public ExternalEditSession(string filePath, Process? process)
        {
            FilePath = filePath;
            Process = process;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The scratch file the editor was pointed at.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// The launched editor process, or <see langword="null"/> if launching it failed - a
        /// misconfigured <c>$EDITOR</c>/<c>$VISUAL</c>, most likely. A caller that gets a
        /// <see langword="null"/> process here should not push <see cref="ExternalEditScreen"/>
        /// on it - there is nothing to wait for and no window for the user to have switched to.
        /// </summary>
        public Process? Process { get; }

        #endregion
    }

    /// <summary>
    /// Round-trips a description through the user's own text editor instead of an in-app multiline
    /// field: writes it to a temp file, launches a GUI editor on it, and later reads back whatever
    /// was saved. GUI editors only, by design - a console editor (vim, nano) needs the terminal
    /// itself, which would mean suspending this app's own screen/input loop around it (leaving the
    /// alternate screen buffer, pausing key reading, invalidating the frame diff...); not supported,
    /// so this round trip stays "launch a window, wait, read the file back" and nothing more.
    /// </summary>
    public static class ExternalEditor
    {
        #region Fields

        private const string ScratchDirectoryName = "scratch";

        #endregion

        #region Methods

        /// <summary>
        /// Writes <paramref name="initialValue"/> to a fresh scratch file named after
        /// <paramref name="ownerTitle"/> and launches the resolved editor command on it.
        /// </summary>
        /// <param name="ownerTitle">The task/project title, used only to name the scratch file so
        /// it reads sensibly in the editor's own window/tab title.</param>
        /// <param name="initialValue">The description's current text.</param>
        /// <param name="pathProvider">Resolves where the scratch file lives - alongside the
        /// database, not the OS temp directory, so a crash mid-edit leaves it somewhere this app's
        /// own <see cref="CleanUpStaleFiles"/> can find and remove rather than lost among unrelated
        /// temp files.</param>
        public static ExternalEditSession Begin(string ownerTitle, string initialValue, IPathProvider pathProvider)
        {
            var filePath = CreateScratchFile(ownerTitle, initialValue, pathProvider);
            var process = TryLaunch(filePath);
            return new ExternalEditSession(filePath, process);
        }

        /// <summary>
        /// Reads back whatever the editor saved to <paramref name="session"/>'s scratch file,
        /// normalizes it, and removes the file. If the file can't be read (locked, deleted out from
        /// under us, ...) <paramref name="fallbackValue"/> is returned instead - the caller's own
        /// value before the edit, so a read failure loses nothing rather than blanking the field.
        /// </summary>
        public static string Accept(ExternalEditSession session, string fallbackValue)
        {
            var text = fallbackValue;

            try
            {
                if (File.Exists(session.FilePath))
                {
                    text = Normalize(File.ReadAllText(session.FilePath));
                }
            }
            catch
            {
                // Not worth surfacing - the caller falls back to what the field already had.
            }

            TryDelete(session.FilePath);
            return text;
        }

        /// <summary>
        /// Discards <paramref name="session"/> without reading its scratch file - the edit is being
        /// cancelled, so whatever the editor saved (if anything) is simply removed unread.
        /// </summary>
        public static void Discard(ExternalEditSession session)
        {
            TryDelete(session.FilePath);
        }

        /// <summary>
        /// Sweeps leftover scratch files from a previous run that crashed or was killed mid-edit.
        /// Called once at startup - best-effort, since a locked or inaccessible scratch directory
        /// (e.g. an editor from the last run is somehow still holding a file open) shouldn't block
        /// this run from starting.
        /// </summary>
        public static void CleanUpStaleFiles(IPathProvider pathProvider)
        {
            try
            {
                var directory = ScratchDirectory(pathProvider);
                if (!Directory.Exists(directory))
                {
                    return;
                }

                foreach (var file in Directory.GetFiles(directory))
                {
                    TryDelete(file);
                }
            }
            catch
            {
                // Best-effort - see the remarks above.
            }
        }

        #endregion

        #region Helpers

        private static string ScratchDirectory(IPathProvider pathProvider)
        {
            var dataDirectory = Path.GetDirectoryName(pathProvider.DatabaseFilePath)!;
            return Path.Combine(dataDirectory, ScratchDirectoryName);
        }

        private static string CreateScratchFile(string ownerTitle, string initialValue, IPathProvider pathProvider)
        {
            var directory = ScratchDirectory(pathProvider);
            Directory.CreateDirectory(directory);

            // The GUID keeps concurrent edits (unlikely, but this app has more than one screen that
            // can open one of these) from colliding on the same file name.
            var fileName = $"{SanitizeFileName(ownerTitle)}-{Guid.NewGuid():N}.txt";
            var filePath = Path.Combine(directory, fileName);

            // CRLF and no BOM - what Notepad itself writes, so nothing round-tripped through it
            // looks any different from a file the user created there directly.
            File.WriteAllText(filePath, initialValue.Replace("\n", "\r\n"), new UTF8Encoding(false));
            return filePath;
        }

        private static string SanitizeFileName(string title)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(title.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "description" : cleaned;
        }

        /// <summary>
        /// Resolves and launches the editor command: <c>$VISUAL</c>, then <c>$EDITOR</c>, then
        /// Notepad - the same order most POSIX tools use, with Notepad standing in for the "always
        /// available" fallback a Unix shell gets from <c>vi</c>. Either environment variable may
        /// carry arguments (e.g. <c>"code --wait"</c>), so the first whitespace-separated token is
        /// taken as the executable and the rest as arguments before the scratch file's path is
        /// appended - this does not handle a variable whose own path contains a space, the same
        /// limitation tools like <c>git</c> place on <c>core.editor</c>.
        /// </summary>
        private static Process? TryLaunch(string filePath)
        {
            var command = Environment.GetEnvironmentVariable("VISUAL")
                ?? Environment.GetEnvironmentVariable("EDITOR")
                ?? "notepad.exe";

            var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
            {
                parts = new[] { "notepad.exe" };
            }

            try
            {
                var startInfo = new ProcessStartInfo { FileName = parts[0], UseShellExecute = false };
                foreach (var arg in parts.Skip(1))
                {
                    startInfo.ArgumentList.Add(arg);
                }

                startInfo.ArgumentList.Add(filePath);

                return Process.Start(startInfo);
            }
            catch
            {
                // A bad path, or a console editor with nowhere to attach a window - the caller
                // treats a null Process as "nothing launched" rather than crashing on it.
                return null;
            }
        }

        private static string Normalize(string text) => text.Replace("\r\n", "\n").TrimEnd('\n');

        private static void TryDelete(string filePath)
        {
            try
            {
                File.Delete(filePath);
            }
            catch
            {
                // Locked by a still-open editor, or already gone - not worth surfacing.
            }
        }

        #endregion
    }
}
