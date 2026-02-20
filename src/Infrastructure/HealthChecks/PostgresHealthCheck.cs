using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.HealthChecks;

public sealed class PostgresHealthCheck(
    LotteryDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("Connexion PostgreSQL ok.");
            }

            return HealthCheckResult.Unhealthy("Impossible de se connecter a PostgreSQL.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Echec verification PostgreSQL.", exception);
        }
    }
}
