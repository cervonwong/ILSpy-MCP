using FluentAssertions;
using ILSpy.Mcp.Transport.Cli;
using Xunit;

namespace ILSpy.Mcp.Tests.Program;

public class CommandLineHelpTests
{
    [Fact]
    public void IsRequested_WithHelpSubcommand_ReturnsTrue()
    {
        CommandLineHelp.IsRequested(new[] { "help" }).Should().BeTrue();
    }

    [Fact]
    public void IsRequested_WithHelpFlag_ReturnsTrue()
    {
        CommandLineHelp.IsRequested(new[] { "--help" }).Should().BeTrue();
    }

    [Fact]
    public void IsRequested_WithShortHelpFlag_ReturnsTrue()
    {
        CommandLineHelp.IsRequested(new[] { "-h" }).Should().BeTrue();
    }

    [Fact]
    public void IsRequested_WithoutHelpTokens_ReturnsFalse()
    {
        CommandLineHelp.IsRequested(new[] { "--transport", "http" }).Should().BeFalse();
    }

    [Fact]
    public void IsRequested_WithEmptyArgs_ReturnsFalse()
    {
        CommandLineHelp.IsRequested(Array.Empty<string>()).Should().BeFalse();
    }

    [Fact]
    public void IsRequested_WithHelpAsValue_ReturnsFalse()
    {
        CommandLineHelp.IsRequested(new[] { "--transport", "help" }).Should().BeFalse();
    }

    [Fact]
    public void GetText_ContainsUsageAndHelpFlag()
    {
        var text = CommandLineHelp.GetText();

        text.Should().Contain("Usage:");
        text.Should().Contain("ilspy-mcp help");
        text.Should().Contain("-h, --help");
    }
}
