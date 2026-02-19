namespace Web.Models;

public sealed class ApiGenerateGridsResponse
{
    public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;

    public string Game { get; init; } = string.Empty;

    public string Strategy { get; init; } = string.Empty;

    public string Disclaimer { get; init; } = string.Empty;

    public long TotalCombinations { get; init; }

    public IReadOnlyCollection<ApiGeneratedGridResponse> Grids { get; init; } = [];

    public string? Warning { get; init; }
}
