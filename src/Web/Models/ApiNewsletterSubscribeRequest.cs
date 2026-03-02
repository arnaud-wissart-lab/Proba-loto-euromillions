namespace Web.Models;

public sealed class ApiNewsletterSubscribeRequest
{
    public string Email { get; init; } = string.Empty;

    public int LotoGridsCount { get; init; }

    public int EuroMillionsGridsCount { get; init; }
}
