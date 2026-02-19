using Application.Models;

namespace UnitTests;

public sealed class GridGenerationStrategyExtensionsTests
{
    [Theory]
    [InlineData("A", GridGenerationStrategy.Uniform)]
    [InlineData("uniform", GridGenerationStrategy.Uniform)]
    [InlineData("frequency", GridGenerationStrategy.FrequencyWeighted)]
    [InlineData("B", GridGenerationStrategy.FrequencyWeighted)]
    [InlineData("recency", GridGenerationStrategy.RecencyWeighted)]
    [InlineData("C", GridGenerationStrategy.RecencyWeighted)]
    public void TryParseStrategyShouldParseKnownValues(string rawValue, GridGenerationStrategy expected)
    {
        var parsed = GridGenerationStrategyExtensions.TryParseStrategy(rawValue, out var strategy);

        Assert.True(parsed);
        Assert.Equal(expected, strategy);
    }

    [Fact]
    public void ToApiValueShouldReturnStableKeys()
    {
        Assert.Equal("uniform", GridGenerationStrategy.Uniform.ToApiValue());
        Assert.Equal("frequency", GridGenerationStrategy.FrequencyWeighted.ToApiValue());
        Assert.Equal("recency", GridGenerationStrategy.RecencyWeighted.ToApiValue());
    }
}
