using Application.Models;

namespace Application.Abstractions;

public interface IStatusService
{
    Task<StatusDto> GetStatusAsync(CancellationToken cancellationToken);
}
