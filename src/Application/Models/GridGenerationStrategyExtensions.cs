namespace Application.Models;

public static class GridGenerationStrategyExtensions
{
    public static bool TryParseStrategy(string? rawValue, out GridGenerationStrategy strategy)
    {
        strategy = default;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = Normalize(rawValue);
        strategy = normalized switch
        {
            "a" or "uniform" or "aleatoire" or "random" => GridGenerationStrategy.Uniform,
            "b" or "frequency" or "weightedfrequency" or "frequence" => GridGenerationStrategy.FrequencyWeighted,
            "c" or "recency" or "recence" or "weightedrecency" => GridGenerationStrategy.RecencyWeighted,
            _ => default
        };

        return normalized is "a" or "uniform" or "aleatoire" or "random"
            or "b" or "frequency" or "weightedfrequency" or "frequence"
            or "c" or "recency" or "recence" or "weightedrecency";
    }

    public static string ToApiValue(this GridGenerationStrategy strategy) =>
        strategy switch
        {
            GridGenerationStrategy.Uniform => "uniform",
            GridGenerationStrategy.FrequencyWeighted => "frequency",
            GridGenerationStrategy.RecencyWeighted => "recency",
            _ => "uniform"
        };

    private static string Normalize(string rawValue) =>
        rawValue
            .Trim()
            .ToLowerInvariant()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
}
