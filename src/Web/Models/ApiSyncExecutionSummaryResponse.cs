namespace Web.Models;

public sealed class ApiSyncExecutionSummaryResponse
{
    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset FinishedAtUtc { get; init; }

    public IReadOnlyCollection<ApiGameSyncResultResponse> Games { get; init; } = [];
}
