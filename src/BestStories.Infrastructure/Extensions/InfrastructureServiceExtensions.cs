using BestStories.Application.Interfaces;
using BestStories.Infrastructure.BackgroundServices;
using BestStories.Infrastructure.Cache;
using BestStories.Infrastructure.Http;
using BestStories.Infrastructure.Repositories;
using BestStories.Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BestStories.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    /// <summary>
    /// Name of the typed client used for Hacker News. Exposed so integration tests can
    /// substitute the transport without reaching into infrastructure internals.
    /// </summary>
    public const string HackerNewsHttpClientName = "HackerNews";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HackerNewsInfrastructureSettings>(configuration.GetSection("HackerNews"));

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, MemoryCacheService>();

        // Singleton: one shared limit for the whole process. See HackerNewsThrottle.
        services.AddSingleton<HackerNewsThrottle>();
        services.AddTransient<ConcurrencyLimitingHandler>();

        services.AddHttpClient<IHackerNewsRepository, HackerNewsRepository>(HackerNewsHttpClientName, (sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<HackerNewsInfrastructureSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpClientTimeoutSeconds);
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<HackerNewsInfrastructureSettings>>().Value;

            // Socket-level backstop, defending the same limit one layer lower in case a future
            // change routes a call around the delegating handler.
            return new SocketsHttpHandler
            {
                MaxConnectionsPerServer = Math.Max(1, settings.MaxParallelRequests),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };
        })
        .AddHttpMessageHandler<ConcurrencyLimitingHandler>();

        services.AddHostedService<CacheWarmupService>();

        return services;
    }
}
