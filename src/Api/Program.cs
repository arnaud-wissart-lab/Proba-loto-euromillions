using Application.Abstractions;
using Application.Models;
using Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Host.UseSerilog(
    (context, services, loggerConfiguration) =>
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "Api")
            .WriteTo.Console(),
    writeToProviders: true);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "API Probabilites Loto & EuroMillions",
        Version = "v1",
        Description = "API informative et statistique. Aucune prediction de tirage n'est fournie."
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

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.DocumentTitle = "Documentation API - Probabilites Loto & EuroMillions";
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
    .WithSummary("Recupere l'etat statistique courant.")
    .WithDescription("Endpoint informatif: il ne predit aucun tirage.")
    .Produces<StatusDto>(StatusCodes.Status200OK);

app.MapHealthChecks("/health", new HealthCheckOptions());

app.Run();
