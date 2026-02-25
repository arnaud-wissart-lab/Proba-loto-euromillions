namespace Application.Models;

public sealed record CreateSubscriptionRequestDto(
    string Email,
    IReadOnlyCollection<CreateSubscriptionEntryDto> Entries);

public sealed record CreateSubscriptionEntryDto(
    string Game,
    int GridCount,
    string Strategy);
