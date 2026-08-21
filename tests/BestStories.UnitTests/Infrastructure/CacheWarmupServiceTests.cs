using BestStories.Application.Exceptions;
using BestStories.Application.Interfaces;
using BestStories.Infrastructure.BackgroundServices;
using BestStories.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BestStories.UnitTests.Infrastructure;

public sealed class CacheWarmupServiceTests
{
    private readonly IHackerNewsRepository _repository = Substitute.For<IHackerNewsRepository>();

    private CacheWarmupService CreateSut(HackerNewsInfrastructureSettings settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_repository);
        var scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new CacheWarmupService(
            scopeFactory,
            Options.Create(settings),
            NullLogger<CacheWarmupService>.Instance);
    }

    [Fact]
    public async Task StartAsync_WhenDisabled_NeverTouchesTheRepository()
    {
        var sut = CreateSut(new HackerNewsInfrastructureSettings { CacheWarmupEnabled = false });

        await sut.StartAsync(default);
        await sut.StopAsync(default);

        await _repository.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenEnabled_RefreshesImmediatelyWithoutWaitingForTheFirstInterval()
    {
        _repository.RefreshAsync(Arg.Any<CancellationToken>()).Returns(42);
        var sut = CreateSut(new HackerNewsInfrastructureSettings
        {
            CacheWarmupEnabled = true,
            CacheWarmupIntervalSeconds = 600
        });

        await sut.StartAsync(default);
        await WaitUntilAsync(() => _repository.ReceivedCalls().Any());
        await sut.StopAsync(default);

        await _repository.Received(1).RefreshAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_WhenTheRefreshFails_DoesNotBringDownTheHost()
    {
        _repository.RefreshAsync(Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new ExternalApiUnavailableException("Hacker News is down."));

        var sut = CreateSut(new HackerNewsInfrastructureSettings
        {
            CacheWarmupEnabled = true,
            CacheWarmupIntervalSeconds = 600
        });

        await sut.StartAsync(default);
        await WaitUntilAsync(() => _repository.ReceivedCalls().Any());

        // ExecuteTask faulting is what stops the host; it must stay healthy instead.
        sut.ExecuteTask!.IsFaulted.Should().BeFalse();

        await sut.StopAsync(default);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
            await Task.Delay(10);
    }
}
