using Domain.Enums;
using Domain.Models;

namespace Domain.Services;

public static class LotteryGameRulesCatalog
{
    private static readonly LotteryGameRules LotoRules = new(
        LotteryGame.Loto,
        MainPoolSize: 49,
        MainNumbersToPick: 5,
        BonusPoolSize: 10,
        BonusNumbersToPick: 1,
        DrawDays: new HashSet<DayOfWeek>
        {
            DayOfWeek.Monday,
            DayOfWeek.Wednesday,
            DayOfWeek.Saturday
        });

    private static readonly LotteryGameRules EuroMillionsRules = new(
        LotteryGame.EuroMillions,
        MainPoolSize: 50,
        MainNumbersToPick: 5,
        BonusPoolSize: 12,
        BonusNumbersToPick: 2,
        DrawDays: new HashSet<DayOfWeek>
        {
            DayOfWeek.Tuesday,
            DayOfWeek.Friday
        });

    public static LotteryGameRules GetRules(LotteryGame game) =>
        game switch
        {
            LotteryGame.Loto => LotoRules,
            LotteryGame.EuroMillions => EuroMillionsRules,
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, "Jeu non supporté.")
        };

    public static bool TryParseGame(string? rawValue, out LotteryGame game)
    {
        game = default;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = Normalize(rawValue);
        game = normalized switch
        {
            "loto" => LotteryGame.Loto,
            "euromillion" or "euromillions" or "euromillionsmymillion" => LotteryGame.EuroMillions,
            _ => default
        };

        return normalized is "loto" or "euromillion" or "euromillions" or "euromillionsmymillion";
    }

    public static DateOnly GetNextDrawDate(LotteryGame game, DateOnly fromDate)
    {
        var rules = GetRules(game);
        for (var offset = 0; offset <= 7; offset++)
        {
            var candidate = fromDate.AddDays(offset);
            if (rules.DrawDays.Contains(candidate.DayOfWeek))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Aucun jour de tirage défini pour {game}.");
    }

    private static string Normalize(string rawValue) =>
        rawValue
            .Trim()
            .ToLowerInvariant()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
}
