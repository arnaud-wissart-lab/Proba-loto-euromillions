using Application.Services;

namespace UnitTests;

public sealed class GridScoreCalculatorTests
{
    [Fact]
    public void CalculateNormalizedLogScoreShouldReturnHigherScoreForHigherWeights()
    {
        var universe = Enumerable.Range(1, 5).ToArray();
        var weights = new Dictionary<int, double>
        {
            [1] = 1,
            [2] = 2,
            [3] = 3,
            [4] = 4,
            [5] = 5
        };

        var lowScore = GridScoreCalculator.CalculateNormalizedLogScore([1, 2], universe, weights);
        var highScore = GridScoreCalculator.CalculateNormalizedLogScore([4, 5], universe, weights);

        Assert.InRange(lowScore, 0, 1);
        Assert.InRange(highScore, 0, 1);
        Assert.True(highScore > lowScore);
    }

    [Fact]
    public void GetTopNumbersShouldOrderByWeightDescending()
    {
        var weights = new Dictionary<int, double>
        {
            [2] = 1,
            [8] = 10,
            [5] = 6
        };

        var top = GridScoreCalculator.GetTopNumbers([2, 5, 8], weights, 2);

        Assert.Equal([8, 5], top);
    }
}
