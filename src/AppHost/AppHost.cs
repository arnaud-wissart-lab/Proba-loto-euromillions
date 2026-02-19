var builder = DistributedApplication.CreateBuilder(args);

var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var postgres = builder.AddPostgres("pgserver", password: postgresPassword)
    .WithDataVolume("postgres-data");
var database = postgres.AddDatabase("postgres", "probabilites_loto");

var mailhog = builder.AddContainer("mailhog", "mailhog/mailhog", "v1.0.1")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(port: 1025, targetPort: 1025, scheme: "tcp", name: "smtp");

var api = builder.AddProject<Projects.Api>("api")
    .WithReference(database)
    .WithEnvironment("Admin__ApiKey", "dev-admin-key-change-me")
    .WithEnvironment("SMTP_HOST", "mailhog")
    .WithEnvironment("SMTP_PORT", "1025")
    .WithEnvironment("SMTP_FROM", "Probabilites Loto & EuroMillions <no-reply@example.local>")
    .WithEnvironment("PUBLIC_BASE_URL", "http://localhost:8080")
    .WithEnvironment("SUBSCRIPTIONS_TOKEN_SECRET", "dev-only-change-me")
    .WithHttpHealthCheck("/health")
    .WaitFor(database);

builder.AddProject<Projects.Worker>("worker")
    .WithReference(database)
    .WithEnvironment("SMTP_HOST", "mailhog")
    .WithEnvironment("SMTP_PORT", "1025")
    .WithEnvironment("SMTP_FROM", "Probabilites Loto & EuroMillions <no-reply@example.local>")
    .WithEnvironment("PUBLIC_BASE_URL", "http://localhost:8080")
    .WithEnvironment("SUBSCRIPTIONS_TOKEN_SECRET", "dev-only-change-me")
    .WithEnvironment("Smtp__UseStartTls", "false")
    .WaitFor(database)
    .WaitFor(mailhog);

builder.AddProject<Projects.Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithHttpHealthCheck("/health")
    .WaitFor(api);

builder.Build().Run();
