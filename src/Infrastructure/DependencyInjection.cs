using Application.Abstractions;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Postgres") ??
            configuration.GetConnectionString("postgres");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "La configuration ConnectionStrings:Postgres est obligatoire (variable d'environnement recommandee).");
        }

        var seedOptions = configuration.GetSection(StatusSeedOptions.SectionName).Get<StatusSeedOptions>() ?? new StatusSeedOptions();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(seedOptions));

        services.AddDbContext<LotteryDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

        services.AddScoped<IStatusService, StatusService>();

        return services;
    }
}
