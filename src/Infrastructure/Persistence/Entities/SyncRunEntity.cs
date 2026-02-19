using Domain.Enums;

namespace Infrastructure.Persistence.Entities;

public sealed class SyncRunEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? FinishedAtUtc { get; set; }

    public LotteryGame Game { get; set; }

    public SyncRunStatus Status { get; set; }

    public int DrawsUpsertedCount { get; set; }

    public string? Error { get; set; }
}
