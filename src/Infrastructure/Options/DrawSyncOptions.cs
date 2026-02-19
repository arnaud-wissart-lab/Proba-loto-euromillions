using Domain.Enums;

namespace Infrastructure.Options;

public sealed class DrawSyncOptions
{
    public const string SectionName = "DrawSync";

    public string UserAgent { get; init; } = "ProbabilitesLotoEuroMillions/1.0 (+https://localhost)";

    public int HttpTimeoutSeconds { get; init; } = 30;

    public GameOptions Loto { get; init; } = new()
    {
        HistoryUrl = "https://www.fdj.fr/jeux-de-tirage/loto/historique",
        RuleStartDate = new DateOnly(2019, 11, 4)
    };

    public GameOptions EuroMillions { get; init; } = new()
    {
        HistoryUrl = "https://www.fdj.fr/jeux-de-tirage/euromillions-my-million/historique",
        RuleStartDate = new DateOnly(2016, 9, 1)
    };

    public GameOptions GetGameOptions(LotteryGame game) =>
        game switch
        {
            LotteryGame.Loto => Loto,
            LotteryGame.EuroMillions => EuroMillions,
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, "Jeu non supporte.")
        };

    public sealed class GameOptions
    {
        public string HistoryUrl { get; init; } = string.Empty;

        public DateOnly RuleStartDate { get; init; }
    }
}
