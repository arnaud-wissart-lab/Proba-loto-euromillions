using Application.Services;

namespace UnitTests;

public sealed class WeightedNumberSamplerTests
{
    [Fact]
    public void SampleWithoutReplacementShouldReturnUniqueNumbersInRange()
    {
        var candidates = Enumerable.Range(1, 10).ToArray();
        var weights = candidates.ToDictionary(number => number, _ => 1d);

        var sample = WeightedNumberSampler.SampleWithoutReplacement(candidates, weights, 5, new Random(1234));

        Assert.Equal(5, sample.Length);
        Assert.Equal(sample.Length, sample.Distinct().Count());
        Assert.All(sample, number => Assert.InRange(number, 1, 10));
    }

    [Fact]
    public void SampleWithoutReplacementShouldPreferHighWeightedNumber()
    {
        var candidates = new[] { 1, 2, 3, 4, 5 };
        var weights = new Dictionary<int, double>
        {
            [1] = 1000,
            [2] = 1,
            [3] = 1,
            [4] = 1,
            [5] = 1
        };

        var random = new Random(42);
        var selectedOneCount = 0;
        const int iterations = 300;

        for (var index = 0; index < iterations; index++)
        {
            var sample = WeightedNumberSampler.SampleWithoutReplacement(candidates, weights, 1, random);
            if (sample[0] == 1)
            {
                selectedOneCount++;
            }
        }

        Assert.True(selectedOneCount > 250);
    }

    [Fact]
    public void SampleWithoutReplacementShouldFallbackToUniformWhenWeightsAreInvalid()
    {
        var candidates = new[] { 1, 2, 3, 4, 5 };
        var weights = new Dictionary<int, double>
        {
            [1] = -1,
            [2] = 0,
            [3] = double.NaN,
            [4] = double.NegativeInfinity,
            [5] = 0
        };

        var sample = WeightedNumberSampler.SampleWithoutReplacement(candidates, weights, 3, new Random(7));

        Assert.Equal(3, sample.Length);
        Assert.Equal(3, sample.Distinct().Count());
        Assert.All(sample, number => Assert.Contains(number, candidates));
    }
}
