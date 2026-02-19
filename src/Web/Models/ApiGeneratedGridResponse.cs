namespace Web.Models;

public sealed class ApiGeneratedGridResponse
{
    public IReadOnlyCollection<int> MainNumbers { get; init; } = [];

    public IReadOnlyCollection<int> BonusNumbers { get; init; } = [];

    public double Score { get; init; }

    public IReadOnlyCollection<int> TopMainNumbers { get; init; } = [];

    public IReadOnlyCollection<int> TopBonusNumbers { get; init; } = [];
}
