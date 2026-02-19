namespace Application.Models;

public sealed record SubscriptionActionResultDto(
    bool Success,
    string Message);
