using Application.Models;

namespace Application.Abstractions;

public interface INewsletterService
{
    Task RequestSubscriptionAsync(NewsletterSubscribeRequestDto request, CancellationToken cancellationToken);

    Task<NewsletterActionResultDto> ConfirmAsync(string token, CancellationToken cancellationToken);

    Task<NewsletterActionResultDto> UnsubscribeAsync(string token, CancellationToken cancellationToken);

    Task<NewsletterPreferencesDto?> GetPreferencesAsync(string token, CancellationToken cancellationToken);

    Task<NewsletterActionResultDto> UpdatePreferencesAsync(
        NewsletterPreferencesUpdateRequestDto request,
        CancellationToken cancellationToken);
}
