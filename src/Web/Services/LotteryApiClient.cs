using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Web.Models;
using Web.Options;

namespace Web.Services;

public sealed class LotteryApiClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AdminOptions> adminOptions,
    ILogger<LotteryApiClient> logger)
{
    public const string ClientName = "lottery-api";
    private const string AdminApiKeyHeaderName = "X-Api-Key";
    private readonly AdminOptions _adminOptions = adminOptions.Value;

    public async Task<ApiStatusResponse?> GetStatusAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            return await client.GetFromJsonAsync<ApiStatusResponse>("/api/status", cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible de recuperer le statut API.");
            return null;
        }
    }

    public async Task<ApiGameStatsResponse?> GetStatsAsync(string game, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            return await client.GetFromJsonAsync<ApiGameStatsResponse>($"/api/stats/{game}", cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible de recuperer les statistiques pour {Game}.", game);
            return null;
        }
    }

    public async Task<ApiGenerateGridsResponse?> GenerateGridsAsync(
        ApiGenerateGridsRequest request,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            using var response = await client.PostAsJsonAsync("/api/grids/generate", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Generation de grilles refusee. status={StatusCode}",
                    response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiGenerateGridsResponse>(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible de generer des grilles.");
            return null;
        }
    }

    public async Task<bool> RequestSubscriptionAsync(
        ApiCreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            using var response = await client.PostAsJsonAsync("/api/subscriptions", request, cancellationToken);
            return response.StatusCode == System.Net.HttpStatusCode.Accepted;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible de creer l'abonnement.");
            return false;
        }
    }

    public async Task<ApiSubscriptionActionResponse?> ConfirmSubscriptionAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            var escapedToken = Uri.EscapeDataString(token);
            return await client.GetFromJsonAsync<ApiSubscriptionActionResponse>($"/api/subscriptions/confirm?token={escapedToken}", cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible de confirmer l'abonnement.");
            return null;
        }
    }

    public async Task<ApiSubscriptionActionResponse?> UnsubscribeAsync(
        string token,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            var escapedToken = Uri.EscapeDataString(token);
            return await client.GetFromJsonAsync<ApiSubscriptionActionResponse>($"/api/subscriptions/unsubscribe?token={escapedToken}", cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible de desinscrire l'abonnement.");
            return null;
        }
    }

    public async Task<IReadOnlyCollection<ApiAdminSyncRunResponse>?> GetAdminSyncRunsAsync(
        int take,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/sync-runs?take={Math.Clamp(take, 1, 200)}");
        if (!TryAddAdminApiKey(request))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Lecture SyncRuns admin refusee. status={StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ApiAdminSyncRunResponse>>(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible de recuperer les SyncRuns admin.");
            return null;
        }
    }

    public async Task<ApiSyncExecutionSummaryResponse?> TriggerAdminSyncAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/sync");
        if (!TryAddAdminApiKey(request))
        {
            return null;
        }

        var client = httpClientFactory.CreateClient(ClientName);

        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Declenchement sync admin refuse. status={StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ApiSyncExecutionSummaryResponse>(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Impossible de declencher une synchronisation admin.");
            return null;
        }
    }

    private bool TryAddAdminApiKey(HttpRequestMessage request)
    {
        if (string.IsNullOrWhiteSpace(_adminOptions.ApiKey))
        {
            logger.LogWarning("Aucune Admin:ApiKey configuree cote Web.");
            return false;
        }

        request.Headers.Remove(AdminApiKeyHeaderName);
        request.Headers.Add(AdminApiKeyHeaderName, _adminOptions.ApiKey);
        return true;
    }
}
