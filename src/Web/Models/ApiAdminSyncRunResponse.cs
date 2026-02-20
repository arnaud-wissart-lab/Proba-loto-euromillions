namespace Web.Models;

public sealed class ApiAdminSyncRunResponse
{
    public Guid Id { get; init; }

    public string Game { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset? FinishedAtUtc { get; init; }

    public int DrawsUpsertedCount { get; init; }

    public string? Error { get; init; }
}
