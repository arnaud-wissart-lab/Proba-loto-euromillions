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
<body style=\"font-family:Segoe UI,Arial,sans-serif;color:#0f172a;\">
  <h2>Confirmer votre abonnement</h2>
  <p>Vous avez demandé un abonnement <strong>Proba Loto</strong>.</p>
  <ul>
    <li>Grilles Loto: <strong>{lotoGridsCount}</strong></li>
    <li>Grilles EuroMillions: <strong>{euroMillionsGridsCount}</strong></li>
  </ul>
  <p>
    <a href=\"{confirmLink}\" style=\"display:inline-block;padding:10px 14px;background:#0f766e;color:#ffffff;text-decoration:none;border-radius:8px;\">Confirmer mon abonnement</a>
  </p>
  <p><a href=\"{preferencesLink}\">Gérer mes préférences</a></p>
  <p><a href=\"{unsubscribeLink}\">Me désinscrire</a></p>
  <p style=\"font-size:12px;color:#475569;\">Message informatif : ce service ne prédit aucun tirage. Le jeu reste un jeu de hasard.</p>
</body>
</html>
""";

        return new EmailMessage(email, ConfirmationSubject, textBody, htmlBody);
    }
}
