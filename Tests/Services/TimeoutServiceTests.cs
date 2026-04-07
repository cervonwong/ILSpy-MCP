using FluentAssertions;
using ILSpy.Mcp.Application.Configuration;
using ILSpy.Mcp.Application.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace ILSpy.Mcp.Tests.Services;

public class TimeoutServiceTests
{
    private readonly TimeoutService _service;

    public TimeoutServiceTests()
    {
        var options = Options.Create(new ILSpyOptions { DefaultTimeoutSeconds = 30 });
        _service = new TimeoutService(options);
    }

    [Fact]
    public void CreateTimeoutToken_ReturnsNonCancelledToken()
    {
        using var timeout = _service.CreateTimeoutToken();
        timeout.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void TimeoutToken_Dispose_DoesNotThrow()
    {
        var timeout = _service.CreateTimeoutToken();
        var act = () => timeout.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void TimeoutToken_AfterDispose_NewTokenWorks()
    {
        var first = _service.CreateTimeoutToken();
        first.Dispose();

        using var second = _service.CreateTimeoutToken();
        second.Token.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void TimeoutToken_WithExternalCancellation_LinksBoth()
    {
        using var externalCts = new CancellationTokenSource();
        using var timeout = _service.CreateTimeoutToken(externalCts.Token);

        timeout.Token.IsCancellationRequested.Should().BeFalse();
        externalCts.Cancel();
        timeout.Token.IsCancellationRequested.Should().BeTrue();
    }
}
