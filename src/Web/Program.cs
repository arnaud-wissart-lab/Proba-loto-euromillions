using MudBlazor.Services;
using Serilog;
using Web.Components;
using Web.Options;
using Web.Security;
using Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Host.UseSerilog(
    (context, services, loggerConfiguration) =>
        loggerConfiguration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Service", "Web"),
    writeToProviders: true);

builder.Services.AddOptions<AdminOptions>()
    .Bind(builder.Configuration.GetSection(AdminOptions.SectionName))
    .PostConfigure(options =>
    {
        options.ApiKey = builder.Configuration["ADMIN_API_KEY"] ?? options.ApiKey;
        options.WebUsername = builder.Configuration["ADMIN_WEB_USERNAME"] ?? options.WebUsername;
        options.WebPassword = builder.Configuration["ADMIN_WEB_PASSWORD"] ?? options.WebPassword;

        if (bool.TryParse(builder.Configuration["ADMIN_WEB_PROTECT_UI"], out var parsedProtectUi))
        {
            options.ProtectUi = parsedProtectUi;
        }
    });

builder.Services.AddMudServices();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient(LotteryApiClient.ClientName, client =>
{
    var configuredBaseUrl = builder.Configuration["Api:BaseUrl"];
    client.BaseAddress = string.IsNullOrWhiteSpace(configuredBaseUrl)
        ? new Uri("https+http://api")
        : new Uri(configuredBaseUrl);
});
builder.Services.AddScoped<LotteryApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseSerilogRequestLogging();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseMiddleware<AdminBasicAuthMiddleware>();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
