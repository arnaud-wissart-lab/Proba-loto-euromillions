namespace Application.Services;

public static class GridScoreCalculator
{
    public static double CalculateNormalizedLogScore(
        IReadOnlyList<int> selectedNumbers,
        IReadOnlyList<int> universe,
        IReadOnlyDictionary<int, double> weights)
    {
        ArgumentNullException.ThrowIfNull(selectedNumbers);
        ArgumentNullException.ThrowIfNull(universe);
        ArgumentNullException.ThrowIfNull(weights);

        if (selectedNumbers.Count == 0 || universe.Count == 0)
        {
            return 0;
        }

        var selectedLength = selectedNumbers.Count;
        var universeWeights = universe
            .Select(number => Math.Max(GetSafeWeight(number, weights), 1e-12))
            .OrderBy(value => value)
            .ToArray();

        var minRaw = SumLog(universeWeights.Take(selectedLength));
        var maxRaw = SumLog(universeWeights.Skip(universeWeights.Length - selectedLength));
        var raw = SumLog(selectedNumbers.Select(number => Math.Max(GetSafeWeight(number, weights), 1e-12)));

        if (Math.Abs(maxRaw - minRaw) < 1e-12)
        {
            return 0.5;
        }

        var normalized = (raw - minRaw) / (maxRaw - minRaw);
        return Math.Clamp(normalized, 0, 1);
    }

    public static int[] GetTopNumbers(
        IReadOnlyCollection<int> selectedNumbers,
        IReadOnlyDictionary<int, double> weights,
        int take)
    {
        ArgumentNullException.ThrowIfNull(selectedNumbers);
        ArgumentNullException.ThrowIfNull(weights);
        if (take <= 0)
        {
            return [];
        }

        return selectedNumbers
            .OrderByDescending(number => GetSafeWeight(number, weights))
            .ThenBy(number => number)
            .Take(take)
            .ToArray();
    }

    private static double SumLog(IEnumerable<double> values)
    {
        double total = 0;
        foreach (var value in values)
        {
            total += Math.Log(value);
        }

        return total;
    }

    private static double GetSafeWeight(int number, IReadOnlyDictionary<int, double> weights) =>
        weights.TryGetValue(number, out var weight) && double.IsFinite(weight) && weight > 0
            ? weight
            : 0;
}
