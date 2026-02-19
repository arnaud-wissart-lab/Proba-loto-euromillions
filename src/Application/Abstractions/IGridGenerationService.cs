using Application.Models;
using Domain.Enums;

namespace Application.Abstractions;

public interface IGridGenerationService
{
    Task<GenerateGridsResponseDto> GenerateAsync(
        LotteryGame game,
        int gridCount,
        GridGenerationStrategy strategy,
        CancellationToken cancellationToken);
}
