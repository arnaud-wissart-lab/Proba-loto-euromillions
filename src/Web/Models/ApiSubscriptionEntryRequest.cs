namespace Web.Models;

public sealed class ApiSubscriptionEntryRequest
{
    public string Game { get; init; } = "Loto";

    public int GridCount { get; init; } = 5;

    public string Strategy { get; init; } = "uniform";
}
