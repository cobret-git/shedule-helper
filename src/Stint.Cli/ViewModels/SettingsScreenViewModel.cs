using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Cli.Services;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The Settings root screen: a short list of sub-pages (General, Database), each holding one
    /// group of related settings. See <c>docs/update-v1.1.0/settings-render-48__idle.txt</c>.
    /// </summary>
    /// <remarks>
    /// Reached from Home's settings key. Only the sections that actually exist are listed - the
    /// others in the mockup (Tracking, API, Display, ...) get added here as they're built.
    /// </remarks>
    public sealed partial class SettingsScreenViewModel : ScreenViewModelBase
    {
        #region Fields

        private static readonly IReadOnlyList<SettingsSection> AllSections =
        [
            SettingsSection.General,
            SettingsSection.Database
        ];

        private int _selectedIndex;
        #endregion

        #region Constructors

        public SettingsScreenViewModel(INavigationService navigation)
            : base(navigation)
        {
            Title = "SETTINGS";

            KeyHints =
            [
                new KeyHint("move", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("move", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("open", OpenCommand, ConsoleKey.Enter),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape),
                new KeyHint("quit", QuitCommand, ConsoleKey.Q)
            ];
        }

        #endregion

        #region Properties

        /// <summary>The sub-pages on offer, in display order.</summary>
        public IReadOnlyList<SettingsSection> Sections => AllSections;

        /// <summary>The currently highlighted row index of <see cref="Sections"/>.</summary>
        public int SelectedIndex { get => _selectedIndex; private set => SetProperty(ref _selectedIndex, value); }

        /// <summary>The currently highlighted section.</summary>
        public SettingsSection SelectedSection => Sections[SelectedIndex];

        #endregion

        #region Commands

        // Wraps around at both ends, same as every other list in the app.
        [RelayCommand] private void MoveSelectionUp()
            => SelectedIndex = (SelectedIndex - 1 + Sections.Count) % Sections.Count;

        [RelayCommand] private void MoveSelectionDown()
            => SelectedIndex = (SelectedIndex + 1) % Sections.Count;

        [RelayCommand] private void Open()
        {
            switch (SelectedSection)
            {
                case SettingsSection.General:
                    Navigation.NavigateTo<SettingsGeneralScreenViewModel>();
                    break;
                case SettingsSection.Database:
                    Navigation.NavigateTo<SettingsDatabaseScreenViewModel>();
                    break;
            }
        }

        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        [RelayCommand] private void Quit()
        {
            // TODO: replace with a proper shutdown hook (flush logs, dispose the DI container)
            // once the render pipeline/host loop exists.
            Environment.Exit(0);
        }

        #endregion

        #region CanExecute

        private bool CanGoBack() => Navigation.CanGoBack;

        #endregion
    }
}
