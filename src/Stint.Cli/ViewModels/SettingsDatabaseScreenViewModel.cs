using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Cli.Services;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// Settings &gt; Database: the database file's size, and the backup/restore actions.
    /// </summary>
    /// <remarks>
    /// <b>Create backup</b> asks where to save (Win32 save dialog) and writes a snapshot of the
    /// live database there, replacing a file of the same name.
    /// <b>Restore from backup</b> only ever runs into an empty database: if the local database
    /// holds any data, <see cref="DatabaseMode.ConfirmingRestore"/> first asks whether to clear it,
    /// and when confirmed a safety copy of the current data is saved beside the database (in
    /// <see cref="IAppPaths.DataDirectory"/>) before anything is replaced. Cancelling any
    /// file dialog is a silent no-op. The real work lives in <see cref="IDatabaseBackupService"/>;
    /// this only drives the dialogs and reports the outcome.
    /// </remarks>
    public sealed partial class SettingsDatabaseScreenViewModel : ScreenViewModelBase
    {
        #region Fields

        private static readonly IReadOnlyList<DatabaseAction> AllActions = Enum.GetValues<DatabaseAction>();

        private readonly IAppPaths _appPaths;
        private readonly IDatabaseBackupService _backups;
        private readonly IFileDialogService _fileDialogs;
        private int _selectedIndex;
        private string _fileSizeText = string.Empty;
        private string? _message;
        private DatabaseMode _mode = DatabaseMode.Idle;

        // True while a backup/restore is running - the host loop keeps polling keys meanwhile, so
        // everything that would navigate away or start a second operation is gated on this.
        private bool _isBusy;
        #endregion

        #region Constructors

        public SettingsDatabaseScreenViewModel(
            INavigationService navigation,
            IAppPaths appPaths,
            IDatabaseBackupService backups,
            IFileDialogService fileDialogs)
            : base(navigation)
        {
            ArgumentNullException.ThrowIfNull(appPaths);
            ArgumentNullException.ThrowIfNull(backups);
            ArgumentNullException.ThrowIfNull(fileDialogs);

            _appPaths = appPaths;
            _backups = backups;
            _fileDialogs = fileDialogs;

            Title = "SETTINGS > DATABASE";

            KeyHints =
            [
                new KeyHint("move", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("move", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("select", SelectCommand, ConsoleKey.Enter),
                new KeyHint("clear and restore", ConfirmRestoreCommand, ConsoleKey.Enter),
                new KeyHint("cancel", CancelRestoreCommand, ConsoleKey.Escape),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape)
            ];
        }

        #endregion

        #region Properties

        /// <summary>Which of the page's two renders is current.</summary>
        public DatabaseMode Mode { get => _mode; private set => SetProperty(ref _mode, value); }

        /// <summary>The action rows, in display order.</summary>
        public IReadOnlyList<DatabaseAction> Actions => AllActions;

        /// <summary>The currently highlighted row index of <see cref="Actions"/>.</summary>
        public int SelectedIndex { get => _selectedIndex; private set => SetProperty(ref _selectedIndex, value); }

        /// <summary>The currently highlighted action.</summary>
        public DatabaseAction SelectedAction => Actions[SelectedIndex];

        /// <summary>The database file's size, formatted for display (e.g. "1.2 MB").</summary>
        public string FileSizeText { get => _fileSizeText; private set => SetProperty(ref _fileSizeText, value); }

        /// <summary>A line to show under the page - the outcome of the last action, or the restore confirmation prompt.</summary>
        public string? Message { get => _message; private set => SetProperty(ref _message, value); }

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void OnActivated() => FileSizeText = FormatSize(GetDatabaseSize());

        #endregion

        #region Commands

        // Wraps around at both ends, same as every other list in the app.
        [RelayCommand(CanExecute = nameof(CanBrowse))] private void MoveSelectionUp()
        {
            Message = null;
            SelectedIndex = (SelectedIndex - 1 + Actions.Count) % Actions.Count;
        }

        [RelayCommand(CanExecute = nameof(CanBrowse))] private void MoveSelectionDown()
        {
            Message = null;
            SelectedIndex = (SelectedIndex + 1) % Actions.Count;
        }

        [RelayCommand(CanExecute = nameof(CanBrowse))] private async Task SelectAsync()
        {
            Message = null;

            switch (SelectedAction)
            {
                case DatabaseAction.CreateBackup:
                    await CreateBackupAsync();
                    break;

                case DatabaseAction.RestoreFromBackup:
                    await BeginRestoreAsync();
                    break;
            }
        }

        // Enter while ConfirmingRestore: the user agreed to clear the local data, so go pick the file.
        [RelayCommand(CanExecute = nameof(CanConfirmRestore))] private async Task ConfirmRestoreAsync()
        {
            Mode = DatabaseMode.Idle;
            Message = null;

            await RestoreAsync(hasLocalData: true);
        }

        // Esc while ConfirmingRestore: nothing was picked or touched, just drop back to browsing.
        // Esc again from there is what actually leaves - see GoBack.
        [RelayCommand(CanExecute = nameof(CanConfirmRestore))] private void CancelRestore()
        {
            Mode = DatabaseMode.Idle;
            Message = null;
        }

        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        #endregion

        #region CanExecute

        private bool CanBrowse() => Mode == DatabaseMode.Idle && !_isBusy;

        private bool CanConfirmRestore() => Mode == DatabaseMode.ConfirmingRestore && !_isBusy;

        private bool CanGoBack() => Mode == DatabaseMode.Idle && !_isBusy && Navigation.CanGoBack;

        #endregion

        #region Helpers

        private async Task CreateBackupAsync()
        {
            var suggestedFileName = $"stint-backup-{DateTime.Now:yyyyMMdd-HHmm}.db";

            _isBusy = true;
            try
            {
                var path = _fileDialogs.PickFileToSave("Create backup", suggestedFileName);
                if (path is null)
                {
                    return;
                }

                await _backups.CreateBackupAsync(path);
                Message = $"Backup saved: {Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                Message = $"Backup failed: {ex.Message}";
            }
            finally
            {
                _isBusy = false;
            }
        }

        // Restore only ever runs into an empty database on its own; anything else needs the
        // user's go-ahead to clear it first.
        private async Task BeginRestoreAsync()
        {
            bool isEmpty;
            try
            {
                isEmpty = await _backups.IsDatabaseEmptyAsync();
            }
            catch (Exception ex)
            {
                Message = $"Restore failed: {ex.Message}";
                return;
            }

            if (isEmpty)
            {
                await RestoreAsync(hasLocalData: false);
                return;
            }

            Mode = DatabaseMode.ConfirmingRestore;
            Message = "The local data will be cleared. A safety copy is saved first.";
        }

        private async Task RestoreAsync(bool hasLocalData)
        {
            // Nothing to protect when the database is empty.
            var safetyCopyPath = hasLocalData
                ? Path.Combine(_appPaths.DataDirectory, $"data-before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.db")
                : null;

            _isBusy = true;
            try
            {
                var path = _fileDialogs.PickFileToOpen("Restore from backup");
                if (path is null)
                {
                    return;
                }

                await _backups.RestoreAsync(path, safetyCopyPath);

                Message = safetyCopyPath is null
                    ? "Restored from backup."
                    : $"Restored. Safety copy: {Path.GetFileName(safetyCopyPath)}";
            }
            catch (Exception ex)
            {
                Message = $"Restore failed: {ex.Message}";
            }
            finally
            {
                _isBusy = false;
                FileSizeText = FormatSize(GetDatabaseSize());
            }
        }

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
