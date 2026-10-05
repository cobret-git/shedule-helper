using CommunityToolkit.Mvvm.Input;
using Stint.Cli.Components;

namespace Stint.Cli.ViewModels
{
    /// <summary>
    /// A date picker dialog: three selector rows (year, month and day) over a <see cref="DateOnly"/>,
    /// opened with a <see cref="DatePickerRequest"/> and closed with a <see cref="DatePickerResult"/>
    /// - see <c>NavigationDialogExtensions.PickDateAsync</c>. See also
    /// <c>docs/update-v1.2.0/date-picker-render-48__idle.txt</c>.
    /// </summary>
    /// <remarks>
    /// Knows nothing about what the date is for - the request's label is only shown as the title.
    /// Up/Down move between the rows, Left/Right step the highlighted one: it moves by
    /// years, months or days, each row wrapping within its own range (months within the year, days
    /// within the month) and the day being clamped when the new month/year is shorter.
    /// </remarks>
    public sealed partial class DatePickerScreenViewModel : DialogScreenViewModelBase<DatePickerRequest, DatePickerResult>
    {
        #region Fields

        private static readonly IReadOnlyList<DatePickerRow> AllRows = Enum.GetValues<DatePickerRow>();

        private DateOnly _date;
        private DatePickerRow _selectedRow = DatePickerRow.Month;
        #endregion

        #region Constructors

        public DatePickerScreenViewModel()
        {
            Title = "PICK A DATE";

            KeyHints =
            [
                new KeyHint("select", MoveSelectionUpCommand, ConsoleKey.UpArrow),
                new KeyHint("select", MoveSelectionDownCommand, ConsoleKey.DownArrow),
                new KeyHint("change", PreviousValueCommand, ConsoleKey.LeftArrow),
                new KeyHint("change", NextValueCommand, ConsoleKey.RightArrow),
                new KeyHint("confirm", ConfirmCommand, ConsoleKey.Enter),
                new KeyHint("cancel", CancelCommand, ConsoleKey.Escape)
            ];
        }

        #endregion

        #region Properties

        /// <summary>Both selector rows, in display order.</summary>
        public IReadOnlyList<DatePickerRow> Rows => AllRows;

        /// <summary>The row Left/Right currently change.</summary>
        public DatePickerRow SelectedRow { get => _selectedRow; private set => SetProperty(ref _selectedRow, value); }

        /// <summary>The date as currently selected.</summary>
        public DateOnly Date { get => _date; private set => SetProperty(ref _date, value); }

        #endregion

        #region Methods

        /// <inheritdoc />
        public override void Initialize(DatePickerRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            Date = request.Initial;
            SelectedRow = DatePickerRow.Month;
            Title = request.Label;
        }

        /// <summary>
        /// What <paramref name="row"/> shows on its right-hand side: <c>[ November 2026 ]</c> /
        /// <c>[ 09 - Mon ]</c>, or - for the highlighted row, the one Left/Right change -
        /// the same text in angle brackets (<c>&lt; November 2026 &gt;</c>).
        /// </summary>
        public string GetValueText(DatePickerRow row)
        {
            var text = row switch
            {
                DatePickerRow.Year => Date.ToString("yyyy"),
                DatePickerRow.Month => Date.ToString("MMMM"),
                _ => Date.ToString("dd - ddd")
            };

            return row == SelectedRow ? $"< {text} >" : $"[ {text} ]";
        }

        #endregion

        #region Commands

        // Wraps around at both ends, like every other picker.
        [RelayCommand] private void MoveSelectionUp() => SelectedRow = ShiftRow(-1);

        [RelayCommand] private void MoveSelectionDown() => SelectedRow = ShiftRow(1);

        [RelayCommand] private void PreviousValue() => Step(-1);

        [RelayCommand] private void NextValue() => Step(1);

        [RelayCommand] private void Confirm() => Close(DatePickerResult.Pick(Date));

        [RelayCommand] private void Cancel() => Close(DatePickerResult.Cancelled);

        #endregion

        #region Helpers

        private DatePickerRow ShiftRow(int offset)
            => AllRows[(AllRows.ToList().IndexOf(SelectedRow) + offset + AllRows.Count) % AllRows.Count];

        private void Step(int offset)
        {
            if (SelectedRow == DatePickerRow.Year)
            {
                // AddYears clamps the day (29-Feb -> 28-Feb).
                Date = Date.AddYears(offset);
                return;
            }

            if (SelectedRow == DatePickerRow.Month)
            {
                // Wraps within the year (December -> January of the same year); AddMonths clamps
                // the day (31-Jan -> 28-Feb).
                var month = (Date.Month - 1 + offset + 12) % 12 + 1;
                Date = Date.AddMonths(month - Date.Month);
                return;
            }

            var daysInMonth = DateTime.DaysInMonth(Date.Year, Date.Month);
            var day = (Date.Day - 1 + offset + daysInMonth) % daysInMonth + 1;
            Date = new DateOnly(Date.Year, Date.Month, day);
        }

        #endregion
    }
}
