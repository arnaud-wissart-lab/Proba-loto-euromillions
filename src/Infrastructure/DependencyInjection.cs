using Application.Abstractions;
using Infrastructure.Email;
using Infrastructure.HealthChecks;
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
        services.AddOptions<MailOptions>()
            .Bind(configuration.GetSection(MailOptions.SectionName))
            .PostConfigure(options => ApplyMailEnvironmentOverrides(configuration, options));

        services.AddDbContext<LotteryDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.EnableRetryOnFailure()));

        var smtpHealthCheckEnabled = configuration.GetValue<bool?>("HealthChecks:Smtp:Enabled") ?? false;
        var healthChecks = services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

        if (smtpHealthCheckEnabled)
        {
            healthChecks.AddCheck<SmtpHealthCheck>("smtp", tags: ["ready"]);
        }

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
        services.AddScoped<IDrawScheduleResolver, DrawScheduleResolver>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<SubscriptionService>();
        services.AddScoped<ISubscriptionService>(provider => provider.GetRequiredService<SubscriptionService>());
        services.AddScoped<ISubscriptionDispatchService>(provider => provider.GetRequiredService<SubscriptionService>());
        services.AddScoped<INewsletterService, NewsletterService>();
        services.AddScoped<INewsletterDispatchService, NewsletterDispatchService>();

        return services;
    }

    private static void ApplyMailEnvironmentOverrides(IConfiguration configuration, MailOptions options)
    {
        var legacyBaseUrl = configuration["PUBLIC_BASE_URL"];
        if (!string.IsNullOrWhiteSpace(legacyBaseUrl))
        {
            options.BaseUrl = legacyBaseUrl;
        }

        var legacyHost = configuration["SMTP_HOST"] ?? configuration["Smtp:Host"];
        if (!string.IsNullOrWhiteSpace(legacyHost))
        {
            options.Smtp.Host = legacyHost;
        }

        var legacyPort = configuration["SMTP_PORT"] ?? configuration["Smtp:Port"];
        if (int.TryParse(legacyPort, out var parsedPort))
        {
            options.Smtp.Port = parsedPort;
        }

        var legacyUsername = configuration["SMTP_USER"] ?? configuration["Smtp:Username"];
        if (!string.IsNullOrWhiteSpace(legacyUsername))
        {
            options.Smtp.Username = legacyUsername;
        }

        var legacyPassword = configuration["SMTP_PASS"] ?? configuration["Smtp:Password"];
        if (!string.IsNullOrWhiteSpace(legacyPassword))
        {
            options.Smtp.Password = legacyPassword;
        }

        var legacyUseSsl = configuration["SMTP_USE_STARTTLS"] ?? configuration["Smtp:UseStartTls"];
        if (bool.TryParse(legacyUseSsl, out var parsedUseSsl))
        {
            options.Smtp.UseSsl = parsedUseSsl;
        }

        var rawSender = configuration["SMTP_FROM"];
        if (string.IsNullOrWhiteSpace(rawSender))
        {
            var legacySenderAddress = configuration["Smtp:SenderAddress"];
            if (!string.IsNullOrWhiteSpace(legacySenderAddress))
            {
                options.From = legacySenderAddress;
            }

            var legacySenderName = configuration["Smtp:SenderName"];
            if (!string.IsNullOrWhiteSpace(legacySenderName))
            {
                options.FromName = legacySenderName;
            }

            return;
        }

        if (MailboxAddress.TryParse(rawSender, out var mailboxAddress))
        {
            options.From = mailboxAddress.Address;
            options.FromName = string.IsNullOrWhiteSpace(mailboxAddress.Name)
                ? options.FromName
                : mailboxAddress.Name;
            return;
        }

        options.From = rawSender;
    }
}
