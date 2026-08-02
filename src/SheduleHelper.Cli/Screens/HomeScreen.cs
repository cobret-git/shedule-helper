using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Widgets;
using SheduleHelper.Core.Services;

namespace SheduleHelper.Cli.Screens
{
    /// <summary>
    /// Placeholder for the Home screen (The Daily Control Center) - proves the skeleton end to end
    /// (DI, migration, current-user resolution, the render loop) ahead of clock-in/out landing in
    /// the next milestone.
    /// </summary>
    public sealed class HomeScreen : IScreen
    {
        #region Fields

        private readonly ICurrentUserContext _currentUserContext;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HomeScreen"/> class.
        /// </summary>
        /// <param name="currentUserContext">Resolved once at startup; its <see cref="ICurrentUserContext.UserId"/> proves the database round-trip worked.</param>
        public HomeScreen(ICurrentUserContext currentUserContext)
        {
            _currentUserContext = currentUserContext;
        }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public void Render(Frame frame)
        {
            Header.Draw(frame, "SCHEDULE HELPER", DateTime.Now.ToString("ddd d MMM   HH:mm"));

            frame.Write(1, 3, $"Signed in as user #{_currentUserContext.UserId}.");
            frame.Write(1, 5, "Home screen skeleton is up - clock in/out lands in the next milestone.", ColorToken.Dim);

            KeyBar.Draw(frame, ("F1", "Help"), ("Q", "Quit"));
        }

        /// <inheritdoc/>
        public void HandleKey(ConsoleKeyInfo key, ScreenStack screens)
        {
            switch (key.Key)
            {
                case ConsoleKey.F1:
                    screens.Push(new HelpScreen());
                    break;
                case ConsoleKey.Q:
                    screens.Quit();
                    break;
            }
        }

        #endregion
    }
}
