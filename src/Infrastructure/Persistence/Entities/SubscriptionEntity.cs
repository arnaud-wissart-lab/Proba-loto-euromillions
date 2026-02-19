namespace Infrastructure.Persistence.Entities;

public sealed class SubscriptionEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;

    public string UnsubscribeToken { get; set; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool IsActive { get; set; } = true;
}
