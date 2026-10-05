using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;
using Stint.Cli.Components.Extensions;
using Stint.Cli.Services;
using Stint.Core;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// The Plan event form: one full-day absence (vacation, sick leave, ...) as a date range plus an
    /// optional note, either new or being edited. See <c>docs/update-v1.2.0/plan-event-render-48__*.txt</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opened from <see cref="PlanScreenViewModel"/> with a <see cref="PlanEventContext"/>. Enter on a
    /// date row opens the date picker dialog; Enter on the note row types into it; <c>[s]</c> saves.
    /// Nothing reaches the gateway until save, and leaving the form discards whatever was changed.
    /// </para>
    /// <para>
    /// Below the rows the form shows what the range adds up to - working days and hours at the
    /// current daily target - and refuses to save a range with no working day in it.
    /// </para>
    /// </remarks>
    public sealed partial class PlanEventScreenViewModel : ScreenViewModelBase, IScreenViewModel<PlanEventContext>
    {
        #region Fields

        private static readonly IReadOnlyList<PlanEventRow> AllRows = Enum.GetValues<PlanEventRow>();

        // Matches PlannedEvent.Note's [MaxLength(32)] - stops a note from being typed past what the
        // gateway would reject on save anyway.
        private const int MaxNoteLength = 32;

        private readonly IStintDataGateway _gateway;
        private readonly ISettingsService<AppSettings> _settingsService;

        private DayType _dayType;
        private int? _eventId;
        private DateOnly _from;
        private DateOnly _to;
        private string _note = string.Empty;
        private string _noteBeforeEdit = string.Empty;

        private PlanEventRow _selectedRow = PlanEventRow.From;
        private bool _isEditingNote;
        private string? _message;
        #endregion

        #region Constructors

        public PlanEventScreenViewModel(INavigationService navigation, IStintDataGateway gateway, ISettingsService<AppSettings> settingsService)
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
                new KeyHint("edit", EditRowCommand, ConsoleKey.Enter),
                new KeyHint("done", EndNoteEditCommand, ConsoleKey.Enter),
                new KeyHint("save", SaveCommand, ConsoleKey.S),
                new KeyHint("cancel", CancelNoteEditCommand, ConsoleKey.Escape),
                new KeyHint("back", GoBackCommand, ConsoleKey.Escape),
                new KeyHint("menu", OpenMenuCommand, ConsoleKey.Tab)
            ];
        }

        #endregion

        #region Properties

        /// <summary>Every row of the form, in display order.</summary>
        public IReadOnlyList<PlanEventRow> Rows => AllRows;

        /// <summary>The currently highlighted row.</summary>
        public PlanEventRow SelectedRow { get => _selectedRow; private set => SetProperty(ref _selectedRow, value); }

        /// <summary>The kind of absence being planned.</summary>
        public DayType DayType => _dayType;

        /// <summary>First day of the event.</summary>
        public DateOnly From { get => _from; private set => SetProperty(ref _from, value); }

        /// <summary>Last day of the event, inclusive.</summary>
        public DateOnly To { get => _to; private set => SetProperty(ref _to, value); }

        /// <summary>The note, as typed so far while <see cref="IsEditingNote"/>.</summary>
        public string Note { get => _note; private set => SetProperty(ref _note, value); }

        /// <summary>Whether <see cref="Note"/> is currently being typed into.</summary>
        public bool IsEditingNote { get => _isEditingNote; private set => SetProperty(ref _isEditingNote, value); }

        /// <summary>How many working days <see cref="From"/>..<see cref="To"/> covers.</summary>
        public int WorkingDays => WorkingCalendar.CountWorkingDays(From, To);

        /// <summary><see cref="WorkingDays"/> times the current daily target.</summary>
        public TimeSpan Duration => _settingsService.Settings.TargetShiftDuration * WorkingDays;

        /// <summary>The last save/validation problem to show under the rows, if any.</summary>
        public string? Message { get => _message; private set => SetProperty(ref _message, value); }

        #endregion

        #region Methods

        /// <inheritdoc />
        public void Initialize(PlanEventContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            _dayType = context.DayType;
            _eventId = context.Existing?.Id;

            var today = DateOnly.FromDateTime(DateTime.Now);
            From = context.Existing?.StartDate ?? today;
            To = context.Existing?.EndDate ?? today;
            Note = context.Existing?.Note ?? string.Empty;

            SelectedRow = PlanEventRow.From;
            Title = $"PLAN > {DayTypeNames.GetName(_dayType)}";
        }

        /// <summary>
        /// Appends one character to <see cref="Note"/>, while <see cref="IsEditingNote"/>. The
        /// render pipeline decides which physical key counts as printable text - this method takes
        /// the character itself, never a <see cref="ConsoleKey"/>/<see cref="ConsoleKeyInfo"/>.
        /// </summary>
        public void AppendNoteCharacter(char character)
        {
            if (!IsEditingNote || char.IsControl(character) || Note.Length >= MaxNoteLength)
            {
                return;
            }

            Note += character;
        }

        /// <summary>Removes the last typed character of <see cref="Note"/>, while <see cref="IsEditingNote"/>.</summary>
        public void RemoveNoteCharacter()
        {
            if (IsEditingNote && Note.Length > 0)
            {
                Note = Note[..^1];
            }
        }

        #endregion

        #region Commands

        // Wraps around at both ends; not while the note is being typed (Up/Down mean nothing there).
        [RelayCommand(CanExecute = nameof(CanBrowse))] private void MoveSelectionUp()
        {
            Message = null;
            SelectedRow = ShiftRow(-1);
        }

        [RelayCommand(CanExecute = nameof(CanBrowse))] private void MoveSelectionDown()
        {
            Message = null;
            SelectedRow = ShiftRow(1);
        }

        // Enter on a date row opens the date picker dialog and resumes here with its answer; on the
        // note row it starts typing. Bound to the same key as EndNoteEdit below - CanBrowse and
        // CanEndNoteEdit are never true at the same time.
        [RelayCommand(CanExecute = nameof(CanBrowse))] private async Task EditRowAsync()
        {
            Message = null;

            switch (SelectedRow)
            {
                case PlanEventRow.From:
                    var pickedFrom = await Navigation.PickDateAsync($"{DayTypeNames.GetName(_dayType)} from", From);
                    if (pickedFrom.IsPicked)
                    {
                        From = pickedFrom.Date;

                        // Pushing the start past the end drags the end along - the range stays valid
                        // instead of making the user fix "To" next.
                        if (To < From)
                        {
                            To = From;
                        }
                    }
                    break;

                case PlanEventRow.To:
                    var pickedTo = await Navigation.PickDateAsync($"{DayTypeNames.GetName(_dayType)} to", To);
                    if (pickedTo.IsPicked && pickedTo.Date < From)
                    {
                        Message = "'To' can't be before 'From'";
                    }
                    else if (pickedTo.IsPicked)
                    {
                        To = pickedTo.Date;
                    }
                    break;

                case PlanEventRow.Note:
                    _noteBeforeEdit = Note;
                    IsEditingNote = true;
                    break;
            }
        }

        [RelayCommand(CanExecute = nameof(CanEndNoteEdit))] private void EndNoteEdit() => IsEditingNote = false;

        // Esc while typing the note: puts the note back as it was and stops typing, staying on the
        // form; Esc again (not typing) is what leaves.
        [RelayCommand(CanExecute = nameof(CanEndNoteEdit))] private void CancelNoteEdit()
        {
            Note = _noteBeforeEdit;
            IsEditingNote = false;
        }

        // Writes the event, then returns to the list. A gateway refusal (it overlaps another planned
        // event) stays on the form with the reason shown, so nothing typed is lost.
        [RelayCommand(CanExecute = nameof(CanSave))] private async Task SaveAsync()
        {
            var plannedEvent = new PlannedEvent
            {
                Id = _eventId ?? 0,
                DayType = _dayType,
                StartDate = From,
                EndDate = To,
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
            };

            try
            {
                if (_eventId is null)
                {
                    await _gateway.AddPlannedEventAsync(plannedEvent);
                }
                else
                {
                    await _gateway.UpdatePlannedEventAsync(plannedEvent);
                }

                Navigation.GoBack();
            }
            catch (InvalidOperationException ex)
            {
                Message = ex.Message;
            }
        }

        // Esc while not typing: leaves the form without saving.
        [RelayCommand(CanExecute = nameof(CanGoBack))] private void GoBack() => Navigation.GoBack();

        [RelayCommand(CanExecute = nameof(CanOpenMenu))] private Task OpenMenuAsync()
            => Navigation.OpenMenuAsync(null);

        #endregion

        #region CanExecute

        private bool CanBrowse() => !IsEditingNote;

        private bool CanEndNoteEdit() => IsEditingNote;

        private bool CanSave() => !IsEditingNote && WorkingDays > 0;

        private bool CanGoBack() => !IsEditingNote && Navigation.CanGoBack;

        private bool CanOpenMenu() => !IsEditingNote;

        #endregion

        #region Helpers

        private PlanEventRow ShiftRow(int offset)
            => AllRows[(AllRows.ToList().IndexOf(SelectedRow) + offset + AllRows.Count) % AllRows.Count];

        #endregion
    }
}
