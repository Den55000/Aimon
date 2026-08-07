using Aimon.Domain.Models;

namespace Aimon.Domain.Tests;

public class GenerationParametersTests
{
    [Fact]
    public void Default_Uses_Neutral_Values()
    {
        var parameters = GenerationParameters.Default;

        Assert.Equal(1.0, parameters.Temperature);
        Assert.Equal(1.0, parameters.TopP);
        Assert.Null(parameters.MaxOutputTokens);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    public void Rejects_Temperature_Out_Of_Range(double temperature)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GenerationParameters(temperature: temperature));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    public void Rejects_TopP_Out_Of_Range(double topP)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GenerationParameters(topP: topP));
    }

    [Fact]
    public void Rejects_NonPositive_MaxOutputTokens()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GenerationParameters(maxOutputTokens: 0));
    }

    [Fact]
    public void Accepts_Valid_Values()
    {
        var parameters = new GenerationParameters(temperature: 0.2, topP: 0.9, maxOutputTokens: 512);

        Assert.Equal(0.2, parameters.Temperature);
        Assert.Equal(0.9, parameters.TopP);
        Assert.Equal(512, parameters.MaxOutputTokens);
    }
}
