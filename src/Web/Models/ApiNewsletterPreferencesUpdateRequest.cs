namespace Web.Models;

public sealed class ApiNewsletterPreferencesUpdateRequest
{
    public string Token { get; init; } = string.Empty;

    public int LotoGridsCount { get; init; }

    public int EuroMillionsGridsCount { get; init; }
}
