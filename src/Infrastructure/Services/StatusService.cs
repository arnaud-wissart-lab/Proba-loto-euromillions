using Application.Abstractions;
using Application.Models;
using Domain.Models;
using Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class StatusService(
    IOptions<StatusSeedOptions> options,
    ILogger<StatusService> logger) : IStatusService
{
    public Task<StatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var seed = options.Value;
        var statistics = new LotteryStatistics(
            DateTimeOffset.UtcNow,
            seed.LotoDrawCount,
            seed.EuroMillionsDrawCount);

        logger.LogInformation(
            "Statut calcule: date={LastUpdateUtc}, loto={LotoDrawCount}, euro={EuroMillionsDrawCount}",
            statistics.LastUpdateUtc,
            statistics.LotoDrawCount,
            statistics.EuroMillionsDrawCount);

        var status = new StatusDto(
            statistics.LastUpdateUtc,
            statistics.LotoDrawCount,
            statistics.EuroMillionsDrawCount,
            "Aucun systeme ne permet de predire un tirage. Les donnees sont purement informatives.");

        return Task.FromResult(status);
    }
}
