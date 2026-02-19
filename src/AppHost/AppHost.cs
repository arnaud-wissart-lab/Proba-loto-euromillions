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
    .WithHttpHealthCheck("/health")
    .WaitFor(database);

builder.AddProject<Projects.Worker>("worker")
    .WithReference(database)
    .WithEnvironment("Smtp__Host", "mailhog")
    .WithEnvironment("Smtp__Port", "1025")
    .WithEnvironment("Smtp__UseStartTls", "false")
    .WithEnvironment("Jobs__SendSubscriptions__DryRun", "true")
    .WaitFor(database)
    .WaitFor(mailhog);

builder.AddProject<Projects.Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(api)
    .WithHttpHealthCheck("/health")
    .WaitFor(api);

builder.Build().Run();
