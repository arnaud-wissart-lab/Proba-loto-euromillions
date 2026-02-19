namespace Domain.Models;

public sealed record LotteryStatistics(
    DateTimeOffset LastUpdateUtc,
    int LotoDrawCount,
    int EuroMillionsDrawCount);
