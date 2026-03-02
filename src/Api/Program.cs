using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Domain.Services;
using Infrastructure;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Host.UseSerilog(
    (context, services, loggerConfiguration) =>
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "Api"),
    writeToProviders: true);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:SubscriptionsPost:PermitLimit") ?? 10;
    var windowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:SubscriptionsPost:WindowSeconds") ?? 60;

    options.AddFixedWindowLimiter("subscriptions-post", limiterOptions =>
    {
        limiterOptions.PermitLimit = permitLimit;
        limiterOptions.Window = TimeSpan.FromSeconds(windowSeconds);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "API Probabilités Loto & EuroMillions",
        Version = "v1",
        Description = "API informative et statistique. Aucune prédiction de tirage n'est fournie."
    });
});

const string corsPolicyName = "web-dev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins);
        }
        else
        {
            policy.SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                {
                    return false;
                }

                return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
            });
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

var autoMigrate = app.Configuration.GetValue("Database:AutoMigrate", true);
if (autoMigrate)
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<LotteryDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "Documentation API - Probabilités Loto & EuroMillions";
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
    });
    app.UseCors(corsPolicyName);
}

app.MapGet(
        "/api/status",
        async (IStatusService statusService, CancellationToken cancellationToken) =>
            Results.Ok(await statusService.GetStatusAsync(cancellationToken)))
    .WithName("GetStatus")
    .WithTags("Statut")
    .WithSummary("Récupère l'état statistique courant.")
    .WithDescription("Endpoint informatif : il ne prédit aucun tirage.")
    .Produces<StatusDto>(StatusCodes.Status200OK);

app.MapGet(
        "/api/stats/{game}",
        async (string game, IStatisticsService statisticsService, CancellationToken cancellationToken) =>
        {
            if (!LotteryGameRulesCatalog.TryParseGame(game, out var parsedGame))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["game"] = ["Valeur invalide. Valeurs supportées : Loto, EuroMillions."]
                });
            }

            var stats = await statisticsService.GetStatsAsync(parsedGame, cancellationToken);
            return Results.Ok(stats);
        })
    .WithName("GetGameStats")
    .WithTags("Statistiques")
    .WithSummary("Retourne les statistiques de fréquences et dernières sorties d'un jeu.")
    .Produces<GameStatsDto>(StatusCodes.Status200OK)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest);

app.MapPost(
        "/api/grids/generate",
        async (GenerateGridsRequestDto request, IGridGenerationService gridGenerationService, CancellationToken cancellationToken) =>
        {
            var errors = ValidateGenerateRequest(request, out var parsedGame, out var parsedStrategy);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var generated = await gridGenerationService.GenerateAsync(
                parsedGame,
                request.Count,
                parsedStrategy,
                cancellationToken);

            return Results.Ok(generated);
        })
    .WithName("PostGenerateGrids")
    .WithTags("Grilles")
    .WithSummary("Génère des grilles uniques (uniforme, fréquence, récence) avec score explicable.")
    .Accepts<GenerateGridsRequestDto>("application/json")
    .Produces<GenerateGridsResponseDto>(StatusCodes.Status200OK)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest);

app.MapPost(
        "/api/v1/newsletter/subscribe",
        async (NewsletterSubscribeRequestDto request, INewsletterService newsletterService, CancellationToken cancellationToken) =>
        {
            var errors = ValidateNewsletterSubscriptionRequest(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            await newsletterService.RequestSubscriptionAsync(request, cancellationToken);
            return Results.Accepted(value: new
            {
                message = "Si l'adresse fournie est valide, un email de confirmation a ete envoye."
            });
        })
    .RequireRateLimiting("subscriptions-post")
    .WithName("PostNewsletterSubscribe")
    .WithTags("Newsletter")
    .WithSummary("Cree ou met a jour un abonnement newsletter et envoie un email de confirmation.")
    .Accepts<NewsletterSubscribeRequestDto>("application/json")
    .Produces(StatusCodes.Status202Accepted)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest);

app.MapGet(
        "/api/v1/newsletter/confirm",
        async (string token, INewsletterService newsletterService, IOptions<MailOptions> mailOptions, CancellationToken cancellationToken) =>
        {
            var result = await newsletterService.ConfirmAsync(token, cancellationToken);
            var redirectUrl = BuildNewsletterRedirectUrl(
                mailOptions.Value.BaseUrl,
                "/abonnement/confirmation",
                result.Success ? "success" : "invalid");
            return Results.Redirect(redirectUrl);
        })
    .WithName("GetNewsletterConfirm")
    .WithTags("Newsletter")
    .WithSummary("Confirme un abonnement newsletter puis redirige vers la page web de confirmation.")
    .Produces(StatusCodes.Status302Found);

app.MapGet(
        "/api/v1/newsletter/confirm-action",
        async (string token, INewsletterService newsletterService, CancellationToken cancellationToken) =>
        {
            var result = await newsletterService.ConfirmAsync(token, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(result);
        })
    .WithName("GetNewsletterConfirmAction")
    .WithTags("Newsletter")
    .WithSummary("Confirme un abonnement newsletter et retourne le resultat JSON.")
    .Produces<NewsletterActionResultDto>(StatusCodes.Status200OK)
    .Produces<NewsletterActionResultDto>(StatusCodes.Status400BadRequest);

app.MapGet(
        "/api/v1/newsletter/unsubscribe",
        async (string token, INewsletterService newsletterService, IOptions<MailOptions> mailOptions, CancellationToken cancellationToken) =>
        {
            var result = await newsletterService.UnsubscribeAsync(token, cancellationToken);
            var redirectUrl = BuildNewsletterRedirectUrl(
                mailOptions.Value.BaseUrl,
                "/abonnement/desinscription",
                result.Success ? "success" : "invalid");
            return Results.Redirect(redirectUrl);
        })
    .WithName("GetNewsletterUnsubscribe")
    .WithTags("Newsletter")
    .WithSummary("Desactive un abonnement newsletter puis redirige vers la page web de desinscription.")
    .Produces(StatusCodes.Status302Found);

app.MapGet(
        "/api/v1/newsletter/unsubscribe-action",
        async (string token, INewsletterService newsletterService, CancellationToken cancellationToken) =>
        {
            var result = await newsletterService.UnsubscribeAsync(token, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(result);
        })
    .WithName("GetNewsletterUnsubscribeAction")
    .WithTags("Newsletter")
    .WithSummary("Desactive un abonnement newsletter et retourne le resultat JSON.")
    .Produces<NewsletterActionResultDto>(StatusCodes.Status200OK)
    .Produces<NewsletterActionResultDto>(StatusCodes.Status400BadRequest);

app.MapGet(
        "/api/v1/newsletter/preferences",
        async (string token, INewsletterService newsletterService, CancellationToken cancellationToken) =>
        {
            var preferences = await newsletterService.GetPreferencesAsync(token, cancellationToken);
            return preferences is null
                ? Results.NotFound(new { message = "Lien de preferences invalide ou expire." })
                : Results.Ok(preferences);
        })
    .WithName("GetNewsletterPreferences")
    .WithTags("Newsletter")
    .WithSummary("Recupere les preferences newsletter via token de gestion.")
    .Produces<NewsletterPreferencesDto>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.MapPost(
        "/api/v1/newsletter/preferences",
        async (NewsletterPreferencesUpdateRequestDto request, INewsletterService newsletterService, CancellationToken cancellationToken) =>
        {
            var errors = ValidateNewsletterPreferencesRequest(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var result = await newsletterService.UpdatePreferencesAsync(request, cancellationToken);
            return result.Success
                ? Results.Ok(result)
                : Results.BadRequest(result);
        })
    .WithName("PostNewsletterPreferences")
    .WithTags("Newsletter")
    .WithSummary("Met a jour les preferences de grilles via token de gestion.")
    .Accepts<NewsletterPreferencesUpdateRequestDto>("application/json")
    .Produces<NewsletterActionResultDto>(StatusCodes.Status200OK)
    .Produces<NewsletterActionResultDto>(StatusCodes.Status400BadRequest)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest);

app.MapPost(
        "/api/subscriptions",
        async (CreateSubscriptionRequestDto request, ISubscriptionService subscriptionService, CancellationToken cancellationToken) =>
        {
            var errors = ValidateSubscriptionRequest(request);
            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            await subscriptionService.RequestSubscriptionAsync(request, cancellationToken);
            return Results.Accepted(value: new
            {
                message = "Si l'adresse fournie est valide, des emails de confirmation ont été envoyés."
            });
        })
    .RequireRateLimiting("subscriptions-post")
    .WithName("PostSubscription")
    .WithTags("Abonnements")
    .WithSummary("Crée un ou plusieurs abonnements en statut Pending et envoie les emails de confirmation.")
    .Accepts<CreateSubscriptionRequestDto>("application/json")
    .Produces(StatusCodes.Status202Accepted)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest);

app.MapGet(
        "/api/admin/sync-runs",
        async (HttpContext httpContext, LotteryDbContext dbContext, int? take, CancellationToken cancellationToken) =>
        {
            var configuredApiKey = ResolveAdminApiKey(app.Configuration);
            if (string.IsNullOrWhiteSpace(configuredApiKey))
            {
                return Results.Problem(
                    "Configuration admin manquante : définir Admin__ApiKey (ou ADMIN_API_KEY).",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!TryValidateApiKey(httpContext, configuredApiKey))
            {
                return Results.Unauthorized();
            }

            var effectiveTake = Math.Clamp(take ?? 50, 1, 200);
            var runs = await dbContext.SyncRuns
                .AsNoTracking()
                .OrderByDescending(entity => entity.StartedAtUtc)
                .Take(effectiveTake)
                .Select(entity => new AdminSyncRunDto(
                    entity.Id,
                    entity.Game.ToString(),
                    entity.Status.ToString(),
                    entity.StartedAtUtc,
                    entity.FinishedAtUtc,
                    entity.DrawsUpsertedCount,
                    entity.Error))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(runs);
        })
    .WithName("GetAdminSyncRuns")
    .WithTags("Admin")
    .WithSummary("Retourne les derniers runs de synchronisation.")
    .WithDescription("Protection simple via header X-Api-Key.")
    .Produces<IReadOnlyCollection<AdminSyncRunDto>>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapGet(
        "/api/subscriptions/confirm",
        async (string token, ISubscriptionService subscriptionService, CancellationToken cancellationToken) =>
            Results.Ok(await subscriptionService.ConfirmAsync(token, cancellationToken)))
    .WithName("GetSubscriptionConfirm")
    .WithTags("Abonnements")
    .WithSummary("Confirme un abonnement via token.")
    .Produces<SubscriptionActionResultDto>(StatusCodes.Status200OK);

app.MapGet(
        "/api/subscriptions/unsubscribe",
        async (string token, ISubscriptionService subscriptionService, CancellationToken cancellationToken) =>
            Results.Ok(await subscriptionService.UnsubscribeAsync(token, cancellationToken)))
    .WithName("GetSubscriptionUnsubscribe")
    .WithTags("Abonnements")
    .WithSummary("Désinscrit un abonnement via token.")
    .Produces<SubscriptionActionResultDto>(StatusCodes.Status200OK);

app.MapGet(
        "/api/subscriptions/status",
        async (string email, ISubscriptionService subscriptionService, CancellationToken cancellationToken) =>
        {
            if (!IsValidEmail(email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["Adresse e-mail invalide."]
                });
            }

            return Results.Ok(await subscriptionService.GetStatusByEmailAsync(email, cancellationToken));
        })
    .WithName("GetSubscriptionStatus")
    .WithTags("Abonnements")
    .WithSummary("Retourne le statut des abonnements connus pour un email.")
    .Produces<SubscriptionStatusDto>(StatusCodes.Status200OK)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest);

app.MapDelete(
        "/api/subscriptions/data",
        async (string email, ISubscriptionService subscriptionService, CancellationToken cancellationToken) =>
        {
            if (!IsValidEmail(email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["Adresse e-mail invalide."]
                });
            }

            await subscriptionService.DeleteDataByEmailAsync(email, cancellationToken);
            return Results.Accepted(value: new
            {
                message = "Si des données existent pour cet email, elles ont été supprimées."
            });
        })
    .WithName("DeleteSubscriptionData")
    .WithTags("Abonnements")
    .WithSummary("Suppression RGPD des données d'abonnement pour un email.")
    .Produces(StatusCodes.Status202Accepted)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest);

app.MapPost(
        "/api/admin/newsletter/dispatch",
        async (HttpContext httpContext, INewsletterDispatchService dispatchService, CancellationToken cancellationToken) =>
        {
            var configuredApiKey = ResolveAdminApiKey(app.Configuration);
            if (string.IsNullOrWhiteSpace(configuredApiKey))
            {
                return Results.Problem(
                    "Configuration admin manquante : definir Admin__ApiKey (ou ADMIN_API_KEY).",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!TryValidateApiKey(httpContext, configuredApiKey))
            {
                return Results.Unauthorized();
            }

            var summary = await dispatchService.DispatchForDueDrawsAsync(DateTimeOffset.UtcNow, force: true, cancellationToken);
            return Results.Ok(summary);
        })
    .WithName("PostAdminNewsletterDispatch")
    .WithTags("Admin")
    .WithSummary("Declenche un dispatch newsletter immediat (force=true) pour la date locale courante.")
    .WithDescription("Protection simple via header X-Api-Key.")
    .Produces<NewsletterDispatchSummaryDto>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapPost(
        "/api/admin/sync",
        async (HttpContext httpContext, IDrawSyncService drawSyncService, CancellationToken cancellationToken) =>
        {
            var configuredApiKey = ResolveAdminApiKey(app.Configuration);

            if (string.IsNullOrWhiteSpace(configuredApiKey))
            {
                return Results.Problem(
                    "Configuration admin manquante : définir Admin__ApiKey (ou ADMIN_API_KEY).",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            if (!TryValidateApiKey(httpContext, configuredApiKey))
            {
                return Results.Unauthorized();
            }

            var summary = await drawSyncService.SyncAllAsync("api", cancellationToken);
            return summary.Games.Any(item => item.Status == SyncRunStatus.Fail)
                ? Results.Json(summary, statusCode: StatusCodes.Status207MultiStatus)
                : Results.Ok(summary);
        })
    .WithName("PostAdminSync")
    .WithTags("Admin")
    .WithSummary("Déclenche une synchronisation manuelle des tirages FDJ.")
    .WithDescription("Protection simple via header X-Api-Key.")
    .Produces<SyncExecutionSummaryDto>(StatusCodes.Status200OK)
    .Produces<SyncExecutionSummaryDto>(StatusCodes.Status207MultiStatus)
    .Produces(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

app.MapHealthChecks("/health", new HealthCheckOptions());

app.Run();

static bool TryValidateApiKey(HttpContext httpContext, string configuredApiKey)
{
    if (!httpContext.Request.Headers.TryGetValue("X-Api-Key", out var providedValues))
    {
        return false;
    }

    var providedApiKey = providedValues.ToString();
    if (string.IsNullOrWhiteSpace(providedApiKey))
    {
        return false;
    }

    var configuredBytes = Encoding.UTF8.GetBytes(configuredApiKey);
    var providedBytes = Encoding.UTF8.GetBytes(providedApiKey);

    return configuredBytes.Length == providedBytes.Length
           && CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes);
}

static string? ResolveAdminApiKey(IConfiguration configuration) =>
    configuration["Admin:ApiKey"] ?? Environment.GetEnvironmentVariable("ADMIN_API_KEY");

static Dictionary<string, string[]> ValidateGenerateRequest(
    GenerateGridsRequestDto request,
    out LotteryGame game,
    out GridGenerationStrategy strategy)
{
    game = default;
    strategy = default;
    var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

    if (!LotteryGameRulesCatalog.TryParseGame(request.Game, out game))
    {
        errors["game"] = ["Valeur invalide. Valeurs supportées : Loto, EuroMillions."];
    }

    if (!GridGenerationStrategyExtensions.TryParseStrategy(request.Strategy, out strategy))
    {
        errors["strategy"] = ["Valeur invalide. Valeurs supportées : uniform, frequency, recency."];
    }

    if (request.Count is < 1 or > 100)
    {
        errors["count"] = ["La valeur doit être comprise entre 1 et 100."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateSubscriptionRequest(CreateSubscriptionRequestDto request)
{
    var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

    if (!IsValidEmail(request.Email))
    {
        errors["email"] = ["Adresse e-mail invalide."];
    }

    var entries = request.Entries?.ToArray() ?? [];
    if (entries.Length == 0)
    {
        errors["entries"] = ["Sélectionnez au moins un abonnement."];
        return errors;
    }

    var seenGames = new HashSet<LotteryGame>();

    for (var index = 0; index < entries.Length; index++)
    {
        var entry = entries[index];
        var prefix = $"entries[{index}]";

        if (!LotteryGameRulesCatalog.TryParseGame(entry.Game, out var game))
        {
            errors[$"{prefix}.game"] = ["Valeur invalide. Valeurs supportées : Loto, EuroMillions."];
            continue;
        }

        if (!seenGames.Add(game))
        {
            errors[$"{prefix}.game"] = ["Chaque jeu ne peut être sélectionné qu'une seule fois."];
        }

        if (entry.GridCount is < 1 or > 100)
        {
            errors[$"{prefix}.gridCount"] = ["La valeur doit être comprise entre 1 et 100."];
        }

        if (!GridGenerationStrategyExtensions.TryParseStrategy(entry.Strategy, out _))
        {
            errors[$"{prefix}.strategy"] = ["Valeur invalide. Valeurs supportées : uniform, frequency, recency."];
        }
    }

    return errors;
}

static Dictionary<string, string[]> ValidateNewsletterSubscriptionRequest(NewsletterSubscribeRequestDto request)
{
    var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

    if (!IsValidEmail(request.Email))
    {
        errors["email"] = ["Adresse e-mail invalide."];
    }

    if (request.LotoGridsCount is < 0 or > 100)
    {
        errors["lotoGridsCount"] = ["La valeur doit etre comprise entre 0 et 100."];
    }

    if (request.EuroMillionsGridsCount is < 0 or > 100)
    {
        errors["euroMillionsGridsCount"] = ["La valeur doit etre comprise entre 0 et 100."];
    }

    if (request.LotoGridsCount == 0 && request.EuroMillionsGridsCount == 0)
    {
        errors["preferences"] = ["Au moins une grille doit etre demandee."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateNewsletterPreferencesRequest(NewsletterPreferencesUpdateRequestDto request)
{
    var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

    if (string.IsNullOrWhiteSpace(request.Token))
    {
        errors["token"] = ["Token requis."];
    }

    if (request.LotoGridsCount is < 0 or > 100)
    {
        errors["lotoGridsCount"] = ["La valeur doit etre comprise entre 0 et 100."];
    }

    if (request.EuroMillionsGridsCount is < 0 or > 100)
    {
        errors["euroMillionsGridsCount"] = ["La valeur doit etre comprise entre 0 et 100."];
    }

    if (request.LotoGridsCount == 0 && request.EuroMillionsGridsCount == 0)
    {
        errors["preferences"] = ["Au moins une grille doit etre demandee."];
    }

    return errors;
}

static string BuildNewsletterRedirectUrl(string baseUrl, string relativePath, string status)
{
    var effectiveBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
        ? "http://localhost:8080"
        : baseUrl.TrimEnd('/');
    var normalizedPath = relativePath.StartsWith('/') ? relativePath : $"/{relativePath}";

    return $"{effectiveBaseUrl}{normalizedPath}?status={Uri.EscapeDataString(status)}";
}

static bool IsValidEmail(string rawEmail) =>
    !string.IsNullOrWhiteSpace(rawEmail) && System.Net.Mail.MailAddress.TryCreate(rawEmail.Trim(), out _);

public partial class Program;
