using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stint.Cli.Services;
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

// TODO: register each screen ViewModel here once it exists, as transient - NavigationService
// resolves a fresh instance per push and disposes it itself when popped for good.
// e.g. services.AddTransient<HomeViewModel>();

await using var provider = services.BuildServiceProvider();

// Must run before anything touches IStintDataGateway - see IDatabaseMigrator.
var migrator = provider.GetRequiredService<IDatabaseMigrator>();
await migrator.MigrateAsync();

var navigation = provider.GetRequiredService<INavigationService>();

// TODO: navigation.NavigateTo<HomeViewModel>() once Home exists, then hand off to the
// render pipeline's main loop.
