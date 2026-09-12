using System.ComponentModel;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// A single console screen: the unit the navigation service switches between and the
    /// render pipeline draws (Home, Projects, Project, Settings, Switch, ...).
    /// </summary>
    /// <remarks>
    /// Mirrors a WPF ViewModel's boundary: this type knows about models/services, but has
    /// no idea how it is rendered or how key presses physically reach it - it only exposes
    /// state (via <see cref="INotifyPropertyChanged"/>) and behavior (via <see cref="KeyHints"/>).
    /// The render pipeline (in Views/) is the only thing that knows how to turn either of
    /// those into console output, and the navigation service (in Services/) is the only thing
    /// that knows when <see cref="OnActivated"/>/<see cref="OnDeactivated"/>/<see cref="IDisposable.Dispose"/>
    /// get called.
    /// </remarks>
    public interface IScreenViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Properties

        /// <summary>
        /// Display name for this screen, shown in the title bar (e.g. "HOME", "PROJECTS").
        /// </summary>
        string Title { get; }

        /// <summary>
        /// The key bindings this screen currently accepts, in the order they should appear
        /// in the footer (e.g. "[i] in  [o] out  [s] switch  [a] all  [q] quit").
        /// </summary>
        /// <remarks>
        /// This list doubles as the input dispatch table: the render pipeline matches a
        /// pressed key against these entries and invokes the bound <see cref="KeyHint.Command"/>
        /// directly when it can execute. A screen only sees raw console input for things that
        /// don't fit this shape (free-form text entry), through a narrower, separate mechanism.
        /// Expected to change over time as a screen's state changes (e.g. Home's hints differ
        /// while clocked in vs. not clocked in) - raise <see cref="INotifyPropertyChanged.PropertyChanged"/>
        /// for this property when that happens so the footer redraws.
        /// </remarks>
        IReadOnlyList<KeyHint> KeyHints { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Called by the navigation service right after this screen becomes <c>Current</c>.
        /// Use this to start timers, subscribe to data changes, or refresh state that should
        /// only be live while the screen is actually showing.
        /// </summary>
        void OnActivated();

        /// <summary>
        /// Called by the navigation service right before this screen stops being <c>Current</c>.
        /// This is not the same as disposal: a long-lived screen (e.g. Home) is deactivated on
        /// every visit but only disposed once, when the app shuts down.
        /// </summary>
        void OnDeactivated();

        #endregion
    }

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
