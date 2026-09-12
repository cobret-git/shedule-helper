namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// An <see cref="IScreenViewModel"/> that operates over a specific context object handed
    /// to it at navigation time (e.g. <c>ProjectViewModel : IScreenViewModel&lt;Project&gt;</c>
    /// displaying one particular <c>Project</c>).
    /// </summary>
    /// <remarks>
    /// Screens shaped like this are inherently per-visit: the navigation service resolves a
    /// fresh instance and calls <see cref="Initialize"/> with the context for that visit, then
    /// disposes the instance on the way out rather than keeping it around as a singleton -
    /// unlike a parameterless screen such as Home, which has no per-visit state to isolate.
    /// </remarks>
    /// <typeparam name="TContext">The type of object this screen displays or edits.</typeparam>
    public interface IScreenViewModel<in TContext> : IScreenViewModel
    {
        #region Methods

        /// <summary>
        /// Supplies the context this screen should display. Called by the navigation service
        /// immediately after resolving the instance, before <see cref="IScreenViewModel.OnActivated"/>.
        /// </summary>
        void Initialize(TContext context);

        #endregion
    }
}
