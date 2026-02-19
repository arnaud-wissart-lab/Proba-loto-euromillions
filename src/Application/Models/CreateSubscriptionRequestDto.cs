namespace Application.Models;

public sealed record CreateSubscriptionRequestDto(
    string Email,
    string Game,
    int GridCount,
    string Strategy);
