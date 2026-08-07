using Aimon.Domain.Profiles;

namespace Aimon.Domain.Tests;

public class ProfileIdTests
{
    [Fact]
    public void New_Produces_Distinct_NonEmpty_Ids()
    {
        var a = ProfileId.New();
        var b = ProfileId.New();

        Assert.NotEqual(a, b);
        Assert.NotEqual(Guid.Empty, a.Value);
    }

    [Fact]
    public void From_Rejects_Empty_Guid()
    {
        Assert.Throws<ArgumentException>(() => ProfileId.From(Guid.Empty));
    }

    [Fact]
    public void Parse_Round_Trips_Through_ToString()
    {
        var id = ProfileId.New();

        var parsed = ProfileId.Parse(id.ToString());

        Assert.Equal(id, parsed);
    }
}
