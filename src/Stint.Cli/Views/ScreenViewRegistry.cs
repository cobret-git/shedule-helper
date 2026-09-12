using Stint.Cli.ViewModels;

namespace Stint.Cli.Views
{
    /// <summary>
    /// Looks up the <see cref="IScreenView"/> registered for a given <see cref="IScreenViewModel"/>
    /// instance's concrete type. One <see cref="IScreenView"/> is registered per screen (in
    /// <c>Program.cs</c>) - adding a new screen just means registering its view here, nothing
    /// about resolution itself changes.
    /// </summary>
    public sealed class ScreenViewRegistry
    {
        #region Fields

        private readonly Dictionary<Type, IScreenView> _viewsByViewModelType;

        #endregion

        #region Constructors

        public ScreenViewRegistry(IEnumerable<IScreenView> views)
        {
            ArgumentNullException.ThrowIfNull(views);

            _viewsByViewModelType = views.ToDictionary(v => v.ViewModelType);
        }

        #endregion

        #region Methods

        public IScreenView Resolve(IScreenViewModel viewModel)
        {
            var viewModelType = viewModel.GetType();
            return _viewsByViewModelType.TryGetValue(viewModelType, out var view)
                ? view
                : throw new InvalidOperationException($"No {nameof(IScreenView)} is registered for {viewModelType.Name}.");
        }

        #endregion
    }
}
