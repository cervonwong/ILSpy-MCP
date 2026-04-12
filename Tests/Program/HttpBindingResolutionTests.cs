using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ILSpy.Mcp.Tests.Program;

public class HttpBindingResolutionTests
{
    private static IConfiguration BuildConfig(params (string Key, string? Value)[] pairs)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(
                pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();
    }

    [Fact]
    public void Resolve_BothKeysPresent_ReturnsConfiguredValues()
    {
        var config = BuildConfig(
            ("Transport:Http:Host", "127.0.0.1"),
            ("Transport:Http:Port", "8080"));

        var (host, port) = HttpBindingResolver.Resolve(config);

        host.Should().Be("127.0.0.1");
        port.Should().Be(8080);
    }

    [Fact]
    public void Resolve_OnlyHostPresent_PortDefaults()
    {
        var config = BuildConfig(("Transport:Http:Host", "10.0.0.1"));

        var (host, port) = HttpBindingResolver.Resolve(config);

        host.Should().Be("10.0.0.1");
        port.Should().Be(3001);
    }

    [Fact]
    public void Resolve_OnlyPortPresent_HostDefaults()
    {
        var config = BuildConfig(("Transport:Http:Port", "9090"));

        var (host, port) = HttpBindingResolver.Resolve(config);

        host.Should().Be("0.0.0.0");
        port.Should().Be(9090);
    }

    [Fact]
    public void Resolve_NoKeysPresent_ReturnsDefaults()
    {
        var config = BuildConfig();

        var (host, port) = HttpBindingResolver.Resolve(config);

        host.Should().Be("0.0.0.0");
        port.Should().Be(3001);
    }

    [Fact]
    public void Resolve_HostIsWhitespace_FallsBackToDefault()
    {
        var config = BuildConfig(
            ("Transport:Http:Host", "   "),
            ("Transport:Http:Port", "8080"));

        var (host, port) = HttpBindingResolver.Resolve(config);

        host.Should().Be("0.0.0.0");
        port.Should().Be(8080);
    }

    [Fact]
    public void Resolve_LastProviderWins_SimulatingCliOverEnv()
    {
        // Two in-memory providers chained to demonstrate the "last provider wins"
        // semantic that makes the real switch-mapped AddCommandLine provider
        // override env and appsettings.json.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Transport:Http:Host", "0.0.0.0"),
                new KeyValuePair<string, string?>("Transport:Http:Port", "3001"),
            })
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Transport:Http:Host", "10.0.0.5"),
                new KeyValuePair<string, string?>("Transport:Http:Port", "9090"),
            })
            .Build();

        var (host, port) = HttpBindingResolver.Resolve(config);

        host.Should().Be("10.0.0.5");
        port.Should().Be(9090);
    }

    [Fact]
    public void StdioHasBindingFlags_WithHost_ReturnsTrue()
    {
        var args = new[] { "--transport", "stdio", "--host", "127.0.0.1" };

        HttpBindingResolver.StdioHasBindingFlags(args).Should().BeTrue();
    }

    [Fact]
    public void StdioHasBindingFlags_WithPort_ReturnsTrue()
    {
        var args = new[] { "--transport", "stdio", "--port", "8080" };

        HttpBindingResolver.StdioHasBindingFlags(args).Should().BeTrue();
    }

    [Fact]
    public void StdioHasBindingFlags_WithoutBindingFlags_ReturnsFalse()
    {
        var args = new[] { "--transport", "stdio" };

        HttpBindingResolver.StdioHasBindingFlags(args).Should().BeFalse();
    }

    [Fact]
    public void StdioHasBindingFlags_EmptyArgs_ReturnsFalse()
    {
        HttpBindingResolver.StdioHasBindingFlags(Array.Empty<string>()).Should().BeFalse();
    }

}
