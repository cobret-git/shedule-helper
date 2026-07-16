using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using SheduleHelper.Core.ViewModels;
using System;

namespace SheduleHelper.Modern.View
{
    /// <summary>
    /// Home page (The Daily Control Center). Resolves its own <see cref="HomeViewModel"/> into its
    /// DataContext; <see cref="Services.NavigationService"/> reads it back to drive the lifecycle.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            DataContext = App.Current.Services.GetRequiredService<HomeViewModel>();
        }

        /// <summary>
        /// The page's ViewModel, exposed for compiled (<c>x:Bind</c>) bindings in the page's XAML.
        /// </summary>
        public HomeViewModel ViewModel => (HomeViewModel)DataContext;

        /// <summary>
        /// Formats the "use the configured default time" button's label.
        /// </summary>
        public string FormatDefaultTimeOption(TimeOnly defaultTime) => $"Default ({defaultTime:HH:mm})";

        /// <summary>
        /// Formats the banner shown for a forgotten prior-day clock-out.
        /// </summary>
        public string FormatForgottenSessionBanner(DateTime date) => $"You forgot to clock out on {date:dddd, MMMM d}.";

        /// <summary>
        /// Formats a completed day's logged attendance range.
        /// </summary>
        public string FormatLoggedRange(DateTime? clockIn, DateTime? clockOut) =>
            clockIn is null || clockOut is null ? string.Empty : $"Logged {clockIn:HH:mm} – {clockOut:HH:mm}";

        /// <summary>
        /// Formats the "clocked in since" label shown while a session is open.
        /// </summary>
        public string FormatClockedInSince(DateTime? clockIn) =>
            clockIn is null ? string.Empty : $"Clocked in since {clockIn:HH:mm}";

        /// <summary>
        /// Formats a duration as "Xh Ym", e.g. "5h 15m".
        /// </summary>
        public string FormatDuration(TimeSpan duration)
        {
            var isNegative = duration < TimeSpan.Zero;
            var magnitude = duration.Duration();
            return $"{(isNegative ? "-" : string.Empty)}{(int)magnitude.TotalHours}h {magnitude.Minutes:D2}m";
        }

        /// <summary>
        /// Formats the "Worked Hours" line, e.g. "5h 15m / 8h 00m".
        /// </summary>
        public string FormatWorkedProgress(TimeSpan worked, TimeSpan target) => $"{FormatDuration(worked)} / {FormatDuration(target)}";

        /// <summary>
        /// Formats the hint below the progress bar - remaining time to the daily goal, or a
        /// congratulatory message once it's reached.
        /// </summary>
        public string FormatRemaining(TimeSpan worked, TimeSpan target)
        {
            var remaining = target - worked;
            return remaining <= TimeSpan.Zero
                ? "You've reached your daily goal!"
                : $"You are {FormatDuration(remaining)} away from your daily goal.";
        }

        /// <summary>
        /// Formats the rolling monthly balance badge, e.g. "+2h 30m Monthly" or "-1h 15m Monthly".
        /// </summary>
        public string FormatMonthlyBalance(TimeSpan balance) => $"{(balance < TimeSpan.Zero ? "-" : "+")}{FormatDuration(balance.Duration())} Monthly";
    }
}
