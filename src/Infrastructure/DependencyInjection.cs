using Application.Abstractions;
using Infrastructure.Email;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Infrastructure.Services.DrawSync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MimeKit;
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
        services.AddOptions<SubscriptionOptions>()
            .Bind(configuration.GetSection(SubscriptionOptions.SectionName))
            .PostConfigure(options =>
            {
                var publicBaseUrl = configuration["PUBLIC_BASE_URL"];
                if (!string.IsNullOrWhiteSpace(publicBaseUrl))
                {
                    options.PublicBaseUrl = publicBaseUrl;
                }

                var tokenSecret = configuration["SUBSCRIPTIONS_TOKEN_SECRET"];
                if (!string.IsNullOrWhiteSpace(tokenSecret))
                {
                    options.TokenSecret = tokenSecret;
                }
            });
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .PostConfigure(options => ApplySmtpEnvironmentOverrides(configuration, options));

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
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<ISubscriptionService>(provider => provider.GetRequiredService<SubscriptionService>());
        services.AddScoped<ISubscriptionDispatchService>(provider => provider.GetRequiredService<SubscriptionService>());

        return services;
    }

    private static void ApplySmtpEnvironmentOverrides(IConfiguration configuration, SmtpOptions options)
    {
        options.Host = configuration["SMTP_HOST"] ?? options.Host;
        options.Username = configuration["SMTP_USER"] ?? options.Username;
        options.Password = configuration["SMTP_PASS"] ?? options.Password;

        var rawPort = configuration["SMTP_PORT"];
        if (int.TryParse(rawPort, out var parsedPort))
        {
            options.Port = parsedPort;
        }

        var rawSender = configuration["SMTP_FROM"];
        if (string.IsNullOrWhiteSpace(rawSender))
        {
            return;
        }

        if (MailboxAddress.TryParse(rawSender, out var mailboxAddress))
        {
            options.SenderAddress = mailboxAddress.Address;
            options.SenderName = string.IsNullOrWhiteSpace(mailboxAddress.Name)
                ? options.SenderName
                : mailboxAddress.Name;
            return;
        }

        options.SenderAddress = rawSender;
    }
}
