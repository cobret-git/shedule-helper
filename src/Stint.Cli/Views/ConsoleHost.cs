using Stint.Cli.Services;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Drives the console: a single-threaded loop that polls for a key, dispatches it, redraws
    /// the current screen, and repeats. No dedicated input thread and no timers anywhere in the
    /// pipeline - a "live" value (the ticking clock, elapsed duration, ...) is just a property
    /// recomputed fresh on every redraw, not state pushed in from elsewhere. That keeps every
    /// ViewModel/View touched from exactly one thread, so nothing needs WPF-style Dispatcher
    /// marshaling.
    /// </summary>
    public sealed class ConsoleHost
    {
        #region Fields

        // Input is only ever noticed right after this delay elapses (the loop is a plain poll,
        // not an event), so this is also the worst-case key-press latency. 150ms sat well above
        // the ~100ms where a delay starts reading as "not instant"; 30ms keeps the same idle-CPU
        // behavior while staying under that threshold.
        private static readonly TimeSpan FrameDelay = TimeSpan.FromMilliseconds(30);

        private readonly INavigationService _navigation;
        private readonly ScreenViewRegistry _views;

        // Starts off-frame so the first stable frame always clears the console once before the
        // initial draw.
        private (int Left, int Top) _lastOrigin = (int.MinValue, int.MinValue);

        // Raw window metrics observed on the previous frame - null until the first frame runs.
        // Used only to detect "the window stopped changing", never to compute the origin itself.
        private (int Width, int Height, int Left, int Top)? _lastObservedWindow;

        #endregion

        #region Constructors

        public ConsoleHost(INavigationService navigation, ScreenViewRegistry views)
        {
            ArgumentNullException.ThrowIfNull(navigation);
            ArgumentNullException.ThrowIfNull(views);

            _navigation = navigation;
            _views = views;
        }

        #endregion

        #region Methods

        public async Task RunAsync(CancellationToken ct = default)
        {
            Console.CursorVisible = false;
            var buffer = new ScreenBuffer();

            while (!ct.IsCancellationRequested)
            {
                // Drain every key already queued this tick, not just one - the console's own
                // input buffer queues one event per OS key-repeat tick while a key is held, and
                // reading only one per frame lets that queue outrun release, so movement keeps
                // trickling out for a while after the key is let go.
                while (Console.KeyAvailable)
                {
                    Dispatch(Console.ReadKey(intercept: true));
                }

                Draw(buffer);

                try
                {
                    await Task.Delay(FrameDelay, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        #endregion

        #region Helpers

        private void Dispatch(ConsoleKeyInfo key)
        {
            var current = _navigation.Current;
            var view = _views.Resolve(current);

            if (view.HandleKey(current, key))
            {
                return;
            }

            var hint = current.KeyHints.FirstOrDefault(h => h.Keys.Contains(key.Key) && h.Command.CanExecute(null));
            hint?.Command.Execute(null);
        }

        private void Draw(ScreenBuffer buffer)
        {
            var window = ReadWindowMetrics();

            if (window != _lastObservedWindow)
            {
                // The window is actively being dragged/resized - wait for it to settle on one
                // size for a full frame before repositioning, instead of recentering (and
                // Console.Clear()-ing) on every in-between size the drag passes through.
                _lastObservedWindow = window;
                return;
            }

            if (window.Width < ScreenBuffer.Width || window.Height < ScreenBuffer.Height)
            {
                // Too small to fit the grid - Console.SetCursorPosition below would throw
                // ArgumentOutOfRangeException (and, left uncaught, take the whole render loop
                // down with it). Just stop repositioning/drawing until it grows back; whatever
                // was last drawn stays on screen, clipped by the console itself in the meantime.
                return;
            }

            var origin = GetOrigin(window);

            if (origin != _lastOrigin)
            {
                // Settled at a genuinely new position/size - clear so no leftover characters
                // remain at the old one.
                Console.Clear();
                _lastOrigin = origin;
            }

            var current = _navigation.Current;

            buffer.Clear();
            buffer.SetLine(0, TitleBar.Render(current.Title));

            _views.Resolve(current).Render(current, buffer);

            // Every screen gets the same footer chrome: a full-width rule directly above the key
            // hints, so the key hints never look like they're just floating under whatever the
            // screen's own last content row happened to be.
            buffer.SetLine(ScreenBuffer.Height - 2, new string('-', ScreenBuffer.Width));
            buffer.SetLine(ScreenBuffer.Height - 1, KeyHintsFooter.Render(current.KeyHints));
            buffer.Flush(origin.Left, origin.Top);
        }

        /// <summary>Snapshot of the console's window size/position, read once per frame.</summary>
        private static (int Width, int Height, int Left, int Top) ReadWindowMetrics() =>
            (Console.WindowWidth, Console.WindowHeight, Console.WindowLeft, Console.WindowTop);

        /// <summary>
        /// Where the fixed-size <see cref="ScreenBuffer"/> grid should be anchored so it sits in
        /// the center of the console window described by <paramref name="window"/>. Only called
        /// once the window has already been confirmed stable and big enough by <see cref="Draw"/>.
        /// </summary>
        private static (int Left, int Top) GetOrigin((int Width, int Height, int Left, int Top) window)
        {
            var left = window.Left + (window.Width - ScreenBuffer.Width) / 2;
            var top = window.Top + (window.Height - ScreenBuffer.Height) / 2;
            return (left, top);
        }

        #endregion
    }
}
