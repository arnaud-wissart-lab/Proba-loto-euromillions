using Domain.Enums;

namespace Infrastructure.Persistence.Entities;

public sealed class SyncStateEntity
{
    public LotteryGame Game { get; set; }

    public DateTimeOffset? LastSuccessfulSyncAtUtc { get; set; }

    public DateOnly? LastKnownDrawDate { get; set; }

    public string? HistoryPageEtag { get; set; }

    public DateTimeOffset? HistoryPageLastModifiedUtc { get; set; }

    public string? CachedArchivesJson { get; set; }
}
