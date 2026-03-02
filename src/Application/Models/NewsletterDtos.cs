namespace Application.Models;

public sealed record NewsletterSubscribeRequestDto(
    string Email,
    int LotoGridsCount,
    int EuroMillionsGridsCount);

public sealed record NewsletterPreferencesUpdateRequestDto(
    string Token,
    int LotoGridsCount,
    int EuroMillionsGridsCount);

public sealed record NewsletterPreferencesDto(
    string Email,
    int LotoGridsCount,
    int EuroMillionsGridsCount,
    bool IsActive,
    DateTimeOffset? ConfirmedAtUtc);

public sealed record NewsletterActionResultDto(
    bool Success,
    string Message);
