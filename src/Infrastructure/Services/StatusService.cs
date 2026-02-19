using Application.Abstractions;
using Application.Models;
using Domain.Enums;
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

        var lotoCount = await dbContext.Draws.CountAsync(entity => entity.Game == LotteryGame.Loto, cancellationToken);
        var euroCount = await dbContext.Draws.CountAsync(entity => entity.Game == LotteryGame.EuroMillions, cancellationToken);
        var lastSyncAtUtc = await dbContext.SyncStates
            .Select(entity => entity.LastSuccessfulSyncAtUtc)
            .MaxAsync(cancellationToken);
        var effectiveLastSyncAtUtc = lastSyncAtUtc ?? DateTimeOffset.MinValue;

        logger.LogInformation(
            "Statut calcule: date={LastUpdateUtc}, loto={LotoDrawCount}, euro={EuroMillionsDrawCount}",
            effectiveLastSyncAtUtc,
            lotoCount,
            euroCount);

        var status = new StatusDto(
            effectiveLastSyncAtUtc,
            lotoCount,
            euroCount,
            "Aucun système ne permet de prédire un tirage. Les données sont purement informatives.");

        return status;
    }
}
