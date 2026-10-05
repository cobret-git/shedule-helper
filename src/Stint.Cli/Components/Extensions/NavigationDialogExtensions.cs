using Stint.Cli.Components;
using Stint.Cli.Services;
using Stint.Cli.ViewModels;
using Stint.Core;

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

        /// <summary>
        /// Opens the kind chooser on <paramref name="options"/> and waits for the user to pick one or
        /// cancel. <paramref name="title"/> says what the choice is for ("Plan &gt; new").
        /// </summary>
        public static Task<DayTypePickerResult> PickDayTypeAsync(this INavigationService navigation, string title, IReadOnlyList<DayType> options)
            => navigation.ShowDialogAsync<DayTypePickerScreenViewModel, DayTypePickerRequest, DayTypePickerResult>(new DayTypePickerRequest(title, options));

        /// <summary>
        /// Opens the navigation menu and, if a section is picked, jumps to it with that section as
        /// the new root (see <see cref="INavigationService.NavigateToSection{TScreen}"/>). Closing
        /// the menu changes nothing. <paramref name="current"/> is the caller's own entry in the menu,
        /// or null for a sub-screen that has none.
        /// </summary>
        public static async Task OpenMenuAsync(this INavigationService navigation, MenuDestination? current)
        {
            var result = await navigation.ShowDialogAsync<MenuScreenViewModel, MenuRequest, MenuResult>(new MenuRequest(current));
            if (!result.IsPicked)
            {
                return;
            }

            switch (result.Destination)
            {
                case MenuDestination.Home:
                    navigation.NavigateToSection<HomeScreenViewModel>();
                    break;
                case MenuDestination.Switch:
                    navigation.NavigateToSection<SwitchScreenViewModel>();
                    break;
                case MenuDestination.Projects:
                    navigation.NavigateToSection<ProjectsScreenViewModel>();
                    break;
                case MenuDestination.Plan:
                    navigation.NavigateToSection<PlanScreenViewModel>();
                    break;
                case MenuDestination.Settings:
                    navigation.NavigateToSection<SettingsScreenViewModel>();
                    break;
            }
        }

        #endregion
    }
}
