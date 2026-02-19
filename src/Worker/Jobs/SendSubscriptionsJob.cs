using Quartz;
using Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Worker.Jobs;

public sealed class SendSubscriptionsJob(
    ISubscriptionDispatchService subscriptionDispatchService,
    IOptions<SendSubscriptionsJobOptions> options,
    ILogger<SendSubscriptionsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var currentOptions = options.Value;

        logger.LogInformation(
            "Execution de {JobName} a {ExecutionTimeUtc} (cron={Cron}, timezone={TimeZone})",
            nameof(SendSubscriptionsJob),
            DateTimeOffset.UtcNow,
            currentOptions.Cron,
            currentOptions.TimeZoneId);

        var summary = await subscriptionDispatchService.SendForUpcomingDrawsAsync(DateTimeOffset.UtcNow, context.CancellationToken);

        logger.LogInformation(
            "{JobName} termine: active={ActiveCount}, sent={SentCount}, failed={FailedCount}, skipped={SkippedCount}, referenceDate={ReferenceDate}",
            nameof(SendSubscriptionsJob),
            summary.ActiveSubscriptions,
            summary.SentCount,
            summary.FailedCount,
            summary.SkippedCount,
            summary.ReferenceDate);
    }
}
