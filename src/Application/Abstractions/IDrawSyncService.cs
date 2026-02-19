using Application.Models;
using Domain.Enums;

namespace Application.Abstractions;

public interface IDrawSyncService
{
    Task<SyncExecutionSummaryDto> SyncAllAsync(string trigger, CancellationToken cancellationToken);

    Task<GameSyncResultDto> SyncGameAsync(LotteryGame game, string trigger, CancellationToken cancellationToken);
}
