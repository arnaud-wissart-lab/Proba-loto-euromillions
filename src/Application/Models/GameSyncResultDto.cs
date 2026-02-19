using Domain.Enums;

namespace Application.Models;

public sealed record GameSyncResultDto(
    LotteryGame Game,
    SyncRunStatus Status,
    int DrawsUpsertedCount,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    DateOnly? LastKnownDrawDate,
    string? Error);
