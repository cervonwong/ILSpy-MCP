using FluentAssertions;
using ILSpy.Mcp.Application.Configuration;
using ILSpy.Mcp.Application.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace ILSpy.Mcp.Tests.Services;

public class ConcurrencyLimiterTests : IDisposable
{
    private readonly ConcurrencyLimiter _limiter;

    public ConcurrencyLimiterTests()
    {
        var options = Options.Create(new ILSpyOptions { MaxConcurrentOperations = 2 });
        _limiter = new ConcurrencyLimiter(options);
    }

    [Fact]
    public async Task ExecuteAsync_BelowLimit_ReturnsImmediately()
    {
        var result = await _limiter.ExecuteAsync(async () =>
        {
            await Task.Delay(1);
            return 42;
        });
        result.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_AtLimit_BlocksUntilSlotFrees()
    {
        var barrier = new TaskCompletionSource();
        var thirdStarted = false;

        // Fill both slots
        var task1 = _limiter.ExecuteAsync(async () => { await barrier.Task; return 1; });
        var task2 = _limiter.ExecuteAsync(async () => { await barrier.Task; return 2; });

        // Third should be blocked
        var task3 = _limiter.ExecuteAsync(async () => { thirdStarted = true; return 3; });

        await Task.Delay(50);
        thirdStarted.Should().BeFalse("third task should be blocked by semaphore");

        barrier.SetResult();
        var result = await task3;
        result.Should().Be(3);
        thirdStarted.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_CancellationWhileWaiting_ThrowsOperationCancelled()
    {
        var barrier = new TaskCompletionSource();
        using var cts = new CancellationTokenSource();

        // Fill both slots
        var task1 = _limiter.ExecuteAsync(async () => { await barrier.Task; return 1; });
        var task2 = _limiter.ExecuteAsync(async () => { await barrier.Task; return 2; });

        // Third waits, then cancel
        var task3 = _limiter.ExecuteAsync(async () => 3, cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        var act = () => task3;
        await act.Should().ThrowAsync<OperationCanceledException>();

        barrier.SetResult();
        await Task.WhenAll(task1, task2);
    }

    [Fact]
    public async Task ExecuteAsync_OperationThrows_ReleasesSemaphore()
    {
        // First call throws
        var act = () => _limiter.ExecuteAsync<int>(async () => throw new InvalidOperationException("test"));
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Should still be able to execute (semaphore released)
        var result = await _limiter.ExecuteAsync(async () => 42);
        result.Should().Be(42);
    }

    public void Dispose() => _limiter.Dispose();
}
