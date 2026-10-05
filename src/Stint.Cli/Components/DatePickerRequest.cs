namespace Stint.Cli.Components
{
    /// <summary>
    /// What the date picker dialog is opened with: <see cref="Label"/> names what's being chosen
    /// (it becomes the dialog's title, e.g. "Vacation from") and <see cref="Initial"/> is the date
    /// it starts on.
    /// </summary>
    /// <param name="Label">What the date is for, e.g. "Vacation from".</param>
    /// <param name="Initial">The date the picker opens on.</param>
    public sealed record DatePickerRequest(string Label, DateOnly Initial);
}
