namespace Application.Models;

public sealed record GameStatsDto(
    string Game,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    int TotalDraws,
    IReadOnlyCollection<NumberStatDto> MainStats,
    IReadOnlyCollection<NumberStatDto> BonusStats);
