namespace Application.Models;

public sealed record AdminSyncRunDto(
    Guid Id,
    string Game,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? FinishedAtUtc,
    int DrawsUpsertedCount,
    string? Error);
