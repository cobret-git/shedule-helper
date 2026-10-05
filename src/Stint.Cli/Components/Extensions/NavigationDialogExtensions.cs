using Stint.Cli.Components;
using Stint.Cli.Services;
using Stint.Cli.ViewModels;

namespace Stint.Cli.Components.Extensions
{
    /// <summary>
    /// One entry point per dialog screen, so a caller never has to spell out
    /// <see cref="INavigationService.ShowDialogAsync{TDialog, TContext, TResult}"/>'s three type
    /// arguments (C# can't infer them from the dialog type). Add a method here when adding a dialog.
    /// </summary>
    public static class NavigationDialogExtensions
    {
        #region Methods

        /// <summary>
        /// Opens the date picker on <paramref name="initial"/> and waits for the user to confirm a
        /// date or cancel. <paramref name="label"/> says what is being chosen ("Vacation from").
        /// </summary>
        public static Task<DatePickerResult> PickDateAsync(this INavigationService navigation, string label, DateOnly initial)
            => navigation.ShowDialogAsync<DatePickerScreenViewModel, DatePickerRequest, DatePickerResult>(new DatePickerRequest(label, initial));

        #endregion
    }
}
