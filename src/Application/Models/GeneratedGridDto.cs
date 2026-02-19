namespace Application.Models;

public sealed record GeneratedGridDto(
    IReadOnlyCollection<int> MainNumbers,
    IReadOnlyCollection<int> BonusNumbers,
    double Score,
    IReadOnlyCollection<int> TopMainNumbers,
    IReadOnlyCollection<int> TopBonusNumbers);
