using System.Net.Http.Json;
using Web.Models;

namespace Web.Services;

public sealed class LotteryApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<LotteryApiClient> logger)
{
    public const string ClientName = "lottery-api";

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
            var response = await client.PostAsJsonAsync("/api/grids/generate", request, cancellationToken);
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
}
