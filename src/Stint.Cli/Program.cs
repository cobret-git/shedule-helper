using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stint.Cli.Services;
using Stint.Cli.ViewModels;
using Stint.Cli.Views;
using Stint.Core;

var services = new ServiceCollection();

services.AddSingleton<INavigationService, NavigationService>();

services.AddSingleton<IAppPaths, AppPaths>();

services.AddDbContextFactory<LocalDbContext>((serviceProvider, options) =>
{
    var appPaths = serviceProvider.GetRequiredService<IAppPaths>();
    options.UseSqlite($"Data Source={appPaths.DatabasePath}");
});

services.AddSingleton<IDatabaseMigrator, DatabaseMigrator>();
services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();
services.AddSingleton<IFileDialogService, FileDialogService>();
services.AddSingleton<IStintDataGateway, StintDataGateway>();

services.AddSingleton<ISettingsService<AppSettings>, SettingsService<AppSettings>>();

// Screen ViewModels are transient - NavigationService resolves a fresh instance per push and
// disposes it itself when popped for good.
services.AddTransient<HomeScreenViewModel>();
services.AddTransient<ProjectsScreenViewModel>();
services.AddTransient<ProjectScreenViewModel>();
services.AddTransient<SwitchScreenViewModel>();
services.AddTransient<SettingsScreenViewModel>();
services.AddTransient<SettingsGeneralScreenViewModel>();
services.AddTransient<SettingsDatabaseScreenViewModel>();
services.AddTransient<DatePickerScreenViewModel>();
services.AddTransient<DayTypePickerScreenViewModel>();
services.AddTransient<ClockTimePickerScreenViewModel>();
services.AddTransient<PlanScreenViewModel>();
services.AddTransient<PlanEventScreenViewModel>();
services.AddTransient<MenuScreenViewModel>();

// One IScreenView per screen ViewModel above, plus the pipeline that resolves/drives them.
services.AddSingleton<IScreenView, HomeScreen>();
services.AddSingleton<IScreenView, ProjectsScreen>();
services.AddSingleton<IScreenView, ProjectScreen>();
services.AddSingleton<IScreenView, SwitchScreen>();
services.AddSingleton<IScreenView, SettingsScreen>();
services.AddSingleton<IScreenView, SettingsGeneralScreen>();
services.AddSingleton<IScreenView, SettingsDatabaseScreen>();
services.AddSingleton<IScreenView, DatePickerScreen>();
services.AddSingleton<IScreenView, DayTypePickerScreen>();
services.AddSingleton<IScreenView, ClockTimePickerScreen>();
services.AddSingleton<IScreenView, PlanScreen>();
services.AddSingleton<IScreenView, PlanEventScreen>();
services.AddSingleton<IScreenView, MenuScreen>();
services.AddSingleton<ScreenViewRegistry>();
services.AddSingleton<ConsoleHost>();

await using var provider = services.BuildServiceProvider();

// Must run before anything touches IStintDataGateway - see IDatabaseMigrator.
var migrator = provider.GetRequiredService<IDatabaseMigrator>();
await migrator.MigrateAsync();

var appSettingsService = provider.GetRequiredService<ISettingsService<AppSettings>>();
await appSettingsService.LoadAsync();

var navigation = provider.GetRequiredService<INavigationService>();
navigation.NavigateTo<HomeScreenViewModel>();

var host = provider.GetRequiredService<ConsoleHost>();
await host.RunAsync();
