namespace Application.Models;

public sealed record SubscriptionStatusDto(
    string Email,
    IReadOnlyCollection<SubscriptionStatusItemDto> Subscriptions);
