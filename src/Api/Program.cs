using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Domain.Services;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
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
                    ["game"] = ["Valeur invalide. Valeurs supportees: Loto, EuroMillions."]
                });
            }

            var stats = await statisticsService.GetStatsAsync(parsedGame, cancellationToken);
            return Results.Ok(stats);
        })
    .WithName("GetGameStats")
    .WithTags("Statistiques")
    .WithSummary("Retourne les statistiques de frequences et dernieres sorties d'un jeu.")
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
    .WithSummary("Genere des grilles uniques (uniforme, frequence, recence) avec score explicable.")
    .Accepts<GenerateGridsRequestDto>("application/json")
    .Produces<GenerateGridsResponseDto>(StatusCodes.Status200OK)
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
                message = "Si l'adresse fournie est valide, un email de confirmation a ete envoye."
            });
        })
    .RequireRateLimiting("subscriptions-post")
    .WithName("PostSubscription")
    .WithTags("Abonnements")
    .WithSummary("Cree un abonnement en statut Pending et envoie un email de confirmation.")
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
    .WithSummary("Desinscrit un abonnement via token.")
    .Produces<SubscriptionActionResultDto>(StatusCodes.Status200OK);

app.MapGet(
        "/api/subscriptions/status",
        async (string email, ISubscriptionService subscriptionService, CancellationToken cancellationToken) =>
        {
            if (!IsValidEmail(email))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["Adresse email invalide."]
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
                    ["email"] = ["Adresse email invalide."]
                });
            }

            await subscriptionService.DeleteDataByEmailAsync(email, cancellationToken);
            return Results.Accepted(value: new
            {
                message = "Si des donnees existent pour cet email, elles ont ete supprimees."
            });
        })
    .WithName("DeleteSubscriptionData")
    .WithTags("Abonnements")
    .WithSummary("Suppression RGPD des donnees d'abonnement pour un email.")
    .Produces(StatusCodes.Status202Accepted)
    .ProducesValidationProblem(StatusCodes.Status400BadRequest);

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
            return Results.Ok(summary);
        })
    .WithName("PostAdminSync")
    .WithTags("Admin")
    .WithSummary("Déclenche une synchronisation manuelle des tirages FDJ.")
    .WithDescription("Protection simple via header X-Api-Key.")
    .Produces<SyncExecutionSummaryDto>(StatusCodes.Status200OK)
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
        errors["game"] = ["Valeur invalide. Valeurs supportees: Loto, EuroMillions."];
    }

    if (!GridGenerationStrategyExtensions.TryParseStrategy(request.Strategy, out strategy))
    {
        errors["strategy"] = ["Valeur invalide. Valeurs supportees: uniform, frequency, recency."];
    }

    if (request.Count is < 1 or > 100)
    {
        errors["count"] = ["La valeur doit etre comprise entre 1 et 100."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateSubscriptionRequest(CreateSubscriptionRequestDto request)
{
    var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

    if (!IsValidEmail(request.Email))
    {
        errors["email"] = ["Adresse email invalide."];
    }

    if (!LotteryGameRulesCatalog.TryParseGame(request.Game, out _))
    {
        errors["game"] = ["Valeur invalide. Valeurs supportees: Loto, EuroMillions."];
    }

    if (request.GridCount is < 1 or > 100)
    {
        errors["gridCount"] = ["La valeur doit etre comprise entre 1 et 100."];
    }

    if (!GridGenerationStrategyExtensions.TryParseStrategy(request.Strategy, out _))
    {
        errors["strategy"] = ["Valeur invalide. Valeurs supportees: uniform, frequency, recency."];
    }

    return errors;
}

static bool IsValidEmail(string rawEmail) =>
    !string.IsNullOrWhiteSpace(rawEmail) && System.Net.Mail.MailAddress.TryCreate(rawEmail.Trim(), out _);

public partial class Program;
