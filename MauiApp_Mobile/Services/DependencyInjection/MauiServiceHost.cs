using Microsoft.Extensions.DependencyInjection;

namespace MauiApp_Mobile.Services.DependencyInjection;

public static class MauiServiceHost
{
    private static IServiceProvider? _serviceProvider;

    public static void Configure(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public static T? GetService<T>() where T : class
    {
        return _serviceProvider?.GetService<T>();
    }
}
