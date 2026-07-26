using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Serilog;
using SheduleHelper.Core.Services;
using SheduleHelper.Core.ViewModels;
using SheduleHelper.Modern.Services;
using SheduleHelper.Modern.View;
using System;
using System.Globalization;
using System.IO.Abstractions;
using Windows.Globalization;

namespace SheduleHelper.Modern
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        #region Fields

        private Window? _window;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            Services = ConfigureServices();

            // Must happen before InitializeComponent() - every .resx lookup (Messages.*,
            // Content.*) from this point on resolves via CultureInfo.CurrentUICulture, so the
            // very first XAML parse needs it already set to render in the right language.
            ApplyCulture(Services.GetRequiredService<ISettingsService>().Settings.Culture);

            InitializeComponent();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the current <see cref="App"/> instance in use.
        /// </summary>
        public new static App Current => (App)Application.Current;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance used to resolve application services and ViewModels.
        /// </summary>
        public IServiceProvider Services { get; }

        #endregion

        #region Handlers

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            var mainWindow = new MainWindow();
            mainWindow.Closed += (_, _) => (Services as IDisposable)?.Dispose();
            _window = mainWindow;
            _window.Activate();

            try
            {
                await Services.GetRequiredService<DatabaseMigrationService>().MigrateAsync();
                await Services.GetRequiredService<ICurrentUserContext>().EnsureInitializedAsync();

                mainWindow.NavigationService.NavigateToHome();
            }
            catch (OperationCanceledException)
            {
                // The window was closed before startup finished - the app is shutting down, nothing more to do.
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Sets the process's active culture from the persisted setting, so every <c>.resx</c>
        /// lookup (<c>Messages.*</c>, <c>Content.*</c>) resolves in the right language from the
        /// very first XAML parse onward.
        /// </summary>
        private static void ApplyCulture(string culture)
        {
            var cultureInfo = new CultureInfo(culture);

            // Both CurrentCulture/CurrentUICulture (this thread, i.e. the UI thread) and the
            // DefaultThreadCurrent* pair (every thread spun up afterward, e.g. ThreadPool
            // continuations after an await) - a ViewModel's resx lookup shouldn't depend on which
            // thread happens to run it.
            CultureInfo.CurrentCulture = cultureInfo;
            CultureInfo.CurrentUICulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
            CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

            try
            {
                // Best-effort: lets WinRT-native controls (e.g. TimePicker/DatePicker) format
                // with the same culture. Requires package identity, so this throws when running
                // unpackaged (e.g. local Debug builds) - CultureInfo above already covers every
                // .resx lookup, which is what actually matters, so failure here is harmless.
                ApplicationLanguages.PrimaryLanguageOverride = culture;
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Configures the services and ViewModels for the application.
        /// </summary>
        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton(Log.Logger);

            services.AddSingleton<IPathProvider, PathProvider>();
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<ILocalDbContextFactory, LocalDbContextFactory>();
            services.AddSingleton<DatabaseMigrationService>();
            services.AddSingleton<ICurrentUserContext, CurrentUserContext>();
            services.AddSingleton<ISettingsService, SettingsService>();

            services.AddSingleton<NavigationService>();
            services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

            services.AddSingleton<DialogService>();
            services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());

            services.AddSingleton<DispatcherService>();
            services.AddSingleton<IDispatcherService>(sp => sp.GetRequiredService<DispatcherService>());

            services.AddSingleton<ThemeService>();

            services.AddTransient<HomeViewModel>();
            services.AddTransient<ProjectsAndTasksViewModel>();
            services.AddTransient<ProjectViewModel>();
            services.AddTransient<HistoryAndReportsViewModel>();
            services.AddTransient<SettingsViewModel>();

            services.AddTransient<EditProjectDialogViewModel>();
            services.AddTransient<EditTaskItemDialogViewModel>();

            return services.BuildServiceProvider();
        }

        #endregion
    }
}
