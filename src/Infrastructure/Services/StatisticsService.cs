using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class StatisticsService(
    LotteryDbContext dbContext,
    ILogger<StatisticsService> logger) : IStatisticsService
{
    public async Task<GameStatsDto> GetStatsAsync(LotteryGame game, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var rules = LotteryGameRulesCatalog.GetRules(game);
        var draws = await dbContext.Draws
            .AsNoTracking()
            .Where(entity => entity.Game == game)
            .OrderBy(entity => entity.DrawDate)
            .ToListAsync(cancellationToken);

        var totalDraws = draws.Count;
        DateOnly? periodStart = totalDraws > 0 ? draws[0].DrawDate : null;
        DateOnly? periodEnd = totalDraws > 0 ? draws[^1].DrawDate : null;

        var mainStats = BuildNumberStats(draws, rules.MainPoolSize, totalDraws, draw => draw.MainNumbers);
        var bonusStats = BuildNumberStats(draws, rules.BonusPoolSize, totalDraws, draw => draw.BonusNumbers);

        logger.LogInformation(
            "Statistiques calculees pour {Game}: tirages={TotalDraws}, periode={PeriodStart}..{PeriodEnd}",
            game,
            totalDraws,
            periodStart,
            periodEnd);

        return new GameStatsDto(
            game.ToString(),
            periodStart,
            periodEnd,
            totalDraws,
            mainStats,
            bonusStats);
    }

    private static NumberStatDto[] BuildNumberStats(
        IReadOnlyCollection<DrawEntity> draws,
        int maxNumber,
        int totalDraws,
        Func<DrawEntity, int[]> selector)
    {
        var stats = new NumberStatDto[maxNumber];

        for (var number = 1; number <= maxNumber; number++)
        {
            var occurrences = 0;
            DateOnly? lastSeenDate = null;

            foreach (var draw in draws)
            {
                if (!selector(draw).Contains(number))
                {
                    continue;
                }

                occurrences++;
                if (lastSeenDate is null || draw.DrawDate > lastSeenDate.Value)
                {
                    lastSeenDate = draw.DrawDate;
                }
            }

            var frequencyPct = totalDraws == 0
                ? 0
                : Math.Round((double)occurrences / totalDraws * 100, 4);

            stats[number - 1] = new NumberStatDto(number, occurrences, frequencyPct, lastSeenDate);
        }

        return stats;
    }
}
