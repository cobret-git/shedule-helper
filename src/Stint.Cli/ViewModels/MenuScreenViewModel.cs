using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The navigation menu: a short list of the app's top-level sections to jump to from anywhere,
    /// opened with Tab. A dialog, so closing it (Tab or Esc) leaves the screen it was opened from
    /// exactly as it was - see <c>NavigationDialogExtensions.OpenMenuAsync</c>, which also does the
    /// actual jump.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The screen the menu was opened from is listed but can't be selected. Neither can an entry
    /// that Home itself wouldn't open right now: Switch needs a live shift that isn't paused for an
    /// absence, and Projects needs a live shift - the same rules Home's old direct keys used. The
    /// shift is looked up when the menu opens, so until that read completes those two stay disabled.
    /// </para>
    /// <para>
    /// Up/Down move the highlight over the enabled entries (wrapping at both ends), Enter goes there.
    /// </para>
    /// </remarks>
    public sealed partial class MenuScreenViewModel : DialogScreenViewModelBase<MenuRequest, MenuResult>
    {
        #region Fields

        private static readonly IReadOnlyList<MenuDestination> AllDestinations = Enum.GetValues<MenuDestination>();

        private readonly IStintDataGateway _gateway;
        private MenuDestination? _current;
        private bool _isShiftLoaded;
        private bool _isClockedIn;
        private bool _isAway;
        private int _selectedIndex;
        // Whether the user has moved the highlight themselves - if not, finishing the shift lookup
        // may still re-pick the starting entry (Switch, from Home, once it turns out to be enabled).
        private bool _hasMoved;
        #endregion

        #region Constructors

        public MenuScreenViewModel(IStintDataGateway gateway)
        {
            ArgumentNullException.ThrowIfNull(gateway);

            _gateway = gateway;

            Title = "MENU";

            KeyHints =
            [
                new KeyHint("move", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("move", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("go", GoCommand, ConsoleKey.Enter),
                new KeyHint("close", CloseMenuCommand, ConsoleKey.Tab, ConsoleKey.Escape),
                new KeyHint("quit", QuitCommand, ConsoleKey.Q)
            ];
        }

        #endregion

        #region Properties

        /// <summary>The sections on offer, in display order.</summary>
        public IReadOnlyList<MenuDestination> Destinations => AllDestinations;

        /// <summary>The currently highlighted index into <see cref="Destinations"/>.</summary>
        public int SelectedIndex { get => _selectedIndex; private set => SetProperty(ref _selectedIndex, value); }

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Initialize(MenuRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            _current = request.Current;
            SelectFirstEnabled();
        }

        /// <inheritdoc />
        public override void OnActivated()
        {
            // Same fire-and-forget shape as Home's RefreshAsync: OnActivated is synchronous, and a
            // local SQLite read is quick enough that the brief gap is harmless.
            _ = LoadShiftAsync();
        }

        /// <summary>
        /// Whether <paramref name="destination"/> can be picked right now - not the screen the menu
        /// was opened from, and (for Switch and Projects) only with a shift to work on.
        /// </summary>
        public bool IsEnabled(MenuDestination destination)
        {
            if (destination == _current)
            {
                return false;
            }

            return destination switch
            {
                MenuDestination.Switch => _isShiftLoaded && _isClockedIn && !_isAway,
                MenuDestination.Projects => _isShiftLoaded && _isClockedIn,
                _ => true
            };
        }

        #endregion

        #region Commands

        [RelayCommand] private void MoveSelectionUp() => MoveSelection(-1);

        [RelayCommand] private void MoveSelectionDown() => MoveSelection(1);

        [RelayCommand(CanExecute = nameof(CanGo))] private void Go() => Close(MenuResult.Go(Destinations[SelectedIndex]));

        [RelayCommand] private void CloseMenu() => Close(MenuResult.Cancelled);

        // Quitting is only offered here and on Home, so it's reachable from anywhere in two keys.
        [RelayCommand] private void Quit()
        {
            // TODO: replace with a proper shutdown hook (flush logs, dispose the DI container)
            // once the render pipeline/host loop exists - same TODO as Home's Quit.
            Environment.Exit(0);
        }

        #endregion

        #region CanExecute

        private bool CanGo() => IsEnabled(Destinations[SelectedIndex]);

        #endregion

        #region Helpers

        // Wraps around at both ends, skipping entries that can't be picked.
        private void MoveSelection(int step)
        {
            _hasMoved = true;

            for (var i = 1; i <= Destinations.Count; i++)
            {
                var index = (SelectedIndex + step * i % Destinations.Count + Destinations.Count) % Destinations.Count;
                if (IsEnabled(Destinations[index]))
                {
                    SelectedIndex = index;
                    return;
                }
            }
        }

        private void SelectFirstEnabled()
        {
            for (var i = 0; i < Destinations.Count; i++)
            {
                if (IsEnabled(Destinations[i]))
                {
                    SelectedIndex = i;
                    return;
                }
            }
        }

        private async Task LoadShiftAsync()
        {
            try
            {
                var attendance = await _gateway.GetAttendanceForDateAsync(DateTime.Today.ToString("yyyy-MM-dd"));

                _isClockedIn = attendance is { ClockOut: null };
                _isAway = _isClockedIn && await _gateway.GetOpenAwayLogAsync(attendance!.Id) is not null;
            }
            catch
            {
                // Can't tell whether there's a shift - leave Switch and Projects disabled; the
                // rest of the menu still works.
                _isClockedIn = false;
                _isAway = false;
            }

            _isShiftLoaded = true;

            // Some entries may have just become enabled: unless the user already moved, start on the
            // first of them again. Either way the highlight must not stay on a disabled entry.
            if (!_hasMoved || !IsEnabled(Destinations[SelectedIndex]))
            {
                SelectFirstEnabled();
            }
        }

        #endregion
    }
}
