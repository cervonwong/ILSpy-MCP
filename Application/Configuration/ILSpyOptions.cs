using Microsoft.Extensions.Options;

namespace ILSpy.Mcp.Application.Configuration;

/// <summary>
/// Configuration options for ILSpy operations.
/// </summary>
public sealed class ILSpyOptions
{
    public const string SectionName = "ILSpy";

    /// <summary>
    /// Maximum size of decompiled code in bytes before truncation.
    /// </summary>
    public int MaxDecompilationSize { get; set; } = 1_048_576; // 1 MB

    /// <summary>
    /// Default timeout for operations in seconds.
    /// </summary>
    public int DefaultTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of concurrent decompilation operations.
    /// </summary>
    public int MaxConcurrentOperations { get; set; } = 10;

    /// <summary>
    /// Validates that all option values are within acceptable ranges.
    /// </summary>
    public void Validate()
    {
        if (MaxDecompilationSize <= 0 || MaxDecompilationSize > 500_000_000)
            throw new ArgumentOutOfRangeException(
                nameof(MaxDecompilationSize),
                MaxDecompilationSize,
                "MaxDecompilationSize must be greater than 0 and at most 500,000,000 (500 MB).");

        if (DefaultTimeoutSeconds <= 0 || DefaultTimeoutSeconds > 3600)
            throw new ArgumentOutOfRangeException(
                nameof(DefaultTimeoutSeconds),
                DefaultTimeoutSeconds,
                "DefaultTimeoutSeconds must be greater than 0 and at most 3600 (1 hour).");

        if (MaxConcurrentOperations <= 0 || MaxConcurrentOperations > 100)
            throw new ArgumentOutOfRangeException(
                nameof(MaxConcurrentOperations),
                MaxConcurrentOperations,
                "MaxConcurrentOperations must be greater than 0 and at most 100.");
    }
}

/// <summary>
/// Validates <see cref="ILSpyOptions"/> on startup using the Microsoft.Extensions.Options validation pipeline.
/// </summary>
public sealed class ILSpyOptionsValidator : IValidateOptions<ILSpyOptions>
{
    public ValidateOptionsResult Validate(string? name, ILSpyOptions options)
    {
        try
        {
            options.Validate();
            return ValidateOptionsResult.Success;
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }
    }
}
