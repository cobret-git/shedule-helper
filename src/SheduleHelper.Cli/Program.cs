using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SheduleHelper.Cli.Infrastructure;
using SheduleHelper.Cli.Screens;
using SheduleHelper.Core.Services;
using System.IO.Abstractions;

var fileSystem = new FileSystem();
var pathProvider = new ConsolePathProvider(fileSystem);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(pathProvider.LogsDirectory, "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

var services = new ServiceCollection();

services.AddSingleton(Log.Logger);
services.AddSingleton<IFileSystem>(fileSystem);
services.AddSingleton<IPathProvider>(pathProvider);
services.AddSingleton<ILocalDbContextFactory, LocalDbContextFactory>();
services.AddSingleton<DatabaseMigrationService>();
services.AddSingleton<ICurrentUserContext, CurrentUserContext>();
services.AddSingleton<ISettingsService, SettingsService>();
services.AddSingleton<IAttendanceService, AttendanceService>();

services.AddTransient<HomeScreen>();

await using var provider = services.BuildServiceProvider();

try
{
    await provider.GetRequiredService<DatabaseMigrationService>().MigrateAsync();
    await provider.GetRequiredService<ICurrentUserContext>().EnsureInitializedAsync();

    var homeScreen = provider.GetRequiredService<HomeScreen>();
    await new ConsoleApp().RunAsync(homeScreen, CancellationToken.None);
}
catch (Exception ex)
{
    Terminal.Shutdown();
    Log.Logger.Fatal(ex, "SheduleHelper.Cli terminated unexpectedly.");
    Console.Error.WriteLine($"Fatal error: {ex.Message}");
    Console.Error.WriteLine($"See the log for details: {pathProvider.LogsDirectory}");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;
