using Domain.Enums;

namespace Infrastructure.Persistence.Entities;

public sealed class EmailSendLogEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid SubscriptionId { get; set; }

    public SubscriptionEntity Subscription { get; set; } = null!;

    public DateOnly IntendedDrawDate { get; set; }

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;

    public EmailSendLogStatus Status { get; set; }

    public string? Error { get; set; }
}
