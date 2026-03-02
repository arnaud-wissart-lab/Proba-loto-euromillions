using Application.Models;

namespace Application.Abstractions;

public interface INewsletterDispatchService
{
    Task<NewsletterDispatchSummaryDto> DispatchForDueDrawsAsync(
        DateTimeOffset executionTimeUtc,
        bool force,
        CancellationToken cancellationToken);
}
