namespace CTFPlatform.Utilities;

public static class ServiceExtensions
{
    public static IServiceCollection AddBrowserTimeProvider(this IServiceCollection services)
        => services.AddScoped<TimeProvider, BrowserTimeProvider>();
}