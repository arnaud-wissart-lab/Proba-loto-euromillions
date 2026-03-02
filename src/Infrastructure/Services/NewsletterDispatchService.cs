using System.Text;
using Application.Abstractions;
using Application.Models;
using Domain.Enums;
using Infrastructure.Email;
using Infrastructure.Options;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services;

public sealed class NewsletterDispatchService(
    LotteryDbContext dbContext,
    IGridGenerationService gridGenerationService,
    IEmailSender emailSender,
    IDrawScheduleResolver drawScheduleResolver,
    IOptions<MailOptions> mailOptions,
    ILogger<NewsletterDispatchService> logger) : INewsletterDispatchService
{
    public async Task<NewsletterDispatchSummaryDto> DispatchForDueDrawsAsync(
        DateTimeOffset executionTimeUtc,
        bool force,
        CancellationToken cancellationToken)
    {
        var options = mailOptions.Value;
        if (!options.Enabled)
        {
            logger.LogInformation("Dispatch newsletter ignore: Mail:Enabled=false.");
            return BuildNoOpSummary(executionTimeUtc, options.Schedule.TimeZone, force);
        }

        if (string.IsNullOrWhiteSpace(options.Smtp.Host) || options.Smtp.Port <= 0)
        {
            logger.LogWarning("Dispatch newsletter ignore: SMTP non configure.");
            return BuildNoOpSummary(executionTimeUtc, options.Schedule.TimeZone, force);
        }

        var timeZone = ResolveTimeZone(options.Schedule.TimeZone);
        var localDateTime = TimeZoneInfo.ConvertTime(executionTimeUtc, timeZone);
        var localDate = DateOnly.FromDateTime(localDateTime.DateTime);
        var scheduledTime = new TimeOnly(options.Schedule.SendHourLocal, options.Schedule.SendMinuteLocal);
        var isScheduleWindowOpen = force || TimeOnly.FromDateTime(localDateTime.DateTime) >= scheduledTime;

        if (!isScheduleWindowOpen)
        {
            logger.LogInformation(
                "Dispatch newsletter ignore: fenetre non ouverte (local={LocalDateTime}, scheduled={ScheduledTime}, timezone={TimeZone}).",
                localDateTime,
                scheduledTime,
                timeZone.Id);

            return new NewsletterDispatchSummaryDto(
                localDate,
                timeZone.Id,
                force,
                false,
                0,
                0,
                0,
                0,
                []);
        }

        var dispatchedGames = GetDispatchedGames(localDate);
        if (dispatchedGames.Count == 0)
        {
            logger.LogInformation(
                "Dispatch newsletter ignore: aucun tirage aujourd'hui ({LocalDate})",
                localDate);

            return new NewsletterDispatchSummaryDto(
                localDate,
                timeZone.Id,
                force,
                true,
                0,
                0,
                0,
                0,
                []);
        }

        var subscribers = await dbContext.NewsletterSubscribers
            .Where(subscriber => subscriber.ConfirmedAtUtc != null && subscriber.IsActive)
            .ToArrayAsync(cancellationToken);

        var sentCount = 0;
        var skippedCount = 0;
        var errorCount = 0;

        foreach (var game in dispatchedGames)
        {
            foreach (var subscriber in subscribers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var requestedGridCount = ResolveGridCount(subscriber, game);
                if (requestedGridCount == 0)
                {
                    skippedCount++;
                    continue;
                }

                var alreadySent = await dbContext.MailDispatchHistory
                    .AsNoTracking()
                    .AnyAsync(
                        history => history.SubscriberId == subscriber.Id
                                   && history.Game == game
                                   && history.DrawDate == localDate,
                        cancellationToken);

                if (alreadySent)
                {
                    skippedCount++;
                    continue;
                }

                try
                {
                    var generated = await gridGenerationService.GenerateAsync(
                        game,
                        requestedGridCount,
                        GridGenerationStrategy.Uniform,
                        cancellationToken);

                    var message = BuildPackEmail(subscriber, game, localDate, generated.Grids);
                    await emailSender.SendAsync(message, cancellationToken);

                    dbContext.MailDispatchHistory.Add(new MailDispatchHistoryEntity
                    {
                        SubscriberId = subscriber.Id,
                        Game = game,
                        DrawDate = localDate,
                        SentAtUtc = DateTimeOffset.UtcNow,
                        GridsCountSent = generated.Grids.Count
                    });

                    await dbContext.SaveChangesAsync(cancellationToken);
                    sentCount++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(
                        exception,
                        "Echec envoi pack newsletter subscriberId={SubscriberId} game={Game} drawDate={DrawDate}",
                        subscriber.Id,
                        game,
                        localDate);
                    errorCount++;
                }
            }
        }

        return new NewsletterDispatchSummaryDto(
            localDate,
            timeZone.Id,
            force,
            true,
            subscribers.Length,
            sentCount,
            skippedCount,
            errorCount,
            dispatchedGames.Select(game => game.ToString()).ToArray());
    }

    private List<LotteryGame> GetDispatchedGames(DateOnly localDate)
    {
        var games = new List<LotteryGame>(2);
        if (drawScheduleResolver.IsDrawDay(LotteryGame.Loto, localDate))
        {
            games.Add(LotteryGame.Loto);
        }

        if (drawScheduleResolver.IsDrawDay(LotteryGame.EuroMillions, localDate))
        {
            games.Add(LotteryGame.EuroMillions);
        }

        return games;
    }

    private static int ResolveGridCount(NewsletterSubscriberEntity subscriber, LotteryGame game) =>
        game switch
        {
            LotteryGame.Loto => subscriber.LotoGridsCount,
            LotteryGame.EuroMillions => subscriber.EuroMillionsGridsCount,
            _ => 0
        };

    private EmailMessage BuildPackEmail(
        NewsletterSubscriberEntity subscriber,
        LotteryGame game,
        DateOnly drawDate,
        IReadOnlyCollection<GeneratedGridDto> grids)
    {
        var gameLabel = game == LotteryGame.EuroMillions ? "EuroMillions" : "Loto";
        var subject = $"[Proba] Vos grilles {gameLabel} - Tirage du {drawDate:dd/MM/yyyy}";
        var baseUrl = mailOptions.Value.BaseUrl.TrimEnd('/');
        var preferencesLink = BuildTokenizedLink(baseUrl, "/abonnement/preferences", subscriber.UnsubscribeToken);
        var unsubscribeLink = BuildTokenizedLink(baseUrl, "/abonnement/desinscription", subscriber.UnsubscribeToken);

        var textBuilder = new StringBuilder();
        textBuilder.AppendLine("Bonjour,");
        textBuilder.AppendLine();
        textBuilder.AppendLine($"Voici vos {grids.Count} grille(s) {gameLabel} pour le tirage du {drawDate:dd/MM/yyyy}.");
        textBuilder.AppendLine();
        textBuilder.AppendLine("Grilles:");

        var index = 1;
        foreach (var grid in grids)
        {
            textBuilder.AppendLine($"- Grille {index}: Main [{string.Join(" ", grid.MainNumbers)}] | Bonus [{string.Join(" ", grid.BonusNumbers)}]");
            index++;
        }

        textBuilder.AppendLine();
        textBuilder.AppendLine("Message informatif: ce service ne prédit aucun tirage. Le jeu reste un jeu de hasard.");
        textBuilder.AppendLine();
        textBuilder.AppendLine($"Gérer mes préférences: {preferencesLink}");
        textBuilder.AppendLine($"Me désinscrire: {unsubscribeLink}");

        var htmlBuilder = new StringBuilder();
        htmlBuilder.Append("""
<!doctype html>
<html lang="fr">
<body style="margin:0;padding:0;background:#f3f6fc;font-family:Segoe UI,Arial,sans-serif;color:#0f172a;">
<div style="max-width:680px;margin:0 auto;padding:22px 14px;">
  <div style="background:#ffffff;border:1px solid #dbe3f0;border-radius:18px;padding:22px 20px;box-shadow:0 8px 24px rgba(15,23,42,0.08);">
""");
        htmlBuilder.AppendLine($"  <h2 style=\"margin:0 0 8px;font-size:24px;color:#0f172a;\">Vos grilles {gameLabel}</h2>");
        htmlBuilder.AppendLine($"  <p style=\"margin:0 0 14px;color:#334155;line-height:1.5;\">Voici vos <strong>{grids.Count}</strong> grille(s) pour le tirage du <strong>{drawDate:dd/MM/yyyy}</strong>.</p>");
        htmlBuilder.AppendLine("  <div style=\"margin:0 0 16px;\">");

        foreach (var grid in grids.Select((value, position) => new { Value = value, Index = position + 1 }))
        {
            var mainRow = EmailBallRenderer.RenderBallRow(grid.Value.MainNumbers);
            var bonusRow = EmailBallRenderer.RenderBallRow(grid.Value.BonusNumbers, bonus: true);

            htmlBuilder.AppendLine("    <div style=\"margin-bottom:14px;\">");
            htmlBuilder.AppendLine("      <div style=\"padding:12px 14px;border:1px solid #e2e8f0;border-radius:14px;background:#f8fafc;\">");
            htmlBuilder.AppendLine($"        <p style=\"margin:0 0 8px;font-weight:700;color:#0f172a;\">Grille {grid.Index}</p>");
            htmlBuilder.AppendLine("        <table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;\">");
            htmlBuilder.AppendLine("          <tr>");
            htmlBuilder.AppendLine($"            <td style=\"padding:0;vertical-align:middle;\">{mainRow}</td>");

            if (grid.Value.BonusNumbers.Count > 0)
            {
                htmlBuilder.AppendLine($"            <td style=\"padding:0 0 0 10px;vertical-align:middle;text-align:right;white-space:nowrap;\"><span style=\"display:inline-block;vertical-align:middle;margin:0 8px 0 0;font-size:11px;font-weight:700;color:#64748b;\">Bonus</span>{bonusRow}</td>");
            }

            htmlBuilder.AppendLine("          </tr>");
            htmlBuilder.AppendLine("        </table>");
            htmlBuilder.AppendLine("      </div>");
            htmlBuilder.AppendLine("    </div>");
        }

        htmlBuilder.AppendLine("  </div>");
        htmlBuilder.AppendLine("  <p style=\"margin:0 0 12px;font-size:12px;color:#64748b;\">Message informatif: ce service ne prédit aucun tirage. Le jeu reste un jeu de hasard.</p>");
        htmlBuilder.AppendLine("  <table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" width=\"100%\" style=\"border-collapse:collapse;\">");
        htmlBuilder.AppendLine("    <tr>");
        htmlBuilder.AppendLine($"      <td style=\"padding:0;vertical-align:middle;text-align:left;\"><a href=\"{preferencesLink}\" style=\"display:inline-block;padding:11px 16px;background:#2563eb;color:#ffffff;text-decoration:none;border-radius:10px;font-weight:700;\">Gérer mes préférences</a></td>");
        htmlBuilder.AppendLine($"      <td style=\"padding:0;vertical-align:middle;text-align:right;\"><a href=\"{unsubscribeLink}\" style=\"display:inline-block;padding:11px 16px;background:#475569;color:#ffffff;text-decoration:none;border-radius:10px;font-weight:700;\">Me désinscrire</a></td>");
        htmlBuilder.AppendLine("    </tr>");
        htmlBuilder.AppendLine("  </table>");
        htmlBuilder.Append("""
  </div>
</div>
</body>
</html>
""");

        return new EmailMessage(subscriber.Email, subject, textBuilder.ToString(), htmlBuilder.ToString());
    }

    private static string BuildTokenizedLink(string publicBaseUrl, string path, string token)
    {
        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return $"{publicBaseUrl}{normalizedPath}?token={Uri.EscapeDataString(token)}";
    }

    private static TimeZoneInfo ResolveTimeZone(string configuredTimeZone)
    {
        var candidates = new[] { configuredTimeZone, "Europe/Paris", "Romance Standard Time" };

        foreach (var candidate in candidates.Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static NewsletterDispatchSummaryDto BuildNoOpSummary(DateTimeOffset executionTimeUtc, string configuredTimeZone, bool force)
    {
        var timeZone = ResolveTimeZone(configuredTimeZone);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(executionTimeUtc, timeZone).DateTime);

        return new NewsletterDispatchSummaryDto(
            localDate,
            timeZone.Id,
            force,
            false,
            0,
            0,
            0,
            0,
            []);
    }
}
