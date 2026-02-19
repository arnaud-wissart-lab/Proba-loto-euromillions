using MudBlazor.Services;
using Web.Components;
using Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapHealthChecks("/health");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
