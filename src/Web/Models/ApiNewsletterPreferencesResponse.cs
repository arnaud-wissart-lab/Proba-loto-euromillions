namespace Web.Models;

public sealed class ApiNewsletterPreferencesResponse
{
    public string Email { get; init; } = string.Empty;

    public int LotoGridsCount { get; init; }

    public int EuroMillionsGridsCount { get; init; }

    public bool IsActive { get; init; }

    public DateTimeOffset? ConfirmedAtUtc { get; init; }
}
