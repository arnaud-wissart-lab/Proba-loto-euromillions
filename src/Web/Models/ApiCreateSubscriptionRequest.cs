namespace Web.Models;

public sealed class ApiCreateSubscriptionRequest
{
    public string Email { get; init; } = string.Empty;

    public IReadOnlyCollection<ApiSubscriptionEntryRequest> Entries { get; init; } = [];
}
