using System.Diagnostics;
using BestStories.Application.Interfaces;
using BestStories.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BestStories.Infrastructure.BackgroundServices;

/// <summary>
/// Keeps the Hacker News working set permanently warm.
/// <para>
/// This is what decouples inbound traffic from outbound load: the API serves reads from cache,
/// and Hacker News sees a steady, predictable trickle driven by
/// <see cref="HackerNewsInfrastructureSettings.CacheWarmupIntervalSeconds"/> — the same whether
/// the API is handling one request per minute or thousands per second.
/// </para>
/// </summary>
internal sealed class CacheWarmupService(
    IServiceScopeFactory scopeFactory,
    IOptions<HackerNewsInfrastructureSettings> options,
    ILogger<CacheWarmupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;

        if (!settings.CacheWarmupEnabled)
        {
            logger.LogInformation("Hacker News cache warm-up is disabled; stories load on demand.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(1, settings.CacheWarmupIntervalSeconds));
        logger.LogInformation("Hacker News cache warm-up starting; refreshing every {Interval}.", interval);

        using var timer = new PeriodicTimer(interval);

        try
        {
            do
            {
                await RefreshAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IHackerNewsRepository>();

            var stopwatch = Stopwatch.StartNew();
            var refreshed = await repository.RefreshAsync(cancellationToken);

            logger.LogInformation(
                "Cache warm-up refreshed {Refreshed} stories in {ElapsedMs}ms.",
                refreshed,
                stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Never let a warm-up failure stop the host: the previously cached data stays
            // available and the next cycle will try again.
            logger.LogWarning(ex, "Cache warm-up failed; serving existing cache until the next cycle.");
        }
    }
}
