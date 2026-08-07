namespace Aimon.Domain.Profiles;

/// <summary>Stable, unique identifier for a <see cref="Profile"/>.</summary>
public readonly record struct ProfileId
{
    public Guid Value { get; }

    private ProfileId(Guid value) => Value = value;

    public static ProfileId New() => new(Guid.NewGuid());

    public static ProfileId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("Profile id must not be empty.", nameof(value));

        return new ProfileId(value);
    }

    public static ProfileId Parse(string value) => From(Guid.Parse(value));

    public override string ToString() => Value.ToString("N");
}
