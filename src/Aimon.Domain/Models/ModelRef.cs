namespace Aimon.Domain.Models;

/// <summary>
/// A concrete reference to a model exposed by a specific provider — e.g. provider <c>openai</c>,
/// model <c>gpt-4o</c>. Used in execution plans and provider requests.
/// </summary>
public sealed record ModelRef
{
    public string Provider { get; }

    public string Model { get; }

    public ModelRef(string provider, string model)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider must not be blank.", nameof(provider));
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model must not be blank.", nameof(model));

        Provider = provider.Trim();
        Model = model.Trim();
    }

    public override string ToString() => $"{Provider}:{Model}";
}
