namespace Web.Models;

public sealed class ApiGameSyncResultResponse
{
    public string Game { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public int DrawsUpsertedCount { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; init; }

    public DateOnly? LastKnownDrawDate { get; init; }

    public string? Error { get; init; }
}
