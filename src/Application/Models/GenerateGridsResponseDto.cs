namespace Application.Models;

public sealed record GenerateGridsResponseDto(
    DateTimeOffset GeneratedAt,
    string Game,
    string Strategy,
    string Disclaimer,
    long TotalCombinations,
    IReadOnlyCollection<GeneratedGridDto> Grids,
    string? Warning);
