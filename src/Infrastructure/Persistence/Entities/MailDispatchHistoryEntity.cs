using Domain.Enums;

namespace Infrastructure.Persistence.Entities;

public sealed class MailDispatchHistoryEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SubscriberId { get; set; }

    public NewsletterSubscriberEntity Subscriber { get; set; } = null!;

    public LotteryGame Game { get; set; }

    public DateOnly DrawDate { get; set; }

    public DateTimeOffset SentAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public int GridsCountSent { get; set; }
}
