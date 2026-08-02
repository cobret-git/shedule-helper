using System.Runtime.InteropServices;

namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// Owns the console's low-level setup: virtual-terminal processing, the alternate screen
    /// buffer, and cursor visibility. Restores everything on <see cref="Shutdown"/> - registered
    /// against <see cref="AppDomain.ProcessExit"/> and <see cref="Console.CancelKeyPress"/> as a
    /// safety net, so the user's shell is never left in a broken state, even on an unhandled
    /// exception or Ctrl+C reaching the process before <see cref="Console.TreatControlCAsInput"/> takes effect.
    /// </summary>
    public static class Terminal
    {
        #region Fields

        private const int StdOutputHandle = -11;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        private static bool _initialized;

        #endregion

        #region Properties

        /// <summary>
        /// Whether <see cref="Initialize"/> managed to enable ANSI virtual-terminal processing on
        /// the output handle. When false, <see cref="FrameRenderer"/> falls back to
        /// <see cref="Console.SetCursorPosition"/> and the 16-colour <see cref="ConsoleColor"/> palette.
        /// </summary>
        public static bool VirtualTerminalEnabled { get; private set; }

        /// <summary>
        /// The current console width, in columns. Read fresh every frame - there is no resize
        /// event on Windows, so <see cref="ConsoleApp"/> polls this each iteration instead.
        /// </summary>
        public static int Width => Console.WindowWidth;

        /// <summary>
        /// The current console height, in rows.
        /// </summary>
        public static int Height => Console.WindowHeight;

        #endregion

        #region Methods

        /// <summary>
        /// Enables VT processing (falling back gracefully if unsupported), switches to the
        /// alternate screen buffer, and hides the cursor. Idempotent - a second call is a no-op.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            VirtualTerminalEnabled = TryEnableVirtualTerminalProcessing();
            Theme.Initialize(VirtualTerminalEnabled);

            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CursorVisible = false;

            // Ctrl+C becomes a regular key delivered through Console.ReadKey (Control modifier +
            // ConsoleKey.C) instead of tearing down the process - screens decide what it means.
            Console.TreatControlCAsInput = true;

            if (VirtualTerminalEnabled)
            {
                Console.Out.Write("\x1b[?1049h");
            }
            else
            {
                Console.Clear();
            }

            AppDomain.CurrentDomain.ProcessExit += (_, _) => Shutdown();
            Console.CancelKeyPress += (_, _) => Shutdown();
        }

        /// <summary>
        /// Leaves the alternate screen buffer and restores the cursor. Safe to call more than
        /// once, and safe to call even if <see cref="Initialize"/> never ran.
        /// </summary>
        public static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _initialized = false;

            if (VirtualTerminalEnabled)
            {
                Console.Out.Write("\x1b[?1049l");
                Console.Out.Flush();
            }

            Console.CursorVisible = true;
            Console.ResetColor();
        }

        #endregion

        #region Helpers

        private static bool TryEnableVirtualTerminalProcessing()
        {
            try
            {
                var handle = GetStdHandle(StdOutputHandle);
                if (handle == IntPtr.Zero || !GetConsoleMode(handle, out var mode))
                {
                    return false;
                }

                return SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Native Methods

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        #endregion
    }
}
