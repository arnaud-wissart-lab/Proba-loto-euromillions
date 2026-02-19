namespace Application.Models;

public sealed record NumberStatDto(
    int Number,
    int Occurrences,
    double FrequencyPct,
    DateOnly? LastSeenDate);
