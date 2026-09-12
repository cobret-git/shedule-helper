using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stint.Cli.Services;
using Stint.Cli.ViewModels;
using Stint.Core;

var services = new ServiceCollection();

services.AddSingleton<INavigationService, NavigationService>();

services.AddSingleton<IAppPaths, AppPaths>();

services.AddDbContextFactory<LocalDbContext>((serviceProvider, options) =>
{
    var appPaths = serviceProvider.GetRequiredService<IAppPaths>();
    var databasePath = Path.Combine(appPaths.DataDirectory, "data.db");
    options.UseSqlite($"Data Source={databasePath}");
});

services.AddSingleton<IDatabaseMigrator, DatabaseMigrator>();
services.AddSingleton<IStintDataGateway, StintDataGateway>();

services.AddSingleton<ISettingsService<AppSettings>, SettingsService<AppSettings>>();

// Screen ViewModels are transient - NavigationService resolves a fresh instance per push and
// disposes it itself when popped for good.
services.AddTransient<HomeScreenViewModel>();

await using var provider = services.BuildServiceProvider();

// Must run before anything touches IStintDataGateway - see IDatabaseMigrator.
var migrator = provider.GetRequiredService<IDatabaseMigrator>();
await migrator.MigrateAsync();

var appSettingsService = provider.GetRequiredService<ISettingsService<AppSettings>>();
await appSettingsService.LoadAsync();

var navigation = provider.GetRequiredService<INavigationService>();
navigation.NavigateTo<HomeScreenViewModel>();

// TODO: hand off to the render pipeline's main loop - nothing reads navigation.Current or
// draws a frame yet.
