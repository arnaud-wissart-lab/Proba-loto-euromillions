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

Vous avez demandé un abonnement e-mail pour Probabilités {gameLabel}.

Paramètres :
- Jeu: {gameLabel}
- Nombre de grilles: {subscription.GridCount}
- Stratégie: {strategyLabel}

Confirmez votre abonnement:
{confirmLink}

Lien de désinscription:
{unsubscribeLink}

Message informatif: ce service ne prédit aucun tirage. Le jeu reste un jeu de hasard.
""";

        var htmlBody = $"""
<!doctype html>
<html lang="fr">
<body style="margin:0;padding:0;background:#f3f6fc;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
  <div style="max-width:640px;margin:0 auto;padding:22px 14px;">
    <div style="background:#ffffff;border:1px solid #dbe3f0;border-radius:18px;padding:22px 20px;box-shadow:0 8px 24px rgba(15,23,42,0.08);">
      <h2 style="margin:0 0 8px;font-size:24px;color:#0f172a;">Confirmez votre abonnement</h2>
      <p style="margin:0 0 18px;color:#334155;line-height:1.5;">Vous avez demandé un abonnement e-mail pour <strong>Probabilités {gameLabel}</strong>.</p>
      <p style="margin:0 0 8px;font-size:14px;color:#334155;"><strong>Nombre de grilles:</strong> {subscription.GridCount}</p>
      <p style="margin:0 0 8px;font-size:14px;color:#334155;"><strong>Jeu:</strong> {gameLabel}</p>
      <p style="margin:0 0 16px;font-size:14px;color:#334155;"><strong>Stratégie:</strong> {strategyLabel}</p>

      <p style="margin:0 0 16px;">
        <a href="{confirmLink}" style="display:inline-block;padding:11px 16px;background:#0f766e;color:#ffffff;text-decoration:none;border-radius:10px;font-weight:700;">Confirmer mon abonnement</a>
      </p>
      <p style="margin:0 0 16px;">Si vous souhaitez annuler: <a href="{unsubscribeLink}" style="color:#2563eb;text-decoration:none;">Désinscription</a></p>
      <p style="margin:0;font-size:12px;color:#64748b;">Message informatif: ce service ne prédit aucun tirage. Le jeu reste un jeu de hasard.</p>
    </div>
  </div>
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
<body style="margin:0;padding:0;background:#f3f6fc;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
<div style="max-width:680px;margin:0 auto;padding:22px 14px;">
  <div style="background:#ffffff;border:1px solid #dbe3f0;border-radius:18px;padding:22px 20px;box-shadow:0 8px 24px rgba(15,23,42,0.08);">
""");
        htmlBuilder.AppendLine($"  <h2 style=\"margin:0 0 8px;font-size:24px;color:#0f172a;\">Vos grilles {gameLabel}</h2>");
        htmlBuilder.AppendLine(
            $"  <p style=\"margin:0 0 6px;color:#334155;line-height:1.5;\">Voici vos <strong>{generatedGrids.Grids.Count}</strong> grille(s) pour le tirage du <strong>{intendedDrawDate:dd/MM/yyyy}</strong> ({drawTimeHint}).</p>");
        htmlBuilder.AppendLine($"  <p style=\"margin:0 0 14px;color:#334155;\">Stratégie: <strong>{strategyLabel}</strong></p>");
        htmlBuilder.AppendLine("  <p style=\"margin:0 0 16px;font-size:12px;color:#64748b;\">Rappel: contenu informatif uniquement. Le jeu reste un jeu de hasard.</p>");
        htmlBuilder.AppendLine("  <div style=\"margin:0 0 16px;\">");

        foreach (var grid in generatedGrids.Grids.Select((value, position) => new { Value = value, Index = position + 1 }))
        {
            var mainRow = EmailBallRenderer.RenderBallRow(grid.Value.MainNumbers);
            var bonusRow = EmailBallRenderer.RenderBallRow(grid.Value.BonusNumbers, bonus: true);

            htmlBuilder.AppendLine("    <div style=\"margin-bottom:14px;\">");
            htmlBuilder.AppendLine("      <div style=\"padding:12px 14px;border:1px solid #e2e8f0;border-radius:14px;background:#f8fafc;\">");
            htmlBuilder.AppendLine($"        <p style=\"margin:0 0 8px;font-weight:700;color:#0f172a;\">Grille {grid.Index}</p>");
            htmlBuilder.AppendLine("        <div style=\"margin:0 0 2px;white-space:nowrap;overflow-x:auto;\">");
            htmlBuilder.AppendLine($"          <span style=\"display:inline-block;vertical-align:middle;\">{mainRow}</span>");

            if (grid.Value.BonusNumbers.Count > 0)
            {
                htmlBuilder.AppendLine("          <span style=\"display:inline-block;vertical-align:middle;margin:0 8px 6px 8px;font-size:11px;font-weight:700;color:#64748b;\">Bonus</span>");
                htmlBuilder.AppendLine($"          <span style=\"display:inline-block;vertical-align:middle;\">{bonusRow}</span>");
            }

            htmlBuilder.AppendLine("        </div>");
            htmlBuilder.AppendLine("      </div>");
            htmlBuilder.AppendLine("    </div>");
        }

        htmlBuilder.AppendLine("  </div>");
        htmlBuilder.AppendLine($"  <p style=\"margin:0;\"><a href=\"{unsubscribeLink}\" style=\"color:#2563eb;text-decoration:none;\">Désinscription en 1 clic</a></p>");
        htmlBuilder.Append("""
  </div>
</div>
</body>
</html>
""");

        return new EmailMessage(subscription.Email, subject, textBuilder.ToString(), htmlBuilder.ToString());
    }

    private static string GetStrategyLabel(GridGenerationStrategy strategy) =>
        strategy switch
        {
            GridGenerationStrategy.Uniform => "A) Aléatoire (uniforme)",
            GridGenerationStrategy.FrequencyWeighted => "B) Pondéré par fréquence",
            GridGenerationStrategy.RecencyWeighted => "C) Pondéré par récence",
            _ => "Aléatoire"
        };
}
