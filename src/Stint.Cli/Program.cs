using Microsoft.Extensions.DependencyInjection;
using Stint.Cli.Services;

var services = new ServiceCollection();

services.AddSingleton<INavigationService, NavigationService>();

// TODO: register each screen ViewModel here once it exists, as transient - NavigationService
// resolves a fresh instance per push and disposes it itself when popped for good.
// e.g. services.AddTransient<HomeViewModel>();

await using var provider = services.BuildServiceProvider();

var navigation = provider.GetRequiredService<INavigationService>();

// TODO: navigation.NavigateTo<HomeViewModel>() once Home exists, then hand off to the
// render pipeline's main loop.
