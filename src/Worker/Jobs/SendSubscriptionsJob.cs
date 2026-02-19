using Quartz;
using Microsoft.Extensions.Options;
using Worker.Email;

namespace Worker.Jobs;

public sealed class SendSubscriptionsJob(
    IEmailSender emailSender,
    IOptions<SendSubscriptionsJobOptions> options,
    ILogger<SendSubscriptionsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var currentOptions = options.Value;

        logger.LogInformation(
            "Execution de {JobName} a {ExecutionTimeUtc} (dryRun={DryRun})",
            nameof(SendSubscriptionsJob),
            DateTimeOffset.UtcNow,
            currentOptions.DryRun);

        if (currentOptions.DryRun || string.IsNullOrWhiteSpace(currentOptions.TestRecipient))
        {
            logger.LogInformation(
                "Mode simulation actif pour {JobName}: aucun email envoye.",
                nameof(SendSubscriptionsJob));
            return;
        }

        await emailSender.SendAsync(
            new EmailMessage(
                currentOptions.TestRecipient,
                "Simulation d'abonnement",
                "Ceci est un email de test. Le service est informatif et ne predit aucun tirage."),
            context.CancellationToken);
    }
}
