using System.Threading.Channels;

namespace SheduleHelper.Cli.Infrastructure
{
    /// <summary>
    /// The main render/input loop. Redraws on every key press and on a 1-second tick (so live
    /// timers keep moving even when the user isn't pressing anything), diffing each frame against
    /// the last one via <see cref="FrameRenderer"/> so an idle screen barely touches the console.
    /// </summary>
    public sealed class ConsoleApp
    {
        #region Fields

        private readonly ScreenStack _screens = new();

        #endregion

        #region Methods

        /// <summary>
        /// Runs the loop until <paramref name="initialScreen"/> (or whatever it navigates to) calls
        /// <see cref="ScreenStack.Quit"/>. Owns <see cref="Terminal"/>'s lifecycle - initializes it
        /// before the first frame and restores the console in a <see langword="finally"/>, so a
        /// screen throwing never leaves the terminal in the alternate screen buffer.
        /// </summary>
        public async Task RunAsync(IScreen initialScreen, CancellationToken cancellationToken)
        {
            Terminal.Initialize();

            try
            {
                await _screens.Push(initialScreen);

                var keys = Channel.CreateUnbounded<ConsoleKeyInfo>();
                _ = ReadKeysAsync(keys.Writer, cancellationToken);

                Frame? previous = null;

                while (!cancellationToken.IsCancellationRequested)
                {
                    using var tickCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    tickCts.CancelAfter(TimeSpan.FromSeconds(1));

                    try
                    {
                        var key = await keys.Reader.ReadAsync(tickCts.Token);
                        if (_screens.Current is { } activeScreen)
                        {
                            await activeScreen.HandleKey(key, _screens);
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // The 1-second tick fired with no key pressed - fall through and redraw anyway.
                    }

                    var current = _screens.Current;
                    if (current is null)
                    {
                        break;
                    }

                    var frame = new Frame(Terminal.Width, Terminal.Height);
                    current.Render(frame);
                    FrameRenderer.Flush(frame, previous);
                    previous = frame;
                }
            }
            finally
            {
                Terminal.Shutdown();
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Reads console keys on a background task and forwards them to <paramref name="writer"/>.
        /// <see cref="Console.ReadKey"/> has no async/cancellable overload, so this polls
        /// <see cref="Console.KeyAvailable"/> - the cost is hidden behind the channel; the main
        /// loop above never busy-polls, it just awaits with a timeout.
        /// </summary>
        private static async Task ReadKeysAsync(ChannelWriter<ConsoleKeyInfo> writer, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        await writer.WriteAsync(key, cancellationToken);
                    }
                    else
                    {
                        await Task.Delay(15, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the app is shutting down.
            }
        }

        #endregion
    }
}
