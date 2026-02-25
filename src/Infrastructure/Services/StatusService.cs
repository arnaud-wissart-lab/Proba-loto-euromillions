using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class StatusService(
    LotteryDbContext dbContext,
    ILogger<StatusService> logger) : IStatusService
{
    public async Task<StatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var drawsByGame = await dbContext.Draws
            .AsNoTracking()
            .GroupBy(entity => entity.Game)
            .Select(group => new
            {
                Game = group.Key,
                DrawsCount = group.Count(),
                LastDrawDate = group.Max(item => item.DrawDate)
            })
            .ToListAsync(cancellationToken);

        var lotoData = drawsByGame.SingleOrDefault(item => item.Game == LotteryGame.Loto);
        var euroData = drawsByGame.SingleOrDefault(item => item.Game == LotteryGame.EuroMillions);

        var lastSyncAtUtc = await dbContext.SyncStates
            .Select(entity => entity.LastSuccessfulSyncAtUtc)
            .MaxAsync(cancellationToken);
        var effectiveLastSyncAt = lastSyncAtUtc ?? DateTimeOffset.MinValue;
        var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);

        logger.LogInformation(
            "Statut calculé : sync={LastSyncAt}, loto={LotoDrawCount}, euro={EuroDrawCount}",
            effectiveLastSyncAt,
            lotoData?.DrawsCount ?? 0,
            euroData?.DrawsCount ?? 0);

        var status = new StatusDto(
            effectiveLastSyncAt,
            new GameStatusDto(
                lotoData?.DrawsCount ?? 0,
                lotoData?.LastDrawDate,
                LotteryGameRulesCatalog.GetNextDrawDate(LotteryGame.Loto, referenceDate)),
            new GameStatusDto(
                euroData?.DrawsCount ?? 0,
                euroData?.LastDrawDate,
                LotteryGameRulesCatalog.GetNextDrawDate(LotteryGame.EuroMillions, referenceDate)),
            "Chaque combinaison reste équiprobable. Les statistiques de fréquences et de récence sont purement informatives.");

        return status;
    }
}
