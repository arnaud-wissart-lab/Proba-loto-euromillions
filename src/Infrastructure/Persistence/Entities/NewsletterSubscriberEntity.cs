namespace Infrastructure.Persistence.Entities;

public sealed class NewsletterSubscriberEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ConfirmedAtUtc { get; set; }

    public string ConfirmToken { get; set; } = string.Empty;

    public string UnsubscribeToken { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public int LotoGridsCount { get; set; }

    public int EuroMillionsGridsCount { get; set; }

    public ICollection<MailDispatchHistoryEntity> MailDispatchHistory { get; } = [];
}
