using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Serilog;
using SheduleHelper.Core.Services;
using SheduleHelper.Core.ViewModels;
using SheduleHelper.Modern.Services;
using System;
using System.Threading;

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
            await Services.GetRequiredService<ICurrentUserContext>().EnsureInitializedAsync(CancellationToken.None);

            _window = new MainWindow();
            _window.Activate();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Configures the services and ViewModels for the application.
        /// </summary>
        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton(Log.Logger);

            services.AddSingleton<IDatabasePathProvider, DatabasePathProvider>();
            services.AddSingleton<ILocalDbContextFactory, LocalDbContextFactory>();
            services.AddSingleton<ICurrentUserContext, CurrentUserContext>();

            services.AddSingleton<NavigationService>();
            services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());

            services.AddSingleton<DialogService>();
            services.AddSingleton<IDialogService>(sp => sp.GetRequiredService<DialogService>());

            services.AddSingleton<DispatcherService>();
            services.AddSingleton<IDispatcherService>(sp => sp.GetRequiredService<DispatcherService>());

            services.AddTransient<HomeViewModel>();
            services.AddTransient<ProjectsAndTasksViewModel>();
            services.AddTransient<ProjectViewModel>();
            services.AddTransient<HistoryAndReportsViewModel>();
            services.AddTransient<SettingsViewModel>();

            services.AddTransient<EditProjectDialogViewModel>();

            return services.BuildServiceProvider();
        }

        #endregion
    }
}
