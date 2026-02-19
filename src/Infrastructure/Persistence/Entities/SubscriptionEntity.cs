using Application.Models;
using Domain.Enums;

namespace Infrastructure.Persistence.Entities;

public sealed class SubscriptionEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    public LotteryGame Game { get; set; }

    public int GridCount { get; set; } = 5;

    public GridGenerationStrategy Strategy { get; set; } = GridGenerationStrategy.Uniform;

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ConfirmedAt { get; set; }

    public DateTimeOffset? UnsubscribedAt { get; set; }

    public string ConfirmTokenHash { get; set; } = string.Empty;

    public string UnsubTokenHash { get; set; } = string.Empty;

    public DateOnly? LastSentForDrawDate { get; set; }

    public ICollection<EmailSendLogEntity> EmailSendLogs { get; } = [];
}
