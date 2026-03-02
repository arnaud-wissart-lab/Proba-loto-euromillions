using Infrastructure.Email;

namespace Infrastructure.Services;

internal static class NewsletterEmailTemplates
{
    private const string ConfirmationSubject = "[Proba Loto] Confirmer votre abonnement";

    public static EmailMessage BuildConfirmationEmail(
        string email,
        int lotoGridsCount,
        int euroMillionsGridsCount,
        string confirmLink,
        string unsubscribeLink,
        string preferencesLink)
    {
        var lotoCountBall = EmailBallRenderer.RenderBall(lotoGridsCount, size: 42);
        var euroCountBall = EmailBallRenderer.RenderBall(euroMillionsGridsCount, bonus: true, size: 42);

        var textBody = $"""
Bonjour,

Vous avez demandé un abonnement Proba Loto.

Préférences actuelles :
- Grilles Loto : {lotoGridsCount}
- Grilles EuroMillions : {euroMillionsGridsCount}

Confirmer mon abonnement :
{confirmLink}

Gérer mes préférences :
{preferencesLink}

Me désinscrire :
{unsubscribeLink}

Message informatif : ce service ne prédit aucun tirage. Le jeu reste un jeu de hasard.
""";

        var htmlBody = $"""
<!doctype html>
<html lang=\"fr\">
<body style=\"margin:0;padding:0;background:#f3f6fc;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;\">
  <div style=\"max-width:640px;margin:0 auto;padding:22px 14px;\">
    <div style=\"background:#ffffff;border:1px solid #dbe3f0;border-radius:18px;padding:22px 20px;box-shadow:0 8px 24px rgba(15,23,42,0.08);\">
      <h2 style=\"margin:0 0 8px;font-size:24px;color:#0f172a;\">Confirmer votre abonnement</h2>
      <p style=\"margin:0 0 18px;color:#334155;line-height:1.5;\">Vous avez demandé un abonnement <strong>Proba Loto</strong>.</p>
      <div style=\"display:block;margin:0 0 18px;\">
        <div style=\"padding:12px 14px;border:1px solid #e2e8f0;border-radius:14px;background:#f8fafc;margin-bottom:10px;\">
          <p style=\"margin:0 0 8px;font-size:13px;font-weight:700;color:#475569;\">Grilles Loto</p>
          <div>{lotoCountBall}</div>
        </div>
        <div style=\"padding:12px 14px;border:1px solid #e2e8f0;border-radius:14px;background:#f8fafc;\">
          <p style=\"margin:0 0 8px;font-size:13px;font-weight:700;color:#475569;\">Grilles EuroMillions</p>
          <div>{euroCountBall}</div>
        </div>
      </div>
      <p style=\"margin:0 0 16px;\">
        <a href=\"{confirmLink}\" style=\"display:inline-block;padding:11px 16px;background:#0f766e;color:#ffffff;text-decoration:none;border-radius:10px;font-weight:700;\">Confirmer mon abonnement</a>
      </p>
      <p style=\"margin:0 0 8px;\"><a href=\"{preferencesLink}\" style=\"color:#2563eb;text-decoration:none;\">Gérer mes préférences</a></p>
      <p style=\"margin:0 0 16px;\"><a href=\"{unsubscribeLink}\" style=\"color:#2563eb;text-decoration:none;\">Me désinscrire</a></p>
      <p style=\"margin:0;font-size:12px;color:#64748b;\">Message informatif : ce service ne prédit aucun tirage. Le jeu reste un jeu de hasard.</p>
    </div>
  </div>
</body>
</html>
""";

        return new EmailMessage(email, ConfirmationSubject, textBody, htmlBody);
    }
}
