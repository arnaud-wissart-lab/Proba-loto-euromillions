using Domain.Enums;

namespace Infrastructure.Persistence.Entities;

public sealed class DrawEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public LotteryGame Game { get; set; }

    public DateOnly DrawDate { get; set; }

    public int[] MainNumbers { get; set; } = [];

    public int[] BonusNumbers { get; set; } = [];

    public string Source { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
