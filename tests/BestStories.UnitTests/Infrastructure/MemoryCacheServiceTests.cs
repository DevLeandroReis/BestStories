using BestStories.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Memory;

namespace BestStories.UnitTests.Infrastructure;

public sealed class MemoryCacheServiceTests
{
    private readonly MemoryCacheService _sut = new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task GetOrCreateAsync_WhenCacheMiss_CallsFactoryAndReturnsValue()
    {
        var callCount = 0;
        Task<string?> factory(CancellationToken _) { callCount++; return Task.FromResult<string?>("value"); }

        var result = await _sut.GetOrCreateAsync("key", TimeSpan.FromMinutes(1), factory, default);

        result.Should().Be("value");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenCacheHit_DoesNotCallFactoryAgain()
    {
        var callCount = 0;
        Task<string?> factory(CancellationToken _) { callCount++; return Task.FromResult<string?>("value"); }

        await _sut.GetOrCreateAsync("key", TimeSpan.FromMinutes(1), factory, default);
        var result = await _sut.GetOrCreateAsync("key", TimeSpan.FromMinutes(1), factory, default);

        result.Should().Be("value");
        callCount.Should().Be(1);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenFactoryReturnsNull_DoesNotCacheAndCallsFactoryOnNextRequest()
    {
        var callCount = 0;
        Task<string?> factory(CancellationToken _) { callCount++; return Task.FromResult<string?>(null); }

        var result1 = await _sut.GetOrCreateAsync("key", TimeSpan.FromMinutes(1), factory, default);
        var result2 = await _sut.GetOrCreateAsync("key", TimeSpan.FromMinutes(1), factory, default);

        result1.Should().BeNull();
        result2.Should().BeNull();
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenDifferentKeys_CachesIndependently()
    {
        var callCount = 0;
        Task<string?> factory(CancellationToken _) { callCount++; return Task.FromResult<string?>("value"); }

        await _sut.GetOrCreateAsync("key-1", TimeSpan.FromMinutes(1), factory, default);
        await _sut.GetOrCreateAsync("key-2", TimeSpan.FromMinutes(1), factory, default);

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task Set_ReplacesAnExistingEntry()
    {
        _sut.Set("key", "first", TimeSpan.FromMinutes(1));
        _sut.Set("key", "second", TimeSpan.FromMinutes(1));

        var factoryCalled = false;
        var result = await _sut.GetOrCreateAsync("key", TimeSpan.FromMinutes(1),
            _ => { factoryCalled = true; return Task.FromResult<string?>("third"); }, default);

        result.Should().Be("second");
        factoryCalled.Should().BeFalse();
    }

    [Fact]
    public async Task GetOrCreateAsync_UnderConcurrentMisses_RunsTheFactoryOnlyOnce()
    {
        var callCount = 0;
        async Task<string?> Factory(CancellationToken _)
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(50);
            return "value";
        }

        var results = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ =>
            _sut.GetOrCreateAsync("key", TimeSpan.FromMinutes(1), Factory, default)));

        results.Should().AllBe("value");
        callCount.Should().Be(1, "cache stampede protection must collapse concurrent misses");
    }
}
