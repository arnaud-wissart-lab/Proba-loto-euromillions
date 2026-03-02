using Quartz;
using Application.Abstractions;
using Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Worker.Jobs;

public sealed class SendSubscriptionsJob(
    INewsletterDispatchService newsletterDispatchService,
    IOptions<SendSubscriptionsJobOptions> options,
    IOptions<MailOptions> mailOptions,
    ILogger<SendSubscriptionsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var currentOptions = options.Value;
        var forceDispatch = mailOptions.Value.Schedule.Force;

        logger.LogInformation(
            "Execution de {JobName} a {ExecutionTimeUtc} (cron={Cron}, timezone={TimeZone}, force={Force})",
            nameof(SendSubscriptionsJob),
            DateTimeOffset.UtcNow,
            currentOptions.Cron,
            currentOptions.TimeZoneId,
            forceDispatch);

        var summary = await newsletterDispatchService.DispatchForDueDrawsAsync(
            DateTimeOffset.UtcNow,
            forceDispatch,
            context.CancellationToken);

        logger.LogInformation(
            "{JobName} termine : localDate={LocalDate}, timezone={TimeZone}, scheduleOpen={IsScheduleWindowOpen}, games=[{Games}], subscribers={Subscribers}, sent={SentCount}, skipped={SkippedCount}, errors={ErrorCount}, force={Force}",
            nameof(SendSubscriptionsJob),
            summary.LocalDate,
            summary.TimeZone,
            summary.IsScheduleWindowOpen,
            string.Join(", ", summary.DispatchedGames),
            summary.TotalSubscribersConsidered,
            summary.SentCount,
            summary.SkippedCount,
            summary.ErrorCount,
            summary.Force);
    }
}
