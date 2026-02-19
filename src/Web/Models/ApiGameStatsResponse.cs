namespace Web.Models;

public sealed class ApiGameStatsResponse
{
    public string Game { get; init; } = string.Empty;

    public DateOnly? PeriodStart { get; init; }

    public DateOnly? PeriodEnd { get; init; }

    public int TotalDraws { get; init; }

    public IReadOnlyCollection<ApiNumberStatResponse> MainStats { get; init; } = [];

    public IReadOnlyCollection<ApiNumberStatResponse> BonusStats { get; init; } = [];
}
