namespace Application.Services;

public static class WeightedNumberSampler
{
    public static int[] SampleWithoutReplacement(
        IReadOnlyList<int> candidates,
        IReadOnlyDictionary<int, double> weights,
        int pickCount,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(weights);
        if (pickCount <= 0 || pickCount > candidates.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pickCount), pickCount, "Nombre a tirer invalide.");
        }

        var selected = new int[pickCount];
        var available = new List<int>(candidates);
        var rng = random ?? Random.Shared;

        for (var index = 0; index < pickCount; index++)
        {
            var selectedIndex = SelectWeightedIndex(available, weights, rng);
            selected[index] = available[selectedIndex];
            available.RemoveAt(selectedIndex);
        }

        Array.Sort(selected);
        return selected;
    }

    private static int SelectWeightedIndex(
        IReadOnlyList<int> available,
        IReadOnlyDictionary<int, double> weights,
        Random random)
    {
        double totalWeight = 0;
        for (var index = 0; index < available.Count; index++)
        {
            totalWeight += GetSafeWeight(available[index], weights);
        }

        if (totalWeight <= 0)
        {
            return random.Next(available.Count);
        }

        var threshold = random.NextDouble() * totalWeight;
        double cumulative = 0;

        for (var index = 0; index < available.Count; index++)
        {
            cumulative += GetSafeWeight(available[index], weights);
            if (threshold <= cumulative)
            {
                return index;
            }
        }

        return available.Count - 1;
    }

    private static double GetSafeWeight(int number, IReadOnlyDictionary<int, double> weights)
    {
        if (!weights.TryGetValue(number, out var weight))
        {
            return 0;
        }

        return double.IsFinite(weight) && weight > 0 ? weight : 0;
    }
}
