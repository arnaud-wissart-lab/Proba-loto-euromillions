using System.Text;
using Application.Models;
using Domain.Enums;
using Infrastructure.Email;
using Infrastructure.Persistence.Entities;

namespace Infrastructure.Services;

internal static class SubscriptionEmailTemplates
{
    public static EmailMessage BuildConfirmationEmail(
        SubscriptionEntity subscription,
        string confirmLink,
        string unsubscribeLink)
    {
        var gameLabel = subscription.Game == LotteryGame.EuroMillions ? "EuroMillions" : "Loto";
        var strategyLabel = GetStrategyLabel(subscription.Strategy);
        var subject = $"Confirmez votre abonnement – Probabilités {gameLabel}";

        var textBody = $"""
Bonjour,

Vous avez demandé un abonnement email pour Probabilités {gameLabel}.

Parametres:
- Jeu: {gameLabel}
- Nombre de grilles: {subscription.GridCount}
- Stratégie: {strategyLabel}

Confirmez votre abonnement:
{confirmLink}

Lien de desinscription:
{unsubscribeLink}

Message informatif: ce service ne prédit aucun tirage. Le jeu reste un jeu de hasard.
""";

        var htmlBody = $"""
<!doctype html>
<html lang="fr">
<body style="font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
  <h2>Confirmez votre abonnement</h2>
  <p>Vous avez demandé un abonnement email pour <strong>Probabilités {gameLabel}</strong>.</p>
  <p><strong>Parametres</strong></p>
  <ul>
    <li>Jeu: {gameLabel}</li>
    <li>Nombre de grilles: {subscription.GridCount}</li>
    <li>Stratégie: {strategyLabel}</li>
  </ul>
  <p>
    <a href="{confirmLink}" style="display:inline-block;padding:10px 14px;background:#0f766e;color:#ffffff;text-decoration:none;border-radius:8px;">Confirmer mon abonnement</a>
  </p>
  <p>Si vous souhaitez annuler: <a href="{unsubscribeLink}">Désinscription</a></p>
  <p style="font-size:12px;color:#475569;">Message informatif: ce service ne prédit aucun tirage. Le jeu reste un jeu de hasard.</p>
</body>
</html>
""";

        return new EmailMessage(subscription.Email, subject, textBody, htmlBody);
    }

    public static EmailMessage BuildNotificationEmail(
        SubscriptionEntity subscription,
        GenerateGridsResponseDto generatedGrids,
        DateOnly intendedDrawDate,
        string unsubscribeLink)
    {
        var gameLabel = subscription.Game == LotteryGame.EuroMillions ? "EuroMillions" : "Loto";
        var strategyLabel = GetStrategyLabel(subscription.Strategy);
        var drawTimeHint = subscription.Game == LotteryGame.EuroMillions ? "vers 21h00 (heure de Paris)" : "vers 20h45 (heure de Paris)";
        var subject = $"Vos grilles {gameLabel} pour le tirage du {intendedDrawDate:dd/MM/yyyy}";

        var textBuilder = new StringBuilder();
        textBuilder.AppendLine("Bonjour,");
        textBuilder.AppendLine();
        textBuilder.AppendLine($"Voici vos {generatedGrids.Grids.Count} grille(s) {gameLabel} pour le tirage du {intendedDrawDate:dd/MM/yyyy} ({drawTimeHint}).");
        textBuilder.AppendLine($"Stratégie: {strategyLabel}");
        textBuilder.AppendLine();
        textBuilder.AppendLine("Rappel: contenu informatif uniquement. Le jeu reste un jeu de hasard.");
        textBuilder.AppendLine();
        textBuilder.AppendLine("Grilles:");

        var index = 1;
        foreach (var grid in generatedGrids.Grids)
        {
            textBuilder.AppendLine(
                $"- Grille {index}: Main [{string.Join(" ", grid.MainNumbers)}] | Bonus [{string.Join(" ", grid.BonusNumbers)}]");
            index++;
        }

        textBuilder.AppendLine();
        textBuilder.AppendLine($"Désinscription (1 clic): {unsubscribeLink}");

        var htmlBuilder = new StringBuilder();
        htmlBuilder.Append("""
<!doctype html>
<html lang="fr">
<body style="font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
""");
        htmlBuilder.AppendLine($"  <h2>Vos grilles {gameLabel}</h2>");
        htmlBuilder.AppendLine(
            $"  <p>Voici vos <strong>{generatedGrids.Grids.Count}</strong> grille(s) pour le tirage du <strong>{intendedDrawDate:dd/MM/yyyy}</strong> ({drawTimeHint}).</p>");
        htmlBuilder.AppendLine($"  <p>Stratégie: <strong>{strategyLabel}</strong></p>");
        htmlBuilder.AppendLine("  <p style=\"font-size:12px;color:#475569;\">Rappel: contenu informatif uniquement. Le jeu reste un jeu de hasard.</p>");
        htmlBuilder.AppendLine("  <ol>");

        foreach (var grid in generatedGrids.Grids.Select((value, position) => new { Value = value, Index = position + 1 }))
        {
            htmlBuilder.AppendLine(
                $"    <li>Grille {grid.Index}: Main [{string.Join(" ", grid.Value.MainNumbers)}] | Bonus [{string.Join(" ", grid.Value.BonusNumbers)}]</li>");
        }

        htmlBuilder.AppendLine("  </ol>");
        htmlBuilder.AppendLine($"  <p><a href=\"{unsubscribeLink}\">Désinscription en 1 clic</a></p>");
        htmlBuilder.Append("""
</body>
</html>
""");

        return new EmailMessage(subscription.Email, subject, textBuilder.ToString(), htmlBuilder.ToString());
    }

    private static string GetStrategyLabel(GridGenerationStrategy strategy) =>
        strategy switch
        {
            GridGenerationStrategy.Uniform => "A) Aléatoire (uniforme)",
            GridGenerationStrategy.FrequencyWeighted => "B) Pondérée par fréquence",
            GridGenerationStrategy.RecencyWeighted => "C) Pondérée par récence",
            _ => "Aléatoire"
        };
}
