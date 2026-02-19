namespace Application.Models;

public sealed record GenerateGridsRequestDto(
    string Game,
    int Count,
    string Strategy);
