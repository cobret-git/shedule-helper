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

        private static readonly TimeSpan FrameDelay = TimeSpan.FromMilliseconds(150);

        private readonly INavigationService _navigation;
        private readonly ScreenViewRegistry _views;

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
                if (Console.KeyAvailable)
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
            var current = _navigation.Current;

            buffer.Clear();
            buffer.SetLine(0, TitleBar.Render(current.Title));

            _views.Resolve(current).Render(current, buffer);

            buffer.SetLine(ScreenBuffer.Height - 1, KeyHintsFooter.Render(current.KeyHints));
            buffer.Flush();
        }

        #endregion
    }
}
