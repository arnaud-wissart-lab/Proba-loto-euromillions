namespace Application.Models;

public sealed record SubscriptionStatusItemDto(
    string Game,
    int GridCount,
    string Strategy,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? UnsubscribedAt);
