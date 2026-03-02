using Infrastructure.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Sockets;

namespace Infrastructure.HealthChecks;

public sealed class SmtpHealthCheck(
    IOptions<MailOptions> mailOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = mailOptions.Value;
        if (!options.Enabled)
        {
            return HealthCheckResult.Healthy("Mail desactive.");
        }

        if (string.IsNullOrWhiteSpace(options.Smtp.Host) || options.Smtp.Port <= 0)
        {
            return HealthCheckResult.Unhealthy("Configuration SMTP invalide.");
        }

        try
        {
            using var tcpClient = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            await tcpClient.ConnectAsync(options.Smtp.Host, options.Smtp.Port, timeoutCts.Token);

            if (tcpClient.Connected)
            {
                return HealthCheckResult.Healthy($"Connexion SMTP ok ({options.Smtp.Host}:{options.Smtp.Port}).");
            }

            return HealthCheckResult.Unhealthy($"Connexion SMTP echouee ({options.Smtp.Host}:{options.Smtp.Port}).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"Timeout connexion SMTP ({options.Smtp.Host}:{options.Smtp.Port}).");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"Echec verification SMTP ({options.Smtp.Host}:{options.Smtp.Port}).", exception);
        }
    }
}
