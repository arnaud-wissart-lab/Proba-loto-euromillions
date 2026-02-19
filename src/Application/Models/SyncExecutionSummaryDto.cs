namespace Application.Models;

public sealed record SyncExecutionSummaryDto(
    DateTimeOffset StartedAtUtc,
    DateTimeOffset FinishedAtUtc,
    IReadOnlyCollection<GameSyncResultDto> Games);
