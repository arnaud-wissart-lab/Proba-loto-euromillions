using System.Net.Http.Json;
using Web.Models;

namespace Web.Services;

public sealed class StatusApiClient(
    IHttpClientFactory httpClientFactory,
    ILogger<StatusApiClient> logger)
{
    public const string ClientName = "status-api";

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
}
