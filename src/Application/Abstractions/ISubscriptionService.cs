using Application.Models;

namespace Application.Abstractions;

public interface ISubscriptionService
{
    Task RequestSubscriptionAsync(CreateSubscriptionRequestDto request, CancellationToken cancellationToken);

    Task<SubscriptionActionResultDto> ConfirmAsync(string token, CancellationToken cancellationToken);

    Task<SubscriptionActionResultDto> UnsubscribeAsync(string token, CancellationToken cancellationToken);

    Task<SubscriptionStatusDto> GetStatusByEmailAsync(string email, CancellationToken cancellationToken);

    Task DeleteDataByEmailAsync(string email, CancellationToken cancellationToken);
}
