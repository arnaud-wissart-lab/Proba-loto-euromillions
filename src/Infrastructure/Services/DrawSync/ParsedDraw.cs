namespace Infrastructure.Services.DrawSync;

public sealed record ParsedDraw(
    DateOnly DrawDate,
    int[] MainNumbers,
    int[] BonusNumbers);
