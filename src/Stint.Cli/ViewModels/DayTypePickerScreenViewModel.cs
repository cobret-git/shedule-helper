using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// A kind chooser dialog: a short list of <see cref="DayType"/>s to pick one from, opened with a
    /// <see cref="DayTypePickerRequest"/> and closed with a <see cref="DayTypePickerResult"/> - see
    /// <c>NavigationDialogExtensions.PickDayTypeAsync</c>. See also
    /// <c>docs/update-v1.2.0/plan-new-render-48__idle.txt</c>.
    /// </summary>
    /// <remarks>
    /// Up/Down move the highlight (wrapping at both ends), Enter picks, Esc cancels.
    /// </remarks>
    public sealed partial class DayTypePickerScreenViewModel : DialogScreenViewModelBase<DayTypePickerRequest, DayTypePickerResult>
    {
        #region Fields

        private IReadOnlyList<DayType> _options = [];
        private int _selectedIndex;
        #endregion

        #region Constructors

        public DayTypePickerScreenViewModel()
        {
            Title = "PICK A KIND";

            KeyHints =
            [
                new KeyHint("select", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("select", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("confirm", ConfirmCommand, ConsoleKey.Enter),
                new KeyHint("cancel", CancelCommand, ConsoleKey.Escape)
            ];
        }

        #endregion

        #region Properties

        /// <summary>The kinds on offer, in display order.</summary>
        public IReadOnlyList<DayType> Options { get => _options; private set => SetProperty(ref _options, value); }

        /// <summary>The currently highlighted index into <see cref="Options"/>.</summary>
        public int SelectedIndex { get => _selectedIndex; private set => SetProperty(ref _selectedIndex, value); }

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Initialize(DayTypePickerRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.Options.Count == 0)
            {
                throw new ArgumentException("A picker needs at least one option.", nameof(request));
            }

            Options = request.Options;
            SelectedIndex = 0;
            Title = request.Title;
        }

        #endregion

        #region Commands

        // Wraps around at both ends, like every other picker.
        [RelayCommand] private void MoveSelectionUp() => SelectedIndex = (SelectedIndex - 1 + Options.Count) % Options.Count;

        [RelayCommand] private void MoveSelectionDown() => SelectedIndex = (SelectedIndex + 1) % Options.Count;

        [RelayCommand] private void Confirm() => Close(DayTypePickerResult.Pick(Options[SelectedIndex]));

        [RelayCommand] private void Cancel() => Close(DayTypePickerResult.Cancelled);

        #endregion
    }
}
