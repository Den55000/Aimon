namespace Aimon.Domain.Models;

/// <summary>
/// Provider-agnostic generation parameters. Ranges are validated on construction so invalid values
/// cannot flow into the pipeline.
/// </summary>
public sealed record GenerationParameters
{
    public double Temperature { get; }

    public double TopP { get; }

    public int? MaxOutputTokens { get; }

    public GenerationParameters(double temperature = 1.0, double topP = 1.0, int? maxOutputTokens = null)
    {
        if (temperature is < 0.0 or > 2.0)
            throw new ArgumentOutOfRangeException(nameof(temperature), temperature, "Temperature must be between 0 and 2.");
        if (topP is < 0.0 or > 1.0)
            throw new ArgumentOutOfRangeException(nameof(topP), topP, "Top-p must be between 0 and 1.");
        if (maxOutputTokens is <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxOutputTokens), maxOutputTokens, "Max output tokens must be positive when specified.");

        Temperature = temperature;
        TopP = topP;
        MaxOutputTokens = maxOutputTokens;
    }

    /// <summary>Neutral defaults (temperature 1.0, top-p 1.0, provider default token limit).</summary>
    public static GenerationParameters Default { get; } = new();
}
