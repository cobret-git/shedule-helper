using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SheduleHelper.WpfApp.Components.Data;
using SheduleHelper.WpfApp.Model;
using SheduleHelper.WpfApp.Services;
using SheduleHelper.WpfApp.ViewModel;
using System;
using System.IO;
using System.IO.Abstractions;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace SheduleHelper.WpfApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        #region Constructors
        public App()
        {
            Services = ConfigureServices();
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
            this.InitializeComponent();
        }
        #endregion

        #region Properties

        /// <summary>
        /// Gets the current <see cref="App"/> instance in use
        /// </summary>
        public new static App Current => (App)Application.Current;

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> instance to resolve application services.
        /// </summary>
        public IServiceProvider Services { get; }
        #endregion

        #region Methods

        /// <summary>
        /// Configures the services for the application.
        /// </summary>
        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            ConfigureDatabaseServices(services);

            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<LoggingService>();
            services.AddSingleton<IScheduleDataService, ScheduleDataService>();

            services.AddSingleton<SettingsTabViewModel>();
            services.AddSingleton<SheduleTabViewModel>();
            services.AddSingleton<DashboardTabViewModel>();
            services.AddSingleton<TasksTabViewModel>();

            return services.BuildServiceProvider();
        }
        private static void ConfigureDatabaseServices(ServiceCollection services)
        {
            // Database configuration
            var dbConfig = CreateDatabaseConfiguration();
            services.AddSingleton(dbConfig);

            // DbContext factory
            services.AddDbContextFactory<LocalDbContext>(options =>
            {
                options.UseSqlite(dbConfig.ToConnectionString());
#if DEBUG
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
#endif
            });
        }
        private static DatabaseConfiguration CreateDatabaseConfiguration()
        {
#if DEBUG
            // Debug: bin/Debug/net8.0-windows/data
            return new DatabaseConfiguration
            {
                DatabasePath = Path.Combine(Environment.CurrentDirectory, "data"),
                DatabaseName = "ScheduleHelper.db"
            };
#else
            // Release: AppData/Local/PixelForge Apps/[AssemblyName]/data
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string publisherName = "PixelForge Apps";
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "SheduleHelper";
            return new DatabaseConfiguration
            {
                DatabasePath = Path.Combine(localAppData, publisherName, assemblyName, "data"),
                DatabaseName = "ScheduleHelper.db"
            };
#endif
        }
        #endregion

        #region Handlers
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Resolve and initialize logging service
            var loggingService = Services.GetService<LoggingService>();
            loggingService?.Initialize();


            // Initialize database before showing UI
            var dbService = Services.GetRequiredService<IScheduleDataService>();
            var result = await dbService.InitializeDatabaseAsync();

            if (!result.IsSuccess)
            {
                MessageBox.Show($"Database initialization failed: {result.ErrorMessage}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
        }
        #endregion
    }

}
