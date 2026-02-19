using Application.Models;
using Domain.Enums;

namespace Application.Abstractions;

public interface IStatisticsService
{
    Task<GameStatsDto> GetStatsAsync(LotteryGame game, CancellationToken cancellationToken);
}
