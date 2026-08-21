using BestStories.Infrastructure.Extensions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BestStories.IntegrationTests;

/// <summary>
/// Boots the real application — real controller, real use case, real cache, real throttle —
/// with only the outermost HTTP hop swapped for <see cref="HackerNewsStubHandler"/>.
/// </summary>
internal sealed class BestStoriesApiFactory(
    HackerNewsStubHandler stub,
    Action<Dictionary<string, string?>>? configure = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        var settings = new Dictionary<string, string?>
        {
            ["HackerNews:BaseUrl"] = "https://hacker-news.test/v0/",
            // Off by default so tests observe only the calls their own requests cause.
            ["HackerNews:CacheWarmupEnabled"] = "false",
            ["HackerNews:MaxParallelRequests"] = "8",
            ["HackerNews:MaxStoriesCount"] = "500",
            ["HackerNews:IdListCacheSeconds"] = "600",
            ["HackerNews:StoryItemCacheSeconds"] = "600"
        };

        configure?.Invoke(settings);

        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings));

        // Appended to the same named client the application registers, so it is applied
        // after — and therefore replaces — the production transport.
        builder.ConfigureTestServices(services =>
            services.AddHttpClient(InfrastructureServiceExtensions.HackerNewsHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => stub));
    }
}
