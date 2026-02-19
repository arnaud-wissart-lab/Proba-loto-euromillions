using Application.Abstractions;
using Application.Models;
using Application.Services;
using Domain.Enums;
using Domain.Services;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public sealed class GridGenerationService(
    LotteryDbContext dbContext,
    ILogger<GridGenerationService> logger) : IGridGenerationService
{
    private const double WeightSmoothingAlpha = 0.75;
    private const double RecencyLambda = 0.08;
    private const int RecencyWindowDraws = 260;
    private const int MaxAttemptsPerRequestedGrid = 80;
    private const int TopNumbersPerZone = 3;

    public async Task<GenerateGridsResponseDto> GenerateAsync(
        LotteryGame game,
        int gridCount,
        GridGenerationStrategy strategy,
        CancellationToken cancellationToken)
    {
        if (gridCount is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(gridCount), gridCount, "Le nombre de grilles doit etre compris entre 1 et 100.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var rules = LotteryGameRulesCatalog.GetRules(game);
        var draws = await dbContext.Draws
            .AsNoTracking()
            .Where(entity => entity.Game == game)
            .OrderByDescending(entity => entity.DrawDate)
            .ToListAsync(cancellationToken);

        var mainUniverse = Enumerable.Range(1, rules.MainPoolSize).ToArray();
        var bonusUniverse = Enumerable.Range(1, rules.BonusPoolSize).ToArray();
        var mainWeights = BuildWeights(mainUniverse, draws, strategy, draw => draw.MainNumbers);
        var bonusWeights = BuildWeights(bonusUniverse, draws, strategy, draw => draw.BonusNumbers);

        var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
        var generatedGrids = new List<GeneratedGridDto>(gridCount);
        var maxAttempts = Math.Max(gridCount * MaxAttemptsPerRequestedGrid, gridCount + 20);
        var attempts = 0;

        while (generatedGrids.Count < gridCount && attempts < maxAttempts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempts++;

            var mainNumbers = WeightedNumberSampler.SampleWithoutReplacement(
                mainUniverse,
                mainWeights,
                rules.MainNumbersToPick);

            var bonusNumbers = WeightedNumberSampler.SampleWithoutReplacement(
                bonusUniverse,
                bonusWeights,
                rules.BonusNumbersToPick);

            var key = BuildGridKey(mainNumbers, bonusNumbers);
            if (!uniqueKeys.Add(key))
            {
                continue;
            }

            var score = CalculateScore(mainNumbers, bonusNumbers, mainUniverse, bonusUniverse, mainWeights, bonusWeights);
            var topMainNumbers = GridScoreCalculator.GetTopNumbers(mainNumbers, mainWeights, Math.Min(TopNumbersPerZone, mainNumbers.Length));
            var topBonusNumbers = GridScoreCalculator.GetTopNumbers(bonusNumbers, bonusWeights, Math.Min(TopNumbersPerZone, bonusNumbers.Length));

            generatedGrids.Add(new GeneratedGridDto(
                mainNumbers,
                bonusNumbers,
                score,
                topMainNumbers,
                topBonusNumbers));
        }

        string? warning = null;
        if (generatedGrids.Count < gridCount)
        {
            warning =
                $"Unicite atteinte: {generatedGrids.Count} grille(s) unique(s) generee(s) sur {gridCount} demandee(s).";
        }

        logger.LogInformation(
            "Generation de grilles {Game} terminee: demande={Requested}, generees={Generated}, strategie={Strategy}, tentatives={Attempts}",
            game,
            gridCount,
            generatedGrids.Count,
            strategy,
            attempts);

        return new GenerateGridsResponseDto(
            DateTimeOffset.UtcNow,
            game.ToString(),
            strategy.ToApiValue(),
            BuildDisclaimer(game, rules.TotalCombinations),
            rules.TotalCombinations,
            generatedGrids,
            warning);
    }

    private static Dictionary<int, double> BuildWeights(
        IReadOnlyCollection<int> universe,
        IReadOnlyList<DrawEntity> draws,
        GridGenerationStrategy strategy,
        Func<DrawEntity, int[]> selector)
    {
        var weights = universe.ToDictionary(number => number, _ => WeightSmoothingAlpha);

        if (strategy == GridGenerationStrategy.Uniform || draws.Count == 0)
        {
            return weights;
        }

        if (strategy == GridGenerationStrategy.FrequencyWeighted)
        {
            foreach (var draw in draws)
            {
                foreach (var number in selector(draw))
                {
                    if (weights.ContainsKey(number))
                    {
                        weights[number] += 1;
                    }
                }
            }

            return weights;
        }

        var limitedDraws = draws.Take(RecencyWindowDraws).ToArray();
        for (var age = 0; age < limitedDraws.Length; age++)
        {
            var decay = Math.Exp(-RecencyLambda * age);
            foreach (var number in selector(limitedDraws[age]))
            {
                if (weights.ContainsKey(number))
                {
                    weights[number] += decay;
                }
            }
        }

        return weights;
    }

    private static double CalculateScore(
        IReadOnlyList<int> mainNumbers,
        IReadOnlyList<int> bonusNumbers,
        IReadOnlyList<int> mainUniverse,
        IReadOnlyList<int> bonusUniverse,
        IReadOnlyDictionary<int, double> mainWeights,
        IReadOnlyDictionary<int, double> bonusWeights)
    {
        var mainNormalized = GridScoreCalculator.CalculateNormalizedLogScore(mainNumbers, mainUniverse, mainWeights);
        var bonusNormalized = GridScoreCalculator.CalculateNormalizedLogScore(bonusNumbers, bonusUniverse, bonusWeights);

        var totalPicked = mainNumbers.Count + bonusNumbers.Count;
        var combinedNormalized = totalPicked == 0
            ? 0
            : (mainNormalized * mainNumbers.Count + bonusNormalized * bonusNumbers.Count) / totalPicked;

        return Math.Round(combinedNormalized * 100, 2);
    }

    private static string BuildGridKey(IEnumerable<int> mainNumbers, IEnumerable<int> bonusNumbers) =>
        $"{string.Join('-', mainNumbers)}|{string.Join('-', bonusNumbers)}";

    private static string BuildDisclaimer(LotteryGame game, long totalCombinations) =>
        $"Chaque combinaison {game} reste equiprobable. Les ponderations (frequence/recence) sont indicatives. " +
        $"Nombre total de combinaisons possibles: {totalCombinations:N0}.";
}
