using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Cli.Services;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// Settings &gt; Database: the database file's size, and the backup/restore actions.
    /// </summary>
    /// <remarks>
    /// The actions are visual only for now - choosing one just says it isn't available yet. The
    /// real backup (a consistent copy of the live SQLite file, saved wherever the user picks via a
    /// save dialog) and restore (open dialog, confirmation, safety copy of the current database
    /// first) are designed to be built together with the platform file-dialog service later.
    /// </remarks>
    public sealed partial class SettingsDatabaseScreenViewModel : ScreenViewModelBase
    {
        #region Fields

        private static readonly IReadOnlyList<DatabaseAction> AllActions = Enum.GetValues<DatabaseAction>();

        private readonly IAppPaths _appPaths;
        private int _selectedIndex;
        private string _fileSizeText = string.Empty;
        private string? _message;
        #endregion

        #region Constructors

        public SettingsDatabaseScreenViewModel(INavigationService navigation, IAppPaths appPaths)
            : base(navigation)
        {
            ArgumentNullException.ThrowIfNull(appPaths);

            _appPaths = appPaths;

            Title = "SETTINGS > DATABASE";

            KeyHints =
            [
                new KeyHint("move", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("move", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("select", SelectCommand, ConsoleKey.Enter),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape)
            ];
        }

        #endregion

        #region Properties

        /// <summary>The action rows, in display order.</summary>
        public IReadOnlyList<DatabaseAction> Actions => AllActions;

        /// <summary>The currently highlighted row index of <see cref="Actions"/>.</summary>
        public int SelectedIndex { get => _selectedIndex; private set => SetProperty(ref _selectedIndex, value); }

        /// <summary>The currently highlighted action.</summary>
        public DatabaseAction SelectedAction => Actions[SelectedIndex];

        /// <summary>The database file's size, formatted for display (e.g. "1.2 MB").</summary>
        public string FileSizeText { get => _fileSizeText; private set => SetProperty(ref _fileSizeText, value); }

        /// <summary>A line to show under the page - currently only "not available yet".</summary>
        public string? Message { get => _message; private set => SetProperty(ref _message, value); }

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void OnActivated() => FileSizeText = FormatSize(GetDatabaseSize());

        #endregion

        #region Commands

        // Wraps around at both ends, same as every other list in the app.
        [RelayCommand] private void MoveSelectionUp()
        {
            Message = null;
            SelectedIndex = (SelectedIndex - 1 + Actions.Count) % Actions.Count;
        }

        [RelayCommand] private void MoveSelectionDown()
        {
            Message = null;
            SelectedIndex = (SelectedIndex + 1) % Actions.Count;
        }

        // Placeholder until backup/restore are built - see the class remarks.
        [RelayCommand] private void Select() => Message = "Not available yet";

        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        #endregion

        #region CanExecute

        private bool CanGoBack() => Navigation.CanGoBack;

        #endregion

        #region Helpers

        private long GetDatabaseSize()
        {
            var file = new FileInfo(_appPaths.DatabasePath);
            return file.Exists ? file.Length : 0;
        }

        private static string FormatSize(long bytes)
        {
            const double Kilobyte = 1024;
            const double Megabyte = Kilobyte * 1024;
            const double Gigabyte = Megabyte * 1024;

            return bytes switch
            {
                < (long)Kilobyte => $"{bytes} B",
                < (long)Megabyte => $"{(bytes / Kilobyte).ToString("0.#", CultureInfo.InvariantCulture)} KB",
                < (long)Gigabyte => $"{(bytes / Megabyte).ToString("0.#", CultureInfo.InvariantCulture)} MB",
                _ => $"{(bytes / Gigabyte).ToString("0.#", CultureInfo.InvariantCulture)} GB"
            };
        }

        #endregion
    }
}
