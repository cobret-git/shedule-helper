using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Cli.Components.Extensions;
using Stint.Cli.Models;
using Stint.Cli.Services;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The Plan screen: events planned ahead of time - upcoming ones, plus whatever already ended
    /// earlier this month - with new/edit/delete. See <c>docs/update-v1.2.0/plan-render-48__*.txt</c>
    /// for the render mockups this takes its layout/labels from.
    /// </summary>
    /// <remarks>
    /// Same split of responsibility as Projects: this class only exposes state and behavior, and
    /// <see cref="Stint.Cli.Views.PlanScreen"/> decides how it is drawn. Planning a new event first
    /// asks for its kind through the day-type picker dialog, then opens the event form on top of
    /// this screen - so saving or cancelling the form lands back here, and <see cref="OnActivated"/>
    /// reloads the list.
    /// </remarks>
    public sealed partial class PlanScreenViewModel : ScreenViewModelBase
    {
        #region Fields

        // The kinds a new event can be planned as, in the order the picker lists them.
        private static readonly IReadOnlyList<DayType> NewEventKinds =
        [
            DayType.Vacation,
            DayType.Sick,
            DayType.Holiday,
            DayType.Unpaid,
            DayType.DayOffInLieu
        ];

        private readonly IStintDataGateway _gateway;
        private readonly ISettingsService<AppSettings> _settingsService;

        private PlanMode _mode = PlanMode.Idle;
        private IReadOnlyList<PlanListRow> _rows = [];
        private int _selectedIndex;
        private string? _loadError;
        #endregion

        #region Constructors

        public PlanScreenViewModel(INavigationService navigation, IStintDataGateway gateway, ISettingsService<AppSettings> settingsService)
            : base(navigation)
        {
            ArgumentNullException.ThrowIfNull(gateway);
            ArgumentNullException.ThrowIfNull(settingsService);

            _gateway = gateway;
            _settingsService = settingsService;

            Title = "PLAN";

            KeyHints =
            [
                new KeyHint("select", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("select", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("new", BeginNewCommand, ConsoleKey.N),
                new KeyHint("edit", OpenCommand, ConsoleKey.Enter),
                new KeyHint("confirm", ConfirmDeleteCommand, ConsoleKey.Enter),
                new KeyHint("delete", BeginDeleteCommand, ConsoleKey.D),
                new KeyHint("cancel", CancelDeleteCommand, ConsoleKey.Escape),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape)
            ];
        }

        #endregion

        #region Properties

        /// <summary>
        /// Which of Plan's two renders is current. Setting this also updates <see cref="Title"/>, so
        /// the title bar always shows what's actually being done ("PLAN - DELETE").
        /// </summary>
        public PlanMode Mode
        {
            get => _mode;
            private set
            {
                if (SetProperty(ref _mode, value))
                {
                    Title = value == PlanMode.ConfirmingDelete ? "PLAN - DELETE" : "PLAN";
                }
            }
        }

        /// <summary>
        /// Every listed event: the upcoming ones first (earliest first), then the ones that already
        /// ended earlier this month.
        /// </summary>
        public IReadOnlyList<PlanListRow> Rows { get => _rows; private set => SetProperty(ref _rows, value); }

        /// <summary>The currently highlighted row's index into <see cref="Rows"/>.</summary>
        public int SelectedIndex { get => _selectedIndex; private set => SetProperty(ref _selectedIndex, value); }

        /// <summary>The error from the last failed load or delete, if any.</summary>
        public string? LoadError { get => _loadError; private set => SetProperty(ref _loadError, value); }

        /// <summary>The row at <see cref="SelectedIndex"/>, or null if <see cref="Rows"/> is empty.</summary>
        public PlanListRow? SelectedRow => SelectedIndex >= 0 && SelectedIndex < Rows.Count ? Rows[SelectedIndex] : null;

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void OnActivated()
        {
            // Fire-and-forget for the same reason as Home's OnActivated: a local SQLite read is
            // fast enough that the one-frame stale gap is harmless.
            _ = RefreshAsync();
        }

        #endregion

        #region Commands

        // Wraps around at both ends. Still works while a delete is pending: moving the highlight
        // there is what lets the user back off by picking another row - see CancelDelete.
        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionUp()
        {
            Mode = PlanMode.Idle;
            SelectedIndex = (SelectedIndex - 1 + Rows.Count) % Rows.Count;
        }

        [RelayCommand(CanExecute = nameof(CanMoveSelection))] private void MoveSelectionDown()
        {
            Mode = PlanMode.Idle;
            SelectedIndex = (SelectedIndex + 1) % Rows.Count;
        }

        // Asks for the kind first (a dialog - so this resumes here, with this screen current
        // again), then opens the form on top of this screen.
        [RelayCommand(CanExecute = nameof(CanBeginNew))] private async Task BeginNewAsync()
        {
            var picked = await Navigation.PickDayTypeAsync("Plan > new", NewEventKinds);

            if (picked.IsPicked)
            {
                Navigation.NavigateTo<PlanEventScreenViewModel, PlanEventContext>(new PlanEventContext(picked.DayType, null));
            }
        }

        // Enter while Idle: opens the selected event in the form. Bound to the same key as
        // ConfirmDelete below - CanOpen/CanConfirmDelete are never true at the same time, so
        // exactly one of the two ever shows in the footer.
        [RelayCommand(CanExecute = nameof(CanOpen))] private void Open()
        {
            if (SelectedRow is { } row)
            {
                Navigation.NavigateTo<PlanEventScreenViewModel, PlanEventContext>(new PlanEventContext(row.Event.DayType, row.Event));
            }
        }

        [RelayCommand(CanExecute = nameof(CanBeginDelete))] private void BeginDelete()
        {
            LoadError = null;
            Mode = PlanMode.ConfirmingDelete;
        }

        [RelayCommand(CanExecute = nameof(CanConfirmDelete))] private async Task ConfirmDeleteAsync()
        {
            if (SelectedRow is not { } row)
            {
                return;
            }

            try
            {
                await _gateway.DeletePlannedEventAsync(row.Event.Id);
                Mode = PlanMode.Idle;
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                Mode = PlanMode.Idle;
                LoadError = ex.Message;
            }
        }

        // Esc while ConfirmingDelete: drops the mark and stays; Esc again (Idle) is what leaves.
        [RelayCommand(CanExecute = nameof(CanConfirmDelete))] private void CancelDelete() => Mode = PlanMode.Idle;

        // Esc while Idle: leaves Plan and returns to Home. CanGoBack requires Idle so this never
        // fires ahead of CancelDelete above - both are bound to the same key, and ConsoleHost
        // dispatch takes the first hint (in KeyHints order) whose command can execute.
        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        #endregion

        #region CanExecute

        private bool CanMoveSelection() => Rows.Count > 0;

        private bool CanBeginNew() => Mode == PlanMode.Idle;

        private bool CanOpen() => Mode == PlanMode.Idle && Rows.Count > 0;

        private bool CanBeginDelete() => Mode == PlanMode.Idle && Rows.Count > 0;

        private bool CanConfirmDelete() => Mode == PlanMode.ConfirmingDelete && SelectedRow is not null;

        private bool CanGoBack() => Mode == PlanMode.Idle && Navigation.CanGoBack;

        #endregion

        #region Helpers

        private async Task RefreshAsync()
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.Now);
                var monthStart = new DateOnly(today.Year, today.Month, 1);
                var target = _settingsService.Settings.TargetShiftDuration;

                // From the start of this month on: everything still to come, plus what already ended
                // earlier this month (anything that ended before this month isn't wanted here).
                var events = await _gateway.GetPlannedEventsAsync(monthStart, DateOnly.MaxValue);

                var rows = events
                    .Select(e =>
                    {
                        var workingDays = WorkingCalendar.CountWorkingDays(e.StartDate, e.EndDate);
                        return new PlanListRow(e, workingDays, target * workingDays, IsUpcoming: e.EndDate >= today);
                    })
                    .OrderByDescending(r => r.IsUpcoming)
                    .ThenBy(r => r.Event.StartDate)
                    .ThenBy(r => r.Event.Id)
                    .ToList();

                Rows = rows;
                SelectedIndex = Math.Clamp(SelectedIndex, 0, Math.Max(0, rows.Count - 1));
                LoadError = null;
            }
            catch (Exception ex)
            {
                // No logging pipeline exists yet - keep the error on the VM itself rather than
                // losing it silently, same approach as Projects' RefreshAsync.
                LoadError = ex.Message;
            }
        }

        #endregion
    }
}
