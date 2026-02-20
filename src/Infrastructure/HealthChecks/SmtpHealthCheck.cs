using Infrastructure.Email;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Sockets;

namespace Infrastructure.HealthChecks;

public sealed class SmtpHealthCheck(
    IOptions<SmtpOptions> smtpOptions) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var options = smtpOptions.Value;
        if (string.IsNullOrWhiteSpace(options.Host) || options.Port <= 0)
        {
            return HealthCheckResult.Unhealthy("Configuration SMTP invalide.");
        }

        try
        {
            using var tcpClient = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
            await tcpClient.ConnectAsync(options.Host, options.Port, timeoutCts.Token);

            if (tcpClient.Connected)
            {
                return HealthCheckResult.Healthy($"Connexion SMTP ok ({options.Host}:{options.Port}).");
            }

            return HealthCheckResult.Unhealthy($"Connexion SMTP echouee ({options.Host}:{options.Port}).");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy($"Timeout connexion SMTP ({options.Host}:{options.Port}).");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy($"Echec verification SMTP ({options.Host}:{options.Port}).", exception);
        }
    }
}
