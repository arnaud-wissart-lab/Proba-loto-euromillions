using Application.Abstractions;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Infrastructure.Services.DrawSync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;

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

        services.Configure<DrawSyncOptions>(configuration.GetSection(DrawSyncOptions.SectionName));

        services.AddDbContext<LotteryDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

        services.AddHttpClient(HttpClientNames.FdjArchive, (serviceProvider, client) =>
            {
                var syncOptions = serviceProvider.GetRequiredService<IOptions<DrawSyncOptions>>().Value;
                var timeoutSeconds = Math.Clamp(syncOptions.HttpTimeoutSeconds, 5, 300);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);

                if (!string.IsNullOrWhiteSpace(syncOptions.UserAgent))
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(syncOptions.UserAgent);
                }
            })
            .AddPolicyHandler(_ =>
                HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .OrResult(response => (int)response.StatusCode == 429)
                    .WaitAndRetryAsync(
                        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)]));

        services.AddScoped<IDrawSyncService, DrawSyncService>();
        services.AddScoped<IFdjArchiveClient, FdjArchiveClient>();
        services.AddSingleton<IFdjDrawFileParser, FdjDrawFileParser>();
        services.AddScoped<IStatusService, StatusService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IGridGenerationService, GridGenerationService>();

        return services;
    }
}
