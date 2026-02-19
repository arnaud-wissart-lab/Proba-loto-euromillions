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
            "Execution de {JobName} a {ExecutionTimeUtc}",
            nameof(SyncDrawsJob),
            DateTimeOffset.UtcNow);

        var summary = await drawSyncService.SyncAllAsync("quartz", context.CancellationToken);
        var failedCount = summary.Games.Count(result => result.Status == SyncRunStatus.Fail);

        logger.LogInformation(
            "Execution {JobName} terminee: jeux={GameCount}, echecs={FailureCount}, debut={StartedAtUtc}, fin={FinishedAtUtc}",
            nameof(SyncDrawsJob),
            summary.Games.Count,
            failedCount,
            summary.StartedAtUtc,
            summary.FinishedAtUtc);
    }
}
