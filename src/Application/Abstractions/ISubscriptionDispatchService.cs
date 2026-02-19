using Application.Models;

namespace Application.Abstractions;

public interface ISubscriptionDispatchService
{
    Task<SubscriptionDispatchSummaryDto> SendForUpcomingDrawsAsync(
        DateTimeOffset executionTimeUtc,
        CancellationToken cancellationToken);
}
