using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Non-generic handle onto a <see cref="ScreenView{TViewModel}"/> - what the render
    /// pipeline holds a collection of and looks up by <see cref="ViewModelType"/>, since it
    /// only knows the current screen as a plain <see cref="IScreenViewModel"/>.
    /// </summary>
    public interface IScreenView
    {
        #region Properties

        /// <summary>The concrete ViewModel type this view knows how to render.</summary>
        Type ViewModelType { get; }

        #endregion

        #region Methods

        /// <summary>Draws <paramref name="viewModel"/>'s body content into <paramref name="buffer"/>.</summary>
        void Render(IScreenViewModel viewModel, ScreenBuffer buffer);

        /// <summary>
        /// Offers a key the current <see cref="IScreenViewModel.KeyHints"/> didn't already
        /// handle. Returns true if this view consumed it (e.g. typing a digit into a masked
        /// field) - false lets the pipeline fall back to normal <see cref="KeyHint"/> dispatch.
        /// </summary>
        bool HandleKey(IScreenViewModel viewModel, ConsoleKeyInfo key);

        #endregion
    }

    /// <summary>
    /// Base class for one screen's view: draws its body content and, optionally, intercepts
    /// raw keys its ViewModel can't represent as a fixed <see cref="KeyHint"/> (free-form text
    /// entry). Title/footer chrome is drawn by the pipeline itself, the same way for every
    /// screen - this only ever fills the body region between them.
    /// </summary>
    /// <typeparam name="TViewModel">The concrete ViewModel type this view renders.</typeparam>
    public abstract class ScreenView<TViewModel> : IScreenView where TViewModel : IScreenViewModel
    {
        #region Properties

        /// <inheritdoc />
        public Type ViewModelType => typeof(TViewModel);

        #endregion

        #region Methods

        /// <inheritdoc cref="IScreenView.Render"/>
        public abstract void Render(TViewModel viewModel, ScreenBuffer buffer);

        /// <inheritdoc cref="IScreenView.HandleKey"/>
        public virtual bool HandleKey(TViewModel viewModel, ConsoleKeyInfo key) => false;

        void IScreenView.Render(IScreenViewModel viewModel, ScreenBuffer buffer) => Render((TViewModel)viewModel, buffer);

        bool IScreenView.HandleKey(IScreenViewModel viewModel, ConsoleKeyInfo key) => HandleKey((TViewModel)viewModel, key);

        #endregion
    }
}
