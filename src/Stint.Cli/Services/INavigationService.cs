using Stint.Cli.Components;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Services
{
    /// <summary>
    /// Moves between screens (Home, Projects, Project, Settings, Switch, ...) using a back
    /// stack: <see cref="NavigateTo{TScreen}"/> pushes a new screen on top - the one you were
    /// on is deactivated but kept, in case you come back to it - and <see cref="GoBack"/> pops
    /// the top screen off (disposing it for good) and reveals the one beneath it again.
    /// </summary>
    public interface INavigationService
    {
        #region Properties

        /// <summary>
        /// The screen currently on top of the stack - the one that should be rendered.
        /// </summary>
        IScreenViewModel Current { get; }

        /// <summary>
        /// Whether <see cref="GoBack"/> would do anything right now. A screen uses this to
        /// decide whether to show a back/cancel key hint at all - the root screen never can.
        /// </summary>
        bool CanGoBack { get; }

        #endregion

        #region Events

        /// <summary>
        /// Raised whenever <see cref="Current"/> changes, after the change has already
        /// happened. The render pipeline subscribes to this to know when to redraw - and,
        /// later, to play a transition.
        /// </summary>
        event EventHandler<NavigatedEventArgs>? Navigated;

        #endregion

        #region Methods

        /// <summary>
        /// Pushes a fresh <typeparamref name="TScreen"/> on top of the stack and makes it
        /// <see cref="Current"/>.
        /// </summary>
        void NavigateTo<TScreen>() where TScreen : IScreenViewModel;

        /// <summary>
        /// Pushes a fresh <typeparamref name="TScreen"/> on top of the stack, hands it
        /// <paramref name="context"/> via <see cref="IScreenViewModel{TContext}.Initialize"/>,
        /// and makes it <see cref="Current"/>.
        /// </summary>
        void NavigateTo<TScreen, TContext>(TContext context) where TScreen : IScreenViewModel<TContext>;

        /// <summary>
        /// Jumps to a top-level section: unwinds the stack down to its root screen (disposing every
        /// screen above it, including the current one) and pushes a fresh <typeparamref name="TScreen"/>
        /// on top of the root - unless <typeparamref name="TScreen"/> is the root itself, which is
        /// just revealed. Going back from the section then always lands on the root.
        /// </summary>
        void NavigateToSection<TScreen>() where TScreen : IScreenViewModel;

        /// <summary>
        /// Opens a fresh <typeparamref name="TDialog"/> on top of the stack, hands it
        /// <paramref name="context"/>, and waits until the dialog closes itself: it is then popped
        /// and disposed, and its <see cref="IDialogScreenViewModel{TContext, TResult}.Result"/> is
        /// returned - <c>MessageBox.Show</c> for the console.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The generic arguments can't be inferred from the dialog type, so they're all spelled out:
        /// <c>await Navigation.ShowDialogAsync&lt;DatePickerScreenViewModel, DatePickerRequest, DatePickerResult&gt;(request)</c>.
        /// Wrap a frequently used dialog in a small extension method to hide that.
        /// </para>
        /// <para>
        /// The caller resumes synchronously inside the dialog's closing command, on the console
        /// loop's own thread and with the caller's screen already current again, so it may
        /// navigate onward straight away. If the app shuts down while a dialog is open, the task
        /// simply never completes.
        /// </para>
        /// </remarks>
        Task<TResult> ShowDialogAsync<TDialog, TContext, TResult>(TContext context) where TDialog : IDialogScreenViewModel<TContext, TResult>;

        /// <summary>
        /// Pops the current screen off the stack and disposes it, revealing the screen
        /// beneath it. Does nothing and returns <c>false</c> if already at the root.
        /// </summary>
        bool GoBack();

        #endregion
    }
}
