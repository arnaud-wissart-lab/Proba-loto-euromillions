using Quartz;

namespace Worker.Jobs;

public sealed class SyncDrawsJob(ILogger<SyncDrawsJob> logger) : IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        logger.LogInformation(
            "Execution de {JobName} a {ExecutionTimeUtc}",
            nameof(SyncDrawsJob),
            DateTimeOffset.UtcNow);

        logger.LogInformation(
            "Synchronisation des tirages: squelette actif (aucune logique metier branchee pour l'instant).");

        return Task.CompletedTask;
    }
}
