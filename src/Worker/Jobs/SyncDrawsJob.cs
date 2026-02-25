using Application.Abstractions;
using Domain.Enums;
using Quartz;

namespace Worker.Jobs;

public sealed class SyncDrawsJob(
    IDrawSyncService drawSyncService,
    ILogger<SyncDrawsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation(
            "Exécution de {JobName} à {ExecutionTimeUtc}",
            nameof(SyncDrawsJob),
            DateTimeOffset.UtcNow);

        var summary = await drawSyncService.SyncAllAsync("quartz", context.CancellationToken);
        var failedCount = summary.Games.Count(result => result.Status == SyncRunStatus.Fail);

        logger.LogInformation(
            "Exécution {JobName} terminée : jeux={GameCount}, échecs={FailureCount}, début={StartedAtUtc}, fin={FinishedAtUtc}",
            nameof(SyncDrawsJob),
            summary.Games.Count,
            failedCount,
            summary.StartedAtUtc,
            summary.FinishedAtUtc);
    }
}
